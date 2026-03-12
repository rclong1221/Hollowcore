using Unity.Entities;
using UnityEngine;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Authoring component for cinematic zone triggers.
    /// Place on zone entry GameObjects, boss arena triggers, or story checkpoint entities.
    /// Baker adds CinematicTrigger (12 bytes) -- no player prefab modification required.
    /// </summary>
    [AddComponentMenu("DIG/Cinematic/Cinematic Trigger")]
    public class CinematicTriggerAuthoring : MonoBehaviour
    {
        [Tooltip("Stable identifier referencing CinematicDefinitionSO.CinematicId.")]
        public int CinematicId;

        [Tooltip("If true, trigger fires only once per session.")]
        public bool PlayOnce = true;

        public CinematicType CinematicType = CinematicType.FullCinematic;
        public SkipPolicy SkipPolicy = SkipPolicy.AnyoneCanSkip;

        [Tooltip("Overlap sphere radius for zone trigger detection.")]
        [Min(0.1f)]
        public float TriggerRadius = 5f;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0.3f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, TriggerRadius);
            Gizmos.color = new Color(0.8f, 0.3f, 1f, 0.1f);
            Gizmos.DrawSphere(transform.position, TriggerRadius);
        }

        public class Baker : Baker<CinematicTriggerAuthoring>
        {
            public override void Bake(CinematicTriggerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new CinematicTrigger
                {
                    CinematicId = authoring.CinematicId,
                    PlayOnce = authoring.PlayOnce,
                    HasPlayed = false,
                    CinematicType = authoring.CinematicType,
                    SkipPolicy = authoring.SkipPolicy,
                    TriggerRadius = authoring.TriggerRadius
                });
            }
        }
    }
}
