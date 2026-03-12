using UnityEngine;

namespace DIG.Targeting
{
    /// <summary>
    /// Data-driven configuration for targeting behavior.
    /// Assign to player prefab to configure targeting mode and parameters.
    /// </summary>
    [CreateAssetMenu(fileName = "TargetingConfig", menuName = "DIG/Targeting/Targeting Config")]
    public class TargetingConfig : ScriptableObject
    {
        [Header("Mode Selection")]
        [Tooltip("Which targeting mode to use.")]
        public TargetingMode TargetingMode = TargetingMode.CameraRaycast;
        
        [Header("Range & Detection")]
        [Tooltip("Maximum range for target detection.")]
        public float MaxTargetRange = 100f;
        
        [Tooltip("Layers that can be targeted.")]
        public LayerMask ValidTargetLayers = ~0; // All layers by default
        
        [Tooltip("Require line of sight to target.")]
        public bool RequireLineOfSight = true;
        
        [Header("Auto-Target Behavior")]
        [Tooltip("Automatically acquire target when using weapon.")]
        public bool AutoTargetOnUse = false;
        
        [Tooltip("Keep target after use action ends.")]
        public bool StickyTargeting = false;
        
        [Tooltip("Priority for auto-target selection.")]
        public TargetPriority TargetPriority = TargetPriority.Nearest;
        
        [Header("Aim Assist")]
        [Tooltip("Strength of aim assist (0 = none, 1 = full snap).")]
        [Range(0f, 1f)]
        public float AimAssistStrength = 0f;
        
        [Tooltip("Detection radius for aim assist.")]
        public float AimAssistRadius = 2f;
        
        [Header("Lock-On Settings")]
        [Tooltip("Maximum angle from forward to acquire lock-on target.")]
        public float LockOnMaxAngle = 45f;
        
        [Tooltip("Maximum distance for lock-on.")]
        public float LockOnMaxDistance = 30f;
        
        // ============================================================
        // STATIC PRESETS
        // ============================================================
        
        /// <summary>
        /// Default config for DIG (TPS shooter with camera raycast).
        /// </summary>
        public static TargetingConfig CreateDIGPreset()
        {
            var config = CreateInstance<TargetingConfig>();
            config.name = "TargetingConfig_DIG";
            config.TargetingMode = TargetingMode.CameraRaycast;
            config.MaxTargetRange = 200f;
            config.RequireLineOfSight = true;
            config.AimAssistStrength = 0.2f;
            config.AimAssistRadius = 1f;
            return config;
        }
        
        /// <summary>
        /// Default config for ARPG (isometric with cursor aim).
        /// </summary>
        public static TargetingConfig CreateARPGPreset()
        {
            var config = CreateInstance<TargetingConfig>();
            config.name = "TargetingConfig_ARPG";
            config.TargetingMode = TargetingMode.CursorAim;
            config.MaxTargetRange = 15f;
            config.RequireLineOfSight = false;
            config.AutoTargetOnUse = true;
            config.TargetPriority = TargetPriority.CursorProximity;
            config.AimAssistStrength = 0f;
            return config;
        }
    }
}
