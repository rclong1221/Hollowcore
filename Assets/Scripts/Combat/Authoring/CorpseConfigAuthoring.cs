using Unity.Entities;
using UnityEngine;
using DIG.Combat.Components;

namespace DIG.Combat.Authoring
{
    /// <summary>
    /// EPIC 16.3: Developer-facing authoring for global corpse lifecycle settings.
    /// Place on a GameObject in your subscene to configure corpse behavior.
    /// If absent, CorpseLifecycleSystem creates a default config at runtime.
    /// </summary>
    [AddComponentMenu("DIG/Combat/Corpse Config")]
    public class CorpseConfigAuthoring : MonoBehaviour
    {
        [Header("Ragdoll")]
        [Tooltip("Seconds the ragdoll plays before settling. Set to 0 to skip ragdoll.")]
        [Range(0f, 10f)]
        public float RagdollDuration = 2.0f;

        [Header("Corpse Persistence")]
        [Tooltip("Seconds the corpse stays visible after ragdoll settles.")]
        [Range(1f, 120f)]
        public float CorpseLifetime = 15.0f;

        [Tooltip("Maximum number of corpses in the world. Oldest non-boss corpse is removed when exceeded.")]
        [Range(5, 200)]
        public int MaxCorpses = 30;

        [Tooltip("Boss/elite corpses are never auto-despawned by the MaxCorpses cap.")]
        public bool PersistentBosses = true;

        [Header("Fade Out")]
        [Tooltip("Seconds for the corpse to sink into the ground before being destroyed.")]
        [Range(0.5f, 5f)]
        public float FadeOutDuration = 1.5f;

        [Header("Distance Culling")]
        [Tooltip("Corpses beyond this distance from any player skip fade and are destroyed instantly.")]
        [Range(20f, 500f)]
        public float DistanceCullRange = 100f;

        public class Baker : Baker<CorpseConfigAuthoring>
        {
            public override void Bake(CorpseConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new CorpseConfig
                {
                    RagdollDuration = authoring.RagdollDuration,
                    CorpseLifetime = authoring.CorpseLifetime,
                    FadeOutDuration = authoring.FadeOutDuration,
                    MaxCorpses = authoring.MaxCorpses,
                    PersistentBosses = authoring.PersistentBosses,
                    DistanceCullRange = authoring.DistanceCullRange
                });
            }
        }
    }
}
