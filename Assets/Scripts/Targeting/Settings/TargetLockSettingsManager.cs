using System;
using UnityEngine;

namespace DIG.Targeting
{
    /// <summary>
    /// EPIC 15.16: Interface for accessing targeting settings.
    /// Decoupled from UI - any settings menu can implement this.
    /// </summary>
    public interface ITargetLockSettingsProvider
    {
        /// <summary>
        /// Whether target locking is enabled.
        /// </summary>
        bool AllowTargetLock { get; set; }
        
        /// <summary>
        /// Whether aim assist is enabled.
        /// </summary>
        bool AllowAimAssist { get; set; }
        
        /// <summary>
        /// Whether to show lock-on indicators.
        /// </summary>
        bool ShowIndicator { get; set; }
        
        /// <summary>
        /// Event fired when any setting changes.
        /// </summary>
        event Action OnSettingsChanged;
        
        /// <summary>
        /// Apply preset configuration.
        /// </summary>
        void ApplyPreset(TargetLockPreset preset);
    }
    
    /// <summary>
    /// Preset configurations for quick setup.
    /// </summary>
    public enum TargetLockPreset
    {
        /// <summary>Default - all features enabled.</summary>
        Normal,
        
        /// <summary>No target lock, no aim assist.</summary>
        Hardcore,
        
        /// <summary>Target lock enabled, no aim assist.</summary>
        LockOnOnly,
        
        /// <summary>Aim assist only, no hard lock.</summary>
        AimAssistOnly
    }
    
    /// <summary>
    /// EPIC 15.16: Runtime manager for target lock settings.
    /// Singleton pattern with PlayerPrefs persistence.
    /// 
    /// Usage:
    /// - TargetLockSettingsManager.Instance.AllowTargetLock = false;
    /// - TargetLockSettingsManager.Instance.OnSettingsChanged += HandleChange;
    /// </summary>
    public class TargetLockSettingsManager : ITargetLockSettingsProvider
    {
        private static TargetLockSettingsManager _instance;
        public static TargetLockSettingsManager Instance => _instance ??= new TargetLockSettingsManager();
        
        // PlayerPrefs keys
        private const string KEY_ALLOW_LOCK = "Targeting_AllowLock";
        private const string KEY_ALLOW_ASSIST = "Targeting_AllowAssist";
        private const string KEY_SHOW_INDICATOR = "Targeting_ShowIndicator";
        
        // Cached values
        private bool _allowTargetLock;
        private bool _allowAimAssist;
        private bool _showIndicator;
        
        public event Action OnSettingsChanged;
        
        public bool AllowTargetLock
        {
            get => _allowTargetLock;
            set
            {
                if (_allowTargetLock == value) return;
                _allowTargetLock = value;
                PlayerPrefs.SetInt(KEY_ALLOW_LOCK, value ? 1 : 0);
                OnSettingsChanged?.Invoke();
            }
        }
        
        public bool AllowAimAssist
        {
            get => _allowAimAssist;
            set
            {
                if (_allowAimAssist == value) return;
                _allowAimAssist = value;
                PlayerPrefs.SetInt(KEY_ALLOW_ASSIST, value ? 1 : 0);
                OnSettingsChanged?.Invoke();
            }
        }
        
        public bool ShowIndicator
        {
            get => _showIndicator;
            set
            {
                if (_showIndicator == value) return;
                _showIndicator = value;
                PlayerPrefs.SetInt(KEY_SHOW_INDICATOR, value ? 1 : 0);
                OnSettingsChanged?.Invoke();
            }
        }
        
        private TargetLockSettingsManager()
        {
            // Load from PlayerPrefs with defaults
            _allowTargetLock = PlayerPrefs.GetInt(KEY_ALLOW_LOCK, 1) == 1;
            _allowAimAssist = PlayerPrefs.GetInt(KEY_ALLOW_ASSIST, 1) == 1;
            _showIndicator = PlayerPrefs.GetInt(KEY_SHOW_INDICATOR, 1) == 1;
        }
        
        public void ApplyPreset(TargetLockPreset preset)
        {
            switch (preset)
            {
                case TargetLockPreset.Normal:
                    _allowTargetLock = true;
                    _allowAimAssist = true;
                    _showIndicator = true;
                    break;
                    
                case TargetLockPreset.Hardcore:
                    _allowTargetLock = false;
                    _allowAimAssist = false;
                    _showIndicator = false;
                    break;
                    
                case TargetLockPreset.LockOnOnly:
                    _allowTargetLock = true;
                    _allowAimAssist = false;
                    _showIndicator = true;
                    break;
                    
                case TargetLockPreset.AimAssistOnly:
                    _allowTargetLock = false;
                    _allowAimAssist = true;
                    _showIndicator = true;
                    break;
            }
            
            // Save all
            PlayerPrefs.SetInt(KEY_ALLOW_LOCK, _allowTargetLock ? 1 : 0);
            PlayerPrefs.SetInt(KEY_ALLOW_ASSIST, _allowAimAssist ? 1 : 0);
            PlayerPrefs.SetInt(KEY_SHOW_INDICATOR, _showIndicator ? 1 : 0);
            
            OnSettingsChanged?.Invoke();
        }
        
        /// <summary>
        /// Force save settings to PlayerPrefs.
        /// </summary>
        public void Save()
        {
            PlayerPrefs.Save();
        }
    }
}
