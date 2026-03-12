using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using DIG.AI.Components;
using DIG.Combat.Systems;
using DIG.Combat.Components;
using Health = Player.Components.Health;
using DIG.Combat.Resolvers;
using DIG.Targeting.Theming;
using DIG.Weapons;
using HitboxRegion = Player.Components.HitboxRegion;

namespace DIG.AI.Systems
{
    /// <summary>
    /// EPIC 15.32: Generic ability execution system.
    /// Replaces AIAttackExecutionSystem from 15.31.
    /// Manages ability lifecycle (Telegraph → Casting → Active → Recovery → Idle)
    /// and creates PendingCombatHit with optional WeaponModifier for status effects.
    ///
    /// Not Burst-compiled due to ECB structural changes (creating PendingCombatHit entities).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AICombatBehaviorSystem))]
    [UpdateBefore(typeof(CombatResolutionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class AbilityExecutionSystem : SystemBase
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        protected override void OnCreate()
        {
            RequireForUpdate<AIBrain>();
            _transformLookup = GetComponentLookup<LocalTransform>(true);
        }

        protected override void OnUpdate()
        {
            _transformLookup.Update(this);
            float deltaTime = SystemAPI.Time.DeltaTime;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (execState, aiState, brain, transform, health, entity) in
                SystemAPI.Query<
                    RefRW<AbilityExecutionState>,
                    RefRW<AIState>,
                    RefRO<AIBrain>,
                    RefRO<LocalTransform>,
                    RefRO<Health>>()
                .WithEntityAccess())
            {
                ref var exec = ref execState.ValueRW;
                if (health.ValueRO.Current <= 0f) { exec.Phase = AbilityCastPhase.Idle; continue; }
                if (exec.Phase == AbilityCastPhase.Idle) continue;

                var brainData = brain.ValueRO;
                exec.PhaseTimer += deltaTime;

                // Read ability data from buffer if available, otherwise use AIBrain fallback
                AbilityDefinition ability;
                bool hasBuffer = SystemAPI.HasBuffer<AbilityDefinition>(entity);
                if (hasBuffer && exec.SelectedAbilityIndex >= 0)
                {
                    var abilities = SystemAPI.GetBuffer<AbilityDefinition>(entity);
                    if (exec.SelectedAbilityIndex >= abilities.Length)
                    {
                        exec.Phase = AbilityCastPhase.Idle;
                        exec.SelectedAbilityIndex = -1;
                        continue;
                    }
                    ability = abilities[exec.SelectedAbilityIndex];
                }
                else
                {
                    // Fallback to AIBrain fields (backward compat)
                    ability = AbilityDefinition.DefaultMelee(
                        brainData.MeleeRange, brainData.AttackWindUp,
                        brainData.AttackActiveDuration, brainData.AttackRecovery,
                        brainData.AttackCooldown, brainData.BaseDamage,
                        brainData.DamageVariance, brainData.DamageType
                    );
                }

                // Get phase multipliers from EncounterState if present
                float damageMultiplier = 1f;
                if (SystemAPI.HasComponent<EncounterState>(entity))
                {
                    var encounter = SystemAPI.GetComponent<EncounterState>(entity);
                    if (encounter.IsEnraged)
                        damageMultiplier = encounter.EnrageDamageMultiplier;
                    else if (SystemAPI.HasBuffer<PhaseDefinition>(entity))
                    {
                        var phases = SystemAPI.GetBuffer<PhaseDefinition>(entity);
                        for (int i = 0; i < phases.Length; i++)
                        {
                            if (phases[i].PhaseIndex == encounter.CurrentPhase)
                            {
                                damageMultiplier = phases[i].DamageMultiplier;
                                break;
                            }
                        }
                    }
                }

                switch (exec.Phase)
                {
                    case AbilityCastPhase.Telegraph:
                    {
                        if (exec.PhaseTimer >= ability.TelegraphDuration)
                        {
                            exec.Phase = AbilityCastPhase.Casting;
                            exec.PhaseTimer = 0f;
                        }
                        break;
                    }

                    case AbilityCastPhase.Casting:
                    {
                        if (exec.PhaseTimer >= ability.CastTime)
                        {
                            // Lock final attack direction at moment of strike
                            if (exec.TargetEntity != Entity.Null &&
                                _transformLookup.HasComponent(exec.TargetEntity))
                            {
                                float3 selfPos = transform.ValueRO.Position;
                                float3 targetPos = _transformLookup[exec.TargetEntity].Position;
                                float3 dir = targetPos - selfPos;
                                dir.y = 0f;
                                float len = math.length(dir);
                                exec.CastDirection = len > 0.01f ? dir / len : new float3(0, 0, 1);
                                exec.TargetPosition = targetPos;
                            }

                            exec.Phase = AbilityCastPhase.Active;
                            exec.PhaseTimer = 0f;
                        }
                        break;
                    }

                    case AbilityCastPhase.Active:
                    {
                        // Handle telegraph-based AOE damage (spawns zone, doesn't create direct hit)
                        if (ability.TelegraphDamageOnExpire && ability.TelegraphShape != TelegraphShape.None
                            && !exec.DamageDealt)
                        {
                            // Spawn telegraph zone entity for AOE damage
                            SpawnTelegraphZone(ecb, ability, exec, entity, damageMultiplier);
                            exec.DamageDealt = true;
                        }
                        // Direct damage (single target or immediate AOE)
                        else if (!exec.DamageDealt && exec.TargetEntity != Entity.Null &&
                            _transformLookup.HasComponent(exec.TargetEntity))
                        {
                            float3 selfPos = transform.ValueRO.Position;
                            float3 targetPos = _transformLookup[exec.TargetEntity].Position;
                            float3 toTarget = targetPos - selfPos;
                            toTarget.y = 0f;
                            float distance = math.length(toTarget);

                            // Distance check (generous for lag)
                            if (distance <= ability.Range * 1.3f)
                            {
                                // Facing check
                                float3 forward = math.forward(transform.ValueRO.Rotation);
                                forward.y = 0f;
                                forward = math.normalizesafe(forward);
                                float3 dirToTarget = distance > 0.01f ? toTarget / distance : forward;
                                float dot = math.dot(forward, dirToTarget);

                                if (dot > 0.5f)
                                {
                                    CreatePendingCombatHit(ecb, entity, exec.TargetEntity,
                                        ability, targetPos, distance, exec.CastDirection, damageMultiplier);

                                    exec.DamageDealt = true;
                                }
                            }
                        }

                        // Transition to recovery when active window ends
                        if (exec.PhaseTimer >= ability.ActiveDuration)
                        {
                            exec.Phase = AbilityCastPhase.Recovery;
                            exec.PhaseTimer = 0f;
                        }
                        break;
                    }

                    case AbilityCastPhase.Recovery:
                    {
                        if (exec.PhaseTimer >= ability.RecoveryTime)
                        {
                            // Set cooldowns
                            if (hasBuffer && exec.SelectedAbilityIndex >= 0 &&
                                SystemAPI.HasBuffer<AbilityCooldownState>(entity))
                            {
                                var cooldowns = SystemAPI.GetBuffer<AbilityCooldownState>(entity);
                                var abilities = SystemAPI.GetBuffer<AbilityDefinition>(entity);

                                if (exec.SelectedAbilityIndex < cooldowns.Length)
                                {
                                    var cd = cooldowns[exec.SelectedAbilityIndex];
                                    cd.CooldownRemaining = ability.Cooldown;
                                    cd.GlobalCooldownRemaining = ability.GlobalCooldown;
                                    if (ability.MaxCharges > 0 && cd.ChargesRemaining > 0)
                                        cd.ChargesRemaining--;
                                    cooldowns[exec.SelectedAbilityIndex] = cd;

                                    // Set global cooldown on all abilities
                                    for (int i = 0; i < cooldowns.Length; i++)
                                    {
                                        if (i == exec.SelectedAbilityIndex) continue;
                                        var otherCd = cooldowns[i];
                                        otherCd.GlobalCooldownRemaining = math.max(
                                            otherCd.GlobalCooldownRemaining, ability.GlobalCooldown);
                                        cooldowns[i] = otherCd;
                                    }

                                    // Set cooldown group
                                    if (ability.CooldownGroupId > 0)
                                    {
                                        for (int i = 0; i < cooldowns.Length; i++)
                                        {
                                            if (i < abilities.Length &&
                                                abilities[i].CooldownGroupId == ability.CooldownGroupId)
                                            {
                                                var groupCd = cooldowns[i];
                                                groupCd.CooldownGroupRemaining = math.max(
                                                    groupCd.CooldownGroupRemaining,
                                                    ability.CooldownGroupDuration);
                                                cooldowns[i] = groupCd;
                                            }
                                        }
                                    }
                                }
                            }

                            // Legacy cooldown support
                            aiState.ValueRW.AttackCooldownRemaining = ability.Cooldown;

                            // Track ability cast count for encounter triggers
                            if (SystemAPI.HasComponent<EncounterState>(entity))
                            {
                                var encounter = SystemAPI.GetComponent<EncounterState>(entity);
                                // Simple counter — track first 2 ability IDs
                                if (ability.AbilityId > 0)
                                {
                                    if (encounter.AbilityCastCount0 < 255)
                                        encounter.AbilityCastCount0++;
                                }
                                SystemAPI.SetComponent(entity, encounter);
                            }

                            // Reset execution state
                            exec.Phase = AbilityCastPhase.Idle;
                            exec.PhaseTimer = 0f;
                            exec.DamageDealt = false;
                            exec.SelectedAbilityIndex = -1;
                            exec.TelegraphEntity = Entity.Null;

                            // Disable movement override
                            if (SystemAPI.HasComponent<MovementOverride>(entity) &&
                                SystemAPI.IsComponentEnabled<MovementOverride>(entity))
                            {
                                SystemAPI.SetComponentEnabled<MovementOverride>(entity, false);
                            }
                        }
                        break;
                    }
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void CreatePendingCombatHit(EntityCommandBuffer ecb, Entity attacker, Entity target,
            in AbilityDefinition ability, float3 hitPoint, float distance, float3 attackDir,
            float damageMultiplier)
        {
            var hitEntity = ecb.CreateEntity();
            ecb.AddComponent(hitEntity, new PendingCombatHit
            {
                AttackerEntity = attacker,
                TargetEntity = target,
                WeaponEntity = attacker, // AI is its own "weapon"
                HitPoint = hitPoint,
                HitNormal = new float3(0, 1, 0),
                HitDistance = distance,
                WasPhysicsHit = true,
                ResolverType = ability.ResolverType,
                WeaponData = new WeaponStats
                {
                    BaseDamage = ability.DamageBase * damageMultiplier,
                    DamageMin = (ability.DamageBase - ability.DamageVariance) * damageMultiplier,
                    DamageMax = (ability.DamageBase + ability.DamageVariance) * damageMultiplier,
                    DamageType = ability.DamageType,
                    CanCrit = ability.CanCrit
                },
                HitRegion = HitboxRegion.Torso,
                HitboxMultiplier = ability.HitboxMultiplier,
                DamagePreApplied = false,
                AttackDirection = attackDir
            });

            // Add weapon modifiers for status effects (reuse existing WeaponModifier pipeline)
            if (ability.Modifier0Type != ModifierType.None || ability.Modifier1Type != ModifierType.None)
            {
                // Write modifiers to a temporary buffer on the attacker so CRS can read them
                if (SystemAPI.HasBuffer<WeaponModifier>(attacker))
                {
                    var modBuffer = SystemAPI.GetBuffer<WeaponModifier>(attacker);
                    modBuffer.Clear();
                    if (ability.Modifier0Type != ModifierType.None)
                    {
                        modBuffer.Add(new WeaponModifier
                        {
                            Type = ability.Modifier0Type,
                            Source = ModifierSource.Innate,
                            Element = ability.DamageType,
                            Chance = ability.Modifier0Chance,
                            Duration = ability.Modifier0Duration,
                            Intensity = ability.Modifier0Intensity
                        });
                    }
                    if (ability.Modifier1Type != ModifierType.None)
                    {
                        modBuffer.Add(new WeaponModifier
                        {
                            Type = ability.Modifier1Type,
                            Source = ModifierSource.Innate,
                            Element = ability.DamageType,
                            Chance = ability.Modifier1Chance,
                            Duration = ability.Modifier1Duration,
                            Intensity = ability.Modifier1Intensity
                        });
                    }
                }
            }
        }

        private void SpawnTelegraphZone(EntityCommandBuffer ecb, in AbilityDefinition ability,
            in AbilityExecutionState exec, Entity owner, float damageMultiplier)
        {
            TelegraphSpawnHelper.SpawnTelegraph(ecb, ability, exec.TargetPosition,
                quaternion.LookRotation(exec.CastDirection, math.up()), owner, damageMultiplier);
        }
    }
}
