using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Player.Components;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: Distributes KillCredited to nearby party members.
    /// Tags distributed kills with PartyKillTag to prevent re-distribution.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PartyXPSharingSystem))]
    public partial class PartyKillCreditSystem : SystemBase
    {
        private EntityQuery _killQuery;

        protected override void OnCreate()
        {
            _killQuery = GetEntityQuery(
                ComponentType.ReadOnly<KillCredited>(),
                ComponentType.ReadOnly<PartyLink>(),
                ComponentType.Exclude<PartyKillTag>());
            RequireForUpdate<PartyConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            if (_killQuery.IsEmpty) return;

            CompleteDependency();

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var partyStateLookup = GetComponentLookup<PartyState>(true);
            var memberBufferLookup = GetBufferLookup<PartyMemberElement>(true);
            var proximityBufferLookup = GetBufferLookup<PartyProximityState>(true);

            var entities = _killQuery.ToEntityArray(Allocator.Temp);
            var kills = _killQuery.ToComponentDataArray<KillCredited>(Allocator.Temp);
            var links = _killQuery.ToComponentDataArray<PartyLink>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var killer = entities[i];
                var kill = kills[i];
                var partyEntity = links[i].PartyEntity;

                if (partyEntity == Entity.Null || !partyStateLookup.HasComponent(partyEntity))
                {
                    // Tag original to prevent reprocessing
                    ecb.AddComponent<PartyKillTag>(killer);
                    continue;
                }

                if (!memberBufferLookup.HasBuffer(partyEntity) || !proximityBufferLookup.HasBuffer(partyEntity))
                {
                    ecb.AddComponent<PartyKillTag>(killer);
                    continue;
                }

                var members = memberBufferLookup[partyEntity];
                var proximity = proximityBufferLookup[partyEntity];

                // Distribute KillCredited to in-range party members (excluding killer)
                for (int m = 0; m < members.Length; m++)
                {
                    var member = members[m].PlayerEntity;
                    if (member == killer) continue;

                    // Check if this member is in kill credit range
                    bool inRange = false;
                    for (int p = 0; p < proximity.Length; p++)
                    {
                        if (proximity[p].PlayerEntity == member)
                        {
                            inRange = proximity[p].InKillCreditRange;
                            break;
                        }
                    }

                    if (!inRange) continue;

                    // Add KillCredited to this member
                    ecb.AddComponent(member, new KillCredited
                    {
                        Victim = kill.Victim,
                        VictimPosition = kill.VictimPosition,
                        ServerTick = kill.ServerTick
                    });
                    ecb.AddComponent<PartyKillTag>(member);
                }

                // Tag original killer
                ecb.AddComponent<PartyKillTag>(killer);
            }

            entities.Dispose();
            kills.Dispose();
            links.Dispose();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
