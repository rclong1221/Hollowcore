using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Client-side system that smoothly reconciles predicted dodge dive elapsed time
    /// with authoritative server state to prevent pop/stutter during network corrections.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(DodgeDiveSystem))]
    public partial class DodgeDiveReconciliationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;

            foreach (var (diveRW, predictedMarker, entity) in SystemAPI.Query<RefRW<DodgeDiveState>, RefRO<PredictedDodgeDive>>()
                .WithAll<GhostOwnerIsLocal>()
                .WithEntityAccess())
            {
                ref var dive = ref diveRW.ValueRW;
                
                // If not reconciling and server has sent an update, check if we need to start reconciliation
                if (dive.IsReconciling == 0 && dive.ServerElapsed > 0f)
                {
                    float diff = math.abs(dive.Elapsed - dive.ServerElapsed);
                    if (diff > 0.01f) // Threshold: 10ms difference
                    {
                        dive.IsReconciling = 1;
                        dive.ReconcileSmoothing = 0.2f; // Smooth over ~200ms
                    }
                }

                // Perform smoothing reconciliation
                if (dive.IsReconciling == 1)
                {
                    // Lerp predicted elapsed toward server elapsed
                    dive.Elapsed = math.lerp(dive.Elapsed, dive.ServerElapsed, dive.ReconcileSmoothing);
                    
                    // Check if we're close enough to stop reconciling
                    float remainingDiff = math.abs(dive.Elapsed - dive.ServerElapsed);
                    if (remainingDiff < 0.01f)
                    {
                        dive.IsReconciling = 0;
                        dive.Elapsed = dive.ServerElapsed; // Snap to exact value
                    }
                }
            }
        }
    }
}
