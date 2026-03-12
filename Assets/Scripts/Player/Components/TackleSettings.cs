using Unity.Entities;
using Unity.NetCode;

namespace DIG.Player.Components
{
    /// <summary>
    /// Tackle settings singleton for Epic 7.4.2.
    /// Configuration for the tackle system (intentional knockdown action).
    /// </summary>
    [GhostComponent(SendTypeOptimization = GhostSendType.OnlyPredictedClients)]
    public struct TackleSettings : IComponentData
    {
        // ===== INITIATION =====
        
        /// <summary>
        /// Minimum speed required to initiate a tackle (m/s).
        /// Player must be moving at least this fast to tackle.
        /// Default: 5.0 m/s (sprinting speed threshold)
        /// </summary>
        public float TackleMinSpeed;
        
        /// <summary>
        /// Duration of active tackle phase (seconds).
        /// Player commits to tackle direction for this duration.
        /// Default: 0.5s
        /// </summary>
        public float TackleDuration;
        
        /// <summary>
        /// Speed multiplier applied during tackle.
        /// Tackle speed = current speed * this multiplier.
        /// Default: 1.3 (30% speed boost during lunge)
        /// </summary>
        public float TackleSpeedMultiplier;
        
        /// <summary>
        /// Stamina cost to initiate tackle.
        /// Tackle fails if player doesn't have enough stamina.
        /// Default: 35 (high cost for powerful action)
        /// </summary>
        public float TackleStaminaCost;
        
        /// <summary>
        /// Cooldown duration before tackle can be used again (seconds).
        /// Prevents tackle spam.
        /// Default: 3.0s
        /// </summary>
        public float TackleCooldownDuration;
        
        // ===== HIT DETECTION =====
        
        /// <summary>
        /// Radius of tackle hit detection cone (meters).
        /// Targets within this radius from tackle direction are hit.
        /// Default: 0.6m (wider than player radius for forgiving hits)
        /// </summary>
        public float TackleHitRadius;
        
        /// <summary>
        /// Distance of tackle hit detection (meters).
        /// Checks for targets up to this distance in front of tackler.
        /// Default: 1.5m (lunge reach)
        /// </summary>
        public float TackleHitDistance;
        
        /// <summary>
        /// Angle of tackle hit cone (degrees).
        /// Targets within this angle from tackle direction are hit.
        /// Default: 45° (90° total cone)
        /// </summary>
        public float TackleHitAngle;
        
        // ===== HIT EFFECTS =====
        
        /// <summary>
        /// Knockdown duration for target when hit by tackle (seconds).
        /// Target enters knockdown state for this duration.
        /// Default: 1.5s (longer than collision knockdown)
        /// </summary>
        public float TackleKnockdownDuration;
        
        /// <summary>
        /// Stagger duration for tackler after successful hit (seconds).
        /// Tackler briefly staggers after hitting target.
        /// Default: 0.3s (brief recovery)
        /// </summary>
        public float TacklerHitRecoveryDuration;
        
        // ===== MISS EFFECTS =====
        
        /// <summary>
        /// Stagger duration for tackler after missing (seconds).
        /// Punishment for whiffing - longer stagger makes tackler vulnerable.
        /// Default: 0.6s (twice as long as hit recovery)
        /// </summary>
        public float TacklerMissRecoveryDuration;
        
        /// <summary>
        /// Knockback force multiplier for tackle hits.
        /// Applied to target on successful tackle.
        /// Default: 1.5 (stronger than normal collision)
        /// </summary>
        public float TackleKnockbackMultiplier;
        
        /// <summary>
        /// Default tackle settings.
        /// </summary>
        public static TackleSettings Default => new TackleSettings
        {
            TackleMinSpeed = 0.5f,
            TackleDuration = 0.5f,
            TackleSpeedMultiplier = 1.3f,
            TackleStaminaCost = 35f,
            TackleCooldownDuration = 3.0f,
            
            TackleHitRadius = 0.6f,
            TackleHitDistance = 1.5f,
            TackleHitAngle = 45f,
            
            TackleKnockdownDuration = 1.5f,
            TacklerHitRecoveryDuration = 0.3f,
            TacklerMissRecoveryDuration = 0.6f,
            TackleKnockbackMultiplier = 1.5f
        };
    }
}
