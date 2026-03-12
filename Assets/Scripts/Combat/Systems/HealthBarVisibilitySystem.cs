using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Combat.Components;
using Player.Components;

namespace DIG.Combat.UI
{
    /// <summary>
    /// ECS System that updates HealthBarVisibilityState for all entities with health.
    /// Runs before the bridge system to ensure visibility state is current.
    /// 
    /// This system:
    /// - Detects damage events and updates timers
    /// - Interpolates alpha for fade transitions
    /// - Does NOT evaluate visibility (that's done by the bridge with the config)
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial struct HealthBarVisibilityStateSystem : ISystem
    {
        private EntityQuery _healthQuery;
        private double _previousTime;
        
        public void OnCreate(ref SystemState state)
        {
            _healthQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<Health>(),
                ComponentType.ReadOnly<LocalToWorld>(),
                ComponentType.ReadWrite<HealthBarVisibilityState>()
            );
            
            state.RequireForUpdate<Health>();
        }
        
        public void OnDestroy(ref SystemState state) { }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            double currentTime = SystemAPI.Time.ElapsedTime;
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            // Update visibility states
            var updateJob = new UpdateVisibilityStateJob
            {
                CurrentTime = currentTime,
                DeltaTime = deltaTime,
                FadeSpeed = 4f // Alpha units per second (0.25s for full fade)
            };
            
            state.Dependency = updateJob.ScheduleParallel(state.Dependency);
            
            _previousTime = currentTime;
        }
        
        [BurstCompile]
        private partial struct UpdateVisibilityStateJob : IJobEntity
        {
            public double CurrentTime;
            public float DeltaTime;
            public float FadeSpeed;
            
            public void Execute(
                Entity entity,
                ref HealthBarVisibilityState visState,
                RefRO<Health> health)
            {
                // Detect damage by comparing with previous HP
                if (visState.PreviousHP >= 0 && health.ValueRO.Current < visState.PreviousHP)
                {
                    // Damage occurred
                    visState.LastDamageTime = CurrentTime;
                }
                
                // Store current HP for next frame comparison
                visState.PreviousHP = health.ValueRO.Current;
                
                // Interpolate alpha towards target
                if (!math.abs(visState.CurrentAlpha - visState.TargetAlpha).Equals(0f))
                {
                    float direction = visState.TargetAlpha > visState.CurrentAlpha ? 1f : -1f;
                    visState.CurrentAlpha += direction * FadeSpeed * DeltaTime;
                    visState.CurrentAlpha = math.clamp(visState.CurrentAlpha, 0f, 1f);
                    
                    // Snap when close enough
                    if (math.abs(visState.CurrentAlpha - visState.TargetAlpha) < 0.01f)
                    {
                        visState.CurrentAlpha = visState.TargetAlpha;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// System that adds HealthBarVisibilityState to entities that have Health but lack visibility state.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct HealthBarVisibilityStateInitSystem : ISystem
    {
        private EntityQuery _missingStateQuery;
        
        public void OnCreate(ref SystemState state)
        {
            _missingStateQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<Health>(),
                ComponentType.Exclude<HealthBarVisibilityState>()
            );
            
            state.RequireForUpdate(_missingStateQuery);
        }
        
        public void OnDestroy(ref SystemState state) { }
        
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (health, entity) in SystemAPI.Query<RefRO<Health>>()
                .WithNone<HealthBarVisibilityState>()
                .WithEntityAccess())
            {
                ecb.AddComponent(entity, HealthBarVisibilityState.Default);
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
