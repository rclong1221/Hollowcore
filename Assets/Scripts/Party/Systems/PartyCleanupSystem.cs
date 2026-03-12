using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Player.Components;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: Handles disconnects, empty parties, stale transients.
    /// Runs OrderLast in SimulationSystemGroup.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class PartyCleanupSystem : SystemBase
    {
        private EntityQuery _partyQuery;
        private EntityQuery _xpModQuery;
        private EntityQuery _killTagQuery;
        private EntityQuery _designationQuery;
        private EntityQuery _claimQuery;

        protected override void OnCreate()
        {
            _partyQuery = GetEntityQuery(
                ComponentType.ReadOnly<PartyTag>(),
                ComponentType.ReadWrite<PartyState>(),
                ComponentType.ReadWrite<PartyMemberElement>());
            _xpModQuery = GetEntityQuery(ComponentType.ReadOnly<PartyXPModifier>());
            _killTagQuery = GetEntityQuery(
                ComponentType.ReadOnly<PartyKillTag>(),
                ComponentType.Exclude<KillCredited>());
            _designationQuery = GetEntityQuery(ComponentType.ReadOnly<LootDesignation>());
            _claimQuery = GetEntityQuery(ComponentType.ReadOnly<PartyLootClaim>());
            RequireForUpdate<PartyConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            // Skip entirely if nothing to clean up
            if (_partyQuery.IsEmpty && _xpModQuery.IsEmpty && _killTagQuery.IsEmpty &&
                _designationQuery.IsEmpty && _claimQuery.IsEmpty)
                return;

            var netTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = netTime.ServerTick.IsValid ? netTime.ServerTick.TickIndexForValidTick : 1;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 1. Handle disconnected members (PlayerTag removed on disconnect)
            if (!_partyQuery.IsEmpty)
            {
                var playerTagLookup = GetComponentLookup<PlayerTag>(true);
                var partyLinkLookup = GetComponentLookup<PartyLink>(false);

                foreach (var (state, members, partyEntity) in
                         SystemAPI.Query<RefRW<PartyState>, DynamicBuffer<PartyMemberElement>>()
                             .WithAll<PartyTag>()
                             .WithEntityAccess())
                {
                    bool modified = false;
                    for (int i = members.Length - 1; i >= 0; i--)
                    {
                        var member = members[i].PlayerEntity;
                        if (!EntityManager.Exists(member) || !playerTagLookup.HasComponent(member))
                        {
                            // Clear PartyLink if entity still exists
                            if (EntityManager.Exists(member) && partyLinkLookup.HasComponent(member))
                                partyLinkLookup[member] = new PartyLink { PartyEntity = Entity.Null };

                            members.RemoveAt(i);
                            modified = true;
                        }
                    }

                    if (modified)
                    {
                        state.ValueRW.MemberCount = (byte)members.Length;

                        // Auto-promote if leader disconnected
                        if (members.Length > 0)
                        {
                            bool leaderPresent = false;
                            for (int m = 0; m < members.Length; m++)
                            {
                                if (members[m].PlayerEntity == state.ValueRO.LeaderEntity)
                                {
                                    leaderPresent = true;
                                    break;
                                }
                            }
                            if (!leaderPresent)
                                state.ValueRW.LeaderEntity = members[0].PlayerEntity;
                        }
                    }

                    // Disband if < 2 members
                    if (members.Length < 2)
                    {
                        for (int m = 0; m < members.Length; m++)
                        {
                            if (partyLinkLookup.HasComponent(members[m].PlayerEntity))
                                partyLinkLookup[members[m].PlayerEntity] = new PartyLink { PartyEntity = Entity.Null };
                        }
                        ecb.DestroyEntity(partyEntity);
                    }
                }
            }

            // 2. Remove stale PartyXPModifier
            if (!_xpModQuery.IsEmpty)
            {
                foreach (var (_, entity) in SystemAPI.Query<RefRO<PartyXPModifier>>().WithEntityAccess())
                {
                    ecb.RemoveComponent<PartyXPModifier>(entity);
                }
            }

            // 3. Remove stale PartyKillTag (only those without a pending KillCredited)
            if (!_killTagQuery.IsEmpty)
            {
                foreach (var (_, entity) in SystemAPI.Query<RefRO<PartyKillTag>>().WithNone<KillCredited>().WithEntityAccess())
                {
                    ecb.RemoveComponent<PartyKillTag>(entity);
                }
            }

            // 4. Expire LootDesignation
            if (!_designationQuery.IsEmpty)
            {
                foreach (var (designation, entity) in SystemAPI.Query<RefRO<LootDesignation>>().WithEntityAccess())
                {
                    if (currentTick >= designation.ValueRO.ExpirationTick)
                    {
                        ecb.RemoveComponent<LootDesignation>(entity);
                    }
                }
            }

            // 5. Orphaned PartyLootClaim
            if (!_claimQuery.IsEmpty)
            {
                foreach (var (claim, claimEntity) in SystemAPI.Query<RefRO<PartyLootClaim>>().WithEntityAccess())
                {
                    if (!EntityManager.Exists(claim.ValueRO.PartyEntity) ||
                        !EntityManager.HasComponent<PartyTag>(claim.ValueRO.PartyEntity))
                    {
                        ecb.DestroyEntity(claimEntity);
                    }
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
