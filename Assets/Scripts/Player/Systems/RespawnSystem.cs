using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.NetCode;
using Player.Components;
using DIG.Player.Abilities;

namespace Player.Systems
{
    /// <summary>
    /// Handles respawning of dead players.
    /// Resets state and moves player to a valid RespawnPoint.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DownedRulesSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct RespawnSystem : ISystem
    {
        public const float RespawnDelay = 5.0f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            double currentTime = SystemAPI.Time.ElapsedTime;

            // Collect Spawn Points
            var spawnQuery = SystemAPI.QueryBuilder()
                .WithAll<RespawnPoint, LocalTransform>()
                .Build();
            
            var spawnPoints = spawnQuery.ToComponentDataArray<RespawnPoint>(Allocator.TempJob);
            var spawnTransforms = spawnQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            new RespawnJob
            {
                CurrentTime = currentTime,
                Delay = RespawnDelay,
                SpawnPoints = spawnPoints,
                SpawnTransforms = spawnTransforms,
                Random = Random.CreateFromIndex((uint)currentTime), // Simple random for fallback
                ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter()
            }.ScheduleParallel();
        }

        // [BurstCompile]
        partial struct RespawnJob : IJobEntity
        {
            public double CurrentTime;
            public float Delay;
            [DeallocateOnJobCompletion] public NativeArray<RespawnPoint> SpawnPoints;
            [DeallocateOnJobCompletion] public NativeArray<LocalTransform> SpawnTransforms;
            public Random Random;
            public EntityCommandBuffer.ParallelWriter ecb;

            void Execute(
                Entity entity,
                [ChunkIndexInQuery] int chunkIndex,
                ref DeathState deathState,
                ref Health health,
                ref LocalTransform transform,
                ref DynamicBuffer<StatusEffect> statusEffects,
                ref DynamicBuffer<HealEvent> healEvents,
                ref DynamicBuffer<ReviveRequest> reviveRequests,
                ref DynamicBuffer<DamageEvent> damageEvents)
            {
                // Only process Dead
                if (deathState.Phase != DeathPhase.Dead)
                    return;

                // Check Delay
                if ((float)(CurrentTime - deathState.StateStartTime) < Delay)
                    return;

                // Respawn!
                deathState.Phase = DeathPhase.Alive;
                health.Current = health.Max; // Full Health
                
                UnityEngine.Debug.Log($"[RespawnSystem] Respawning Entity {entity} at time {CurrentTime}");
                
                // Clear Buffers
                statusEffects.Clear();
                healEvents.Clear();
                reviveRequests.Clear();
                damageEvents.Clear();

                // 13.16.4: Spawn Invincibility
                // Grant 3 seconds of immunity to all damage
                ecb.SetComponent(chunkIndex, entity, new DamageInvulnerabilityWindow
                {
                    EndTime = (float)CurrentTime + 3.0f,
                    BlockedTypeMask = -1 // Block All
                });
                
                // 13.14.P9: Re-enable JumpAbility
                ecb.SetComponentEnabled<JumpAbility>(chunkIndex, entity, true);

                // Pick Spawn Point
                // MVP: First enabled point, or random?
                // Priority scan.
                int bestIndex = -1;
                int bestPriority = int.MaxValue;
                
                for (int i = 0; i < SpawnPoints.Length; i++)
                {
                    var sp = SpawnPoints[i];
                    if (!sp.Enabled) continue;
                    
                    if (sp.Priority < bestPriority)
                    {
                        bestPriority = sp.Priority;
                        bestIndex = i;
                    }
                }

                if (bestIndex >= 0)
                {
                    transform.Position = SpawnTransforms[bestIndex].Position;
                    transform.Rotation = SpawnTransforms[bestIndex].Rotation;
                }
                else
                {
                    // Fallback: Origin
                    transform.Position = new float3(0, 2, 0);
                    transform.Rotation = quaternion.identity;
                }
            }
        }
    }
}
