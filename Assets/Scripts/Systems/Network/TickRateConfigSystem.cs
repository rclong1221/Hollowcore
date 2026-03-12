using Unity.Entities;
using Unity.NetCode;

namespace DIG.Network
{
    /// <summary>
    /// Configures the NetCode simulation tick rate on the server.
    ///
    /// Default is 60Hz with MaxSimulationStepsPerFrame=1. At low frame rates
    /// (e.g. 22 FPS in editor with 100 enemies), the server can only run 1 tick
    /// per frame and falls behind real time — causing choppiness and rubber-banding.
    ///
    /// Fix: 30Hz tick rate + MaxSimulationStepsPerFrame=4.
    /// At 30Hz, a 45ms frame only needs ~1.5 ticks (not ~3), so the server
    /// keeps up with real time. MaxSimulationStepsPerFrame=4 allows catching up
    /// after frame spikes without dropping ticks.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [CreateAfter(typeof(NetworkStreamReceiveSystem))]
    public partial struct TickRateConfigSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Enabled = false;

            var tickRate = new ClientServerTickRate();
            tickRate.SimulationTickRate = 30;
            tickRate.NetworkTickRate = 30;
            tickRate.MaxSimulationStepsPerFrame = 4;
            tickRate.MaxSimulationStepBatchSize = 4;
            tickRate.ResolveDefaults();

            if (SystemAPI.HasSingleton<ClientServerTickRate>())
            {
                var entity = SystemAPI.GetSingletonEntity<ClientServerTickRate>();
                state.EntityManager.SetComponentData(entity, tickRate);
            }
            else
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, tickRate);
            }
        }
    }
}
