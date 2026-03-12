using Unity.Entities;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Transient entity created by EncounterTriggerSystem when
    /// TriggerActionType.PlayMusic fires. Consumed by MusicCombatIntensitySystem.
    /// </summary>
    public struct MusicBossOverride : IComponentData
    {
        /// <summary>Boss music track ID.</summary>
        public int TrackId;

        /// <summary>true = start override, false = clear override.</summary>
        public bool Activate;
    }
}
