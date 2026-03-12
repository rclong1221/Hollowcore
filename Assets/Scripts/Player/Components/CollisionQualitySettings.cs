using Unity.Entities;

namespace DIG.Player.Components
{
    /// <summary>
    /// Epic 7.7.4: Quality level for collision detection.
    /// 
    /// Higher quality = more accurate detection, more CPU cost.
    /// Lower quality = faster processing, reduced fidelity.
    /// </summary>
    public enum CollisionQualityLevel : byte
    {
        /// <summary>
        /// Full collision detection with all features enabled.
        /// Best for &lt;30 players.
        /// - Full proximity detection with spatial hash
        /// - All filtering: team checks, grace periods, dodge i-frames
        /// - Power calculations with asymmetric mass/velocity/stance
        /// - Stagger/knockdown state management
        /// - Audio/VFX/haptics for all collisions
        /// </summary>
        High = 0,
        
        /// <summary>
        /// Reduced collision detection for moderate player counts.
        /// Best for 30-100 players.
        /// - Proximity detection with increased threshold (1.5m)
        /// - Simplified filtering: team checks, no dodge deflection
        /// - Skip soft collision force calculation
        /// - Batched audio/VFX (1 per player per second)
        /// </summary>
        Medium = 1,
        
        /// <summary>
        /// Minimal collision detection for high player counts.
        /// Best for &gt;100 players.
        /// - Spatial hash only, no sub-cell queries
        /// - No grace period checks
        /// - No GroupIndex overrides
        /// - No stagger animations (instant transitions)
        /// - Audio/VFX disabled entirely
        /// </summary>
        Low = 2
    }
    
    /// <summary>
    /// Epic 7.7.7: Platform preset for default quality settings.
    /// </summary>
    public enum CollisionPlatformPreset : byte
    {
        /// <summary>PC/Desktop - highest performance, supports 100+ players at High quality.</summary>
        PC = 0,
        /// <summary>Console (PS5/Xbox) - good performance, supports 50 players at High quality.</summary>
        Console = 1,
        /// <summary>Mobile/Steam Deck - limited performance, supports 25 players at High quality.</summary>
        Mobile = 2,
        /// <summary>Use custom settings, don't apply platform defaults.</summary>
        Custom = 3
    }
    
    /// <summary>
    /// Epic 7.7.4: Singleton component for adaptive collision quality scaling.
    /// 
    /// Automatically adjusts collision quality based on player count and frame time
    /// to maintain target framerate. Can also be manually overridden.
    /// </summary>
    public struct CollisionQualitySettings : IComponentData
    {
        // === Current State ===
        
        /// <summary>
        /// Current quality level being used for collision detection.
        /// Updated by CollisionQualitySystem based on player count and frame time.
        /// </summary>
        public CollisionQualityLevel CurrentQuality;
        
        /// <summary>
        /// Whether auto-adjustment is enabled.
        /// When false, CurrentQuality stays at ManualQuality.
        /// </summary>
        public bool AutoAdjustEnabled;
        
        /// <summary>
        /// Manual quality override when AutoAdjustEnabled is false.
        /// </summary>
        public CollisionQualityLevel ManualQuality;
        
        /// <summary>
        /// Current player count (cached for UI display).
        /// Updated each frame by CollisionQualitySystem.
        /// </summary>
        public int CurrentPlayerCount;
        
        /// <summary>
        /// Epic 7.7.7: Platform preset for default quality settings.
        /// Set on initialization based on Application.platform.
        /// </summary>
        public CollisionPlatformPreset PlatformPreset;
        
        // === Thresholds ===
        
        /// <summary>
        /// Player count threshold for High → Medium quality transition.
        /// Default: 30 players.
        /// </summary>
        public int HighToMediumThreshold;
        
        /// <summary>
        /// Player count threshold for Medium → Low quality transition.
        /// Default: 100 players.
        /// </summary>
        public int MediumToLowThreshold;
        
        /// <summary>
        /// Hysteresis offset for quality upgrade (prevents oscillation).
        /// Quality upgrades when player count drops below threshold - hysteresis.
        /// Default: 5 players.
        /// </summary>
        public int HysteresisOffset;
        
        // === Frame Time Settings ===
        
        /// <summary>
        /// Target frame time in seconds.
        /// Default: 0.01667 (60 FPS).
        /// </summary>
        public float TargetFrameTime;
        
        /// <summary>
        /// Frame time threshold that triggers quality downgrade.
        /// Default: 0.020 (50 FPS = 20ms).
        /// </summary>
        public float DowngradeFrameTime;
        
        /// <summary>
        /// Seconds of stable frame time before attempting quality upgrade.
        /// Default: 5 seconds.
        /// </summary>
        public float UpgradeStabilityDuration;
        
