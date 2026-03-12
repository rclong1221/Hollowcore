using System;
using UnityEngine;

namespace DIG.Combat.UI
{
    /// <summary>
    /// Singleton manager for health bar visibility settings.
    /// Provides centralized access to settings and handles persistence.
    /// 
    /// Usage:
    /// - Access via HealthBarSettingsManager.Instance
    /// - Subscribe to OnSettingsChanged for live updates
    /// - Call ApplyPreset() or individual setters to change settings
    /// </summary>
    public class HealthBarSettingsManager : MonoBehaviour, IHealthBarSettingsProvider
    {
        private static HealthBarSettingsManager _instance;
        public static HealthBarSettingsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Try to find existing instance
                    _instance = FindFirstObjectByType<HealthBarSettingsManager>();
                    
                    if (_instance == null)
                    {
                        // Create new instance
                        var go = new GameObject("[HealthBarSettingsManager]");
                        _instance = go.AddComponent<HealthBarSettingsManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// Returns true if an instance exists (without creating one).
        /// </summary>
        public static bool HasInstance => _instance != null;
        
        [Header("Configuration")]
        [Tooltip("Default player settings to use. If null, creates default at runtime.")]
        [SerializeField] private HealthBarPlayerSettings _defaultSettings;
        
        [Tooltip("Current active player settings (runtime copy).")]
        [SerializeField] private HealthBarPlayerSettings _currentSettings;
        
        [Header("Debug")]
        [SerializeField] private bool _logSettingsChanges = false;
        
        // Cached generated config
        private HealthBarVisibilityConfig _activeConfig;
        private bool _configDirty = true;

        // EPIC 15.17: Direct mode override for modes without preset mappings (e.g. WhenInLineOfSight).
        // Survives config regeneration so subsequent setter calls don't clobber the mode.
        private HealthBarVisibilityMode? _directModeOverride;
        
        // Events
        public event Action OnSettingsChanged;
        
        // IHealthBarSettingsProvider implementation
        public HealthBarVisibilityConfig ActiveConfig
        {
            get
            {
                if (_configDirty || _activeConfig == null)
                {
                    RegenerateConfig();
                }
                return _activeConfig;
            }
        }
        
        public HealthBarPlayerSettings PlayerSettings => _currentSettings;
        
        // Convenience accessors for UI binding
        public bool UseFadeTransitions => _currentSettings?.useFadeTransitions ?? true;
        public bool ShowName => _currentSettings?.showEnemyNames ?? true;
        public bool ShowLevel => _currentSettings?.showEnemyLevels ?? true;
        public float FadeTimeout => _currentSettings?.fadeTimeout ?? 5f;
        public float ProximityRange => _currentSettings?.maxDistance ?? 30f;
        public HealthBarVisibilityMode CurrentMode => ActiveConfig?.primaryMode ?? HealthBarVisibilityMode.WhenDamaged;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            Initialize();
        }
        
        private void Initialize()
        {
            // Create runtime copy of settings
            if (_defaultSettings != null)
            {
                _currentSettings = Instantiate(_defaultSettings);
                _currentSettings.name = "RuntimeHealthBarSettings";
            }
            else
            {
                _currentSettings = ScriptableObject.CreateInstance<HealthBarPlayerSettings>();
                _currentSettings.name = "RuntimeHealthBarSettings";
            }
            
            // Load any saved preferences
            LoadSettings();
            
            // Generate initial config
            RegenerateConfig();
        }
        
        private void RegenerateConfig()
        {
            if (_currentSettings == null) return;
            
            // Destroy old runtime config if it exists
            if (_activeConfig != null && !ReferenceEquals(_activeConfig, _currentSettings.customConfig))
            {
                Destroy(_activeConfig);
            }
            
            _activeConfig = _currentSettings.GenerateConfig();
            _activeConfig.name = "GeneratedHealthBarConfig";
            _configDirty = false;

            // Re-apply direct mode override if active (EPIC 15.17)
            if (_directModeOverride.HasValue)
            {
                _activeConfig.primaryMode = _directModeOverride.Value;
            }
            
            if (_logSettingsChanges)
            {
                Debug.Log($"[HealthBarSettings] Config regenerated: Mode={_activeConfig.primaryMode}, Flags={_activeConfig.flags}");
            }
        }
        
