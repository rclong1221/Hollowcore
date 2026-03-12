using UnityEngine;

namespace DIG.Targeting.Debug
{
    /// <summary>
    /// EPIC 15.16: Debug component for testing target lock settings at runtime.
    /// Attach to any GameObject in scene. Toggle via Inspector - no hotkeys.
    /// 
    /// For designers:
    /// 1. Add this component to any GameObject
    /// 2. Use Inspector checkboxes to toggle settings live
    /// 3. Use preset dropdown for quick config switches
    /// 
    /// For UI integration:
    /// This component is a reference implementation. Your settings menu
    /// should directly use TargetLockSettingsManager.Instance instead.
    /// </summary>
    [AddComponentMenu("DIG/Debug/Target Lock Tester")]
    public class TargetLockTester : MonoBehaviour
    {
        [Header("Settings (Runtime Editable)")]
        [Tooltip("Allow target locking (Tab key / Grab input)")]
        [SerializeField] private bool _allowTargetLock = true;
        
        [Tooltip("Allow aim assist (soft targeting)")]
        [SerializeField] private bool _allowAimAssist = true;
        
        [Tooltip("Show lock-on indicator UI")]
        [SerializeField] private bool _showIndicator = true;
        
        [Header("Preset (Apply on Change)")]
        [Tooltip("Apply a preset configuration")]
        [SerializeField] private TargetLockPreset _preset = TargetLockPreset.Normal;
        
        [Header("Debug Info (Read Only)")]
        [SerializeField] private bool _isCurrentlyLocked;
        [SerializeField] private string _lockedTargetName = "None";
        
        private bool _lastAllowTargetLock;
        private bool _lastAllowAimAssist;
        private bool _lastShowIndicator;
        private TargetLockPreset _lastPreset;
        
        private void Start()
        {
            // Initialize from manager
            SyncFromManager();
            
            // Subscribe to external changes
            TargetLockSettingsManager.Instance.OnSettingsChanged += SyncFromManager;
            
            UnityEngine.Debug.Log("[TargetLockTester] Initialized. Use Inspector to toggle settings.");
        }
        
        private void OnDestroy()
        {
            TargetLockSettingsManager.Instance.OnSettingsChanged -= SyncFromManager;
        }
        
        private void Update()
        {
            // Detect Inspector changes and push to manager
            if (_allowTargetLock != _lastAllowTargetLock)
            {
                TargetLockSettingsManager.Instance.AllowTargetLock = _allowTargetLock;
                _lastAllowTargetLock = _allowTargetLock;
                UnityEngine.Debug.Log($"[TargetLockTester] AllowTargetLock = {_allowTargetLock}");
            }
            
            if (_allowAimAssist != _lastAllowAimAssist)
            {
                TargetLockSettingsManager.Instance.AllowAimAssist = _allowAimAssist;
                _lastAllowAimAssist = _allowAimAssist;
                UnityEngine.Debug.Log($"[TargetLockTester] AllowAimAssist = {_allowAimAssist}");
            }
            
            if (_showIndicator != _lastShowIndicator)
            {
                TargetLockSettingsManager.Instance.ShowIndicator = _showIndicator;
                _lastShowIndicator = _showIndicator;
                UnityEngine.Debug.Log($"[TargetLockTester] ShowIndicator = {_showIndicator}");
            }
            
            // Preset changed - apply it
            if (_preset != _lastPreset)
            {
                TargetLockSettingsManager.Instance.ApplyPreset(_preset);
                _lastPreset = _preset;
                SyncFromManager(); // Update local fields to match preset
                UnityEngine.Debug.Log($"[TargetLockTester] Applied preset: {_preset}");
            }
        }
        
        private void SyncFromManager()
        {
            _allowTargetLock = TargetLockSettingsManager.Instance.AllowTargetLock;
            _allowAimAssist = TargetLockSettingsManager.Instance.AllowAimAssist;
            _showIndicator = TargetLockSettingsManager.Instance.ShowIndicator;
            
            _lastAllowTargetLock = _allowTargetLock;
            _lastAllowAimAssist = _allowAimAssist;
            _lastShowIndicator = _showIndicator;
        }
        
        /// <summary>
        /// Set lock state from external source (e.g., CameraLockOnSystem via bridge).
        /// </summary>
        public void SetDebugLockState(bool isLocked, string targetName)
        {
            _isCurrentlyLocked = isLocked;
            _lockedTargetName = isLocked ? targetName : "None";
        }
        
#if UNITY_EDITOR
        [ContextMenu("Force Unlock All Targets")]
        private void ForceUnlock()
        {
            _allowTargetLock = false;
            TargetLockSettingsManager.Instance.AllowTargetLock = false;
            _lastAllowTargetLock = false;
            UnityEngine.Debug.Log("[TargetLockTester] Forced unlock by disabling target lock");
        }
        
        [ContextMenu("Reset to Defaults")]
        private void ResetDefaults()
        {
            _preset = TargetLockPreset.Normal;
            TargetLockSettingsManager.Instance.ApplyPreset(TargetLockPreset.Normal);
            _lastPreset = _preset;
            SyncFromManager();
            UnityEngine.Debug.Log("[TargetLockTester] Reset to default settings");
        }
#endif
    }
}
