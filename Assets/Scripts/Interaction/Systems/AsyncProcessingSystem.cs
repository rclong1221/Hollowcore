using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 16.1 Phase 2: Server-side async processing for stations.
    ///
    /// Ticks ProcessingTimeElapsed on stations with IsProcessing = true.
    /// When elapsed >= total, sets OutputReady and stops processing.
    ///
    /// Runs on server only — processing continues even when players walk away.
    /// Clients see state via ghost replication of AsyncProcessingState fields.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct AsyncProcessingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AsyncProcessingState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var processing in
                     SystemAPI.Query<RefRW<AsyncProcessingState>>())
            {
                ref var proc = ref processing.ValueRW;

                if (!proc.IsProcessing)
                    continue;

                proc.ProcessingTimeElapsed += deltaTime;

                if (proc.ProcessingTimeElapsed >= proc.ProcessingTimeTotal)
                {
                    proc.ProcessingTimeElapsed = proc.ProcessingTimeTotal;
                    proc.IsProcessing = false;
                    proc.OutputReady = true;
                }
            }
        }
    }
}
