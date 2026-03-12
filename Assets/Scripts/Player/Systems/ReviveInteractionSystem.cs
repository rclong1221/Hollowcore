using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Detects player interaction (T key) to revive downed teammates.
    /// Runs on Server (Authoritative).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ReviveInteractionSystem : ISystem
    {
        public const float ReviveRange = 2.5f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ReviveRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            // 1. Collect Downed players
            var downedQuery = SystemAPI.QueryBuilder()
                .WithAll<DeathState, LocalTransform>()
                .Build();
            
            // We need a way to pass downed entities to the job.
            // Since we can't query inside a job easily without component lookup,
            // we'll capture them in a NativeList or iterate in main thread chunks?
            // "ScheduleParallel" requirement.
            
            // Approach: Use a CollisionWorld query or simplistic EntityQuery.ToEntityArray.
            // But we need positions.
            
            // Efficient approach:
            // Job 1: Collect Downed (Entity, Position) into a list.
            // Job 2: Iterate Revivers, check against list, command buffer add request.
            
            // Actually, for Burst, we can just grab the NativeArrays.
            var downedEntities = downedQuery.ToEntityArray(Allocator.TempJob);
            var downedTransforms = downedQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            var downedDeathStates = downedQuery.ToComponentDataArray<DeathState>(Allocator.TempJob);
            
            // Filter strictly for Downed phase (query includes all DeathState)
            // We'll filter in the job.

            new ReviveInteractionJob
            {
                DownedEntities = downedEntities,
                DownedTransforms = downedTransforms,
                DownedDeathStates = downedDeathStates,
                ECB = ecb.AsParallelWriter(),
                RangeSq = ReviveRange * ReviveRange
            }.ScheduleParallel();
            
            // Dispose arrays needed? Yes, via job dependency or DeallocateOnJobCompletion.
            // Since using TempJob, we must dispose. Can use WithDisposeOnCompletion? No, that's for NativeArray passed to job?
            // Yes.
        }

        [BurstCompile]
        partial struct ReviveInteractionJob : IJobEntity
        {
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> DownedEntities;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<LocalTransform> DownedTransforms;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<DeathState> DownedDeathStates;
            
            public EntityCommandBuffer.ParallelWriter ECB;
            public float RangeSq;

            void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in LocalTransform transform, in PlayerInput input, in DeathState reviverState)
            {
                // Must be Alive to revive
                if (reviverState.Phase != DeathPhase.Alive) return;

                // Must have Interact pressed
                if (input.Interact.IsSetByte == 0) return;

                // Check length safety
                int count = DownedEntities.Length;
                if (DownedTransforms.Length < count) count = DownedTransforms.Length;
                if (DownedDeathStates.Length < count) count = DownedDeathStates.Length;

                // Find nearest downed player
                for (int i = 0; i < count; i++)
                {
                    if (DownedDeathStates[i].Phase != DeathPhase.Downed) continue;

                    var targetEntity = DownedEntities[i];
                    var targetPos = DownedTransforms[i].Position;

                    if (math.distancesq(transform.Position, targetPos) <= RangeSq)
                    {
                        // Found target. Request revive.
                        ECB.AppendToBuffer(sortKey, targetEntity, new ReviveRequest
                        {
                            Reviver = entity,
                            ClientTick = 0 
                        });
                        
                        break;
                    }
                }
            }
        }
    }
}
