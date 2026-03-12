using Unity.Entities;
using Unity.NetCode;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Closes stale dialogue sessions where:
    /// 1. MaxSessionDurationTicks exceeded (session timeout)
    /// 2. InteractingPlayer entity no longer exists (disconnected player)
    /// Runs OrderLast to catch any sessions missed by other systems.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class DialogueCleanupSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<DialogueConfig>();
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<DialogueConfig>();
            var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.TickIndexForValidTick;

            foreach (var (session, entity) in
                SystemAPI.Query<RefRW<DialogueSessionState>>().WithEntityAccess())
            {
                if (!session.ValueRO.IsActive) continue;

                bool shouldClose = false;

                // Check timeout
                if (config.MaxSessionDurationTicks > 0 &&
                    (uint)tick - session.ValueRO.SessionStartTick > config.MaxSessionDurationTicks)
                {
                    shouldClose = true;
                }

                // Check if player entity still exists
                if (session.ValueRO.InteractingPlayer != Entity.Null &&
                    !EntityManager.Exists(session.ValueRO.InteractingPlayer))
                {
                    shouldClose = true;
                }

                if (shouldClose)
                {
                    session.ValueRW.IsActive = false;
                    session.ValueRW.InteractingPlayer = Entity.Null;
                    session.ValueRW.CurrentNodeId = 0;
                    session.ValueRW.CurrentTreeId = 0;
                    session.ValueRW.ValidChoicesMask = 0;
                    session.ValueRW.SessionStartTick = 0;
                }
            }
        }
    }
}
