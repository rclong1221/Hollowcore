using System;

namespace DIG.Combat.UI
{
    /// <summary>
    /// Interface for providing health bar visibility settings.
    /// This decouples the visibility system from any specific UI implementation.
    /// 
    /// Implementations:
    /// - ScriptableObject-based (for testing via Inspector)
    /// - PlayerPrefs-based (for persisted settings)
    /// - UI-bound (future settings menu)
    /// </summary>
    public interface IHealthBarSettingsProvider
    {
        /// <summary>
        /// Gets the current active configuration.
        /// </summary>
        HealthBarVisibilityConfig ActiveConfig { get; }
        
        /// <summary>
        /// Gets the player settings (for UI binding).
        /// </summary>
        HealthBarPlayerSettings PlayerSettings { get; }
        
        /// <summary>
        /// Event fired when settings change (for live updates).
        /// </summary>
        event Action OnSettingsChanged;
        
        /// <summary>
        /// Apply a preset by enum value.
        /// </summary>
        void ApplyPreset(HealthBarPlayerSettings.PlayerVisibilityPreset preset);
        
        /// <summary>
        /// Set the fade timeout duration.
        /// </summary>
        void SetFadeTimeout(float seconds);
        
        /// <summary>
        /// Set the proximity distance.
        /// </summary>
        void SetProximityDistance(float distance);
        
        /// <summary>
        /// Toggle fade transitions.
        /// </summary>
        void SetUseFadeTransitions(bool enabled);
        
        /// <summary>
        /// Toggle enemy name display.
        /// </summary>
        void SetShowEnemyNames(bool enabled);
        
        /// <summary>
        /// Toggle enemy level display.
        /// </summary>
        void SetShowEnemyLevels(bool enabled);
        
        /// <summary>
        /// Toggle status effect icons.
        /// </summary>
        void SetShowStatusEffects(bool enabled);
        
        /// <summary>
        /// Toggle boss bar scaling.
        /// </summary>
        void SetScaleBossBars(bool enabled);
        
        /// <summary>
        /// Toggle friendly health bars.
        /// </summary>
        void SetShowFriendlyBars(bool enabled);
        
        /// <summary>
        /// Toggle neutral health bars.
        /// </summary>
        void SetShowNeutralBars(bool enabled);
        
        /// <summary>
        /// Toggle elite/boss only filter.
        /// </summary>
        void SetEliteAndBossOnly(bool enabled);
        
        /// <summary>
        /// Force regenerate the config from current settings.
        /// Call this after batch updates to avoid multiple regenerations.
        /// </summary>
        void RefreshConfig();
        
        /// <summary>
        /// Save settings to persistent storage.
        /// </summary>
        void SaveSettings();
        
        /// <summary>
        /// Load settings from persistent storage.
        /// </summary>
        void LoadSettings();
        
        /// <summary>
        /// Reset to default settings.
        /// </summary>
        void ResetToDefaults();
    }
    
    /// <summary>
    /// Event args for settings changes.
    /// </summary>
    public class HealthBarSettingsChangedEventArgs : EventArgs
    {
        public HealthBarVisibilityConfig NewConfig { get; }
        public string ChangedProperty { get; }
        
        public HealthBarSettingsChangedEventArgs(HealthBarVisibilityConfig config, string property = null)
        {
            NewConfig = config;
            ChangedProperty = property;
        }
    }
}
