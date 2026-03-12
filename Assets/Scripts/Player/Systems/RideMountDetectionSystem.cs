using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Detects nearby rideable entities for players.
    /// SERVER-ONLY: Detection happens on server to ensure consistent state with input processing.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct RideMountDetectionSystem : ISystem
    {
        private const bool DebugEnabled = false;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RideState>();
            state.RequireForUpdate<RideableState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            // Gather all rideables
            var rideableQuery = SystemAPI.QueryBuilder()
                .WithAll<RideableState, LocalTransform>()
                .Build();
                
            var rideables = rideableQuery.ToEntityArray(Allocator.Temp);
            var rideableTransforms = rideableQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var rideableStates = rideableQuery.ToComponentDataArray<RideableState>(Allocator.Temp);
            
            // Debug: Log rideable count periodically
            if (DebugEnabled && (int)(SystemAPI.Time.ElapsedTime * 10) % 50 == 0)
            {
                UnityEngine.Debug.Log($"[Blitz RideDetection] [Server] Found {rideables.Length} rideable(s)");
            }
            
            foreach (var (rideState, rideConfig, transform, entity) 
                in SystemAPI.Query<RefRO<RideState>, RefRO<RideConfig>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
            {
                // Skip if already riding
                if (rideState.ValueRO.IsRiding)
                {
                    // Remove nearby indicator if present
                    if (state.EntityManager.HasComponent<NearbyRideable>(entity))
                    {
                        ecb.RemoveComponent<NearbyRideable>(entity);
                    }
                    continue;
                }
                    
                // Find closest rideable in range
                float3 playerPos = transform.ValueRO.Position;
                float closestDist = float.MaxValue;
                Entity closestRideable = Entity.Null;
                bool fromLeft = true;
                
                for (int i = 0; i < rideables.Length; i++)
                {
                    var rideableState = rideableStates[i];
                    
                    // Skip if can't be ridden or has rider
                    if (!rideableState.CanBeRidden || rideableState.HasRider)
                        continue;
                        
                    float3 rideablePos = rideableTransforms[i].Position;
                    float dist = math.distance(playerPos, rideablePos);
                    
                    if (dist < rideableState.InteractionRadius && dist < closestDist)
                    {
                        closestDist = dist;
                        closestRideable = rideables[i];
                        
                        // Determine mount side based on relative position
                        float3 toPlayer = playerPos - rideablePos;
                        float3 rideableRight = math.mul(rideableTransforms[i].Rotation, new float3(1, 0, 0));
                        fromLeft = math.dot(toPlayer, rideableRight) < 0;
                    }
                }
                
                // Update nearby rideable state
                if (closestRideable != Entity.Null)
                {
                    if (!state.EntityManager.HasComponent<NearbyRideable>(entity))
                    {
                        ecb.AddComponent<NearbyRideable>(entity);
                    }
                    ecb.SetComponent(entity, new NearbyRideable
                    {
                        RideableEntity = closestRideable,
                        MountFromLeft = fromLeft
                    });
                }
                else
                {
                    // No rideable nearby
                    if (state.EntityManager.HasComponent<NearbyRideable>(entity))
                    {
                        ecb.RemoveComponent<NearbyRideable>(entity);
                    }
                }
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            
            rideables.Dispose();
            rideableTransforms.Dispose();
            rideableStates.Dispose();
        }
    }
}

