using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Combat.Resolvers;
using DIG.Combat.Components;
using DIG.Combat.Definitions;
using DIG.Combat.UI;
using DIG.Targeting.Theming;
using DIG.Weapons;
using DIG.Combat.Utility;
using DIG.Combat.Knockback;

using Player.Components;
using HitboxRegion = global::Player.Components.HitboxRegion;
using DamageType = DIG.Targeting.Theming.DamageType;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// Central system for resolving combat interactions.
    /// Receives hit events and delegates to appropriate resolvers.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CombatResolutionSystem : SystemBase
    {
        private EntityQuery _pendingHitsQuery;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<AttackStats> _attackStatsLookup;
        private ComponentLookup<CharacterAttributes> _charAttrsLookup;
        private ComponentLookup<DefenseStats> _defenseStatsLookup;

        // EPIC 15.29: Weapon modifier processing
        private BufferLookup<WeaponModifier> _modifierLookup;
        private BufferLookup<global::Player.Components.DamageEvent> _damageBufferLookup;
        private BufferLookup<global::Player.Components.StatusEffectRequest> _statusRequestLookup;
        private BufferLookup<HealEvent> _healBufferLookup;
        private ComponentLookup<Health> _healthLookup;

        protected override void OnCreate()
        {
            _pendingHitsQuery = GetEntityQuery(
                ComponentType.ReadOnly<PendingCombatHit>()
            );
            RequireForUpdate(_pendingHitsQuery);

            _transformLookup = GetComponentLookup<LocalTransform>(true);
            _attackStatsLookup = GetComponentLookup<AttackStats>(true);
            _charAttrsLookup = GetComponentLookup<CharacterAttributes>(true);
            _defenseStatsLookup = GetComponentLookup<DefenseStats>(true);

            // EPIC 15.29
            _modifierLookup = GetBufferLookup<WeaponModifier>(true);
            _damageBufferLookup = GetBufferLookup<global::Player.Components.DamageEvent>(false);
            _statusRequestLookup = GetBufferLookup<global::Player.Components.StatusEffectRequest>(false);
            _healBufferLookup = GetBufferLookup<HealEvent>(false);
            _healthLookup = GetComponentLookup<Health>(true);
        }

        protected override void OnUpdate()
        {
            // Complete any in-flight jobs to avoid safety conflicts
            CompleteDependency();

            _transformLookup.Update(this);
            _attackStatsLookup.Update(this);
            _charAttrsLookup.Update(this);
            _defenseStatsLookup.Update(this);
            _modifierLookup.Update(this);
            _damageBufferLookup.Update(this);
            _statusRequestLookup.Update(this);
            _healBufferLookup.Update(this);
            _healthLookup.Update(this);

            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = networkTime.ServerTick.IsValid ? networkTime.ServerTick.TickIndexForValidTick : 0;

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var random = new Unity.Mathematics.Random((uint)(SystemAPI.Time.ElapsedTime * 10000) | 1u);

            // Manual EntityQuery iteration — SystemAPI.Query source-gen has matching issues
            var entities = _pendingHitsQuery.ToEntityArray(Allocator.Temp);
            var hits = _pendingHitsQuery.ToComponentDataArray<PendingCombatHit>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var pendingHit = hits[i];

                // Build combat context from hit data
                var context = BuildContext(pendingHit);

                // Get the resolver for this combat type
                var resolver = CombatResolverFactory.GetResolver(pendingHit.ResolverType);

                // Resolve the attack
                var result = resolver.ResolveAttack(in context);

                // EPIC 15.29: Process weapon modifiers (on-hit effects)
                bool hasModBuffer = pendingHit.WeaponEntity != Entity.Null
                    && _modifierLookup.HasBuffer(pendingHit.WeaponEntity);
                if (result.DidHit && hasModBuffer)
                {
                    var modifiers = _modifierLookup[pendingHit.WeaponEntity];
                    for (int m = 0; m < modifiers.Length; m++)
                    {
                        var mod = modifiers[m];
                        if (mod.Type == ModifierType.None) continue;

                        // BonusDamage is passive — always applies, no proc roll
                        if (mod.Type == ModifierType.BonusDamage)
                        {
                            if (mod.BonusDamage > 0f && pendingHit.TargetEntity != Entity.Null
                                && _damageBufferLookup.HasBuffer(pendingHit.TargetEntity))
                            {
                                ecb.AppendToBuffer(pendingHit.TargetEntity, new global::Player.Components.DamageEvent
                                {
                                    Amount = mod.BonusDamage,
                                    Type = DamageTypeConverter.ToSurvival(mod.Element),
                                    SourceEntity = pendingHit.AttackerEntity,
                                    HitPosition = pendingHit.HitPoint,
                                    ServerTick = currentTick
                                });
                            }
                            continue;
                        }

                        // Roll proc chance
                        if (random.NextFloat() > mod.Chance) continue;

                        // Set proc flag on result
                        result.ProcsTriggered |= MapModifierToProc(mod.Type);
                        result.ProcsTriggeredCount++;

                        switch (mod.Type)
                        {
                            // Status DOTs + debuffs
                            case ModifierType.Bleed:
                            case ModifierType.Burn:
                            case ModifierType.Freeze:
                            case ModifierType.Shock:
                            case ModifierType.Poison:
                            case ModifierType.Stun:
                            case ModifierType.Slow:
                            case ModifierType.Weaken:
                                AppendStatusEffect(ref ecb, pendingHit.TargetEntity, in mod);
                                break;

                            case ModifierType.Lifesteal:
                            {
                                // Heal attacker for Intensity% of final damage dealt
                                float healAmount = result.FinalDamage * mod.Intensity;
                                if (healAmount > 0f && pendingHit.AttackerEntity != Entity.Null
                                    && _healBufferLookup.HasBuffer(pendingHit.AttackerEntity))
                                {
                                    ecb.AppendToBuffer(pendingHit.AttackerEntity, new HealEvent
                                    {
                                        Amount = healAmount,
                                        SourceEntity = pendingHit.AttackerEntity,
                                        Position = pendingHit.HitPoint,
                                        ServerTick = currentTick,
                                        Type = HealType.Lifesteal
                                    });
                                }
                                break;
                            }

                            case ModifierType.Knockback:
                            {
                                // EPIC 16.9: Create KnockbackRequest for target
                                if (mod.Force > 0f && pendingHit.TargetEntity != Entity.Null)
                                {
                                    float3 hitDir;
                                    if (pendingHit.AttackerEntity != Entity.Null &&
                                        _transformLookup.HasComponent(pendingHit.AttackerEntity))
                                    {
                                        float3 attackerPos = _transformLookup[pendingHit.AttackerEntity].Position;
                                        hitDir = math.normalizesafe(pendingHit.HitPoint - attackerPos, new float3(0, 0, 1));
                                    }
                                    else
                                    {
                                        hitDir = new float3(0, 0, 1);
                                    }

                                    var kbEntity = ecb.CreateEntity();
                                    ecb.AddComponent(kbEntity, new KnockbackRequest
                                    {
                                        TargetEntity = pendingHit.TargetEntity,
                                        SourceEntity = pendingHit.AttackerEntity,
                                        Direction = hitDir,
                                        Force = mod.Force,
                                        Type = KnockbackType.Push,
                                        Falloff = KnockbackFalloff.None,
                                        Easing = KnockbackEasing.EaseOut,
                                        TriggersInterrupt = mod.Force >= 500f
                                    });
                                }
                                break;
                            }

                            case ModifierType.Explosion:
                                CreateExplosionEvent(ref ecb, pendingHit.HitPoint,
                                    pendingHit.AttackerEntity, mod.BonusDamage,
                                    mod.Radius, mod.Element, mod.Force);
                                break;

                            case ModifierType.Chain:
                            case ModifierType.Cleave:
                                // Future: chain/cleave damage events
                                break;
                        }
                    }
                }

                // Create result event for other systems to consume
                var resultEntity = ecb.CreateEntity();
                ecb.AddComponent(resultEntity, new CombatResultEvent
                {
                    AttackerEntity = pendingHit.AttackerEntity,
                    TargetEntity = pendingHit.TargetEntity,
                    WeaponEntity = pendingHit.WeaponEntity,
                    HitPoint = pendingHit.HitPoint,
                    HitNormal = pendingHit.HitNormal,
                    DidHit = result.DidHit,
                    HitType = result.HitType,
                    RawDamage = result.RawDamage,
                    FinalDamage = result.FinalDamage,
                    DamageType = result.DamageType,
                    CritMultiplier = result.CritMultiplier,
                    TargetKilled = result.TargetKilled,
                    ProcFlags = (int)result.ProcsTriggered,
                    Flags = result.Flags, // EPIC 15.22
                    DamagePreApplied = pendingHit.DamagePreApplied // EPIC 15.28
                });

                // EPIC 15.30: Visual enqueue removed — DamageEventVisualBridgeSystem handles all
                // damage numbers via DamageVisualQueue. CRE still drives hitmarkers/combo/killfeed.
                // Pass resolver HitType + Flags so the bridge can apply severity/context styling.
                if (result.DidHit && pendingHit.DamagePreApplied)
                {
                    DamageVisualQueue.SetCombatHint(
                        pendingHit.TargetEntity.Index,
                        new CombatVisualHint
                        {
                            HitType = result.HitType,
                            Flags = result.Flags
                        });
                }

                // Remove the pending hit
                ecb.DestroyEntity(entity);
            }

            entities.Dispose();
            hits.Dispose();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        // ========== EPIC 15.29: Modifier Processing Helpers ==========

        private void AppendStatusEffect(ref EntityCommandBuffer ecb, Entity target, in WeaponModifier mod)
        {
            if (target == Entity.Null) return;
            var statusType = MapModifierToStatus(mod.Type);
            if (statusType == global::Player.Components.StatusEffectType.None) return;
            if (!_statusRequestLookup.HasBuffer(target)) return;

            ecb.AppendToBuffer(target, new global::Player.Components.StatusEffectRequest
            {
                Type = statusType,
                Severity = mod.Intensity,
                Duration = mod.Duration,
                Additive = false
            });
        }

        private static void CreateExplosionEvent(ref EntityCommandBuffer ecb, float3 position,
            Entity source, float damage, float radius, DamageType element, float knockbackForce)
        {
            var explosionEntity = ecb.CreateEntity();
            ecb.AddComponent(explosionEntity, new ModifierExplosionRequest
            {
                Position = position,
                SourceEntity = source,
                Damage = damage,
                Radius = radius,
                Element = element,
                KnockbackForce = knockbackForce
            });
        }

        private static ProcFlags MapModifierToProc(ModifierType type)
        {
            return type switch
            {
                ModifierType.Bleed => ProcFlags.Bleed,
                ModifierType.Burn => ProcFlags.Burn,
                ModifierType.Freeze => ProcFlags.Freeze,
                ModifierType.Shock => ProcFlags.Shock,
                ModifierType.Poison => ProcFlags.Poison,
                ModifierType.Lifesteal => ProcFlags.Lifesteal,
                ModifierType.Stun => ProcFlags.Stun,
                ModifierType.Slow => ProcFlags.Slow,
                ModifierType.Weaken => ProcFlags.WeakenTarget,
                ModifierType.Knockback => ProcFlags.Knockback,
                ModifierType.Explosion => ProcFlags.Explosion,
                ModifierType.Chain => ProcFlags.ChainLightning,
                ModifierType.Cleave => ProcFlags.Cleave,
                _ => ProcFlags.None
            };
        }

        private static global::Player.Components.StatusEffectType MapModifierToStatus(ModifierType type)
        {
            return type switch
            {
                ModifierType.Bleed => global::Player.Components.StatusEffectType.Bleed,
                ModifierType.Burn => global::Player.Components.StatusEffectType.Burn,
                ModifierType.Freeze => global::Player.Components.StatusEffectType.Frostbite,
                ModifierType.Shock => global::Player.Components.StatusEffectType.Shock,
                ModifierType.Poison => global::Player.Components.StatusEffectType.PoisonDOT,
                ModifierType.Stun => global::Player.Components.StatusEffectType.Stun,
                ModifierType.Slow => global::Player.Components.StatusEffectType.Slow,
                ModifierType.Weaken => global::Player.Components.StatusEffectType.Weaken,
                _ => global::Player.Components.StatusEffectType.None
            };
        }

        private CombatContext BuildContext(in PendingCombatHit hit)
        {
            var context = new CombatContext
            {
                AttackerEntity = hit.AttackerEntity,
                TargetEntity = hit.TargetEntity,
                WeaponEntity = hit.WeaponEntity,
                HitPoint = hit.HitPoint,
                HitNormal = hit.HitNormal,
                HitDistance = hit.HitDistance,
                WasPhysicsHit = hit.WasPhysicsHit,
                WeaponData = hit.WeaponData,
                // EPIC 15.28: Hitbox enrichment
                HitRegion = hit.HitRegion,
                HitboxMultiplier = hit.HitboxMultiplier > 0f ? hit.HitboxMultiplier : 1f,
                AttackDirection = hit.AttackDirection
            };

            // EPIC 15.28: Compute target forward for backstab detection
            if (hit.TargetEntity != Entity.Null &&
                _transformLookup.HasComponent(hit.TargetEntity))
            {
                var targetTransform = _transformLookup[hit.TargetEntity];
                context.TargetForward = targetTransform.Forward();
            }

            // Read attacker stats if available
            if (_attackStatsLookup.HasComponent(hit.AttackerEntity))
            {
                var attackStats = _attackStatsLookup[hit.AttackerEntity];
                context.AttackerStats.AttackPower = attackStats.AttackPower;
                context.AttackerStats.SpellPower = attackStats.SpellPower;
                context.AttackerStats.CritChance = attackStats.CritChance;
                context.AttackerStats.CritMultiplier = attackStats.CritMultiplier;
                context.AttackerStats.Accuracy = attackStats.Accuracy;
            }

            if (_charAttrsLookup.HasComponent(hit.AttackerEntity))
            {
                var attrs = _charAttrsLookup[hit.AttackerEntity];
                context.AttackerStats.Strength = attrs.Strength;
                context.AttackerStats.Dexterity = attrs.Dexterity;
                context.AttackerStats.Intelligence = attrs.Intelligence;
                context.AttackerStats.Level = attrs.Level;
            }

            // Read target stats if available and target exists
            if (hit.TargetEntity != Entity.Null)
            {
                if (_defenseStatsLookup.HasComponent(hit.TargetEntity))
                {
                    var defenseStats = _defenseStatsLookup[hit.TargetEntity];
                    context.TargetStats.Defense = defenseStats.Defense;
                    context.TargetStats.Armor = defenseStats.Armor;
                    context.TargetStats.Evasion = defenseStats.Evasion;
                }

                if (_charAttrsLookup.HasComponent(hit.TargetEntity))
                {
                    var attrs = _charAttrsLookup[hit.TargetEntity];
                    context.TargetStats.Level = attrs.Level;
                }

                if (_healthLookup.HasComponent(hit.TargetEntity))
                {
                    var hp = _healthLookup[hit.TargetEntity];
                    context.TargetStats.HealthPercent = hp.Max > 0f ? hp.Current / hp.Max : 1f;
                }
                else
                {
                    context.TargetStats.HealthPercent = 1f;
                }
            }

            return context;
        }
    }

    /// <summary>
    /// Component representing a pending combat hit to be resolved.
    /// Created by hit detection systems (projectile, melee, raycast).
    /// </summary>
    public struct PendingCombatHit : IComponentData
    {
        public Entity AttackerEntity;
        public Entity TargetEntity;
        public Entity WeaponEntity;
        public float3 HitPoint;
        public float3 HitNormal;
        public float HitDistance;
        public bool WasPhysicsHit;
        public CombatResolverType ResolverType;
        public WeaponStats WeaponData;
        // EPIC 15.28: Hitbox and combat enrichment fields
        public HitboxRegion HitRegion;
        public float HitboxMultiplier;
        public bool DamagePreApplied;
        public float3 AttackDirection;
    }

    /// <summary>
    /// Event component created after combat resolution.
    /// Consumed by damage application, UI, and effect systems.
    /// EPIC 15.22: Extended with ResultFlags for contextual combat feedback.
    /// </summary>
    public struct CombatResultEvent : IComponentData
    {
        public Entity AttackerEntity;
        public Entity TargetEntity;
        public Entity WeaponEntity;
        public float3 HitPoint;
        public float3 HitNormal;
        public bool DidHit;
        public HitType HitType;
        public float RawDamage;
        public float FinalDamage;
        public DamageType DamageType;
        public float CritMultiplier;
        public bool TargetKilled;
        public int ProcFlags;
        public ResultFlags Flags; // EPIC 15.22
        public bool DamagePreApplied; // EPIC 15.28: Skip health in DamageApplicationSystem
    }
}
