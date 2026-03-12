using Unity.Entities;
using UnityEngine;

namespace DIG.Core.Physics
{
    /// <summary>
    /// EPIC 15.23: ECS component for physics solver configuration.
    /// Baked from PhysicsConfigAuthoring, read by PhysicsOptimizationSystem.
    /// </summary>
    public struct PhysicsConfig : IComponentData
    {
        public int SolverIterationCount;
        public bool IncrementalDynamicBroadphase;
        public bool IncrementalStaticBroadphase;
    }

    /// <summary>
    /// EPIC 15.23: Developer-facing authoring component for physics solver settings.
    /// Place on a GameObject in your subscene to configure physics performance.
    /// PhysicsOptimizationSystem reads these values at initialization.
    /// </summary>
    [AddComponentMenu("DIG/Core/Physics Config")]
    public class PhysicsConfigAuthoring : MonoBehaviour
    {
        [Header("Solver")]
        [Tooltip("Solver iterations per physics step. Lower = faster but less stable contacts. Default: 4")]
        [Range(1, 8)]
        public int SolverIterationCount = 4;

        [Header("Broadphase")]
        [Tooltip("Incremental dynamic BVH — only updates moved bodies. Essential for many kinematic enemies.")]
        public bool IncrementalDynamicBroadphase = true;

        [Tooltip("Incremental static BVH — only updates changed static bodies.")]
        public bool IncrementalStaticBroadphase = true;

        public class Baker : Baker<PhysicsConfigAuthoring>
        {
            public override void Bake(PhysicsConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new PhysicsConfig
                {
                    SolverIterationCount = authoring.SolverIterationCount,
                    IncrementalDynamicBroadphase = authoring.IncrementalDynamicBroadphase,
                    IncrementalStaticBroadphase = authoring.IncrementalStaticBroadphase,
                });
            }
        }
    }
}
