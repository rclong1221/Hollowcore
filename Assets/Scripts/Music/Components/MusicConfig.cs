using Unity.Entities;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Client-only singleton holding music system configuration.
    /// Created by MusicBootstrapSystem from MusicConfigSO loaded from Resources.
    /// </summary>
    public struct MusicConfig : IComponentData
    {
        /// <summary>Fallback track when no zone active.</summary>
        public int DefaultTrackId;

        /// <summary>Lerp speed for CombatIntensity smoothing (default 2.0).</summary>
        public float CombatFadeSpeed;

        /// <summary>Default crossfade speed between zones (default 1.5).</summary>
        public float ZoneFadeSpeed;

        /// <summary>Master volume for stinger playback (default 0.8).</summary>
        public float StingerVolume;

        /// <summary>Min seconds between stingers (default 3.0).</summary>
        public float StingerCooldown;

        /// <summary>Max distance to read AlertState from AI (default 40.0).</summary>
        public float MaxCombatIntensityRange;

        /// <summary>Lerp speed for per-stem volume changes (default 3.0).</summary>
        public float StemTransitionSpeed;

        /// <summary>Fast fade speed for boss music entry (default 4.0).</summary>
        public float BossOverrideFadeSpeed;

        /// <summary>Weight for COMBAT alert level (default 1.0).</summary>
        public float IntensityWeightCombat;

        /// <summary>Weight for SEARCHING alert level (default 0.6).</summary>
        public float IntensityWeightSearching;

        /// <summary>Weight for SUSPICIOUS alert level (default 0.3).</summary>
        public float IntensityWeightSuspicious;

        /// <summary>Weight for CURIOUS alert level (default 0.1).</summary>
        public float IntensityWeightCurious;

        /// <summary>Cap on AI entities counted for intensity (default 8).</summary>
        public int MaxIntensityContributors;
    }
}
