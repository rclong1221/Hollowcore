using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Player.Components;
using DIG.Performance;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Epic 7.7.4: Adaptive collision quality scaling system.
    /// 
    /// Monitors player count and frame time to automatically adjust collision
    /// quality level. Runs early in the frame to set quality before collision
    /// systems execute.
    /// 
    /// Quality Levels:
    /// - High (&lt;30 players): Full collision detection, all features enabled
    /// - Medium (30-100 players): Reduced fidelity, batched audio/VFX
    /// - Low (&gt;100 players): Minimal detection, audio/VFX disabled
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PlayerSpatialHashSystem))]
    public partial struct CollisionQualitySystem : ISystem
    {
        private EntityQuery _playerQuery;
        private bool _initialized;
        private float _lastFrameTime;
        
        public void OnCreate(ref SystemState state)
        {
            // Require network time for prediction
            state.RequireForUpdate<NetworkTime>();
            
            // Query for all players
            _playerQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<PlayerTag>()
            );
        }
        
        public void OnUpdate(ref SystemState state)
        {
            // Initialize quality settings singleton on first update
            if (!_initialized)
            {
                InitializeQualitySettings(ref state);
                _initialized = true;
            }
            
            // Get quality settings singleton
            if (!SystemAPI.TryGetSingleton<CollisionQualitySettings>(out var settings))
            {
                return;
            }
            
            // Count current players
            int playerCount = _playerQuery.CalculateEntityCount();
            settings.CurrentPlayerCount = playerCount;
            
            // Calculate frame time (use Unity's deltaTime for now)
            float deltaTime = SystemAPI.Time.DeltaTime;
            _lastFrameTime = deltaTime;
            
            // If auto-adjust is enabled, update quality based on player count and frame time
            if (settings.AutoAdjustEnabled)
            {
                UpdateQualityLevel(ref settings, playerCount, deltaTime);
            }
            else
            {
                // Manual mode: use the manual quality setting
                settings.CurrentQuality = settings.ManualQuality;
            }
            
            // Write back the updated settings
            SystemAPI.SetSingleton(settings);
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Log quality changes periodically
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick.TickIndexForValidTick;
            if ((currentTick % 300) == 0)
            {
                // string mode = settings.AutoAdjustEnabled ? "Auto" : "Manual";
                // UnityEngine.Debug.Log($"[CollisionQuality] {settings.CurrentQuality} ({mode}) - {playerCount} players, {deltaTime * 1000:F1}ms frame");
            }
            #endif
        }
        
        private void InitializeQualitySettings(ref SystemState state)
        {
            // Check if settings already exist
            if (SystemAPI.TryGetSingletonEntity<CollisionQualitySettings>(out _))
                return;
            
            // Create quality settings singleton entity
            var settingsEntity = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(settingsEntity, "CollisionQualitySettings");
            
            // Epic 7.7.7: Detect platform and use appropriate preset
            var platformPreset = DetectPlatformPreset();
            var settings = CollisionQualitySettings.CreateForPlatform(platformPreset);
            state.EntityManager.AddComponentData(settingsEntity, settings);
            

        }
        
        /// <summary>
        /// Epic 7.7.7: Detect platform and return appropriate quality preset.
        /// </summary>
        private CollisionPlatformPreset DetectPlatformPreset()
        {
            #if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            return CollisionPlatformPreset.PC;
            #elif UNITY_PS5 || UNITY_XBOXONE || UNITY_GAMECORE || UNITY_SWITCH
            return CollisionPlatformPreset.Console;
            #elif UNITY_IOS || UNITY_ANDROID
            return CollisionPlatformPreset.Mobile;
            #else
            // Default to PC for unknown platforms (editor, etc.)
            // Also check at runtime for Steam Deck detection
            if (UnityEngine.Application.platform == UnityEngine.RuntimePlatform.LinuxPlayer)
            {
                // Steam Deck runs Linux - check for low memory as a heuristic
                if (UnityEngine.SystemInfo.systemMemorySize < 16000)
                {
                    return CollisionPlatformPreset.Mobile; // Steam Deck uses Mobile preset
                }
            }
            return CollisionPlatformPreset.PC;
            #endif
        }
        
        private void UpdateQualityLevel(ref CollisionQualitySettings settings, int playerCount, float deltaTime)
        {
            var previousQuality = settings.CurrentQuality;
            
            // Check if we need to downgrade due to high player count
            CollisionQualityLevel targetQuality = CalculateQualityFromPlayerCount(ref settings, playerCount);
            
            // Check if we need to downgrade due to high frame time
            if (deltaTime > settings.DowngradeFrameTime)
            {
                // Frame time too high - consider downgrade
                settings.StableTimeAccumulator = 0f; // Reset stability counter
                
                // If target from player count is already lower, use that
                // Otherwise, downgrade one level due to frame time
                if (targetQuality < settings.CurrentQuality)
                {
                    settings.CurrentQuality = targetQuality;
                }
                else if (settings.CurrentQuality != CollisionQualityLevel.Low)
                {
                    // Downgrade due to frame time
                    settings.CurrentQuality = (CollisionQualityLevel)((int)settings.CurrentQuality + 1);
                    
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    UnityEngine.Debug.Log($"[CollisionQuality] Downgrade due to frame time: {previousQuality} → {settings.CurrentQuality} ({deltaTime * 1000:F1}ms > {settings.DowngradeFrameTime * 1000:F1}ms)");
                    #endif
                }
            }
            else if (deltaTime < settings.TargetFrameTime)
            {
                // Frame time is good - accumulate stability time
                settings.StableTimeAccumulator += deltaTime;
                
                // Check if we can upgrade
                if (settings.StableTimeAccumulator >= settings.UpgradeStabilityDuration)
                {
                    // Stable for long enough - consider upgrade
                    if (targetQuality > settings.CurrentQuality)
                    {
                        // Player count suggests higher quality is possible
                        settings.CurrentQuality = (CollisionQualityLevel)((int)settings.CurrentQuality - 1);
                        settings.StableTimeAccumulator = 0f; // Reset after upgrade
                        
                        #if UNITY_EDITOR || DEVELOPMENT_BUILD
                        UnityEngine.Debug.Log($"[CollisionQuality] Upgrade due to stable frame time: {previousQuality} → {settings.CurrentQuality} (stable for {settings.UpgradeStabilityDuration}s)");
                        #endif
                    }
                    else
                    {
                        // Already at appropriate quality for player count
                        settings.CurrentQuality = targetQuality;
                    }
                }
                else
                {
                    // Not stable long enough yet - use player count target
                    // But only if it's a downgrade (never upgrade without stability)
                    if (targetQuality > settings.CurrentQuality)
                    {
                        settings.CurrentQuality = targetQuality;
                    }
                }
            }
            else
            {
                // Frame time is between target and downgrade threshold
                // Maintain current quality, but allow player-count-based downgrades
                if (targetQuality > settings.CurrentQuality)
                {
                    settings.CurrentQuality = targetQuality;
                }
                
                // Slowly accumulate stability (half rate when in "acceptable" zone)
                settings.StableTimeAccumulator += deltaTime * 0.5f;
            }
        }
        
        /// <summary>
        /// Calculate target quality level based on player count alone.
        /// Uses hysteresis to prevent oscillation at threshold boundaries.
        /// </summary>
        private CollisionQualityLevel CalculateQualityFromPlayerCount(ref CollisionQualitySettings settings, int playerCount)
        {
            // Current quality affects thresholds (hysteresis)
            int highToMedium = settings.HighToMediumThreshold;
            int mediumToLow = settings.MediumToLowThreshold;
            
            // Apply hysteresis for upgrades (need to drop further below threshold)
            if (settings.CurrentQuality == CollisionQualityLevel.Medium)
            {
                // To upgrade to High, need to drop below threshold - hysteresis
                highToMedium -= settings.HysteresisOffset;
            }
            else if (settings.CurrentQuality == CollisionQualityLevel.Low)
            {
                // To upgrade to Medium, need to drop below threshold - hysteresis
                mediumToLow -= settings.HysteresisOffset;
            }
            
            // Determine target quality
            if (playerCount >= mediumToLow)
            {
                return CollisionQualityLevel.Low;
            }
            else if (playerCount >= highToMedium)
            {
                return CollisionQualityLevel.Medium;
            }
            else
            {
                return CollisionQualityLevel.High;
            }
        }
        
        public void OnDestroy(ref SystemState state)
        {
            // Nothing to dispose
        }
    }
}
