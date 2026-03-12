using Unity.Entities;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Placed on trigger volume entities in client subscenes.
    /// MusicZoneSystem detects player overlap and sets TargetTrackId.
    /// Not ghost-replicated — client-only.
    /// </summary>
    public struct MusicZone : IComponentData
    {
        /// <summary>MusicTrackSO.TrackId reference.</summary>
        public int TrackId;

        /// <summary>Higher priority overrides lower (boss arenas > overworld).</summary>
        public int Priority;

        /// <summary>Seconds to crossfade into this zone's track (0 = use default).</summary>
        public float FadeInDuration;

        /// <summary>Seconds to crossfade out when leaving (0 = use default).</summary>
        public float FadeOutDuration;
    }
}
