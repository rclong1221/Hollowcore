using Unity.Burst;
using Unity.Entities;
using DIG.Aggro.Components;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.33: Counts down ThreatFixate timer and disables the component when expired.
    /// AggroTargetSelectorSystem checks ThreatFixate before normal scoring.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ThreatModifierSystem))]
    [BurstCompile]
    public partial struct ThreatFixateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Runs when ThreatFixate exists
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            foreach (var (fixate, entity) in
                SystemAPI.Query<RefRW<ThreatFixate>>()
                .WithEntityAccess())
            {
                if (!SystemAPI.IsComponentEnabled<ThreatFixate>(entity))
                    continue;

                fixate.ValueRW.Timer -= dt;

                if (fixate.ValueRO.Timer <= 0f)
                {
                    SystemAPI.SetComponentEnabled<ThreatFixate>(entity, false);
                }
            }
        }
    }
}
