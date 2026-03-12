using UnityEngine;
using Player.Systems;

namespace Player.Utilities
{
    /// <summary>
    /// Debug helper for EPIC 13.19 Ragdoll Hips Sync.
    /// Add to any GameObject in the scene to enable/disable diagnostics via Inspector.
    /// </summary>
    public class RagdollHipsSyncDebugger : MonoBehaviour
    {
        [Header("Logging Controls")]
        [Tooltip("Enable server-side logging (only shows on host/server)")]
        public bool EnableServerLogging = false;

        [Tooltip("Enable client-side logging (shows on all clients)")]
        public bool EnableClientLogging = false;

        [Tooltip("Enable position comparison logging (shows when body moves, useful for testing same-spot landing)")]
        public bool EnablePositionComparison = false;

        [Tooltip("Frames between periodic logs (30 = ~0.5 sec at 60fps)")]
        [Range(1, 120)]
        public int LogIntervalFrames = 30;

        [Header("Quick Actions")]
        [Tooltip("Enable both server and client logging")]
        public bool EnableAll = false;

        [Tooltip("Disable all logging")]
        public bool DisableAll = false;

        private void OnValidate()
        {
            // Handle quick action buttons
            if (EnableAll)
            {
                EnableAll = false;
                EnableServerLogging = true;
                EnableClientLogging = true;
                EnablePositionComparison = true;
            }

            if (DisableAll)
            {
                DisableAll = false;
                EnableServerLogging = false;
                EnableClientLogging = false;
                EnablePositionComparison = false;
            }

            // Apply settings
            ApplySettings();
        }

        private void Awake()
        {
            ApplySettings();
        }

        private void Update()
        {
            // Continuously apply in case changed at runtime
            ApplySettings();
        }

        private void ApplySettings()
        {
            RagdollHipsSyncDiagnostics.ServerLogging = EnableServerLogging;
            RagdollHipsSyncDiagnostics.ClientLogging = EnableClientLogging;
            RagdollHipsSyncDiagnostics.PositionComparisonLogging = EnablePositionComparison;
            RagdollHipsSyncDiagnostics.LogIntervalFrames = LogIntervalFrames;
        }

        [Header("Runtime Info (Read-Only)")]
        [SerializeField, Tooltip("Current server logging state")]
        private bool _serverLoggingActive;

        [SerializeField, Tooltip("Current client logging state")]
        private bool _clientLoggingActive;

        private void LateUpdate()
        {
            // Show current state in inspector
            _serverLoggingActive = RagdollHipsSyncDiagnostics.ServerLogging;
            _clientLoggingActive = RagdollHipsSyncDiagnostics.ClientLogging;
        }
    }
}
