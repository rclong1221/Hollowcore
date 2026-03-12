using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// System ordering group for damage pipeline.
    /// Ensures consistent ordering: Hazards → Mitigation → Apply → Death.
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    public partial class DamageSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// Server-authoritative system that consumes DamageEvent buffers and applies damage to Health.
    /// Includes Shield absorption (13.16.3) and Kill Attribution tracking (13.16.12).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(DamageSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct DamageApplySystem : ISystem
    {
        private const int MaxEventsPerTick = 16; // Cap to prevent unbounded processing

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;

            new ApplyDamageJob
            {
                CurrentTime = currentTime,
                MaxEvents = MaxEventsPerTick
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct ApplyDamageJob : IJobEntity
        {
            public float CurrentTime;
            public int MaxEvents;

            void Execute(
                ref Health health,
                ref ShieldComponent shield, // 13.16.3: Shield
                ref CombatState combatState, // 13.16.12: Kill Attribution
                ref PlayerBlockingState blockingState, // 15.7: Shield Block
                ref DynamicBuffer<RecentAttackerElement> recentAttackers, // 13.16.12: Assists
                ref DynamicBuffer<DamageEvent> damageBuffer,
                in DeathState deathState)
            {
                // Skip if already dead - no more damage while dead
                if (deathState.Phase != DeathPhase.Alive)
                {
                    damageBuffer.Clear();
                    return;
                }

                // Skip if invulnerable
                if (deathState.IsInvulnerable(CurrentTime))
                {
                    damageBuffer.Clear();
                    return;
                }

                // Process damage events (capped to prevent runaway)
                int eventsToProcess = math.min(damageBuffer.Length, MaxEvents);
                float totalHealthDamage = 0f;
                int blockedHits = 0;
                int parriedHits = 0;

                for (int i = 0; i < eventsToProcess; i++)
                {
                    var damage = damageBuffer[i];

                    // Validate damage amount (reject NaN, Inf, negative)
                    if (math.isnan(damage.Amount) || math.isinf(damage.Amount) || damage.Amount <= 0f)
                        continue;

                    // 13.16.12: Update Combat State & Recent Attackers
                    if (damage.SourceEntity != Entity.Null)
                    {
                        combatState.LastAttacker = damage.SourceEntity;
                        combatState.LastAttackTime = CurrentTime;

                        // Add to recent attackers (simple linear scan to update existing or add new)
                        bool found = false;
                        for (int k = 0; k < recentAttackers.Length; k++)
                        {
                            if (recentAttackers[k].Attacker == damage.SourceEntity)
                            {
                                var existing = recentAttackers[k];
                                existing.DamageDealt += damage.Amount;
                                existing.Time = CurrentTime;
                                recentAttackers[k] = existing;
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            recentAttackers.Add(new RecentAttackerElement
                            {
                                Attacker = damage.SourceEntity,
                                DamageDealt = damage.Amount,
                                Time = CurrentTime
                            });
                        }
                    }

                    float remainingDamage = damage.Amount;

                    // 15.7: Shield Block Damage Reduction
                    // Check if blocking and damage has a direction
                    if (blockingState.IsBlocking && damage.Direction.x != 0 || damage.Direction.y != 0 || damage.Direction.z != 0)
                    {
                        // Check if attack is within block angle
                        float3 attackDir = math.normalizesafe(damage.Direction);
                        float3 blockDir = math.normalizesafe(blockingState.BlockDirection);
                        
                        // Dot product: 1 = same direction, -1 = opposite (attacking from front)
                        // We want to block attacks coming FROM the front, so we compare with negative attack direction
                        float dotProduct = math.dot(-attackDir, blockDir);
                        float attackAngle = math.degrees(math.acos(math.clamp(dotProduct, -1f, 1f)));
                        
                        if (attackAngle <= blockingState.BlockAngle * 0.5f)
                        {
                            // Attack is within block arc
                            if (blockingState.IsParrying)
                            {
                                // Perfect parry - no damage!
                                remainingDamage = 0f;
                                parriedHits++;
                            }
                            else
                            {
                                // Normal block - apply damage reduction
                                remainingDamage *= (1f - blockingState.DamageReduction);
                                blockedHits++;
                            }
                        }
                    }
                    else if (blockingState.IsBlocking)
                    {
                        // No direction info - still apply block reduction (generous interpretation)
                        if (blockingState.IsParrying)
                        {
                            remainingDamage = 0f;
                            parriedHits++;
                        }
                        else
                        {
                            remainingDamage *= (1f - blockingState.DamageReduction);
                            blockedHits++;
                        }
                    }

                    // 13.16.3: Shield Absorption (passive energy shield, after block reduction)
                    // Only absorb if shield has positive value
                    if (shield.Current > 0f && remainingDamage > 0f)
                    {
                        float absorbed = math.min(remainingDamage, shield.Current);
                        shield.Current -= absorbed;
                        remainingDamage -= absorbed;
                        
                        // Mark shield damaged time for regen delay
                        shield.LastDamageTime = CurrentTime;
                    }

                    totalHealthDamage += remainingDamage;
                }

                // Apply total damage to Health
                if (totalHealthDamage > 0f)
                {
                    health.Current = math.max(0f, health.Current - totalHealthDamage);
                }

                // Clear buffer after processing
                damageBuffer.Clear();
            }
        }
    }

    /// <summary>
    /// Alternative damage job that respects DodgeRollInvuln component for backwards compatibility.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(DamageSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateBefore(typeof(DamageApplySystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct DamageApplyWithDodgeInvulnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new ClearDamageJob().ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(DodgeRollInvuln))]
        partial struct ClearDamageJob : IJobEntity
        {
            void Execute(ref DynamicBuffer<DamageEvent> damageBuffer)
            {
                damageBuffer.Clear();
            }
        }
    }
}
