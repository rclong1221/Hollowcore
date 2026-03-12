using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Player.Components;
using DIG.Player.Abilities;
using DIG.Combat.Components;

namespace Player.Systems
{
    /// <summary>
    /// Server-authoritative system that transitions DeathState.
    /// Includes 13.16.10 Death Events and 13.16.12 Kill Attribution.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(DamageSystemGroup))]
    [UpdateAfter(typeof(DamageApplySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct DeathTransitionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            
            // Get NetworkTime singleton for ServerTick
            var netTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = netTime.ServerTick.IsValid ? netTime.ServerTick.TickIndexForValidTick : 0;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // Create Lookups
            var combatLookup = SystemAPI.GetComponentLookup<Player.Components.CombatState>(true);
            var recentAttackersLookup = SystemAPI.GetBufferLookup<RecentAttackerElement>(true);
            var willDieLookup = SystemAPI.GetComponentLookup<WillDieEvent>(false);
            var diedLookup = SystemAPI.GetComponentLookup<DiedEvent>(false);
            var playerTagLookup = SystemAPI.GetComponentLookup<PlayerTag>(true);
            var corpseLookup = SystemAPI.GetComponentLookup<CorpseState>(false);
            var corpseOverrideLookup = SystemAPI.GetComponentLookup<CorpseSettingsOverride>(true);
            var corpseConfig = SystemAPI.HasSingleton<CorpseConfig>()
                ? SystemAPI.GetSingleton<CorpseConfig>()
                : CorpseConfig.Default;

            foreach (var (health, transform, deathState, entity) in
                     SystemAPI.Query<RefRW<Health>, RefRO<LocalTransform>, RefRW<DeathState>>()
                     .WithEntityAccess())
            {
                // Only check living entities
                if (deathState.ValueRO.Phase != DeathPhase.Alive)
                    continue;

                // Check if health is depleted
                if (health.ValueRO.Current <= 0f)
                {
                    // Check if WillDieEvent is present/enabled
                    bool hasWillDie = willDieLookup.HasComponent(entity);
                    if (!hasWillDie)
                    {
                         // UnityEngine.Debug.Log($"[DeathTransition] Entity {entity.Index} has 0 HP but NO WillDieEvent component!");
                         continue;
                    }

                    bool isWillDieEnabled = willDieLookup.IsComponentEnabled(entity);
                    // UnityEngine.Debug.Log($"[DeathTransition] Entity {entity.Index} has 0 HP. Event Enabled: {isWillDieEnabled}");

                    // 13.16.10: WillDie Event Loop
                    if (!isWillDieEnabled)
                    {
                        // UnityEngine.Debug.Log($"[DeathTransition] Firing WillDieEvent for {entity.Index}");
                        willDieLookup.SetComponentEnabled(entity, true);
                        
                        var willDie = willDieLookup[entity];
                        willDie.Cancelled = false; // Reset
                        if (combatLookup.HasComponent(entity))
                        {
                            willDie.Killer = combatLookup[entity].LastAttacker;
                        }
                        willDieLookup[entity] = willDie;
                        
                        continue; // Return and wait for next frame
                    }

                    // Read current WillDie state
                    var currentWillDie = willDieLookup[entity];

                    // If fired and Cancelled, abort death
                    if (currentWillDie.Cancelled)
                    {
                        // UnityEngine.Debug.Log($"[DeathTransition] Death Cancelled for {entity.Index}");
                        health.ValueRW.Current = 1.0f; // Restore 1 HP
                        willDieLookup.SetComponentEnabled(entity, false); // Reset event
                        continue;
                    }

                    // If fired and NOT Cancelled (or passed check), Proceed to Death
                    // UnityEngine.Debug.Log($"[DeathTransition] Executing Death for {entity.Index}");
                    willDieLookup.SetComponentEnabled(entity, false); // Cleanup WillDie

                    // Transition to Downed/Dead
                    deathState.ValueRW.Phase = DeathPhase.Downed;
                    deathState.ValueRW.StateStartTime = currentTime;
                    
                    // 13.16.10: Fire DiedEvent
                    if (diedLookup.HasComponent(entity))
                    {
                        diedLookup.SetComponentEnabled(entity, true);
                    }
                    else
                    {
                         // UnityEngine.Debug.Log($"[DeathTransition] Warning: Missing DiedEvent on {entity.Index}");
                    }
                    
                    // 13.14.P9: Disable JumpAbility when downed/dead (players only)
                    if (SystemAPI.HasComponent<JumpAbility>(entity))
                        ecb.SetComponentEnabled<JumpAbility>(entity, false);

                    // EPIC 16.3: Start corpse lifecycle instead of instant-hide
                    if (!playerTagLookup.HasComponent(entity))
                    {
                        if (corpseLookup.HasComponent(entity))
                        {
                            // Resolve timings from per-entity override or global config
                            float ragdollDur = corpseConfig.RagdollDuration;
                            float corpseLife = corpseConfig.CorpseLifetime;
                            float fadeDur = corpseConfig.FadeOutDuration;
                            bool isBoss = false;

                            if (corpseOverrideLookup.HasComponent(entity))
                            {
                                var ov = corpseOverrideLookup[entity];
                                if (ov.RagdollDuration >= 0f) ragdollDur = ov.RagdollDuration;
                                if (ov.CorpseLifetime >= 0f) corpseLife = ov.CorpseLifetime;
                                if (ov.FadeOutDuration >= 0f) fadeDur = ov.FadeOutDuration;
                                isBoss = ov.IsBoss;
                            }

                            corpseLookup.SetComponentEnabled(entity, true);
                            corpseLookup[entity] = new CorpseState
                            {
                                Phase = CorpsePhase.Ragdoll,
                                PhaseStartTime = currentTime,
                                RagdollDuration = ragdollDur,
                                CorpseLifetime = corpseLife,
                                FadeOutDuration = fadeDur,
                                IsBoss = isBoss
                            };
                        }
                        else
                        {
                            ecb.AddComponent<Disabled>(entity); // fallback for entities without CorpseState
                        }
                    }

                    // ========================================================
                    // 13.16.12: KILL ATTRIBUTION
                    // ========================================================
                    if (combatLookup.HasComponent(entity))
                    {
                        Entity killer = combatLookup[entity].LastAttacker;
                        
                        if (killer != Entity.Null)
                        {
                            // Award Kill
                            ecb.AddComponent(killer, new KillCredited
                            {
                                Victim = entity,
                                VictimPosition = transform.ValueRO.Position,
                                ServerTick = currentTick
                            });

                            // Award Assists
                            if (recentAttackersLookup.HasBuffer(entity))
                            {
                                var recentAttackers = recentAttackersLookup[entity];
                                for (int i = 0; i < recentAttackers.Length; i++)
                                {
                                    var attackerElem = recentAttackers[i];
                                    if (attackerElem.Attacker == killer) continue;
                                    if (attackerElem.Attacker == entity) continue;
                                    if (attackerElem.Attacker == Entity.Null) continue;

                                    if (currentTime - attackerElem.Time < 15.0f)
                                    {
                                        ecb.AddComponent(attackerElem.Attacker, new AssistCredited
                                        {
                                            Victim = entity,
                                            DamageDealt = attackerElem.DamageDealt,
                                            ServerTick = currentTick
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Reset WillDie if health restored
                    if (willDieLookup.HasComponent(entity) && willDieLookup.IsComponentEnabled(entity))
                    {
                        willDieLookup.SetComponentEnabled(entity, false);
                    }
                    
                    // Reset DiedEvent if alive
                    if (diedLookup.HasComponent(entity) && diedLookup.IsComponentEnabled(entity))
                    {
                        diedLookup.SetComponentEnabled(entity, false);
                    }
                }
            }
        }
    }

    /// <summary>
    /// System to update PlayerState.Mode to Dead when DeathState transitions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(DamageSystemGroup))]
    [UpdateAfter(typeof(DeathTransitionSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct DeathPlayerStateSyncSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new SyncPlayerStateJob().ScheduleParallel();
        }

        [BurstCompile]
        partial struct SyncPlayerStateJob : IJobEntity
        {
            void Execute(
                in DeathState deathState,
                ref PlayerState playerState)
            {
                // Sync death state to player mode
                if (deathState.Phase == DeathPhase.Dead || deathState.Phase == DeathPhase.Downed)
                {
                    if (playerState.Mode != PlayerMode.Dead)
                    {
                        playerState.Mode = PlayerMode.Dead;
                    }
                }
                else if (deathState.Phase == DeathPhase.Alive)
                {
                    // Reset to EVA when alive
                    if (playerState.Mode == PlayerMode.Dead)
                    {
                        playerState.Mode = PlayerMode.EVA;
                    }
                }
            }
        }
    }
}
