using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Player.Components
{
    /// <summary>
    /// Tracks collision state for cooldown management, collision history, and stagger response.
    /// Replicated via NetCode for authoritative collision handling.
    /// Extended for 7.3.5 asymmetric stagger and 7.3.8 stagger state support.
    /// 
    /// Epic 7.5.3 Bandwidth Optimizations:
    /// - All timer fields use Quantization=100 (0.01s precision, ~7 bits vs 32-bit float)
    /// - StaggerVelocity uses Quantization=1000 + InterpolateAndExtrapolate for smooth remote visuals
    /// - Intensity fields use Interpolate smoothing for animation blending
    /// - LastPowerRatio uses Quantization=1000 (0.001 precision for ratio accuracy)
    /// - LastHitDirection is a byte (0-3 values, 2 bits effective)
    /// 
    /// Epic 7.7.8 Delta Compression:
    /// - SendTypeOptimization = OnlyPredictedClients reduces bandwidth for non-predicted ghosts
    /// - Only sends collision state to clients that are predicting this entity
    /// - Remote spectators receive interpolated state only when needed
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted, SendTypeOptimization = GhostSendType.OnlyPredictedClients)]
    public struct PlayerCollisionState : IComponentData
    {
        // === Collision Tracking ===
        
        /// <summary>
        /// Network tick when last collision occurred (used for cooldown).
        /// </summary>
        public uint LastCollisionTick;
        
        /// <summary>
        /// Remaining cooldown time before next collision can be processed (in seconds).
        /// Prevents repeated collision processing in consecutive frames.
        /// </summary>
        [GhostField(Quantization = 100)] // 0.01s precision
        public float CollisionCooldown;
        
        /// <summary>
        /// Entity that was last collided with (for deduplication).
        /// </summary>
        public Entity LastCollisionEntity;
        
        // === Stagger State (7.3.8) ===
        
        /// <summary>
        /// Remaining stagger duration in seconds. Player is staggered while > 0.
        /// Set by collision response when impact exceeds stagger threshold.
        /// </summary>
        [GhostField(Quantization = 100)] // 0.01s precision
        public float StaggerTimeRemaining;
        
        /// <summary>
        /// Knockback velocity applied during stagger (decays with friction).
        /// Player moves in this direction while staggered.
        /// Epic 7.5.3: Quantization=1000 (0.001 precision) reduces bandwidth vs float precision.
        /// Smoothing=InterpolateAndExtrapolate enables client-side interpolation for smooth visuals.
        /// </summary>
        [GhostField(Quantization = 1000, Smoothing = SmoothingAction.InterpolateAndExtrapolate)]
        public float3 StaggerVelocity;
        
        /// <summary>
        /// Impact speed that triggered the current stagger (for animation intensity).
        /// Range 0-1 normalized for animator parameter.
        /// Epic 7.5.3: Smoothing for smooth animation blending on remote clients.
        /// </summary>
        [GhostField(Quantization = 100, Smoothing = SmoothingAction.Interpolate)]
        public float StaggerIntensity;
        
        // === Knockdown State (7.4.1) ===
        
        /// <summary>
        /// Remaining time in full knockdown phase (on ground, cannot move).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float KnockdownTimeRemaining;
        
        /// <summary>
        /// True if player is in recovery phase (getting up, limited movement).
        /// </summary>
        [GhostField]
        public bool IsRecoveringFromKnockdown;
        
        /// <summary>
        /// Remaining time in knockdown recovery phase.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float KnockdownRecoveryTimeRemaining;
        
        /// <summary>
        /// Impact speed that triggered knockdown (for animation intensity).
        /// Epic 7.5.3: Smoothing for smooth animation blending on remote clients.
        /// </summary>
        [GhostField(Quantization = 100, Smoothing = SmoothingAction.Interpolate)]
        public float KnockdownImpactSpeed;
        
        // === Collision Metrics (for asymmetric response - 7.3.5) ===
        
        /// <summary>
        /// Power ratio from last significant collision (0-1).
        /// 0.5 = equal power, >0.5 = we had more power, &lt;0.5 = they had more power.
        /// </summary>
        [GhostField(Quantization = 1000)] // 0.001 precision
        public float LastPowerRatio;
        
        /// <summary>
        /// Hit direction from last collision (see HitDirectionType constants).
        /// 0=braced, 1=side, 2=back, 3=evaded
        /// </summary>
        [GhostField]
        public byte LastHitDirection;
        
        // === Helper Properties ===
        
        /// <summary>True if player is currently in stagger state.</summary>
        public bool IsStaggered => StaggerTimeRemaining > 0;
        
        /// <summary>True if player is in knockdown (either phase).</summary>
        public bool IsKnockedDown => KnockdownTimeRemaining > 0 || IsRecoveringFromKnockdown;
        
        /// <summary>True if player cannot be staggered (already in worse state).</summary>
        public bool IsImmuneToStagger => IsKnockedDown;
    }
}
