using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Closes dialogue sessions on End nodes and DialogueSkipRpc.
    /// Clears DialogueSessionState.IsActive and zeroes InteractingPlayer.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DialogueActionSystem))]
    public partial class DialogueEndSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Process skip RPCs
            var rpcSystem = World.GetExistingSystemManaged<DialogueRpcReceiveSystem>();
            if (rpcSystem != null && rpcSystem.PendingSkips.IsCreated)
            {
                for (int i = 0; i < rpcSystem.PendingSkips.Length; i++)
                {
                    var skip = rpcSystem.PendingSkips[i];
                    if (!EntityManager.HasComponent<DialogueSessionState>(skip.NpcEntity)) continue;
                    CloseSession(skip.NpcEntity);
                }
            }

            // Close sessions where AdvanceSystem set IsActive=false but node is End
            foreach (var (session, entity) in
                SystemAPI.Query<RefRW<DialogueSessionState>>().WithEntityAccess())
            {
                if (!session.ValueRO.IsActive && session.ValueRO.InteractingPlayer != Entity.Null)
                {
                    session.ValueRW.InteractingPlayer = Entity.Null;
                    session.ValueRW.CurrentNodeId = 0;
                    session.ValueRW.CurrentTreeId = 0;
                    session.ValueRW.ValidChoicesMask = 0;
                    session.ValueRW.SessionStartTick = 0;
                }
            }
        }

        private void CloseSession(Entity npcEntity)
        {
            var session = EntityManager.GetComponentData<DialogueSessionState>(npcEntity);
            session.IsActive = false;
            session.InteractingPlayer = Entity.Null;
            session.CurrentNodeId = 0;
            session.CurrentTreeId = 0;
            session.ValidChoicesMask = 0;
            session.SessionStartTick = 0;
            EntityManager.SetComponentData(npcEntity, session);
        }
    }
}
