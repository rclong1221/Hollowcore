using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Client-only singleton tracking all runtime music state.
    /// Updated by MusicZoneSystem, MusicCombatIntensitySystem, MusicTransitionSystem, MusicStemMixSystem.
    /// Read by MusicPlaybackSystem and CombatMusicDuckSystem.
    /// Zero impact on player entity archetype.
    /// </summary>
    public struct MusicState : IComponentData
    {
        /// <summary>Currently playing track (0 = none).</summary>
        public int CurrentTrackId;

        /// <summary>Track to crossfade toward.</summary>
        public int TargetTrackId;

        /// <summary>0.0 = fully old track, 1.0 = fully new track.</summary>
        public float CrossfadeProgress;

        /// <summary>0 = idle, 1 = fading.</summary>
        public byte CrossfadeDirection;

        /// <summary>Raw combat intensity 0.0 (peaceful) to 1.0 (maximum combat).</summary>
        public float CombatIntensity;

        /// <summary>Lerped CombatIntensity (avoids jitter).</summary>
        public float SmoothedIntensity;

        /// <summary>Non-zero = boss music forced. 0 = normal zone music.</summary>
        public int BossOverrideTrackId;

        /// <summary>Centralized combat flag (replaces direct CombatState reads).</summary>
        public bool IsInCombat;

        /// <summary>Per-stem volumes: x=Base, y=Percussion, z=Melody, w=Intensity.</summary>
        public float4 StemVolumes;

        /// <summary>Priority of the active music zone.</summary>
        public int CurrentZonePriority;

        /// <summary>Current zone's fade-in speed override.</summary>
        public float ZoneFadeInDuration;

        /// <summary>Current zone's fade-out speed override.</summary>
        public float ZoneFadeOutDuration;

        /// <summary>Remaining cooldown before another stinger can play.</summary>
        public float StingerCooldown;

        /// <summary>Cached thresholds from current track: x=Perc, y=Melody, z=Intensity. Avoids managed DB lookup in Burst.</summary>
        public float3 CurrentTrackThresholds;
    }
}
