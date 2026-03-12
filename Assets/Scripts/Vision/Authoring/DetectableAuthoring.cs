using UnityEngine;
using Unity.Entities;
using DIG.Vision.Components;

namespace DIG.Vision.Authoring
{
    /// <summary>
    /// Authoring component that marks an entity as detectable by DetectionSensor.
    /// Attach to players, allies, or any entity that AI should be able to see.
    /// EPIC 15.17: Detection / Line-of-Sight System
    /// </summary>
    [AddComponentMenu("DIG/Detection/Detectable Authoring")]
    [DisallowMultipleComponent]
    public class DetectableAuthoring : MonoBehaviour
    {
        [Tooltip("Vertical offset from entity origin for the raycast target point (center mass).")]
        public float DetectionHeightOffset = 1.0f;

        [Tooltip("Stealth multiplier. 1.0 = fully visible, 0.5 = half detection range, 0.0 = invisible.")]
        [Range(0f, 1f)]
        public float StealthMultiplier = 1.0f;

        [Tooltip("Whether this entity starts as detectable. Can be toggled at runtime.")]
        public bool StartEnabled = true;

        class Baker : Baker<DetectableAuthoring>
        {
            public override void Bake(DetectableAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new Detectable
                {
                    DetectionHeightOffset = authoring.DetectionHeightOffset,
                    StealthMultiplier = authoring.StealthMultiplier
                });

                SetComponentEnabled<Detectable>(entity, authoring.StartEnabled);
            }
        }
    }
}
