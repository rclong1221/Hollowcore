using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using DIG.Items.Components;

namespace DIG.Items.Systems
{
    /// <summary>
    /// Processes ShellSpawnRequest entities and spawns physics-enabled shell entities.
    /// Works with per-weapon shell prefabs (no global spawner needed).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ShellSpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (request, requestEntity) in SystemAPI.Query<RefRO<ShellSpawnRequest>>().WithEntityAccess())
            {
                if (request.ValueRO.ShellPrefab == Entity.Null)
                {
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }
                
                // Spawn the shell entity from the weapon's specific prefab
                var shellEntity = ecb.Instantiate(request.ValueRO.ShellPrefab);
                
                // Set position and rotation
                ecb.SetComponent(shellEntity, new LocalTransform
                {
                    Position = request.ValueRO.Position,
                    Rotation = request.ValueRO.Rotation,
                    Scale = 1f
                });
                
                // Apply physics velocity
                ecb.SetComponent(shellEntity, new PhysicsVelocity
                {
                    Linear = request.ValueRO.EjectionVelocity,
                    Angular = request.ValueRO.AngularVelocity
                });
                
                // Add lifetime component for auto-destruction
                ecb.AddComponent(shellEntity, new ShellLifetime
                {
                    RemainingTime = request.ValueRO.Lifetime > 0 ? request.ValueRO.Lifetime : 5f
                });
                
                // Destroy the request entity
                ecb.DestroyEntity(requestEntity);
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
    
    /// <summary>
    /// System to destroy shells after their lifetime expires.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ShellSpawnSystem))]
    public partial struct ShellLifetimeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (lifetime, entity) in SystemAPI.Query<RefRW<ShellLifetime>>().WithEntityAccess())
            {
                lifetime.ValueRW.RemainingTime -= deltaTime;
                if (lifetime.ValueRO.RemainingTime <= 0)
                {
                    ecb.DestroyEntity(entity);
                }
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
