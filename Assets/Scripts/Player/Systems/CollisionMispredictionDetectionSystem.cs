using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Player.Components;
using DIG.Performance;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Detects collision state mispredictions by comparing predicted values before and after
    /// ghost snapshot application. When significant differences are detected, adds a
    /// CollisionReconcile component to smooth the correction over multiple frames.
    /// 
    /// Epic 7.5.1: Rollback handling for collision mispredictions.
    /// 
    /// This system runs after ghost snapshots are applied but before the next prediction tick.
    /// It stores predicted values before snapshot application and compares after.
    /// </summary>
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [UpdateAfter(typeof(GhostReceiveSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class CollisionMispredictionDetectionSystem : SystemBase
    {
        /// <summary>
        /// Minimum velocity difference magnitude to trigger smoothing (avoid micro-corrections).
        /// </summary>
        private const float VelocityThreshold = 0.5f;
        
        /// <summary>
        /// Minimum timer difference to trigger smoothing.
        /// </summary>
        private const float TimerThreshold = 0.05f;
        
        /// <summary>
        /// Duration over which to smooth corrections (seconds).
        /// </summary>
        private const float SmoothingDuration = 0.1f;

        private CollisionPredictionCaptureSystem _captureSystem;

        protected override void OnCreate()
        {
            _captureSystem = World.GetExistingSystemManaged<CollisionPredictionCaptureSystem>();
        }

        protected override void OnUpdate()
        {
            // Epic 7.7.1: Profile misprediction detection
            using (CollisionProfilerMarkers.MispredictionDetection.Auto())
            {
            if (_captureSystem == null)
            {
                _captureSystem = World.GetExistingSystemManaged<CollisionPredictionCaptureSystem>();
                if (_captureSystem == null)
                    return;
            }

            // Epic 7.7.2: Use TempJob allocator for better per-frame performance
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.TempJob);

            // Compare post-snapshot state with captured pre-snapshot state
            foreach (var (collisionState, entity) in 
                SystemAPI.Query<RefRO<PlayerCollisionState>>()
                    .WithAll<GhostOwnerIsLocal, Simulate>()
                    .WithNone<CollisionReconcile>() // Don't add if already reconciling
                    .WithEntityAccess())
            {
                if (!_captureSystem.TryGetCapturedState(entity, out var captured))
                    continue;

                var current = collisionState.ValueRO;
                
                // Calculate differences between predicted and server-corrected state
                float3 velocityDiff = current.StaggerVelocity - captured.StaggerVelocity;
                float staggerTimeDiff = current.StaggerTimeRemaining - captured.StaggerTimeRemaining;
                float knockdownTimeDiff = current.KnockdownTimeRemaining - captured.KnockdownTimeRemaining;
                float cooldownDiff = current.CollisionCooldown - captured.CollisionCooldown;
                
                float velocityDiffMag = math.length(velocityDiff);
                
                // Check if correction is significant enough to warrant smoothing
                // Epic 7.5.2: Include cooldown differences for dual-client collision scenarios
                bool needsSmoothing = velocityDiffMag > VelocityThreshold ||
                                      math.abs(staggerTimeDiff) > TimerThreshold ||
                                      math.abs(knockdownTimeDiff) > TimerThreshold ||
                                      math.abs(cooldownDiff) > TimerThreshold;
                
                if (needsSmoothing)
                {
                    // Add reconciliation component to smooth the correction
                    // We apply the INVERSE adjustment to undo the snap, then let it lerp back
                    ecb.AddComponent(entity, new CollisionReconcile
                    {
                        VelocityAdjustment = -velocityDiff, // Undo snap, then smooth back
                        StaggerTimeAdjustment = -staggerTimeDiff,
                        KnockdownTimeAdjustment = -knockdownTimeDiff,
                        CooldownAdjustment = -cooldownDiff, // Epic 7.5.2: Cooldown reconciliation
                        TotalTime = SmoothingDuration,
                        RemainingTime = SmoothingDuration
                    });
                    
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    UnityEngine.Debug.Log($"[CollisionReconcile] Entity {entity.Index}: vel diff={velocityDiffMag:F2}, stagger diff={staggerTimeDiff:F2}, knockdown diff={knockdownTimeDiff:F2}, cooldown diff={cooldownDiff:F2}");
                    #endif
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
            } // End MispredictionDetection profiler marker
        }
    }
    
    /// <summary>
    /// Stores predicted collision state values before ghost snapshot application
    /// for comparison and misprediction detection.
    /// Epic 7.5.1: Pre-snapshot state capture for rollback detection.
    /// </summary>
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [UpdateBefore(typeof(GhostReceiveSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class CollisionPredictionCaptureSystem : SystemBase
    {
        /// <summary>
        /// Captured predicted values from before snapshot application.
        /// Keyed by entity index for fast lookup.
        /// </summary>
        private Unity.Collections.NativeHashMap<int, CapturedCollisionState> _capturedStates;

        protected override void OnCreate()
        {
            _capturedStates = new Unity.Collections.NativeHashMap<int, CapturedCollisionState>(32, Unity.Collections.Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (_capturedStates.IsCreated)
                _capturedStates.Dispose();
        }

        protected override void OnUpdate()
        {
            _capturedStates.Clear();

            // Capture current predicted state before ghost snapshots modify it
            foreach (var (collisionState, entity) in 
                SystemAPI.Query<RefRO<PlayerCollisionState>>()
                    .WithAll<GhostOwnerIsLocal, Simulate>()
                    .WithEntityAccess())
            {
                var state = collisionState.ValueRO;
                
                _capturedStates[entity.Index] = new CapturedCollisionState
                {
                    StaggerVelocity = state.StaggerVelocity,
                    StaggerTimeRemaining = state.StaggerTimeRemaining,
                    KnockdownTimeRemaining = state.KnockdownTimeRemaining,
                    CollisionCooldown = state.CollisionCooldown
                };
            }
        }
        
        /// <summary>
        /// Gets the captured state for an entity, if available.
        /// </summary>
        public bool TryGetCapturedState(Entity entity, out CapturedCollisionState captured)
        {
            return _capturedStates.TryGetValue(entity.Index, out captured);
        }
    }
    
    /// <summary>
    /// Captured collision state values for misprediction comparison.
    /// </summary>
    public struct CapturedCollisionState
    {
        public float3 StaggerVelocity;
        public float StaggerTimeRemaining;
        public float KnockdownTimeRemaining;
        /// <summary>
        /// Collision cooldown for Epic 7.5.2 cooldown reconciliation.
        /// </summary>
        public float CollisionCooldown;
    }
}
