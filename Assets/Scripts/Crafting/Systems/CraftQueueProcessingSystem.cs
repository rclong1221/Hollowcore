using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: Burst ISystem that ticks crafting queue timers.
    /// Advances first Queued to InProgress, ticks elapsed time, transitions Complete when done.
    /// Server-only.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CraftValidationSystem))]
    public partial struct CraftQueueProcessingSystem : ISystem
    {
        private EntityQuery _stationQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _stationQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<CraftingStation>()
                .WithAllRW<CraftQueueElement>()
                .Build(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var entities = _stationQuery.ToEntityArray(Allocator.Temp);

            for (int s = 0; s < entities.Length; s++)
            {
                var station = state.EntityManager.GetComponentData<CraftingStation>(entities[s]);
                var queue = state.EntityManager.GetBuffer<CraftQueueElement>(entities[s]);
                float speedMul = station.SpeedMultiplier;
                bool hasInProgress = false;

                // First pass: tick InProgress items
                for (int i = 0; i < queue.Length; i++)
                {
                    var elem = queue[i];
                    if (elem.State == CraftState.InProgress)
                    {
                        hasInProgress = true;
                        elem.CraftTimeElapsed += dt * speedMul;
                        if (elem.CraftTimeElapsed >= elem.CraftTimeTotal)
                        {
                            elem.CraftTimeElapsed = elem.CraftTimeTotal;
                            elem.State = CraftState.Complete;
                        }
                        queue[i] = elem;
                    }
                }

                // Second pass: if no InProgress, advance first Queued
                if (!hasInProgress)
                {
                    for (int i = 0; i < queue.Length; i++)
                    {
                        var elem = queue[i];
                        if (elem.State == CraftState.Queued)
                        {
                            elem.State = CraftState.InProgress;
                            queue[i] = elem;
                            break;
                        }
                    }
                }
            }

            entities.Dispose();
        }
    }
}
