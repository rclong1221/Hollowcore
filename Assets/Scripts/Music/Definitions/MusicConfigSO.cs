using UnityEngine;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: ScriptableObject holding music system configuration.
    /// Loaded from Resources/MusicConfig by MusicBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Music/Music Config")]
    public class MusicConfigSO : ScriptableObject
    {
        [Header("Track Defaults")]
        [Tooltip("Default exploration track ID.")]
        public int DefaultTrackId;

        [Header("Fade Speeds")]
        [Tooltip("Intensity smoothing lerp speed (default 2.0).")]
        public float CombatFadeSpeed = 2f;

        [Tooltip("Zone crossfade speed in seconds (default 1.5).")]
        public float ZoneFadeSpeed = 1.5f;

        [Tooltip("Boss music transition speed (default 4.0).")]
        public float BossOverrideFadeSpeed = 4f;

        [Tooltip("Per-stem volume lerp speed (default 3.0).")]
        public float StemTransitionSpeed = 3f;

        [Header("Stingers")]
        [Range(0f, 1f)]
        [Tooltip("Master stinger volume (default 0.8).")]
        public float StingerVolume = 0.8f;

        [Tooltip("Min seconds between stingers (default 3.0).")]
        public float StingerCooldown = 3f;

        [Header("Combat Intensity")]
        [Tooltip("AlertState read range in meters (default 40.0).")]
        public float MaxCombatIntensityRange = 40f;

        [Tooltip("COMBAT alert weight (default 1.0).")]
        public float IntensityWeightCombat = 1f;

        [Tooltip("SEARCHING alert weight (default 0.6).")]
        public float IntensityWeightSearching = 0.6f;

        [Tooltip("SUSPICIOUS alert weight (default 0.3).")]
        public float IntensityWeightSuspicious = 0.3f;

        [Tooltip("CURIOUS alert weight (default 0.1).")]
        public float IntensityWeightCurious = 0.1f;

        [Tooltip("AI entity count cap for intensity (default 8).")]
        public int MaxIntensityContributors = 8;
    }
}
