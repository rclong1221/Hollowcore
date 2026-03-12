using Unity.Entities;

namespace DIG.Player.Components
{
    /// <summary>
    /// Singleton component containing global collision response settings.
    /// Designer-tunable parameters for collision behavior and performance.
    /// Extended for 7.3.1 Two-Phase Architecture, 7.3.4 Separation, and 7.3.5 Asymmetric Stagger.
    /// </summary>
    public struct PlayerCollisionSettings : IComponentData
    {
        // === Basic Settings ===
        
        /// <summary>
        /// Global multiplier for all push forces (tuning knob for gameplay feel).
        /// Default: 1.0 (no scaling)
        /// </summary>
        public float PushForceMultiplier;
        
        /// <summary>
        /// Maximum push force magnitude to prevent explosive separations (in Newtons).
        /// Default: 50.0 (sufficient for 80kg player at ~0.6 m/s change)
        /// </summary>
        public float MaxPushForce;
        
        /// <summary>
        /// Cooldown duration between collision processing for same entity pair (in seconds).
        /// Prevents repeated frame-by-frame collision events.
        /// Default: 0.1s (6 frames at 60 FPS)
        /// </summary>
        public float CollisionCooldownDuration;
        
        /// <summary>
        /// Enable/disable collision response system (for debugging or cutscenes).
        /// Default: true
        /// </summary>
        public bool EnableCollisionResponse;
        
        /// <summary>
        /// Coefficient of restitution (bounciness) for player collisions.
        /// Range: 0.0 (perfectly inelastic) to 1.0 (perfectly elastic)
        /// Default: 0.1 (slight bounce, mostly absorbed)
        /// </summary>
        public float Restitution;
        
        /// <summary>
        /// Friction coefficient for tangential collision forces.
        /// Higher values = more resistance to sliding past each other.
        /// Default: 0.3 (moderate friction)
        /// </summary>
        public float Friction;
        
        // === Physical Separation Settings (7.3.4) ===
        
        /// <summary>
        /// How aggressively to resolve player overlap (impulse multiplier).
        /// Higher = faster separation, may feel snappy.
        /// Default: 10.0
        /// </summary>
        public float SeparationStrength;
        
        /// <summary>
        /// Maximum separation speed to prevent physics explosions (m/s).
        /// Default: 3.0
        /// </summary>
        public float MaxSeparationSpeed;
        
        /// <summary>
        /// Combined radius for two players (slightly more than 2*capsuleRadius for safety).
        /// Default: 0.85 (two 0.4m radius capsules + 0.05m margin)
        /// </summary>
        public float CombinedPlayerRadius;
        
        // === Asymmetric Stagger Settings (7.3.5) ===
        
        /// <summary>
        /// Base effective mass for power calculations (kg).
        /// Used in power = mass * speed * stanceMultiplier formula.
        /// Default: 80.0 (average human mass)
        /// </summary>
        public float EffectiveMass;
        
        /// <summary>
        /// Minimum impact speed to trigger stagger (m/s).
        /// Collisions below this threshold only cause separation.
        /// Default: 3.0
        /// </summary>
        public float StaggerThreshold;
        
        /// <summary>
        /// Base stagger duration at minimum threshold (seconds).
        /// Actual duration scaled by power ratio.
        /// Default: 0.15
        /// </summary>
        public float MinStaggerDuration;
        
        /// <summary>
        /// Maximum stagger duration for extreme power imbalances (seconds).
        /// Default: 0.4
        /// </summary>
        public float MaxStaggerDuration;
        
        /// <summary>
        /// Power ratio threshold for triggering knockdown instead of stagger.
        /// If loser's ratio &lt; (1 - this), they get knocked down.
        /// Default: 0.8 (meaning loser must have &lt;20% of total power)
        /// </summary>
        public float KnockdownPowerThreshold;
        
        /// <summary>
        /// Friction applied to stagger velocity each frame.
        /// Higher = quicker stop after knockback.
        /// Default: 8.0
        /// </summary>
        public float StaggerFriction;
        
        // === Stance Multipliers (7.3.5) ===
        
        /// <summary>Collision power multiplier when standing. Default: 1.0</summary>
        public float StanceMultiplierStanding;
        
        /// <summary>Collision power multiplier when crouching (lower CoM = more stable). Default: 1.3</summary>
        public float StanceMultiplierCrouching;
        
        /// <summary>Collision power multiplier when prone (can be stepped over). Default: 0.5</summary>
        public float StanceMultiplierProne;
        
        // === Movement State Multipliers (7.3.5) ===
        
        /// <summary>Collision power multiplier when idle. Default: 0.6</summary>
        public float MovementMultiplierIdle;
        
        /// <summary>Collision power multiplier when walking. Default: 0.8</summary>
        public float MovementMultiplierWalking;
        
        /// <summary>Collision power multiplier when running. Default: 1.0</summary>
        public float MovementMultiplierRunning;
        
        /// <summary>Collision power multiplier when sprinting. Default: 1.5</summary>
        public float MovementMultiplierSprinting;
        
        // === Directional Bonuses (7.3.6) ===
        
        /// <summary>Enable directional hit bonuses (braced/side/back). Default: true</summary>
        public bool DirectionalBonusEnabled;
        
        /// <summary>Stagger multiplier when facing collision (braced). Default: 0.6</summary>
        public float BracedStaggerMultiplier;
        
        /// <summary>Stagger multiplier for side hits. Default: 1.0</summary>
        public float SideHitStaggerMultiplier;
        
        /// <summary>Stagger multiplier when hit from behind. Default: 1.4</summary>
        public float BackHitStaggerMultiplier;
        
        /// <summary>Facing dot product threshold for braced (facing collision). Default: 0.5</summary>
        public float BracedDotThreshold;
        
