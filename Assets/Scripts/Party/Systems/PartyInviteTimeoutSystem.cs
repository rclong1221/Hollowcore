using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: Destroys expired PartyInvite transient entities.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PartyFormationSystem))]
    public partial class PartyInviteTimeoutSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PartyConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            var netTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = netTime.ServerTick.IsValid ? netTime.ServerTick.TickIndexForValidTick : 1;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (invite, entity) in SystemAPI.Query<RefRO<PartyInvite>>()
                         .WithNone<PartyJoinRequest>()
                         .WithEntityAccess())
            {
                if (currentTick >= invite.ValueRO.ExpirationTick)
                {
                    ecb.DestroyEntity(entity);

                    PartyVisualQueue.Enqueue(new PartyVisualQueue.PartyVisualEvent
                    {
                        Type = PartyNotifyType.InviteExpired,
                        SourcePlayer = invite.ValueRO.InviterEntity
                    });
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
