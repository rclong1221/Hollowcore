using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using DIG.Targeting.Components;
using Player.Components;
using Player.Authoring;

namespace DIG.Targeting.Systems
{
    /// <summary>
    /// Automatically adds LockOnTarget component to entities with Health that don't have it yet.
    /// This runs on the server to ensure enemies spawned from prefabs get tagged for lock-on.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct LockOnTargetAutoTagSystem : ISystem
    {
        private bool _hasLoggedOnce;

        public void OnCreate(ref SystemState state)
        {
            // Use EntityQueryBuilder which is Burst-compatible
            state.RequireForUpdate<Health>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (health, entity) in 
                SystemAPI.Query<RefRO<Health>>()
                    .WithNone<LockOnTarget>()
                    .WithNone<PlayerTag>()
                    .WithEntityAccess())
            {
                // Add LockOnTarget with default values
                ecb.AddComponent(entity, new LockOnTarget
                {
                    Priority = 0,
                    IndicatorHeightOffset = 1.5f
                });
                
                // Enable the component
                ecb.SetComponentEnabled<LockOnTarget>(entity, true);
                
                #if UNITY_EDITOR
                if (!_hasLoggedOnce)
                {
                    UnityEngine.Debug.Log($"[LockOnAutoTag] Tagged entity {entity.Index} with LockOnTarget");
                    _hasLoggedOnce = true;
                }
                #endif
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