        private void MarkDirtyAndNotify(string propertyName = null)
        {
            _configDirty = true;
            
            if (_logSettingsChanges)
            {
                Debug.Log($"[HealthBarSettings] Setting changed: {propertyName ?? "Unknown"}");
            }
            
            OnSettingsChanged?.Invoke();
        }
        
        #region IHealthBarSettingsProvider Implementation
        
        public void ApplyPreset(HealthBarPlayerSettings.PlayerVisibilityPreset preset)
        {
            _directModeOverride = null; // Clear any direct mode override

            if (_currentSettings.visibilityPreset == preset) return;

            _currentSettings.visibilityPreset = preset;
            MarkDirtyAndNotify(nameof(_currentSettings.visibilityPreset));
        }
        
        public void SetFadeTimeout(float seconds)
        {
            seconds = Mathf.Clamp(seconds, 1f, 15f);
            if (Mathf.Approximately(_currentSettings.fadeTimeout, seconds)) return;
            
            _currentSettings.fadeTimeout = seconds;
            MarkDirtyAndNotify(nameof(_currentSettings.fadeTimeout));
        }
        
        public void SetProximityDistance(float distance)
        {
            distance = Mathf.Clamp(distance, 5f, 50f);
            if (Mathf.Approximately(_currentSettings.maxDistance, distance)) return;
            
            _currentSettings.maxDistance = distance;
            MarkDirtyAndNotify(nameof(_currentSettings.maxDistance));
        }
        
        public void SetUseFadeTransitions(bool enabled)
        {
            if (_currentSettings.useFadeTransitions == enabled) return;
            
            _currentSettings.useFadeTransitions = enabled;
            MarkDirtyAndNotify(nameof(_currentSettings.useFadeTransitions));
        }
        
        public void SetShowEnemyNames(bool enabled)
        {
            if (_currentSettings.showEnemyNames == enabled) return;
            
            _currentSettings.showEnemyNames = enabled;
            MarkDirtyAndNotify(nameof(_currentSettings.showEnemyNames));
        }
        
        public void SetShowEnemyLevels(bool enabled)
        {
            if (_currentSettings.showEnemyLevels == enabled) return;
            
            _currentSettings.showEnemyLevels = enabled;
            MarkDirtyAndNotify(nameof(_currentSettings.showEnemyLevels));
        }
        
        public void SetShowStatusEffects(bool enabled)
        {
            if (_currentSettings.showStatusEffects == enabled) return;
            
            _currentSettings.showStatusEffects = enabled;
            MarkDirtyAndNotify(nameof(_currentSettings.showStatusEffects));
        }
        
        public void SetScaleBossBars(bool enabled)
        {
            if (_currentSettings.scaleBossBars == enabled) return;
            
            _currentSettings.scaleBossBars = enabled;
            MarkDirtyAndNotify(nameof(_currentSettings.scaleBossBars));
        }
        
        public void SetShowFriendlyBars(bool enabled)
        {
            if (_currentSettings.showFriendlyBars == enabled) return;
            
            _currentSettings.showFriendlyBars = enabled;
            MarkDirtyAndNotify(nameof(_currentSettings.showFriendlyBars));
        }
        
        public void SetShowNeutralBars(bool enabled)
        {
            if (_currentSettings.showNeutralBars == enabled) return;
            
            _currentSettings.showNeutralBars = enabled;
            MarkDirtyAndNotify(nameof(_currentSettings.showNeutralBars));
        }
        
        public void SetEliteAndBossOnly(bool enabled)
        {
            if (_currentSettings.eliteAndBossOnly == enabled) return;
            
            _currentSettings.eliteAndBossOnly = enabled;
            MarkDirtyAndNotify(nameof(_currentSettings.eliteAndBossOnly));
        }
        
        // Convenience aliases for tester/UI
        public void SetShowName(bool enabled) => SetShowEnemyNames(enabled);
        public void SetShowLevel(bool enabled) => SetShowEnemyLevels(enabled);
        public void SetProximityRange(float range) => SetProximityDistance(range);
        
