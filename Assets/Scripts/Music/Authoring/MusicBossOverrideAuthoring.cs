using Unity.Entities;
using UnityEngine;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Baker for boss music override.
    /// Place on boss encounter entities alongside EncounterProfileAuthoring.
    /// EncounterTriggerSystem creates MusicBossOverride transient entities when PlayMusic fires.
    /// The baked component stores the TrackId for reference; actual override is via transient entities.
    /// </summary>
    [AddComponentMenu("DIG/Music/Boss Music Override")]
    public class MusicBossOverrideAuthoring : MonoBehaviour
    {
        [Tooltip("Boss music track ID from MusicTrackSO.")]
        public int BossTrackId;

        public class Baker : Baker<MusicBossOverrideAuthoring>
        {
            public override void Bake(MusicBossOverrideAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new MusicBossOverride
                {
                    TrackId = authoring.BossTrackId,
                    Activate = false
                });
            }
        }
    }
}
