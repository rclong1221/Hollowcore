using Unity.Entities;
using UnityEngine;
using DIG.AI.Components;

namespace DIG.AI.Authoring
{
    /// <summary>
    /// EPIC 15.23: Authoring component for the EnemySeparationConfig singleton.
    /// Place on a single GameObject in your enemy subscene to configure separation behavior.
    /// </summary>
    [AddComponentMenu("DIG/AI/Enemy Separation Config")]
    public class EnemySeparationConfigAuthoring : MonoBehaviour
    {
        [Header("Separation")]
        [Tooltip("Distance within which enemies push apart (meters). Roughly enemy capsule diameter.")]
        [Range(0.5f, 5.0f)]
        public float SeparationRadius = 1.5f;

        [Tooltip("Strength of separation push. Higher = snappier separation, lower = softer.")]
        [Range(0.1f, 20.0f)]
        public float SeparationWeight = 8.0f;

        [Tooltip("Maximum separation displacement per second (m/s). Prevents teleporting on overlap.")]
        [Range(0.5f, 20.0f)]
        public float MaxSeparationSpeed = 8.0f;

        [Header("Performance")]
        [Tooltip("Run separation every N frames (must be power of 2). 2 = every other frame, 4 = every 4th frame.")]
        [Range(1, 8)]
        public int FrameInterval = 1;

        public class Baker : Baker<EnemySeparationConfigAuthoring>
        {
            public override void Bake(EnemySeparationConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new EnemySeparationConfig
                {
                    SeparationRadius = authoring.SeparationRadius,
                    SeparationWeight = authoring.SeparationWeight,
                    MaxSeparationSpeed = authoring.MaxSeparationSpeed,
                    FrameInterval = authoring.FrameInterval
                });
            }
        }
    }
}
