using Unity.Entities;
using Unity.Mathematics;

namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: Handles respec requests — clears talent allocations, refunds points.
    /// Validates gold cost: RespecBaseCost * pow(RespecCostMultiplier, RespecCount), capped.
    /// Deducts gold via CurrencyTransaction buffer.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TalentAllocationSystem))]
    public partial class TalentRespecSystem : SystemBase
    {
        private ComponentLookup<TalentOwner> _ownerLookup;
        private ComponentLookup<DIG.Economy.CurrencyInventory> _currencyLookup;

        protected override void OnCreate()
        {
            RequireForUpdate<SkillTreeRegistrySingleton>();
            _ownerLookup = GetComponentLookup<TalentOwner>(true);
            _currencyLookup = GetComponentLookup<DIG.Economy.CurrencyInventory>(true);
        }

        protected override void OnUpdate()
        {
            _ownerLookup.Update(this);
            _currencyLookup.Update(this);

            var registry = SystemAPI.GetSingleton<SkillTreeRegistrySingleton>();
            ref var blob = ref registry.Registry.Value;

            var configExists = SystemAPI.TryGetSingleton(out SkillTreeConfigData config);
            if (configExists && !config.AllowRespec) return;

            foreach (var (state, allocations, requests, treeProgress, entity) in
                SystemAPI.Query<RefRW<TalentState>,
                    DynamicBuffer<TalentAllocation>,
                    DynamicBuffer<TalentAllocationRequest>,
                    DynamicBuffer<TalentTreeProgress>>()
                    .WithAll<TalentChildTag>()
                    .WithEntityAccess())
            {
                for (int r = requests.Length - 1; r >= 0; r--)
                {
                    var req = requests[r];
                    if (req.RequestType != TalentRequestType.Respec) continue;
                    requests.RemoveAt(r);

                    if (!_ownerLookup.HasComponent(entity)) continue;
                    var playerEntity = _ownerLookup[entity].Owner;
                    if (playerEntity == Entity.Null) continue;

                    // Calculate respec cost
                    int baseCost = blob.RespecBaseCost;
                    float multiplier = blob.RespecCostMultiplier;
                    int cap = blob.RespecCostCap;
                    int cost = math.min(cap, (int)(baseCost * math.pow(multiplier, state.ValueRO.RespecCount)));

                    // Validate gold
                    if (_currencyLookup.HasComponent(playerEntity))
                    {
                        var currency = _currencyLookup[playerEntity];
                        if (currency.Gold < cost) continue;
                    }

                    // Deduct gold via CurrencyTransaction
                    if (SystemAPI.HasBuffer<DIG.Economy.CurrencyTransaction>(playerEntity))
                    {
                        var transactions = SystemAPI.GetBuffer<DIG.Economy.CurrencyTransaction>(playerEntity);
                        transactions.Add(new DIG.Economy.CurrencyTransaction
                        {
                            Type = DIG.Economy.CurrencyType.Gold,
                            Amount = -cost,
                            Source = entity
                        });
                    }

                    // Respec: clear allocations and refund points
                    if (req.TreeId == 0)
                    {
                        // Full respec — all trees
                        allocations.Clear();
                        treeProgress.Clear();
                        state.ValueRW.SpentTalentPoints = 0;
                    }
                    else
                    {
                        // Per-tree respec
                        int refunded = 0;
                        for (int a = allocations.Length - 1; a >= 0; a--)
                        {
                            if (allocations[a].TreeId == req.TreeId)
                            {
                                // Look up point cost from blob
                                refunded += GetNodeCost(ref blob, allocations[a].TreeId, allocations[a].NodeId);
                                allocations.RemoveAt(a);
                            }
                        }
                        state.ValueRW.SpentTalentPoints -= refunded;

                        // Remove tree progress entry
                        for (int p = treeProgress.Length - 1; p >= 0; p--)
                        {
                            if (treeProgress[p].TreeId == req.TreeId)
                                treeProgress.RemoveAt(p);
                        }
                    }

                    state.ValueRW.RespecCount++;
                }
            }
        }

        private static int GetNodeCost(ref SkillTreeRegistryBlob blob, ushort treeId, ushort nodeId)
        {
            for (int t = 0; t < blob.Trees.Length; t++)
            {
                if (blob.Trees[t].TreeId != treeId) continue;
                for (int n = 0; n < blob.Trees[t].Nodes.Length; n++)
                {
                    if (blob.Trees[t].Nodes[n].NodeId == nodeId)
                        return blob.Trees[t].Nodes[n].PointCost;
                }
            }
            return 1;
        }
    }
}
