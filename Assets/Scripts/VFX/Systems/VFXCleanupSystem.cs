using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;

namespace DIG.VFX.Systems
{
    /// <summary>
    /// EPIC 16.7: Destroys ALL remaining VFXRequest entities after VFXExecutionSystem.
    /// Safety net ensuring zero request entity accumulation across frames.
    /// Also handles VFXCleanupTag entities (structural cleanup if VFX systems are removed).
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(VFXExecutionSystem))]
    public partial struct VFXCleanupSystem : ISystem
    {
        private EntityQuery _remainingRequests;
        private EntityQuery _cleanupOnly;
        static readonly ProfilerMarker k_Marker = new("VFXCleanupSystem.Cleanup");

        public void OnCreate(ref SystemState state)
        {
            _remainingRequests = state.GetEntityQuery(ComponentType.ReadOnly<VFXRequest>());
            _cleanupOnly = state.GetEntityQuery(
                ComponentType.ReadOnly<VFXCleanupTag>(),
                ComponentType.Exclude<VFXRequest>()
            );
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            k_Marker.Begin();

            // Destroy any remaining VFXRequest entities (culled, unresolved, missing LOD, etc.)
            if (!_remainingRequests.IsEmpty)
                state.EntityManager.DestroyEntity(_remainingRequests);

            // Destroy orphaned cleanup tag entities (edge case: VFXRequest removed but cleanup tag remains)
            if (!_cleanupOnly.IsEmpty)
                state.EntityManager.DestroyEntity(_cleanupOnly);

            k_Marker.End();
        }
    }
}
