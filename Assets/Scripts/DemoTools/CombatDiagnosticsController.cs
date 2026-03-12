#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace DIG.Diagnostics
{
    /// <summary>
    /// Runtime control for CombatDiagnostics.
    /// Add to any GameObject in your scene to control logging via Inspector.
    /// 
    /// This allows toggling diagnostic levels without recompiling.
    /// </summary>
    [AddComponentMenu("DIG/Debug/Combat Diagnostics Controller")]
    public class CombatDiagnosticsController : MonoBehaviour
    {
        [Header("Master Toggle")]
        [Tooltip("Master switch for all combat diagnostics")]
        public bool EnableDiagnostics = true;
        
        [Header("Log Categories")]
        [Tooltip("Log when damage events are created (melee/projectile hits)")]
        public bool LogDamageCreation = true;
        
        [Tooltip("Log when damage is applied to health")]
        public bool LogDamageApplication = true;
        
        [Tooltip("Log ghost sync mismatches between server and client")]
        public bool LogGhostSync = true;
        
        [Tooltip("Log health bar updates")]
        public bool LogHealthBarUpdates = true;
        
        [Tooltip("Log one-time startup diagnostics")]
        public bool LogStartupDiagnostics = true;
        
        private void Awake()
        {
            ApplySettings();
        }
        
        private void OnValidate()
        {
            // Apply in editor when values change
            ApplySettings();
        }
        
        private void ApplySettings()
        {
            CombatDiagnostics.Enabled = EnableDiagnostics;
            CombatDiagnostics.LogDamageCreation = LogDamageCreation;
            CombatDiagnostics.LogDamageApplication = LogDamageApplication;
            CombatDiagnostics.LogGhostSync = LogGhostSync;
            CombatDiagnostics.LogHealthBarUpdates = LogHealthBarUpdates;
            CombatDiagnostics.LogStartupDiagnostics = LogStartupDiagnostics;
        }
        
        /// <summary>
        /// Force log diagnostic state for a specific entity.
        /// Call from console or debug UI.
        /// </summary>
        [ContextMenu("Log Current Settings")]
        public void LogCurrentSettings()
        {
            UnityEngine.Debug.Log(
                $"[CombatDiagnostics] Settings:\n" +
                $"  Enabled: {CombatDiagnostics.Enabled}\n" +
                $"  DamageCreation: {CombatDiagnostics.LogDamageCreation}\n" +
                $"  DamageApplication: {CombatDiagnostics.LogDamageApplication}\n" +
                $"  GhostSync: {CombatDiagnostics.LogGhostSync}\n" +
                $"  HealthBarUpdates: {CombatDiagnostics.LogHealthBarUpdates}\n" +
                $"  StartupDiagnostics: {CombatDiagnostics.LogStartupDiagnostics}");
        }
        
        /// <summary>
        /// Reset cached state (call on scene load).
        /// </summary>
        [ContextMenu("Reset Diagnostics Cache")]
        public void ResetCache()
        {
            CombatDiagnostics.Reset();
            UnityEngine.Debug.Log("[CombatDiagnostics] Cache reset - startup logs will fire again");
        }
    }
}
#endif
