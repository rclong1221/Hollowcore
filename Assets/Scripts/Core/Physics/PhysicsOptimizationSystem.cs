using Unity.Entities;
using Unity.Physics;

namespace DIG.Core.Physics
{
    /// <summary>
    /// Creates a PhysicsStep singleton with optimized settings if none exists.
    ///
    /// Key optimization: IncrementalDynamicBroadphase = true
    /// Without this, Unity Physics rebuilds the entire dynamic BVH every tick.
    /// With 100+ kinematic enemies, that's O(n log n) every tick even if nothing moved.
    /// Incremental mode only updates BVH nodes for bodies that actually moved: O(k log n).
    ///
    /// EPIC 15.23: Now reads from PhysicsConfig singleton (baked from PhysicsConfigAuthoring)
    /// for developer-configurable solver iterations and broadphase settings.
    ///
    /// Runs once during initialization, then disables itself.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(BeginInitializationEntityCommandBufferSystem))]
    public partial struct PhysicsOptimizationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            // Only run once
            state.Enabled = false;

            // Read developer config if present (baked from PhysicsConfigAuthoring)
            bool hasConfig = SystemAPI.HasSingleton<PhysicsConfig>();
            PhysicsConfig config = default;
            if (hasConfig)
            {
                config = SystemAPI.GetSingleton<PhysicsConfig>();
            }

            // If PhysicsStep already exists (from PhysicsStepAuthoring in subscene),
            // apply config overrides to it
            if (SystemAPI.HasSingleton<PhysicsStep>())
            {
                if (hasConfig)
                {
                    var existingEntity = SystemAPI.GetSingletonEntity<PhysicsStep>();
                    var existingStep = SystemAPI.GetSingleton<PhysicsStep>();
                    existingStep.SolverIterationCount = config.SolverIterationCount;
                    existingStep.IncrementalDynamicBroadphase = config.IncrementalDynamicBroadphase;
                    existingStep.IncrementalStaticBroadphase = config.IncrementalStaticBroadphase;
                    state.EntityManager.SetComponentData(existingEntity, existingStep);
                }
                return;
            }

            // No PhysicsStep exists — create one
            var step = PhysicsStep.Default;

            if (hasConfig)
            {
                step.SolverIterationCount = config.SolverIterationCount;
                step.IncrementalDynamicBroadphase = config.IncrementalDynamicBroadphase;
                step.IncrementalStaticBroadphase = config.IncrementalStaticBroadphase;
            }
            else
            {
                // Defaults when no config is present
                step.IncrementalDynamicBroadphase = true;
                step.IncrementalStaticBroadphase = true;
            }

            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, step);
        }
    }
}
