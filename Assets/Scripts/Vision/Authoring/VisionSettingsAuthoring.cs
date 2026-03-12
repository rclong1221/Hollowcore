using UnityEngine;
using Unity.Entities;
using DIG.Vision.Components;

namespace DIG.Vision.Authoring
{
    /// <summary>
    /// Authoring component that creates the DetectionSettings singleton.
    /// Place on a single GameObject in the scene (e.g. "DetectionManager" or "GameSettings").
    /// EPIC 15.17: Detection / Line-of-Sight System
    /// </summary>
    [AddComponentMenu("DIG/Detection/Detection Settings")]
    [DisallowMultipleComponent]
    public class VisionSettingsAuthoring : MonoBehaviour
    {
        [Header("Timing")]
        [Tooltip("Default scan interval for sensors that don't override it.")]
        [Range(0.05f, 2f)]
        public float GlobalUpdateInterval = 0.2f;

        [Tooltip("How long (seconds) a sensor remembers a target after losing sight.")]
        [Range(0f, 30f)]
        public float MemoryDuration = 5.0f;

        [Header("Performance")]
        [Tooltip("Maximum number of occlusion raycasts per frame across all sensors.")]
        [Range(1, 256)]
        public int MaxRaycastsPerFrame = 64;

        [Header("Stealth")]
        [Tooltip("Master toggle for stealth modifier application.")]
        public bool EnableStealthModifiers = true;

        class Baker : Baker<VisionSettingsAuthoring>
        {
            public override void Bake(VisionSettingsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new VisionSettings
                {
                    GlobalUpdateInterval = authoring.GlobalUpdateInterval,
                    MemoryDuration = authoring.MemoryDuration,
                    MaxRaycastsPerFrame = authoring.MaxRaycastsPerFrame,
                    EnableStealthModifiers = authoring.EnableStealthModifiers
                });
            }
        }
    }
}
