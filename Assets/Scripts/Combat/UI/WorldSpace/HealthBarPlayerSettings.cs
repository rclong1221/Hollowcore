using UnityEngine;

namespace DIG.Combat.UI
{
    /// <summary>
    /// Player-facing settings for health bar visibility.
    /// This is what appears in the game's options menu.
    /// Translates player-friendly options into the full config system.
    /// </summary>
    [CreateAssetMenu(fileName = "HealthBarPlayerSettings", menuName = "DIG/Combat/Health Bar Player Settings")]
    public class HealthBarPlayerSettings : ScriptableObject
    {
        /// <summary>
        /// Simplified visibility options for the player settings menu.
        /// These map to combinations of HealthBarVisibilityMode and flags.
        /// </summary>
        public enum PlayerVisibilityPreset
        {
            /// <summary>Always show all enemy health bars.</summary>
            [Tooltip("Always show health bars for all enemies")]
            AlwaysShow,
            
            /// <summary>Show when damaged, no timeout.</summary>
            [Tooltip("Show health bars only for damaged enemies")]
            WhenDamaged,
            
            /// <summary>Show when damaged, hide after timeout.</summary>
            [Tooltip("Show when damaged, hide after a few seconds")]
            WhenDamagedWithFade,
            
            /// <summary>Show only for targeted enemy.</summary>
            [Tooltip("Show health bar only for your current target")]
            TargetOnly,
            
            /// <summary>Target always visible, others when damaged.</summary>
            [Tooltip("Target always visible, others when damaged")]
            TargetAndDamaged,
            
            /// <summary>Show for nearby enemies only.</summary>
            [Tooltip("Show for enemies within close range")]
            NearbyOnly,
            
            /// <summary>Show for nearby damaged enemies.</summary>
            [Tooltip("Show for nearby damaged enemies")]
            NearbyAndDamaged,
            
            /// <summary>Show when enemy has aggro on player OR is damaged (EPIC 15.19: Recommended).</summary>
            [Tooltip("Show when enemies aggro you or take damage")]
            AggroedOrDamaged,
            
            /// <summary>Never show health bars.</summary>
            [Tooltip("Hide all enemy health bars (hardcore mode)")]
            Never,
            
            /// <summary>Custom - uses advanced settings.</summary>
            [Tooltip("Use custom advanced settings")]
            Custom
        }
        
        [Header("Basic Settings")]
        [Tooltip("When should enemy health bars be visible?")]
        public PlayerVisibilityPreset visibilityPreset = PlayerVisibilityPreset.AggroedOrDamaged; // EPIC 15.19: Updated default
        
        [Header("Timing")]
        [Tooltip("How long health bars stay visible after damage (for timed modes).")]
        [Range(1f, 15f)]
        public float fadeTimeout = 5f;
        
        [Header("Distance")]
        [Tooltip("Maximum distance to show health bars (for proximity modes).")]
        [Range(5f, 50f)]
        public float maxDistance = 30f;
        
        [Header("Visual Options")]
        [Tooltip("Use smooth fade in/out transitions.")]
        public bool useFadeTransitions = true;
        
        [Tooltip("Show enemy names above health bars.")]
        public bool showEnemyNames = true;
        
        [Tooltip("Show enemy levels.")]
        public bool showEnemyLevels = true;
        
        [Tooltip("Show status effect icons.")]
        public bool showStatusEffects = true;
        
        [Tooltip("Make boss health bars larger.")]
        public bool scaleBossBars = true;
        
        [Header("Filtering")]
        [Tooltip("Show health bars for friendly NPCs.")]
        public bool showFriendlyBars = false;
        
        [Tooltip("Show health bars for neutral NPCs.")]
        public bool showNeutralBars = false;
        
        [Tooltip("Only show health bars for elite/boss enemies.")]
        public bool eliteAndBossOnly = false;
        
        [Header("Advanced (Custom Mode Only)")]
        [Tooltip("Reference to a full custom config (only used when preset is Custom).")]
        public HealthBarVisibilityConfig customConfig;
        
        /// <summary>
        /// Generates a HealthBarVisibilityConfig based on current player settings.
        /// This is called when settings change to update the active config.
        /// </summary>
        public HealthBarVisibilityConfig GenerateConfig()
        {
            // If using custom, return that directly
            if (visibilityPreset == PlayerVisibilityPreset.Custom && customConfig != null)
            {
                return customConfig;
            }
            
            // Create runtime config instance
            var config = ScriptableObject.CreateInstance<HealthBarVisibilityConfig>();
            
            // Map preset to primary mode
            config.primaryMode = visibilityPreset switch
            {
                PlayerVisibilityPreset.AlwaysShow => HealthBarVisibilityMode.Always,
                PlayerVisibilityPreset.WhenDamaged => HealthBarVisibilityMode.WhenDamaged,
                PlayerVisibilityPreset.WhenDamagedWithFade => HealthBarVisibilityMode.WhenDamagedWithTimeout,
                PlayerVisibilityPreset.TargetOnly => HealthBarVisibilityMode.WhenTargeted,
                PlayerVisibilityPreset.TargetAndDamaged => HealthBarVisibilityMode.WhenTargetedOrDamaged,
                PlayerVisibilityPreset.NearbyOnly => HealthBarVisibilityMode.WhenInProximity,
                PlayerVisibilityPreset.NearbyAndDamaged => HealthBarVisibilityMode.WhenInProximityAndDamaged,
                PlayerVisibilityPreset.AggroedOrDamaged => HealthBarVisibilityMode.WhenAggroedOrDamagedWithTimeout, // EPIC 15.19: With timeout
                PlayerVisibilityPreset.Never => HealthBarVisibilityMode.Never,
                _ => HealthBarVisibilityMode.WhenAggroedOrDamagedWithTimeout // EPIC 15.19: With timeout so bars hide
            };
            
            // Build flags
            var flags = HealthBarVisibilityFlags.HostileOnly;
            
            if (useFadeTransitions)
                flags |= HealthBarVisibilityFlags.UseFadeTransitions;
            
            if (showEnemyNames)
                flags |= HealthBarVisibilityFlags.ShowName;
            
            if (showEnemyLevels)
                flags |= HealthBarVisibilityFlags.ShowLevel;
            
            if (showStatusEffects)
                flags |= HealthBarVisibilityFlags.ShowStatusEffects;
            
            if (scaleBossBars)
                flags |= HealthBarVisibilityFlags.ScaleByImportance;
            
            if (showFriendlyBars)
                flags |= HealthBarVisibilityFlags.IncludeFriendlies;
            
            if (showNeutralBars)
                flags |= HealthBarVisibilityFlags.IncludeNeutrals;
            
            if (eliteAndBossOnly)
                flags |= HealthBarVisibilityFlags.ElitesOnly;
            
            config.flags = flags;
            
            // Timing
            config.hideAfterSeconds = fadeTimeout;
            config.fadeInDuration = 0.2f;
            config.fadeOutDuration = 0.5f;
            
            // Distance
            config.proximityDistance = maxDistance;
            config.fadeStartDistance = maxDistance * 0.8f;
            
            return config;
        }
        
        /// <summary>
        /// Returns a localization key for the current preset description.
        /// </summary>
        public string GetPresetDescriptionKey()
        {
            return $"UI_HEALTHBAR_PRESET_{visibilityPreset.ToString().ToUpperInvariant()}_DESC";
        }
    }
}
