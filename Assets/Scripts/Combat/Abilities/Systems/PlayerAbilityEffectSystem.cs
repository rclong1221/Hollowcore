using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;
using DIG.Targeting;
using DIG.Targeting.Theming;
using DIG.Combat.Resolvers;
using DIG.Combat.Systems;
using DIG.VFX;

namespace DIG.Combat.Abilities
{
    /// <summary>
    /// Creates PendingCombatHit entities when a player ability enters the Active phase
    /// at the correct hit timing. Resolves targets based on AbilityTargetType using
    /// TargetData.TargetEntity / TargetData.TargetPoint.
    ///
    /// Also creates VFXRequest entities for impact VFX.
    ///
    /// Server/LocalSimulation only — damage resolution is authoritative.
    ///
    /// EPIC 18.19 - Phase 5
    /// </summary>
    [UpdateInGroup(typeof(PlayerAbilitySystemGroup))]
    [UpdateAfter(typeof(PlayerAbilityExecutionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class PlayerAbilityEffectSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem _ecbSystem;

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerAbilityState>();
            RequireForUpdate<AbilityDatabaseRef>();
            _ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var ecb = _ecbSystem.CreateCommandBuffer();
            var dbRef = SystemAPI.GetSingleton<AbilityDatabaseRef>();
            if (!dbRef.Value.IsCreated) return;

            ref var abilities = ref dbRef.Value.Value.Abilities;

            foreach (var (abilityState, slots, targetData, ltw, entity) in
                SystemAPI.Query<RefRW<PlayerAbilityState>, DynamicBuffer<PlayerAbilitySlot>,
                    RefRO<TargetData>, RefRO<LocalToWorld>>()
                    .WithAll<Simulate>()
                    .WithEntityAccess())
            {
                // Only deliver effects during Active phase
                if (abilityState.ValueRO.Phase != AbilityCastPhase.Active) continue;

                byte activeSlot = abilityState.ValueRO.ActiveSlotIndex;
                if (activeSlot == 255 || activeSlot >= slots.Length) continue;

                var slot = slots[activeSlot];
                if (slot.AbilityId < 0 || slot.AbilityId >= abilities.Length) continue;

                ref var def = ref abilities[slot.AbilityId];

                // Determine if we should deliver a hit this frame
                bool shouldDeliver = false;

                if (def.TickInterval > 0f)
                {
                    // Channeled/multi-hit: check tick timing
                    int expectedTicks = (int)(abilityState.ValueRO.PhaseElapsed / def.TickInterval);
                    if (expectedTicks > abilityState.ValueRO.TicksDelivered)
                    {
                        shouldDeliver = true;
                        abilityState.ValueRW.TicksDelivered = (byte)math.min(expectedTicks, 255);
                    }
                }
                else
                {
                    // Single hit: deliver once
                    if (abilityState.ValueRO.DamageDealt == 0)
                    {
                        shouldDeliver = true;
                        abilityState.ValueRW.DamageDealt = 1;
                    }
                }

                if (!shouldDeliver) continue;

                float3 attackerPos = ltw.ValueRO.Position;
                float3 aimDir = targetData.ValueRO.AimDirection;

                // Resolve target based on ability target type
                switch (def.TargetType)
                {
                    case AbilityTargetType.SingleTarget:
                    {
                        var target = targetData.ValueRO.TargetEntity;
                        if (target == Entity.Null) continue;

                        CreateCombatHit(ecb, entity, target, attackerPos, aimDir, ref def);
                        break;
                    }

                    case AbilityTargetType.GroundTarget:
                    {
                        // Ground-target abilities create a hit at TargetPoint
                        // CombatResolutionSystem will handle AOE radius resolution
                        var hitEntity = ecb.CreateEntity();
                        ecb.AddComponent(hitEntity, new PendingCombatHit
                        {
                            AttackerEntity = entity,
                            TargetEntity = Entity.Null, // AOE — no single target
                            WeaponEntity = Entity.Null,
                            HitPoint = targetData.ValueRO.TargetPoint,
                            HitNormal = new float3(0f, 1f, 0f),
                            HitDistance = math.distance(attackerPos, targetData.ValueRO.TargetPoint),
                            WasPhysicsHit = false,
                            ResolverType = def.ResolverType,
                            WeaponData = CreateWeaponStats(ref def),
                            HitRegion = 0, // Default
                            HitboxMultiplier = 1f,
                            DamagePreApplied = false,
                            AttackDirection = aimDir
                        });

                        CreateImpactVFX(ecb, targetData.ValueRO.TargetPoint, aimDir, ref def);
                        break;
                    }

                    case AbilityTargetType.Self:
                    {
                        // Self-targeted buff/heal
                        CreateCombatHit(ecb, entity, entity, attackerPos, aimDir, ref def);
                        break;
                    }

                    case AbilityTargetType.Cone:
                    case AbilityTargetType.Line:
                    case AbilityTargetType.AoE:
                    case AbilityTargetType.Cleave:
                    {
                        // Area abilities — create a single PendingCombatHit at attacker position
                        // CombatResolutionSystem handles multi-target resolution for area shapes
                        var hitEntity = ecb.CreateEntity();
                        ecb.AddComponent(hitEntity, new PendingCombatHit
                        {
                            AttackerEntity = entity,
                            TargetEntity = Entity.Null,
                            WeaponEntity = Entity.Null,
                            HitPoint = attackerPos,
                            HitNormal = new float3(0f, 1f, 0f),
                            HitDistance = 0f,
                            WasPhysicsHit = false,
                            ResolverType = def.ResolverType,
                            WeaponData = CreateWeaponStats(ref def),
                            HitRegion = 0,
                            HitboxMultiplier = 1f,
                            DamagePreApplied = false,
                            AttackDirection = aimDir
                        });
                        break;
                    }

                    case AbilityTargetType.Projectile:
                    {
                        var target = targetData.ValueRO.TargetEntity;
                        if (target != Entity.Null)
                        {
                            // Targeted projectile (locks onto entity)
                            CreateCombatHit(ecb, entity, target, attackerPos, aimDir, ref def);
                        }
                        else if (math.lengthsq(aimDir) > 0.001f)
                        {
                            // Skillshot projectile (fires toward aim direction, no locked target)
                            CreateCombatHit(ecb, entity, Entity.Null, attackerPos, aimDir, ref def);
                        }
                        break;
                    }
                }
            }
        }

        private static void CreateCombatHit(EntityCommandBuffer ecb, Entity attacker, Entity target,
            float3 attackerPos, float3 aimDir, ref AbilityDef def)
        {
            var hitEntity = ecb.CreateEntity();
            ecb.AddComponent(hitEntity, new PendingCombatHit
            {
                AttackerEntity = attacker,
                TargetEntity = target,
                WeaponEntity = Entity.Null,
                HitPoint = attackerPos + aimDir * 1f,
                HitNormal = -aimDir,
                HitDistance = def.Range,
                WasPhysicsHit = false,
                ResolverType = def.ResolverType,
                WeaponData = CreateWeaponStats(ref def),
                HitRegion = 0,
                HitboxMultiplier = 1f,
                DamagePreApplied = false,
                AttackDirection = aimDir
            });

            if (def.ImpactVFXTypeId > 0)
            {
                CreateImpactVFX(ecb, attackerPos + aimDir * 1f, aimDir, ref def);
            }
        }

        private static WeaponStats CreateWeaponStats(ref AbilityDef def)
        {
            return new WeaponStats
            {
                BaseDamage = def.DamageBase,
                DamageMin = def.DamageBase - def.DamageVariance,
                DamageMax = def.DamageBase + def.DamageVariance,
                AttackSpeed = 1f,
                DamageType = def.DamageType,
                CategoryID = 0,
                CanCrit = def.CanCrit,
                CritChanceBonus = 0f,
                CritMultiplierBonus = 0f
            };
        }

        private static void CreateImpactVFX(EntityCommandBuffer ecb, float3 position, float3 direction,
            ref AbilityDef def)
        {
            if (def.ImpactVFXTypeId <= 0) return;

            var vfxEntity = ecb.CreateEntity();
            ecb.AddComponent(vfxEntity, new VFXRequest
            {
                Position = position,
                Rotation = quaternion.LookRotationSafe(direction, new float3(0f, 1f, 0f)),
                VFXTypeId = def.ImpactVFXTypeId,
                Category = VFXCategory.Ability,
                Intensity = 1f,
                Scale = 1f
            });
        }
    }
}
