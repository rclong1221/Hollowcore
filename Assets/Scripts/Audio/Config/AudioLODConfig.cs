using UnityEngine;

namespace Audio.Config
{
    /// <summary>
    /// Configuration for audio LOD tiers and voice budget.
    /// Controls distance-based quality reduction and platform voice limits.
    /// EPIC 15.27 Phase 5.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioLODConfig", menuName = "DIG/Audio/LOD Config")]
    public class AudioLODConfig : ScriptableObject
    {
        [Header("LOD Distance Thresholds (meters)")]
        [Tooltip("Full quality: stereo, full occlusion, full reverb")]
        public float FullQualityDistance = 20f;

        [Tooltip("Reduced quality: mono, cached occlusion, reduced reverb")]
        public float ReducedQualityDistance = 40f;

        [Tooltip("Minimal quality: mono, no occlusion, no reverb")]
        public float MinimalQualityDistance = 60f;

        [Header("Voice Budget")]
        [Tooltip("Maximum active voices on PC")]
        public int PCVoiceBudget = 48;

        [Tooltip("Maximum active voices on Console")]
        public int ConsoleVoiceBudget = 32;

        [Header("Scoring")]
        [Tooltip("Distance falloff for priority scoring (higher = faster falloff)")]
        [Range(0.01f, 1f)]
        public float DistanceFalloff = 0.1f;

        [Tooltip("Priority threshold for exempt sources (never culled)")]
        [Range(0, 255)]
        public int ExemptPriorityThreshold = 200;

        [Header("Paradigm Multiplier")]
        [Tooltip("Multiplier for LOD distances in isometric/top-down paradigms (needs larger ranges)")]
        [Range(0.5f, 3f)]
        public float ParadigmDistanceMultiplier = 1f;

        [Header("Quality Reduction")]
        [Tooltip("Whether to downmix stereo to mono at Reduced tier")]
        public bool DownmixAtReduced = true;

        /// <summary>Get the voice budget for the current platform.</summary>
        public int GetVoiceBudget()
        {
#if UNITY_STANDALONE
            return PCVoiceBudget;
#else
            return ConsoleVoiceBudget;
#endif
        }

        /// <summary>Get the effective LOD tier for a given distance.</summary>
        public AudioLODTier GetTier(float distance)
        {
            float mult = ParadigmDistanceMultiplier;
            if (distance <= FullQualityDistance * mult) return AudioLODTier.Full;
            if (distance <= ReducedQualityDistance * mult) return AudioLODTier.Reduced;
            if (distance <= MinimalQualityDistance * mult) return AudioLODTier.Minimal;
            return AudioLODTier.Culled;
        }
    }

    public enum AudioLODTier : byte
    {
        Full = 0,
        Reduced = 1,
        Minimal = 2,
        Culled = 3
    }
}
