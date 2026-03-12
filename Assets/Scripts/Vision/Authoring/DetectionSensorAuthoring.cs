using UnityEngine;
using Unity.Entities;
using DIG.Vision.Components;

namespace DIG.Vision.Authoring
{
    /// <summary>
    /// Authoring component that bakes a DetectionSensor and SeenTargetElement buffer
    /// onto an entity. Attach to any AI entity that needs to detect targets.
    /// EPIC 15.17: Detection / Line-of-Sight System
    /// </summary>
    [AddComponentMenu("DIG/Detection/Detection Sensor Authoring")]
    [DisallowMultipleComponent]
    public class DetectionSensorAuthoring : MonoBehaviour
    {
        [Header("Vision Cone")]
        [Tooltip("Maximum detection range in meters.")]
        public float ViewDistance = 20f;

        [Tooltip("Half-angle of the horizontal vision cone in degrees (e.g. 45 = 90 degree total FOV).")]
        [Range(1f, 180f)]
        public float ViewAngle = 45f;

        [Tooltip("Half-angle of vertical vision in degrees. Humans have limited up/down peripheral. Set to 180 for all-seeing creatures.")]
        [Range(1f, 180f)]
        public float VerticalViewAngle = 30f;

        [Tooltip("Vertical offset from entity origin for eye position.")]
        public float EyeHeight = 1.6f;

        [Header("Proximity Detection (360°)")]
        [Tooltip("Close-range 360° detection. Targets within this radius are always detected. 0 = disabled (default).")]
        public float ProximityRadius = 0f;

        [Tooltip("Hearing radius for combat sounds, running, etc. 360° detection. 0 = deaf.")]
        public float HearingRadius = 15f;

        [Header("Performance")]
        [Tooltip("Seconds between detection scans. 0 = use global default.")]
        [Range(0f, 2f)]
        public float UpdateInterval = 0.2f;

        class Baker : Baker<DetectionSensorAuthoring>
        {
            public override void Bake(DetectionSensorAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new DetectionSensor
                {
                    ViewDistance = authoring.ViewDistance,
                    ViewAngle = authoring.ViewAngle,
                    VerticalViewAngle = authoring.VerticalViewAngle,
                    EyeHeight = authoring.EyeHeight,
                    ProximityRadius = authoring.ProximityRadius,
                    HearingRadius = authoring.HearingRadius,
                    UpdateInterval = authoring.UpdateInterval,
                    TimeSinceLastUpdate = 0f
                });

                AddBuffer<SeenTargetElement>(entity);
            }
        }
    }
}
