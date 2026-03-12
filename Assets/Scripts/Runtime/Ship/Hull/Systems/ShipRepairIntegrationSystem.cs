using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using DIG.Survival.Tools;

namespace DIG.Ship.Hull.Systems
{
    /// <summary>
    /// Integrates Welder tool changes back into ShipHullSection.
    /// Clears breach flags if repairs are sufficient.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PredictedSimulationSystemGroup))] // Run after Welder
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ShipRepairIntegrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (hull, repairable) in
                     SystemAPI.Query<RefRW<ShipHullSection>, RefRO<WeldRepairable>>())
            {
                // Detect if repairable health changed (increased) by welder
                // Since this runs after welder, we trust repairable.CurrentHealth is newer if it's higher
                // But actually, we just blindly sync back because DamageSystem ran before welder and set baseline.
                // So any diff is from welder.
                
                hull.ValueRW.Current = repairable.ValueRO.CurrentHealth;

                // Re-evaluate breach state
                float fraction = hull.ValueRO.Current / hull.ValueRO.Max;
                
                if (fraction > 0.5f) // Threshold for fixing breach
                {
                    if (hull.ValueRO.IsBreached)
                    {
                        hull.ValueRW.IsBreached = false;
                        hull.ValueRW.BreachSeverity = 0f;
                    }
                }
                else
                {
                    // Still breached, update severity
                    hull.ValueRW.IsBreached = true;
                    hull.ValueRW.BreachSeverity = 1f - (fraction * 2f);
                }
            }
        }
    }
}