        /// <summary>Facing dot product threshold for back hit. Default: -0.5</summary>
        public float BackHitDotThreshold;
        
        // === Knockdown Settings (7.4.1) ===
        
        /// <summary>Duration of full knockdown phase (on ground). Default: 0.8</summary>
        public float KnockdownDuration;
        
        /// <summary>Duration of knockdown recovery phase (getting up). Default: 0.5</summary>
        public float KnockdownRecoveryDuration;
        
        /// <summary>Movement speed multiplier during knockdown recovery. Default: 0.3</summary>
        public float KnockdownRecoverySpeedMultiplier;
        
        // === Dodge Immunity Settings (7.4.3) ===
        
        /// <summary>Power multiplier when dodging. Lower = more reduction. Default: 0.3 (70% reduction)</summary>
        public float DodgeCollisionMultiplier;
        
        /// <summary>Angle to deflect collision during dodge (degrees). Default: 30</summary>
        public float DodgeDeflectionAngle;
        
        /// <summary>Time after dodge start when i-frames begin (seconds). Default: 0.1</summary>
        public float DodgeIFrameStart;
        
        /// <summary>Duration of full i-frame immunity (seconds). Default: 0.4</summary>
        public float DodgeIFrameDuration;

        // === Collision Audio & VFX Settings (7.4.4) ===

        /// <summary>Minimum impact force required to play collision audio. Default: 0.2</summary>
        public float CollisionAudioMinForce;

        /// <summary>Impact force corresponding to max collision audio volume/intensity. Default: 1.0</summary>
        public float CollisionAudioMaxForce;

        /// <summary>Global cap on collision sounds played per frame to prevent spam. Default: 3</summary>
        public int MaxCollisionSoundsPerFrame;

        /// <summary>Impact force threshold for camera shake on local player. Default: 0.6</summary>
        public float CameraShakeForceThreshold;

        /// <summary>Camera shake amplitude at max intensity. Default: 0.3</summary>
        public float CameraShakeIntensity;

        /// <summary>Duration of camera shake (seconds). Default: 0.2</summary>
        public float CameraShakeDuration;
        
        // === Debug Settings (7.3.2) ===
        
        /// <summary>Enable collision debug visualization gizmos. Default: false</summary>
        public bool DebugVisualizationEnabled;
        
        /// <summary>Duration to display collision gizmos (seconds). Default: 0.5</summary>
        public float DebugGizmoDuration;
        
        /// <summary>Radius of contact point sphere gizmos. Default: 0.1</summary>
        public float DebugContactPointRadius;
        
        /// <summary>Length of contact normal arrow gizmos. Default: 0.5</summary>
        public float DebugNormalArrowLength;
        
        /// <summary>
        /// Creates default collision settings with recommended values.
        /// </summary>
        public static PlayerCollisionSettings Default => new PlayerCollisionSettings
        {
            // Basic
            PushForceMultiplier = 3.0f,
            MaxPushForce = 150.0f,
            CollisionCooldownDuration = 0.1f,
            EnableCollisionResponse = true,
            Restitution = 0.1f,
            Friction = 0.3f,
            
            // Separation (7.3.4)
            SeparationStrength = 10.0f,
            MaxSeparationSpeed = 3.0f,
            CombinedPlayerRadius = 0.85f,
            
            // Asymmetric Stagger (7.3.5)
            EffectiveMass = 80.0f,
            StaggerThreshold = 1.0f,  // Lower for easier testing (was 3.0f)
            MinStaggerDuration = 0.15f,
            MaxStaggerDuration = 0.4f,
            KnockdownPowerThreshold = 0.9f,  // Knockdown only if powerRatio < 0.1 (extreme power difference required)
            StaggerFriction = 8.0f,
            
            // Stance Multipliers
            StanceMultiplierStanding = 1.0f,
            StanceMultiplierCrouching = 1.3f,
            StanceMultiplierProne = 0.5f,
            
            // Movement Multipliers
            MovementMultiplierIdle = 0.6f,
            MovementMultiplierWalking = 0.8f,
            MovementMultiplierRunning = 1.0f,
            MovementMultiplierSprinting = 1.5f,
            
            // Directional Bonuses (7.3.6)
            DirectionalBonusEnabled = true,
            BracedStaggerMultiplier = 0.6f,
            SideHitStaggerMultiplier = 1.0f,
            BackHitStaggerMultiplier = 1.4f,
            BracedDotThreshold = 0.5f,
            BackHitDotThreshold = -0.5f,
            
            // Knockdown (7.4.1)
            KnockdownDuration = 0.8f,
            KnockdownRecoveryDuration = 0.5f,
            KnockdownRecoverySpeedMultiplier = 0.3f,
            
            // Dodge Immunity (7.4.3)
            DodgeCollisionMultiplier = 0.3f,       // 70% damage reduction while dodging
            DodgeDeflectionAngle = 30f,            // Deflect collision 30 degrees tangent
            DodgeIFrameStart = 0.1f,               // I-frames start 0.1s after dodge begins
            DodgeIFrameDuration = 0.4f,            // I-frames last 0.4s

            // Collision Audio/VFX (7.4.4)
            CollisionAudioMinForce = 0.2f,
            CollisionAudioMaxForce = 1.0f,
            MaxCollisionSoundsPerFrame = 3,
            CameraShakeForceThreshold = 0.6f,
            CameraShakeIntensity = 0.3f,
            CameraShakeDuration = 0.2f,
            
            // Debug (7.3.2)
            DebugVisualizationEnabled = false,
            DebugGizmoDuration = 0.5f,
            DebugContactPointRadius = 0.1f,
            DebugNormalArrowLength = 0.5f
        };
    }
}
