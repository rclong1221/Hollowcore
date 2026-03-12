using Unity.Entities;
using UnityEngine;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Baker for music zone trigger volumes.
    /// Place on a GameObject with a Collider (IsTrigger=true) in a client subscene.
    /// When the player enters this volume, MusicZoneSystem switches to this zone's track.
    /// </summary>
    [AddComponentMenu("DIG/Music/Music Zone")]
    public class MusicZoneAuthoring : MonoBehaviour
    {
        [Tooltip("Track ID from MusicTrackSO to play in this zone.")]
        public int TrackId;

        [Tooltip("Higher priority zones override lower (boss arenas > overworld). Default 0.")]
        public int Priority;

        [Tooltip("Seconds to crossfade into this zone's track. 0 = use global default.")]
        public float FadeInDuration;

        [Tooltip("Seconds to crossfade out when leaving. 0 = use global default.")]
        public float FadeOutDuration;

        public class Baker : Baker<MusicZoneAuthoring>
        {
            public override void Bake(MusicZoneAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new MusicZone
                {
                    TrackId = authoring.TrackId,
                    Priority = authoring.Priority,
                    FadeInDuration = authoring.FadeInDuration,
                    FadeOutDuration = authoring.FadeOutDuration
                });
            }
        }
    }
}
