using UnityEngine;
using Unity.Entities;

namespace DIG.Widgets.Authoring
{
    /// <summary>
    /// EPIC 15.26 Phase 5: Baker for OffScreenTracker component.
    /// Add to boss, quest objective, waypoint, or party member prefabs
    /// to enable edge-of-screen tracking indicators when off-screen.
    /// </summary>
    [AddComponentMenu("DIG/Widgets/Off-Screen Tracker Authoring")]
    public class OffScreenTrackerAuthoring : MonoBehaviour
    {
        [Header("Tracking")]
        [Tooltip("What type of entity this is (determines icon and color).")]
        public TrackedEntityType TrackedType = TrackedEntityType.Boss;

        [Tooltip("Always track this entity regardless of paradigm settings.")]
        public bool AlwaysTrack = true;

        public class Baker : Baker<OffScreenTrackerAuthoring>
        {
            public override void Bake(OffScreenTrackerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new OffScreenTracker
                {
                    TrackedType = authoring.TrackedType,
                    AlwaysTrack = authoring.AlwaysTrack
                });
            }
        }
    }
}
