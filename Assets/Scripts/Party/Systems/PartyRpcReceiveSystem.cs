using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: Receives PartyRpc from clients on server.
    /// Resolves player entity via CommandTarget, validates, creates transient entities or request components.
    /// Follows StatAllocationRpcReceiveSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PartyFormationSystem))]
    public partial class PartyRpcReceiveSystem : SystemBase
    {
        private EntityQuery _rpcQuery;

        protected override void OnCreate()
        {
            _rpcQuery = GetEntityQuery(
                ComponentType.ReadOnly<PartyRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            RequireForUpdate<PartyConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<PartyConfigSingleton>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var commandTargetLookup = GetComponentLookup<CommandTarget>(true);
            var partyLinkLookup = GetComponentLookup<PartyLink>(true);
            var partyStateLookup = GetComponentLookup<PartyState>(true);
            var playerTagLookup = GetComponentLookup<PlayerTag>(true);
            var netTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = netTime.ServerTick.IsValid ? netTime.ServerTick.TickIndexForValidTick : 1;

            var entities = _rpcQuery.ToEntityArray(Allocator.Temp);
            var rpcs = _rpcQuery.ToComponentDataArray<PartyRpc>(Allocator.Temp);
            var receives = _rpcQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var rpc = rpcs[i];
                var connection = receives[i].SourceConnection;

                // Resolve player entity from connection
                if (connection == Entity.Null || !commandTargetLookup.HasComponent(connection))
                {
                    ecb.DestroyEntity(entities[i]);
                    continue;
                }

                var playerEntity = commandTargetLookup[connection].targetEntity;
                if (playerEntity == Entity.Null || !playerTagLookup.HasComponent(playerEntity))
                {
                    ecb.DestroyEntity(entities[i]);
                    continue;
                }

                switch (rpc.Type)
                {
                    case PartyRpcType.Invite:
                        HandleInvite(ecb, config, partyLinkLookup, partyStateLookup, playerTagLookup,
                            playerEntity, connection, rpc.TargetPlayer, currentTick);
                        break;

                    case PartyRpcType.AcceptInvite:
                        HandleAcceptInvite(ecb, playerEntity);
                        break;

                    case PartyRpcType.DeclineInvite:
                        HandleDeclineInvite(ecb, playerEntity);
                        break;

                    case PartyRpcType.Leave:
                        HandleLeave(ecb, partyLinkLookup, playerEntity);
                        break;

                    case PartyRpcType.Kick:
                        HandleKick(ecb, partyLinkLookup, partyStateLookup, playerEntity, rpc.TargetPlayer);
                        break;

                    case PartyRpcType.Promote:
                        HandlePromote(ecb, partyLinkLookup, partyStateLookup, playerEntity, rpc.TargetPlayer);
                        break;

                    case PartyRpcType.SetLootMode:
                        HandleSetLootMode(ecb, config, partyLinkLookup, partyStateLookup, playerEntity, rpc.Payload);
                        break;

                    case PartyRpcType.LootVote:
                        HandleLootVote(ecb, partyLinkLookup, playerEntity, rpc.Payload);
                        break;
                }

                ecb.DestroyEntity(entities[i]);
            }

            entities.Dispose();
            rpcs.Dispose();
            receives.Dispose();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void HandleInvite(EntityCommandBuffer ecb, PartyConfigSingleton config,
            ComponentLookup<PartyLink> partyLinkLookup, ComponentLookup<PartyState> partyStateLookup,
            ComponentLookup<PlayerTag> playerTagLookup,
            Entity inviter, Entity inviterConnection, Entity invitee, uint currentTick)
        {
            // Validate target exists and is a player
            if (invitee == Entity.Null || !playerTagLookup.HasComponent(invitee))
                return;

            // Can't invite self
            if (inviter == invitee) return;

            // Check invitee isn't already in a party
            if (partyLinkLookup.HasComponent(invitee))
            {
                var inviteeLink = partyLinkLookup[invitee];
                if (inviteeLink.PartyEntity != Entity.Null)
                    return;
            }

            // Check inviter's party isn't full
            Entity inviterParty = Entity.Null;
            if (partyLinkLookup.HasComponent(inviter))
            {
                inviterParty = partyLinkLookup[inviter].PartyEntity;
                if (inviterParty != Entity.Null && partyStateLookup.HasComponent(inviterParty))
                {
                    var state = partyStateLookup[inviterParty];
                    if (state.MemberCount >= state.MaxSize)
                        return;
                }
            }

            // Check for duplicate pending invite
            bool hasDuplicate = false;
            foreach (var (invite, _) in SystemAPI.Query<RefRO<PartyInvite>>().WithEntityAccess())
            {
                if (invite.ValueRO.InviterEntity == inviter && invite.ValueRO.InviteeEntity == invitee)
                {
                    hasDuplicate = true;
                    break;
                }
            }
            if (hasDuplicate) return;

            // Create invite transient entity
            var inviteEntity = ecb.CreateEntity();
            ecb.AddComponent(inviteEntity, new PartyInvite
            {
                InviterEntity = inviter,
                InviteeEntity = invitee,
                ExpirationTick = currentTick + (uint)config.InviteTimeoutTicks,
                InviterParty = inviterParty
            });

            // Send notification to invitee
            var notifyEntity = ecb.CreateEntity();
            ecb.AddComponent(notifyEntity, new PartyNotifyRpc
            {
                Type = PartyNotifyType.InviteReceived,
                SourcePlayer = inviter
            });
            ecb.AddComponent(notifyEntity, new SendRpcCommandRequest { TargetConnection = inviterConnection });
        }

        private void HandleAcceptInvite(EntityCommandBuffer ecb, Entity acceptor)
        {
            // Find matching invite
            foreach (var (invite, inviteEntity) in SystemAPI.Query<RefRO<PartyInvite>>().WithEntityAccess())
            {
                if (invite.ValueRO.InviteeEntity == acceptor)
                {
                    // Tag the invite for PartyFormationSystem to process
                    ecb.AddComponent(inviteEntity, new PartyJoinRequest { AcceptedBy = acceptor });
                    return;
                }
            }
        }

        private void HandleDeclineInvite(EntityCommandBuffer ecb, Entity decliner)
        {
            foreach (var (invite, inviteEntity) in SystemAPI.Query<RefRO<PartyInvite>>().WithEntityAccess())
            {
                if (invite.ValueRO.InviteeEntity == decliner)
                {
                    ecb.DestroyEntity(inviteEntity);

                    // Notify inviter
                    var notifyEntity = ecb.CreateEntity();
                    ecb.AddComponent(notifyEntity, new PartyNotifyRpc
                    {
                        Type = PartyNotifyType.InviteDeclined,
                        SourcePlayer = decliner
                    });
                    ecb.AddComponent(notifyEntity, new SendRpcCommandRequest());
                    return;
                }
            }
        }

        private void HandleLeave(EntityCommandBuffer ecb, ComponentLookup<PartyLink> partyLinkLookup,
            Entity player)
        {
            if (!partyLinkLookup.HasComponent(player)) return;
            var link = partyLinkLookup[player];
            if (link.PartyEntity == Entity.Null) return;

            ecb.AddComponent(player, new PartyLeaveRequest());
        }

        private void HandleKick(EntityCommandBuffer ecb, ComponentLookup<PartyLink> partyLinkLookup,
            ComponentLookup<PartyState> partyStateLookup, Entity source, Entity target)
        {
            if (!partyLinkLookup.HasComponent(source)) return;
            var sourceLink = partyLinkLookup[source];
            if (sourceLink.PartyEntity == Entity.Null) return;

            // Must be leader
            if (!partyStateLookup.HasComponent(sourceLink.PartyEntity)) return;
            if (partyStateLookup[sourceLink.PartyEntity].LeaderEntity != source) return;

            // Target must be in same party
            if (!partyLinkLookup.HasComponent(target)) return;
            if (partyLinkLookup[target].PartyEntity != sourceLink.PartyEntity) return;

            ecb.AddComponent(target, new PartyKickRequest());
        }

        private void HandlePromote(EntityCommandBuffer ecb, ComponentLookup<PartyLink> partyLinkLookup,
            ComponentLookup<PartyState> partyStateLookup, Entity source, Entity target)
        {
            if (!partyLinkLookup.HasComponent(source)) return;
            var sourceLink = partyLinkLookup[source];
            if (sourceLink.PartyEntity == Entity.Null) return;

            // Must be leader
            if (!partyStateLookup.HasComponent(sourceLink.PartyEntity)) return;
            if (partyStateLookup[sourceLink.PartyEntity].LeaderEntity != source) return;

            // Target must be in same party
            if (!partyLinkLookup.HasComponent(target)) return;
            if (partyLinkLookup[target].PartyEntity != sourceLink.PartyEntity) return;

            ecb.AddComponent(target, new PartyPromoteRequest());
        }

        private void HandleSetLootMode(EntityCommandBuffer ecb, PartyConfigSingleton config,
            ComponentLookup<PartyLink> partyLinkLookup, ComponentLookup<PartyState> partyStateLookup,
            Entity source, byte payload)
        {
            if (!partyLinkLookup.HasComponent(source)) return;
            var sourceLink = partyLinkLookup[source];
            if (sourceLink.PartyEntity == Entity.Null) return;

            if (!partyStateLookup.HasComponent(sourceLink.PartyEntity)) return;
            var state = partyStateLookup[sourceLink.PartyEntity];

            // Must be leader (or AllowLootModeVote)
            if (state.LeaderEntity != source && !config.AllowLootModeVote) return;

            if (payload > (byte)LootMode.MasterLoot) return;

            ecb.AddComponent(sourceLink.PartyEntity, new PartyLootModeRequest
            {
                NewMode = (LootMode)payload
            });
        }

        private void HandleLootVote(EntityCommandBuffer ecb, ComponentLookup<PartyLink> partyLinkLookup,
            Entity voter, byte payload)
        {
            if (payload > (byte)LootVoteType.Pass) return;
            if (!partyLinkLookup.HasComponent(voter)) return;
            var link = partyLinkLookup[voter];
            if (link.PartyEntity == Entity.Null) return;

            // Find active loot claim for this party
            foreach (var (claim, claimEntity) in
                     SystemAPI.Query<RefRO<PartyLootClaim>>()
                         .WithAll<LootVoteElement>()
                         .WithEntityAccess())
            {
                if (claim.ValueRO.PartyEntity != link.PartyEntity) continue;

                var voteBuffer = EntityManager.GetBuffer<LootVoteElement>(claimEntity);

                // Update this player's vote
                for (int v = 0; v < voteBuffer.Length; v++)
                {
                    if (voteBuffer[v].PlayerEntity == voter && voteBuffer[v].Vote == LootVoteType.Pending)
                    {
                        voteBuffer[v] = new LootVoteElement
                        {
                            PlayerEntity = voter,
                            Vote = (LootVoteType)payload
                        };
                        return;
                    }
                }
            }
        }
    }

    // Internal request components used by PartyRpcReceiveSystem -> PartyFormationSystem
    internal struct PartyJoinRequest : IComponentData { public Entity AcceptedBy; }
    internal struct PartyLeaveRequest : IComponentData { }
    internal struct PartyKickRequest : IComponentData { }
    internal struct PartyPromoteRequest : IComponentData { }
    internal struct PartyLootModeRequest : IComponentData { public LootMode NewMode; }
}
