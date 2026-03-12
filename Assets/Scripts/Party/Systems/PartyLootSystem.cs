using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Economy;
using Player.Components;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: Intercepts enemy death loot distribution based on party loot mode.
    /// Runs before DeathLootSystem. Adds LootDesignation or creates PartyLootClaim.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DIG.Loot.Systems.DeathLootSystem))]
    public partial class PartyLootSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PartyConfigSingleton>();
            RequireForUpdate<NetworkTime>();
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<PartyConfigSingleton>();
            var netTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = netTime.ServerTick.IsValid ? netTime.ServerTick.TickIndexForValidTick : 1;

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var partyLinkLookup = GetComponentLookup<PartyLink>(true);
            var partyStateLookup = GetComponentLookup<PartyState>(false);
            var memberBufferLookup = GetBufferLookup<PartyMemberElement>(true);
            var proximityBufferLookup = GetBufferLookup<PartyProximityState>(true);
            var currencyTxLookup = GetBufferLookup<CurrencyTransaction>(false);

            foreach (var (combatState, entity) in
                     SystemAPI.Query<RefRO<CombatState>>()
                         .WithAll<DiedEvent>()
                         .WithNone<PlayerTag>()
                         .WithEntityAccess())
            {
                var killer = combatState.ValueRO.LastAttacker;
                if (killer == Entity.Null || !partyLinkLookup.HasComponent(killer))
                    continue;

                var partyEntity = partyLinkLookup[killer].PartyEntity;
                if (partyEntity == Entity.Null || !partyStateLookup.HasComponent(partyEntity))
                    continue;

                var state = partyStateLookup[partyEntity];

                switch (state.LootMode)
                {
                    case LootMode.FreeForAll:
                        // No modification needed
                        break;

                    case LootMode.RoundRobin:
                        HandleRoundRobin(ecb, config, partyEntity, ref state, entity,
                            memberBufferLookup, proximityBufferLookup, currentTick);
                        partyStateLookup[partyEntity] = state;
                        break;

                    case LootMode.NeedGreed:
                        HandleNeedGreed(ecb, config, partyEntity, entity,
                            memberBufferLookup, proximityBufferLookup, currentTick);
                        break;

                    case LootMode.MasterLoot:
                        HandleMasterLoot(ecb, config, partyEntity, state, entity, currentTick);
                        break;
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void HandleRoundRobin(EntityCommandBuffer ecb, PartyConfigSingleton config,
            Entity partyEntity, ref PartyState state, Entity deadEntity,
            BufferLookup<PartyMemberElement> memberBufferLookup,
            BufferLookup<PartyProximityState> proximityBufferLookup, uint currentTick)
        {
            if (!memberBufferLookup.HasBuffer(partyEntity)) return;
            var members = memberBufferLookup[partyEntity];
            if (members.Length == 0) return;
            if (!proximityBufferLookup.HasBuffer(partyEntity)) return;
            var proximity = proximityBufferLookup[partyEntity];

            // Find next in-range member
            Entity designated = Entity.Null;
            int startIdx = state.RoundRobinIndex;
            for (int attempt = 0; attempt < members.Length; attempt++)
            {
                int idx = (startIdx + attempt) % members.Length;
                var member = members[idx].PlayerEntity;

                for (int p = 0; p < proximity.Length; p++)
                {
                    if (proximity[p].PlayerEntity == member && proximity[p].InLootRange)
                    {
                        designated = member;
                        state.RoundRobinIndex = (idx + 1) % members.Length;
                        break;
                    }
                }

                if (designated != Entity.Null) break;
            }

            if (designated != Entity.Null)
            {
                ecb.AddComponent(deadEntity, new LootDesignation
                {
                    DesignatedOwner = designated,
                    ExpirationTick = currentTick + (uint)config.LootDesignationTimeoutTicks
                });
            }
        }

        private void HandleNeedGreed(EntityCommandBuffer ecb, PartyConfigSingleton config,
            Entity partyEntity, Entity deadEntity,
            BufferLookup<PartyMemberElement> memberBufferLookup,
            BufferLookup<PartyProximityState> proximityBufferLookup, uint currentTick)
        {
            if (!memberBufferLookup.HasBuffer(partyEntity)) return;
            var members = memberBufferLookup[partyEntity];

            // Create loot claim transient entity
            var claimEntity = ecb.CreateEntity();
            ecb.AddComponent(claimEntity, new PartyLootClaim
            {
                LootEntity = deadEntity,
                PartyEntity = partyEntity,
                ExpirationTick = currentTick + (uint)config.NeedGreedVoteTimeoutTicks
            });

            var voteBuffer = ecb.AddBuffer<LootVoteElement>(claimEntity);
            if (proximityBufferLookup.HasBuffer(partyEntity))
            {
                var proximity = proximityBufferLookup[partyEntity];
                for (int p = 0; p < proximity.Length; p++)
                {
                    if (proximity[p].InLootRange)
                    {
                        voteBuffer.Add(new LootVoteElement
                        {
                            PlayerEntity = proximity[p].PlayerEntity,
                            Vote = LootVoteType.Pending
                        });
                    }
                }
            }

            PartyVisualQueue.Enqueue(new PartyVisualQueue.PartyVisualEvent
            {
                Type = PartyNotifyType.LootRollStart
            });
        }

        private void HandleMasterLoot(EntityCommandBuffer ecb, PartyConfigSingleton config,
            Entity partyEntity, PartyState state, Entity deadEntity, uint currentTick)
        {
            ecb.AddComponent(deadEntity, new LootDesignation
            {
                DesignatedOwner = state.LeaderEntity,
                ExpirationTick = currentTick + (uint)config.LootDesignationTimeoutTicks
            });
        }
    }
}
