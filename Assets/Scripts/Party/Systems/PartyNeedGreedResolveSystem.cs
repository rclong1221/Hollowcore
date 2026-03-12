using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: Resolves NeedGreed votes after all members voted or timeout.
    /// Need > Greed > Pass. Ties broken deterministically by entity index + tick.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DIG.Loot.Systems.DeathLootSystem))]
    public partial class PartyNeedGreedResolveSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PartyConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            var netTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = netTime.ServerTick.IsValid ? netTime.ServerTick.TickIndexForValidTick : 1;
            var config = SystemAPI.GetSingleton<PartyConfigSingleton>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (claim, claimEntity) in
                     SystemAPI.Query<RefRO<PartyLootClaim>>()
                         .WithAll<LootVoteElement>()
                         .WithEntityAccess())
            {
                var voteBuffer = EntityManager.GetBuffer<LootVoteElement>(claimEntity);

                // Check if all voted or expired
                bool allVoted = true;
                bool expired = currentTick >= claim.ValueRO.ExpirationTick;

                for (int v = 0; v < voteBuffer.Length; v++)
                {
                    if (voteBuffer[v].Vote == LootVoteType.Pending)
                    {
                        if (expired)
                        {
                            // Auto-pass on timeout
                            voteBuffer[v] = new LootVoteElement
                            {
                                PlayerEntity = voteBuffer[v].PlayerEntity,
                                Vote = LootVoteType.Pass
                            };
                        }
                        else
                        {
                            allVoted = false;
                        }
                    }
                }

                if (!allVoted && !expired) continue;

                // Resolve winner: Need > Greed > Pass
                Entity winner = Entity.Null;
                LootVoteType winningVote = LootVoteType.Pass;
                uint winnerSeed = uint.MaxValue;

                for (int v = 0; v < voteBuffer.Length; v++)
                {
                    var vote = voteBuffer[v];
                    if (vote.Vote == LootVoteType.Pass) continue;

                    // Deterministic tie-breaker
                    uint seed = (uint)vote.PlayerEntity.Index ^ currentTick;

                    if (vote.Vote < winningVote || // Need(1) < Greed(2)
                        (vote.Vote == winningVote && seed < winnerSeed))
                    {
                        winner = vote.PlayerEntity;
                        winningVote = vote.Vote;
                        winnerSeed = seed;
                    }
                }

                // Apply loot designation
                var lootEntity = claim.ValueRO.LootEntity;
                if (winner != Entity.Null && EntityManager.Exists(lootEntity))
                {
                    if (!EntityManager.HasComponent<LootDesignation>(lootEntity))
                    {
                        ecb.AddComponent(lootEntity, new LootDesignation
                        {
                            DesignatedOwner = winner,
                            ExpirationTick = currentTick + (uint)config.LootDesignationTimeoutTicks
                        });
                    }
                    else
                    {
                        ecb.SetComponent(lootEntity, new LootDesignation
                        {
                            DesignatedOwner = winner,
                            ExpirationTick = currentTick + (uint)config.LootDesignationTimeoutTicks
                        });
                    }
                }

                PartyVisualQueue.Enqueue(new PartyVisualQueue.PartyVisualEvent
                {
                    Type = PartyNotifyType.LootRollResult,
                    SourcePlayer = winner,
                    Payload = (byte)winningVote
                });

                ecb.DestroyEntity(claimEntity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
