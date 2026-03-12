using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Player.Components;
using DIG.Performance;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Smoothly reconciles collision state differences between client prediction and server authority.
    /// When a CollisionReconcile component is present, this system gradually applies the correction
    /// to StaggerVelocity and timer fields to avoid visible pops/snaps.
    /// 
    /// Epic 7.5.1: Prediction smoothing for collision corrections.
    /// Epic 7.7.8: Added extrapolation cap to prevent runaway predictions.
    /// 
    /// Runs in PredictedSimulationSystemGroup after collision response systems.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerProximityCollisionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class CollisionReconciliationSystem : SystemBase
    {
        /// <summary>
        /// Maximum stagger velocity magnitude allowed (m/s).
        /// Prevents runaway extrapolation from creating unrealistic knockback.
        /// Typical max knockback is ~10 m/s; cap at 15 m/s for safety margin.
        /// Epic 7.7.8: Extrapolation cap.
        /// </summary>
        private const float MaxStaggerVelocityMagnitude = 15f;
        
        /// <summary>
        /// Maximum stagger time allowed (seconds).
        /// Prevents extrapolation from creating artificially long stagger states.
        /// Typical max stagger is ~2s; cap at 3s for safety margin.
        /// Epic 7.7.8: Extrapolation cap.
        /// </summary>
        private const float MaxStaggerTime = 3f;
        
        /// <summary>
        /// Maximum knockdown time allowed (seconds).
        /// Prevents extrapolation from creating artificially long knockdown states.
        /// Typical max knockdown is ~5s; cap at 7s for safety margin.
        /// Epic 7.7.8: Extrapolation cap.
        /// </summary>
        private const float MaxKnockdownTime = 7f;
        
        protected override void OnUpdate()
        {
            // Epic 7.7.1: Profile collision reconciliation
            using (CollisionProfilerMarkers.Reconciliation.Auto())
            {
            float dt = SystemAPI.Time.DeltaTime;
            
            // Epic 7.7.2: Use TempJob allocator for better per-frame performance
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.TempJob);

            foreach (var (collisionStateRW, reconcileRW, entity) in 
                SystemAPI.Query<RefRW<PlayerCollisionState>, RefRW<CollisionReconcile>>()
                    .WithEntityAccess())
            {
                ref var state = ref collisionStateRW.ValueRW;
                ref var recon = ref reconcileRW.ValueRW;

                if (recon.RemainingTime <= 0f)
                {
                    ecb.RemoveComponent<CollisionReconcile>(entity);
                    continue;
                }

                // Calculate interpolation fraction for this frame
                float step = math.min(dt, recon.RemainingTime);
                float frac = recon.TotalTime > 0f ? step / recon.TotalTime : 1f;

                // Apply velocity adjustment gradually
                float3 velocityStep = recon.VelocityAdjustment * frac;
                state.StaggerVelocity += velocityStep;

                // Apply timer adjustments gradually
                state.StaggerTimeRemaining += recon.StaggerTimeAdjustment * frac;
                state.KnockdownTimeRemaining += recon.KnockdownTimeAdjustment * frac;
                
                // Epic 7.5.2: Apply cooldown adjustment for dual-client collision scenarios
                state.CollisionCooldown += recon.CooldownAdjustment * frac;

                // Clamp to valid ranges
                state.StaggerTimeRemaining = math.max(0f, state.StaggerTimeRemaining);
                state.KnockdownTimeRemaining = math.max(0f, state.KnockdownTimeRemaining);
                state.CollisionCooldown = math.max(0f, state.CollisionCooldown);
                
                // Epic 7.7.8: Apply extrapolation caps to prevent runaway predictions
                // Cap timer maximums to prevent artificially long states from packet loss
                state.StaggerTimeRemaining = math.min(state.StaggerTimeRemaining, MaxStaggerTime);
                state.KnockdownTimeRemaining = math.min(state.KnockdownTimeRemaining, MaxKnockdownTime);
                
                // Cap velocity magnitude to prevent unrealistic knockback from extrapolation
                float velocityMagnitude = math.length(state.StaggerVelocity);
                if (velocityMagnitude > MaxStaggerVelocityMagnitude)
                {
                    state.StaggerVelocity = math.normalize(state.StaggerVelocity) * MaxStaggerVelocityMagnitude;
                }

                // Update remaining time
                recon.RemainingTime -= step;

                if (recon.RemainingTime <= 0f)
                {
                    ecb.RemoveComponent<CollisionReconcile>(entity);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
            } // End Reconciliation profiler marker
        }
    }
}