        /// <summary>
        /// Accumulated stable time (frame time below target).
        /// Reset when frame time exceeds target.
        /// </summary>
        public float StableTimeAccumulator;
        
        // === Quality-Specific Settings ===
        
        /// <summary>
        /// Collision threshold at Medium quality (increased from default 1.2m).
        /// Default: 1.5m.
        /// </summary>
        public float MediumQualityThreshold;
        
        /// <summary>
        /// Maximum audio/VFX events per player per second at Medium quality.
        /// Default: 1.0 (one event per second per player).
        /// </summary>
        public float MediumQualityEventRate;
        
        /// <summary>
        /// Create default settings for typical game scenarios.
        /// </summary>
        public static CollisionQualitySettings CreateDefault()
        {
            return new CollisionQualitySettings
            {
                CurrentQuality = CollisionQualityLevel.High,
                AutoAdjustEnabled = true,
                ManualQuality = CollisionQualityLevel.High,
                CurrentPlayerCount = 0,
                PlatformPreset = CollisionPlatformPreset.PC,
                
                HighToMediumThreshold = 30,
                MediumToLowThreshold = 100,
                HysteresisOffset = 5,
                
                TargetFrameTime = 1f / 60f, // 60 FPS
                DowngradeFrameTime = 0.020f, // 50 FPS
                UpgradeStabilityDuration = 5f,
                StableTimeAccumulator = 0f,
                
                MediumQualityThreshold = 1.5f,
                MediumQualityEventRate = 1f
            };
        }
        
        /// <summary>
        /// Epic 7.7.7: Create settings for a specific platform preset.
        /// </summary>
        public static CollisionQualitySettings CreateForPlatform(CollisionPlatformPreset platform)
        {
            var settings = CreateDefault();
            settings.PlatformPreset = platform;
            
            switch (platform)
            {
                case CollisionPlatformPreset.PC:
                    // PC: Highest performance, High quality for up to 100 players
                    settings.HighToMediumThreshold = 100;
                    settings.MediumToLowThreshold = 200;
                    settings.TargetFrameTime = 1f / 120f; // 120 FPS target
                    break;
                    
                case CollisionPlatformPreset.Console:
                    // Console: Good performance, High quality for up to 50 players
                    settings.HighToMediumThreshold = 50;
                    settings.MediumToLowThreshold = 100;
                    settings.TargetFrameTime = 1f / 60f; // 60 FPS target
                    break;
                    
                case CollisionPlatformPreset.Mobile:
                    // Mobile: Limited performance, High quality for up to 25 players
                    settings.HighToMediumThreshold = 25;
                    settings.MediumToLowThreshold = 50;
                    settings.TargetFrameTime = 1f / 30f; // 30 FPS target
                    settings.DowngradeFrameTime = 0.040f; // 25 FPS threshold
                    break;
                    
                default:
                    // Custom: use default settings
                    break;
            }
            
            return settings;
        }
        
        /// <summary>
        /// Get the collision threshold based on current quality level.
        /// High quality uses the default system threshold.
        /// </summary>
        public float GetCollisionThreshold(float defaultThreshold)
        {
            return CurrentQuality switch
            {
                CollisionQualityLevel.High => defaultThreshold,
                CollisionQualityLevel.Medium => MediumQualityThreshold,
                CollisionQualityLevel.Low => MediumQualityThreshold, // Same as medium, but fewer checks
                _ => defaultThreshold
            };
        }
        
        /// <summary>
        /// Whether team filtering should be applied at current quality.
        /// </summary>
        public bool ShouldCheckTeams => CurrentQuality != CollisionQualityLevel.Low;
        
        /// <summary>
        /// Whether grace period filtering should be applied at current quality.
        /// </summary>
        public bool ShouldCheckGracePeriods => CurrentQuality != CollisionQualityLevel.Low;
        
        /// <summary>
        /// Whether dodge deflection should be calculated at current quality.
        /// </summary>
        public bool ShouldCalculateDodgeDeflection => CurrentQuality == CollisionQualityLevel.High;
        
        /// <summary>
        /// Whether soft collision forces should be calculated at current quality.
        /// </summary>
        public bool ShouldCalculateSoftCollisions => CurrentQuality == CollisionQualityLevel.High;
        
        /// <summary>
        /// Whether stagger animations should play at current quality.
        /// </summary>
        public bool ShouldPlayStaggerAnimations => CurrentQuality != CollisionQualityLevel.Low;
        
        /// <summary>
        /// Whether audio/VFX should be enabled at current quality.
        /// </summary>
        public bool ShouldPlayAudioVFX => CurrentQuality != CollisionQualityLevel.Low;
        
        /// <summary>
        /// Whether to use full 3x3 neighborhood query or just same-cell at current quality.
        /// </summary>
        public bool ShouldQueryNeighborCells => CurrentQuality != CollisionQualityLevel.Low;
    }
}