        /// <summary>
        /// Set the visibility mode by changing the preset.
        /// Maps common modes to appropriate presets.
        /// For modes without presets, directly modifies the active config.
        /// </summary>
        public void SetMode(HealthBarVisibilityMode mode)
        {
            // Check if we have a preset mapping
            HealthBarPlayerSettings.PlayerVisibilityPreset? preset = mode switch
            {
                HealthBarVisibilityMode.Always => HealthBarPlayerSettings.PlayerVisibilityPreset.AlwaysShow,
                HealthBarVisibilityMode.WhenDamaged => HealthBarPlayerSettings.PlayerVisibilityPreset.WhenDamaged,
                HealthBarVisibilityMode.WhenDamagedWithTimeout => HealthBarPlayerSettings.PlayerVisibilityPreset.WhenDamagedWithFade,
                HealthBarVisibilityMode.WhenTargeted => HealthBarPlayerSettings.PlayerVisibilityPreset.TargetOnly,
                HealthBarVisibilityMode.WhenInProximity => HealthBarPlayerSettings.PlayerVisibilityPreset.NearbyOnly,
                HealthBarVisibilityMode.Never => HealthBarPlayerSettings.PlayerVisibilityPreset.Never,
                _ => null // No preset mapping - will set directly
            };
            
            if (preset.HasValue)
            {
                _directModeOverride = null; // Clear override when using a preset
                ApplyPreset(preset.Value);
            }
            else
            {
                // For modes without preset mappings (e.g. WhenInLineOfSight),
                // store override so it survives config regeneration from setter calls
                _directModeOverride = mode;
                MarkDirtyAndNotify("DirectModeOverride");
            }
        }
        
        public void RefreshConfig()
        {
            RegenerateConfig();
            OnSettingsChanged?.Invoke();
        }
        
        #endregion
        
        #region Persistence
        
        private const string PREFS_PREFIX = "HealthBar_";
        
        public void SaveSettings()
        {
            if (_currentSettings == null) return;
            
            PlayerPrefs.SetInt(PREFS_PREFIX + "Preset", (int)_currentSettings.visibilityPreset);
            PlayerPrefs.SetFloat(PREFS_PREFIX + "FadeTimeout", _currentSettings.fadeTimeout);
            PlayerPrefs.SetFloat(PREFS_PREFIX + "MaxDistance", _currentSettings.maxDistance);
            PlayerPrefs.SetInt(PREFS_PREFIX + "UseFade", _currentSettings.useFadeTransitions ? 1 : 0);
            PlayerPrefs.SetInt(PREFS_PREFIX + "ShowNames", _currentSettings.showEnemyNames ? 1 : 0);
            PlayerPrefs.SetInt(PREFS_PREFIX + "ShowLevels", _currentSettings.showEnemyLevels ? 1 : 0);
            PlayerPrefs.SetInt(PREFS_PREFIX + "ShowStatus", _currentSettings.showStatusEffects ? 1 : 0);
            PlayerPrefs.SetInt(PREFS_PREFIX + "ScaleBoss", _currentSettings.scaleBossBars ? 1 : 0);
            PlayerPrefs.SetInt(PREFS_PREFIX + "ShowFriendly", _currentSettings.showFriendlyBars ? 1 : 0);
            PlayerPrefs.SetInt(PREFS_PREFIX + "ShowNeutral", _currentSettings.showNeutralBars ? 1 : 0);
            PlayerPrefs.SetInt(PREFS_PREFIX + "EliteBossOnly", _currentSettings.eliteAndBossOnly ? 1 : 0);
            
            PlayerPrefs.Save();
            
            if (_logSettingsChanges)
            {
                Debug.Log("[HealthBarSettings] Settings saved to PlayerPrefs");
            }
        }
        
