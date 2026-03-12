using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: Creates/destroys party entities, manages membership.
    /// Processes request components written by PartyRpcReceiveSystem.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PartyProximitySystem))]
    public partial class PartyFormationSystem : SystemBase
    {
        private EntityQuery _joinQuery;
        private EntityQuery _leaveQuery;
        private EntityQuery _kickQuery;
        private EntityQuery _promoteQuery;
        private EntityQuery _lootModeQuery;

        protected override void OnCreate()
        {
            _joinQuery = GetEntityQuery(ComponentType.ReadOnly<PartyInvite>(), ComponentType.ReadOnly<PartyJoinRequest>());
            _leaveQuery = GetEntityQuery(ComponentType.ReadOnly<PartyLeaveRequest>(), ComponentType.ReadOnly<PartyLink>());
            _kickQuery = GetEntityQuery(ComponentType.ReadOnly<PartyKickRequest>(), ComponentType.ReadOnly<PartyLink>());
            _promoteQuery = GetEntityQuery(ComponentType.ReadOnly<PartyPromoteRequest>(), ComponentType.ReadOnly<PartyLink>());
            _lootModeQuery = GetEntityQuery(ComponentType.ReadOnly<PartyLootModeRequest>(), ComponentType.ReadOnly<PartyState>());
            RequireForUpdate<PartyConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<PartyConfigSingleton>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var netTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = netTime.ServerTick.IsValid ? netTime.ServerTick.TickIndexForValidTick : 1;

            ProcessJoins(ecb, config, currentTick);
            ProcessLeaves(ecb);
            ProcessKicks(ecb);
            ProcessPromotes(ecb);
            ProcessLootModeChanges(ecb);

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void ProcessJoins(EntityCommandBuffer ecb, PartyConfigSingleton config, uint currentTick)
        {
            foreach (var (invite, joinReq, inviteEntity) in
                     SystemAPI.Query<RefRO<PartyInvite>, RefRO<PartyJoinRequest>>().WithEntityAccess())
            {
                var inviter = invite.ValueRO.InviterEntity;
                var invitee = invite.ValueRO.InviteeEntity;
                var inviterParty = invite.ValueRO.InviterParty;

                if (inviterParty != Entity.Null && EntityManager.Exists(inviterParty) &&
                    EntityManager.HasComponent<PartyState>(inviterParty))
                {
                    // Join existing party
                    var state = EntityManager.GetComponentData<PartyState>(inviterParty);
                    if (state.MemberCount >= state.MaxSize)
                    {
                        ecb.DestroyEntity(inviteEntity);
                        continue;
                    }

                    var members = EntityManager.GetBuffer<PartyMemberElement>(inviterParty);
                    members.Add(new PartyMemberElement
                    {
                        PlayerEntity = invitee,
                        ConnectionEntity = Entity.Null,
                        JoinTick = currentTick
                    });

                    var proximity = EntityManager.GetBuffer<PartyProximityState>(inviterParty);
                    proximity.Add(new PartyProximityState
                    {
                        PlayerEntity = invitee,
                        InXPRange = true,
                        InLootRange = true,
                        InKillCreditRange = true
                    });

                    state.MemberCount++;
                    EntityManager.SetComponentData(inviterParty, state);

                    // Set PartyLink on invitee
                    if (EntityManager.HasComponent<PartyLink>(invitee))
                        EntityManager.SetComponentData(invitee, new PartyLink { PartyEntity = inviterParty });

                    PartyVisualQueue.Enqueue(new PartyVisualQueue.PartyVisualEvent
                    {
                        Type = PartyNotifyType.MemberJoined,
                        SourcePlayer = invitee
                    });
                }
                else
                {
                    // Create new party
                    var partyEntity = EntityManager.CreateEntity();
                    EntityManager.AddComponent<PartyTag>(partyEntity);
                    EntityManager.AddComponentData(partyEntity, new PartyState
                    {
                        LeaderEntity = inviter,
                        LootMode = config.DefaultLootMode,
                        MaxSize = config.MaxPartySize,
                        MemberCount = 2,
                        CreationTick = currentTick,
                        RoundRobinIndex = 0,
                        PartyOwnerConnection = Entity.Null
                    });

                    var memberBuffer = EntityManager.AddBuffer<PartyMemberElement>(partyEntity);
                    memberBuffer.Add(new PartyMemberElement
                    {
                        PlayerEntity = inviter,
                        ConnectionEntity = Entity.Null,
                        JoinTick = currentTick
                    });
                    memberBuffer.Add(new PartyMemberElement
                    {
                        PlayerEntity = invitee,
                        ConnectionEntity = Entity.Null,
                        JoinTick = currentTick
                    });

                    var proximityBuffer = EntityManager.AddBuffer<PartyProximityState>(partyEntity);
                    proximityBuffer.Add(new PartyProximityState
                    {
                        PlayerEntity = inviter,
                        InXPRange = true,
                        InLootRange = true,
                        InKillCreditRange = true
                    });
                    proximityBuffer.Add(new PartyProximityState
                    {
                        PlayerEntity = invitee,
                        InXPRange = true,
                        InLootRange = true,
                        InKillCreditRange = true
                    });

#if UNITY_EDITOR
                    EntityManager.SetName(partyEntity, "Party");
#endif

                    // Set PartyLink on both players
                    if (EntityManager.HasComponent<PartyLink>(inviter))
                        EntityManager.SetComponentData(inviter, new PartyLink { PartyEntity = partyEntity });
                    if (EntityManager.HasComponent<PartyLink>(invitee))
                        EntityManager.SetComponentData(invitee, new PartyLink { PartyEntity = partyEntity });

                    PartyVisualQueue.Enqueue(new PartyVisualQueue.PartyVisualEvent
                    {
                        Type = PartyNotifyType.MemberJoined,
                        SourcePlayer = invitee
                    });

#if UNITY_EDITOR
                    Debug.Log($"[PartyFormation] Created party entity {partyEntity.Index} with leader {inviter.Index}");
#endif
                }

                ecb.DestroyEntity(inviteEntity);
            }
        }

        private void ProcessLeaves(EntityCommandBuffer ecb)
        {
            foreach (var (_, link, playerEntity) in
                     SystemAPI.Query<RefRO<PartyLeaveRequest>, RefRO<PartyLink>>().WithEntityAccess())
            {
                RemoveMember(ecb, link.ValueRO.PartyEntity, playerEntity);
                ecb.RemoveComponent<PartyLeaveRequest>(playerEntity);
            }
        }

        private void ProcessKicks(EntityCommandBuffer ecb)
        {
            foreach (var (_, link, playerEntity) in
                     SystemAPI.Query<RefRO<PartyKickRequest>, RefRO<PartyLink>>().WithEntityAccess())
            {
                RemoveMember(ecb, link.ValueRO.PartyEntity, playerEntity);
                ecb.RemoveComponent<PartyKickRequest>(playerEntity);

                PartyVisualQueue.Enqueue(new PartyVisualQueue.PartyVisualEvent
                {
                    Type = PartyNotifyType.MemberKicked,
                    SourcePlayer = playerEntity
                });
            }
        }

        private void ProcessPromotes(EntityCommandBuffer ecb)
        {
            foreach (var (_, link, playerEntity) in
                     SystemAPI.Query<RefRO<PartyPromoteRequest>, RefRO<PartyLink>>().WithEntityAccess())
            {
                var partyEntity = link.ValueRO.PartyEntity;
                if (partyEntity != Entity.Null && EntityManager.HasComponent<PartyState>(partyEntity))
                {
                    var state = EntityManager.GetComponentData<PartyState>(partyEntity);
                    state.LeaderEntity = playerEntity;
                    EntityManager.SetComponentData(partyEntity, state);

                    PartyVisualQueue.Enqueue(new PartyVisualQueue.PartyVisualEvent
                    {
                        Type = PartyNotifyType.LeaderChanged,
                        SourcePlayer = playerEntity
                    });
                }

                ecb.RemoveComponent<PartyPromoteRequest>(playerEntity);
            }
        }

        private void ProcessLootModeChanges(EntityCommandBuffer ecb)
        {
            foreach (var (req, state, partyEntity) in
                     SystemAPI.Query<RefRO<PartyLootModeRequest>, RefRW<PartyState>>().WithEntityAccess())
            {
                state.ValueRW.LootMode = req.ValueRO.NewMode;
                ecb.RemoveComponent<PartyLootModeRequest>(partyEntity);

                PartyVisualQueue.Enqueue(new PartyVisualQueue.PartyVisualEvent
                {
                    Type = PartyNotifyType.LootModeChanged,
                    Payload = (byte)req.ValueRO.NewMode
                });
            }
        }

        private void RemoveMember(EntityCommandBuffer ecb, Entity partyEntity, Entity playerEntity)
        {
            if (partyEntity == Entity.Null || !EntityManager.Exists(partyEntity) ||
                !EntityManager.HasComponent<PartyState>(partyEntity))
                return;

            // Clear player's PartyLink
            if (EntityManager.HasComponent<PartyLink>(playerEntity))
                EntityManager.SetComponentData(playerEntity, new PartyLink { PartyEntity = Entity.Null });

            // Remove from member buffer
            var members = EntityManager.GetBuffer<PartyMemberElement>(partyEntity);
            for (int i = members.Length - 1; i >= 0; i--)
            {
                if (members[i].PlayerEntity == playerEntity)
                {
                    members.RemoveAt(i);
                    break;
                }
            }

            // Remove from proximity buffer
            if (EntityManager.HasBuffer<PartyProximityState>(partyEntity))
            {
                var proximity = EntityManager.GetBuffer<PartyProximityState>(partyEntity);
                for (int i = proximity.Length - 1; i >= 0; i--)
                {
                    if (proximity[i].PlayerEntity == playerEntity)
                    {
                        proximity.RemoveAt(i);
                        break;
                    }
                }
            }

            var state = EntityManager.GetComponentData<PartyState>(partyEntity);
            state.MemberCount = (byte)members.Length;

            // If leaving player was leader, auto-promote longest-standing
            if (state.LeaderEntity == playerEntity && members.Length > 0)
            {
                state.LeaderEntity = members[0].PlayerEntity;
                PartyVisualQueue.Enqueue(new PartyVisualQueue.PartyVisualEvent
                {
                    Type = PartyNotifyType.LeaderChanged,
                    SourcePlayer = members[0].PlayerEntity
                });
            }

            EntityManager.SetComponentData(partyEntity, state);

            // If party has < 2 members, disband
            if (members.Length < 2)
            {
                // Clear remaining member's PartyLink
                for (int i = 0; i < members.Length; i++)
                {
                    if (EntityManager.HasComponent<PartyLink>(members[i].PlayerEntity))
                        EntityManager.SetComponentData(members[i].PlayerEntity, new PartyLink { PartyEntity = Entity.Null });
                }

                ecb.DestroyEntity(partyEntity);

                PartyVisualQueue.Enqueue(new PartyVisualQueue.PartyVisualEvent
                {
                    Type = PartyNotifyType.PartyDisbanded
                });

#if UNITY_EDITOR
                Debug.Log($"[PartyFormation] Party {partyEntity.Index} disbanded");
#endif
            }

            PartyVisualQueue.Enqueue(new PartyVisualQueue.PartyVisualEvent
            {
                Type = PartyNotifyType.MemberLeft,
                SourcePlayer = playerEntity
            });
        }
    }
}
