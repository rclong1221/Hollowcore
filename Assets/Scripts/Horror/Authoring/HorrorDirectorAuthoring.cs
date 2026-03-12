using UnityEngine;
using Unity.Entities;
using Horror.Components;

namespace Horror.Authoring
{
    /// <summary>
    /// Authoring component to configure the Horror Director singleton.
    /// Add to a single GameObject in your scene/subscene.
    /// </summary>
    [AddComponentMenu("DIG/Horror/Horror Director")]
    public class HorrorDirectorAuthoring : MonoBehaviour
    {
        [Header("Tension Build")]
        [Tooltip("How quickly global tension builds (per second). Full tension at 1.0 means 100% after 1/rate seconds.")]
        [Range(0.001f, 0.1f)]
        public float TensionBuildRate = 0.005f; // ~3.3 minutes to max tension
        
        [Header("Event Timing")]
        [Tooltip("Minimum seconds between global horror events")]
        [Range(5f, 120f)]
        public float MinEventCooldown = 15f;
        
        [Tooltip("Maximum seconds between global horror events (at low tension)")]
        [Range(30f, 300f)]
        public float MaxEventCooldown = 60f;

        [Header("Hallucination Settings")]
        [Tooltip("Stress level (0-1) at which hallucinations can start")]
        [Range(0.3f, 0.9f)]
        public float HallucinationThreshold = 0.6f;
        
        [Tooltip("Minimum seconds between hallucinations")]
        [Range(5f, 60f)]
        public float MinHallucinationCooldown = 15f;
        
        [Tooltip("Maximum hallucination duration in seconds")]
        [Range(1f, 10f)]
        public float MaxHallucinationDuration = 4f;
        
        [Tooltip("Base chance of hallucination per second at max stress")]
        [Range(0.01f, 0.5f)]
        public float HallucinationProbability = 0.1f;

        [Header("Light Flicker")]
        [Tooltip("Minimum flicker duration")]
        [Range(0.05f, 0.5f)]
        public float FlickerDurationMin = 0.1f;
        
        [Tooltip("Maximum flicker duration")]
        [Range(0.2f, 2f)]
        public float FlickerDurationMax = 0.6f;

        class Baker : Baker<HorrorDirectorAuthoring>
        {
            public override void Bake(HorrorDirectorAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                
                // Add HorrorDirector singleton
                AddComponent(entity, new HorrorDirector
                {
                    GlobalTension = 0f,
                    TimeSinceLastEvent = 0f,
                    MissionTime = 0f,
                    TensionBuildRate = authoring.TensionBuildRate,
                    MinEventCooldown = authoring.MinEventCooldown,
                    MaxEventCooldown = authoring.MaxEventCooldown,
                    RandomSeed = (uint)System.DateTime.Now.Ticks
                });
                
                // Add HorrorSettings singleton
                AddComponent(entity, new HorrorSettings
                {
                    HallucinationThreshold = authoring.HallucinationThreshold,
                    MinHallucinationCooldown = authoring.MinHallucinationCooldown,
                    MaxHallucinationDuration = authoring.MaxHallucinationDuration,
                    HallucinationProbabilityPerSecond = authoring.HallucinationProbability,
                    FlickerDurationMin = authoring.FlickerDurationMin,
                    FlickerDurationMax = authoring.FlickerDurationMax
                });
            }
        }
    }
}