        public void LoadSettings()
        {
            if (_currentSettings == null) return;
            
            // Check if we have saved settings
            if (!PlayerPrefs.HasKey(PREFS_PREFIX + "Preset"))
            {
                if (_logSettingsChanges)
                {
                    Debug.Log("[HealthBarSettings] No saved settings found, using defaults");
                }
                return;
            }
            
            // EPIC 15.19: Validate loaded preset is still valid (enum may have changed)
            int savedPreset = PlayerPrefs.GetInt(PREFS_PREFIX + "Preset", (int)_currentSettings.visibilityPreset);
            if (System.Enum.IsDefined(typeof(HealthBarPlayerSettings.PlayerVisibilityPreset), savedPreset))
            {
                _currentSettings.visibilityPreset = (HealthBarPlayerSettings.PlayerVisibilityPreset)savedPreset;
            }
            else
            {
                // Invalid saved preset, use new default
                _currentSettings.visibilityPreset = HealthBarPlayerSettings.PlayerVisibilityPreset.AggroedOrDamaged;
                Debug.LogWarning($"[HealthBarSettings] Invalid saved preset {savedPreset}, resetting to AggroedOrDamaged");
            }
            
            _currentSettings.fadeTimeout = PlayerPrefs.GetFloat(PREFS_PREFIX + "FadeTimeout", _currentSettings.fadeTimeout);
            _currentSettings.maxDistance = PlayerPrefs.GetFloat(PREFS_PREFIX + "MaxDistance", _currentSettings.maxDistance);
            _currentSettings.useFadeTransitions = PlayerPrefs.GetInt(PREFS_PREFIX + "UseFade", 1) == 1;
            _currentSettings.showEnemyNames = PlayerPrefs.GetInt(PREFS_PREFIX + "ShowNames", 1) == 1;
            _currentSettings.showEnemyLevels = PlayerPrefs.GetInt(PREFS_PREFIX + "ShowLevels", 1) == 1;
            _currentSettings.showStatusEffects = PlayerPrefs.GetInt(PREFS_PREFIX + "ShowStatus", 1) == 1;
            _currentSettings.scaleBossBars = PlayerPrefs.GetInt(PREFS_PREFIX + "ScaleBoss", 1) == 1;
            _currentSettings.showFriendlyBars = PlayerPrefs.GetInt(PREFS_PREFIX + "ShowFriendly", 0) == 1;
            _currentSettings.showNeutralBars = PlayerPrefs.GetInt(PREFS_PREFIX + "ShowNeutral", 0) == 1;
            _currentSettings.eliteAndBossOnly = PlayerPrefs.GetInt(PREFS_PREFIX + "EliteBossOnly", 0) == 1;
            
            _configDirty = true;
            
            if (_logSettingsChanges)
            {
                Debug.Log($"[HealthBarSettings] Settings loaded: Preset={_currentSettings.visibilityPreset}");
            }
        }
        
        public void ResetToDefaults()
        {
            // Delete all saved prefs
            PlayerPrefs.DeleteKey(PREFS_PREFIX + "Preset");
            PlayerPrefs.DeleteKey(PREFS_PREFIX + "FadeTimeout");
            PlayerPrefs.DeleteKey(PREFS_PREFIX + "MaxDistance");
            PlayerPrefs.DeleteKey(PREFS_PREFIX + "UseFade");
            PlayerPrefs.DeleteKey(PREFS_PREFIX + "ShowNames");
            PlayerPrefs.DeleteKey(PREFS_PREFIX + "ShowLevels");
            PlayerPrefs.DeleteKey(PREFS_PREFIX + "ShowStatus");
            PlayerPrefs.DeleteKey(PREFS_PREFIX + "ScaleBoss");
            PlayerPrefs.DeleteKey(PREFS_PREFIX + "ShowFriendly");
            PlayerPrefs.DeleteKey(PREFS_PREFIX + "ShowNeutral");
            PlayerPrefs.DeleteKey(PREFS_PREFIX + "EliteBossOnly");
            PlayerPrefs.Save();
            
            // Reinitialize with defaults
            if (_defaultSettings != null)
            {
                Destroy(_currentSettings);
                _currentSettings = Instantiate(_defaultSettings);
                _currentSettings.name = "RuntimeHealthBarSettings";
            }
            else
            {
                _currentSettings.visibilityPreset = HealthBarPlayerSettings.PlayerVisibilityPreset.WhenDamagedWithFade;
                _currentSettings.fadeTimeout = 5f;
                _currentSettings.maxDistance = 30f;
                _currentSettings.useFadeTransitions = true;
                _currentSettings.showEnemyNames = true;
                _currentSettings.showEnemyLevels = true;
                _currentSettings.showStatusEffects = true;
                _currentSettings.scaleBossBars = true;
                _currentSettings.showFriendlyBars = false;
                _currentSettings.showNeutralBars = false;
                _currentSettings.eliteAndBossOnly = false;
            }
            
            _directModeOverride = null; // Clear any direct mode override
            MarkDirtyAndNotify("ResetToDefaults");

            if (_logSettingsChanges)
            {
                Debug.Log("[HealthBarSettings] Settings reset to defaults");
            }
        }
        
        #endregion
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            
            // Cleanup runtime objects
            if (_currentSettings != null)
            {
                Destroy(_currentSettings);
            }
            if (_activeConfig != null)
            {
                Destroy(_activeConfig);
            }
        }
    }
}
