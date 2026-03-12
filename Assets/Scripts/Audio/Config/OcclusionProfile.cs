using UnityEngine;

namespace Audio.Config
{
    /// <summary>
    /// Configurable occlusion parameters: per-material blocking factors,
    /// raycast spreading, transition speed, and budget limits.
    /// EPIC 15.27 Phase 3.
    /// </summary>
    [CreateAssetMenu(fileName = "OcclusionProfile", menuName = "DIG/Audio/Occlusion Profile")]
    public class OcclusionProfile : ScriptableObject
    {
        [Header("Raycast Settings")]
        [Tooltip("Number of frames to spread occlusion raycasts across (lower = more frequent, higher = cheaper)")]
        [Range(1, 12)]
        public int SpreadFrames = 6;

        [Tooltip("Layer mask for occlusion raycasts (environment + structures only)")]
        public LayerMask OcclusionLayers = ~0; // Default to everything; configure in inspector

        [Tooltip("Max distance beyond which sources skip raycasts and assume clear")]
        public float MaxOcclusionDistance = 80f;

        [Tooltip("Minimum source priority to perform occlusion raycasts (skip cheap ambient sounds)")]
        [Range(0, 255)]
        public int MinPriorityForOcclusion = 20;

        [Header("Transition")]
        [Tooltip("Occlusion transition speed (seconds to reach target factor)")]
        [Range(0.05f, 1f)]
        public float TransitionSpeed = 0.15f;

        [Header("Occlusion Factors")]
        [Tooltip("Factor when 0 hits (clear line of sight)")]
        public float ClearFactor = 1.0f;

        [Tooltip("Factor when 1 hit (partial occlusion)")]
        public float PartialFactor = 0.5f;

        [Tooltip("Factor when 2+ hits (heavy occlusion)")]
        public float HeavyFactor = 0.15f;

        [Header("Audio Application")]
        [Tooltip("Low-pass cutoff when fully occluded (Hz)")]
        public float OccludedCutoff = 500f;

        [Tooltip("Low-pass cutoff when clear (Hz)")]
        public float ClearCutoff = 22000f;

        [Tooltip("Volume when fully occluded (0-1)")]
        [Range(0f, 1f)]
        public float OccludedVolume = 0.15f;

        [Tooltip("Volume when clear (0-1)")]
        [Range(0f, 1f)]
        public float ClearVolume = 1f;
    }
}
