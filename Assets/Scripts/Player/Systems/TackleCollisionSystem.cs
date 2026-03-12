using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using DIG.Player.Components;
using DIG.Performance;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Tackle collision system for Epic 7.4.2.
    /// Handles hit detection during tackle, applies knockdown to targets,
    /// and applies stagger to tackler based on hit/miss.
    /// 
    /// Uses cone-based detection in front of tackler.
    /// Runs after TackleSystem to process active tackles.
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(TackleSystem))]
    [BurstCompile]
    public partial struct TackleCollisionSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<TackleState> _tackleStateLookup;
        private ComponentLookup<PlayerCollisionState> _collisionStateLookup;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TackleSettings>();
            state.RequireForUpdate<NetworkTime>();
            
            _transformLookup = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
            _tackleStateLookup = state.GetComponentLookup<TackleState>();
            _collisionStateLookup = state.GetComponentLookup<PlayerCollisionState>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TackleSettings>(out var settings))
                return;
            
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick.TickIndexForValidTick;
            
            // Update lookups
            _transformLookup.Update(ref state);
            _tackleStateLookup.Update(ref state);
            _collisionStateLookup.Update(ref state);
            
            // Epic 7.7.2: Use optimized capacity and TempJob allocator
            // Track memory allocation for profiling
            MemoryOptimizationUtility.PlayerListAllocation.Begin();
            
            int targetCapacity = MemoryOptimizationUtility.CalculatePlayerListCapacity();
            var potentialTargets = new NativeList<TargetData>(targetCapacity, Allocator.TempJob);
            
            MemoryOptimizationUtility.PlayerListAllocation.End();
            
            foreach (var (transform, collisionState, entity) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerCollisionState>>()
                    .WithAll<PlayerTag, Simulate>()
                    .WithEntityAccess())
            {
                // Skip if already knocked down or staggered
                if (collisionState.ValueRO.KnockdownTimeRemaining > 0 ||
                    collisionState.ValueRO.StaggerTimeRemaining > 0)
                    continue;
                
                potentialTargets.Add(new TargetData
                {
                    Entity = entity,
                    Position = transform.ValueRO.Position
                });
            }
            
            // Process active tacklers
            foreach (var (tackleState, playerState, transform, collisionState, entity) in
                SystemAPI.Query<RefRW<TackleState>, RefRW<PlayerState>, RefRO<LocalTransform>, RefRW<PlayerCollisionState>>()
                    .WithAll<PlayerTag, Simulate>()
                    .WithEntityAccess())
            {
                ref var tackle = ref tackleState.ValueRW;
                ref var pState = ref playerState.ValueRW;
                ref var collision = ref collisionState.ValueRW;
                
                // Skip if not actively tackling
                if (tackle.TackleTimeRemaining <= 0)
                    continue;
                
                // Skip if already processed a hit this tackle
                if (tackle.HasProcessedHit)
                    continue;
                
                float3 tacklerPos = transform.ValueRO.Position;
                float3 tackleDir = tackle.TackleDirection;
                
                // Calculate hit cone parameters
                float cosHalfAngle = math.cos(math.radians(settings.TackleHitAngle));
                
                // Check all potential targets
                Entity hitTarget = Entity.Null;
                float closestDist = float.MaxValue;
                
                for (int i = 0; i < potentialTargets.Length; i++)
                {
                    var target = potentialTargets[i];
                    
                    // Skip self
                    if (target.Entity == entity)
                        continue;
                    
                    // Calculate direction and distance to target
                    float3 toTarget = target.Position - tacklerPos;
                    toTarget.y = 0; // Horizontal only
                    float dist = math.length(toTarget);
                    
                    // Skip if too far
                    if (dist > settings.TackleHitDistance)
                        continue;
                    
                    // Skip if too close (avoid edge cases)
                    if (dist < 0.1f)
                        continue;
                    
                    // Check if within cone angle
                    float3 toTargetNorm = math.normalize(toTarget);
                    float dot = math.dot(tackleDir, toTargetNorm);
                    
                    if (dot < cosHalfAngle)
                        continue;
                    
                    // Check if within hit radius (lateral distance from tackle line)
                    float lateralDist = math.sqrt(dist * dist - dot * dot * dist * dist);
                    if (lateralDist > settings.TackleHitRadius)
                        continue;
                    
                    // Valid target - check if closest
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        hitTarget = target.Entity;
                    }
                }
                
                // Process hit or miss
                if (hitTarget != Entity.Null)
                {
                    // === HIT ===
                    tackle.DidHitTarget = true;
                    tackle.HasProcessedHit = true;
                    
                    // Apply knockdown to target
                    if (_collisionStateLookup.HasComponent(hitTarget))
                    {
                        var targetCollision = _collisionStateLookup.GetRefRW(hitTarget);
                        
                        targetCollision.ValueRW.KnockdownTimeRemaining = settings.TackleKnockdownDuration;
                        targetCollision.ValueRW.KnockdownImpactSpeed = tackle.TackleSpeed;
                        targetCollision.ValueRW.StaggerVelocity = tackleDir * tackle.TackleSpeed * settings.TackleKnockbackMultiplier;
                        targetCollision.ValueRW.LastCollisionTick = currentTick;
                        targetCollision.ValueRW.LastCollisionEntity = entity;
                        
                        // Enable knockdown tag on target
                        SystemAPI.SetComponentEnabled<KnockedDown>(hitTarget, true);
                        SystemAPI.SetComponentEnabled<Staggered>(hitTarget, false);
                    }
                    
                    // Apply brief stagger to tackler (hit recovery)
                    collision.StaggerTimeRemaining = settings.TacklerHitRecoveryDuration;
                    collision.StaggerIntensity = 0.3f; // Light stagger
                    collision.StaggerVelocity = -tackleDir * 2f; // Slight knockback
                    
                    // Enable stagger tag on tackler
                    SystemAPI.SetComponentEnabled<Staggered>(entity, true);
                    
                    // End tackle early on hit
                    tackle.TackleTimeRemaining = 0;
                    pState.MovementState = PlayerMovementState.Staggered;
                }
                else if (tackle.TackleTimeRemaining <= SystemAPI.Time.DeltaTime)
                {
                    // === MISS (tackle ending without hitting anyone) ===
                    tackle.HasProcessedHit = true;
                    
                    // Apply longer stagger to tackler (punishment for whiffing)
                    collision.StaggerTimeRemaining = settings.TacklerMissRecoveryDuration;
                    collision.StaggerIntensity = 0.6f; // Heavier stagger
                    collision.StaggerVelocity = -tackleDir * 3f; // More knockback (stumble)
                    
                    // Enable stagger tag on tackler
                    SystemAPI.SetComponentEnabled<Staggered>(entity, true);
                    
                    pState.MovementState = PlayerMovementState.Staggered;
                }
            }
            
            // Epic 7.7.2: Track container disposal
            MemoryOptimizationUtility.ContainerDisposal.Begin();
            potentialTargets.Dispose();
            MemoryOptimizationUtility.ContainerDisposal.End();
        }
        
        private struct TargetData
        {
            public Entity Entity;
            public float3 Position;
        }
    }
}
