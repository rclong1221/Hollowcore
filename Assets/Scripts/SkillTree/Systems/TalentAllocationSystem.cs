using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: Processes TalentAllocationRequest entries with RequestType.Allocate.
    /// Validates prerequisites, point availability, and node limits.
    /// Appends TalentAllocation on success, updates TalentTreeProgress.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TalentRpcReceiveSystem))]
    public partial class TalentAllocationSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<SkillTreeRegistrySingleton>();
        }

        protected override void OnUpdate()
        {
            var registry = SystemAPI.GetSingleton<SkillTreeRegistrySingleton>();
            ref var blob = ref registry.Registry.Value;
            uint tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.TickIndexForValidTick;

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
                    if (req.RequestType != TalentRequestType.Allocate) continue;
                    requests.RemoveAt(r);

                    if (!TryFindNode(ref blob, req.TreeId, req.NodeId,
                        out int treeIdx, out int nodeIdx)) continue;

                    ref var node = ref blob.Trees[treeIdx].Nodes[nodeIdx];

                    // Check available points
                    int available = state.ValueRO.TotalTalentPoints - state.ValueRO.SpentTalentPoints;
                    if (available < node.PointCost) continue;

                    // Check max ranks
                    int currentRanks = CountAllocations(allocations, req.TreeId, req.NodeId);
                    if (currentRanks >= node.MaxRanks) continue;

                    // Check tier points required
                    int treeSpent = GetTreeSpent(treeProgress, req.TreeId);
                    if (treeSpent < node.TierPointsRequired) continue;

                    // Check prerequisites
                    if (!PrerequisitesMet(allocations, ref node)) continue;

                    // Check keystone uniqueness (only one keystone per tree)
                    if (node.NodeType == SkillNodeType.Keystone)
                    {
                        if (HasKeystoneInTree(allocations, ref blob.Trees[treeIdx], req.TreeId))
                            continue;
                    }

                    // Allocate
                    allocations.Add(new TalentAllocation
                    {
                        TreeId = req.TreeId,
                        NodeId = req.NodeId,
                        AllocatedTick = (int)tick
                    });

                    state.ValueRW.SpentTalentPoints += node.PointCost;

                    // Update tree progress
                    UpdateTreeProgress(treeProgress, req.TreeId, node.PointCost, node.Tier);
                }
            }
        }

        private static bool TryFindNode(ref SkillTreeRegistryBlob blob, ushort treeId, ushort nodeId,
            out int treeIdx, out int nodeIdx)
        {
            treeIdx = -1;
            nodeIdx = -1;
            for (int t = 0; t < blob.Trees.Length; t++)
            {
                if (blob.Trees[t].TreeId != treeId) continue;
                treeIdx = t;
                for (int n = 0; n < blob.Trees[t].Nodes.Length; n++)
                {
                    if (blob.Trees[t].Nodes[n].NodeId == nodeId)
                    {
                        nodeIdx = n;
                        return true;
                    }
                }
                return false;
            }
            return false;
        }

        private static int CountAllocations(DynamicBuffer<TalentAllocation> buffer,
            ushort treeId, ushort nodeId)
        {
            int count = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].TreeId == treeId && buffer[i].NodeId == nodeId)
                    count++;
            }
            return count;
        }

        private static int GetTreeSpent(DynamicBuffer<TalentTreeProgress> progress, ushort treeId)
        {
            for (int i = 0; i < progress.Length; i++)
            {
                if (progress[i].TreeId == treeId)
                    return progress[i].PointsSpent;
            }
            return 0;
        }

        private static bool PrerequisitesMet(DynamicBuffer<TalentAllocation> buffer, ref SkillNodeBlob node)
        {
            if (node.PrereqNodeId0 >= 0 && !IsAllocated(buffer, node.PrereqNodeId0)) return false;
            if (node.PrereqNodeId1 >= 0 && !IsAllocated(buffer, node.PrereqNodeId1)) return false;
            if (node.PrereqNodeId2 >= 0 && !IsAllocated(buffer, node.PrereqNodeId2)) return false;
            return true;
        }

        private static bool IsAllocated(DynamicBuffer<TalentAllocation> buffer, int nodeId)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].NodeId == nodeId) return true;
            }
            return false;
        }

        private static bool HasKeystoneInTree(DynamicBuffer<TalentAllocation> allocations,
            ref SkillTreeBlob tree, ushort treeId)
        {
            for (int a = 0; a < allocations.Length; a++)
            {
                if (allocations[a].TreeId != treeId) continue;
                for (int n = 0; n < tree.Nodes.Length; n++)
                {
                    if (tree.Nodes[n].NodeId == allocations[a].NodeId &&
                        tree.Nodes[n].NodeType == SkillNodeType.Keystone)
                        return true;
                }
            }
            return false;
        }

        private static void UpdateTreeProgress(DynamicBuffer<TalentTreeProgress> progress,
            ushort treeId, int pointCost, int tier)
        {
            for (int i = 0; i < progress.Length; i++)
            {
                if (progress[i].TreeId == treeId)
                {
                    var entry = progress[i];
                    entry.PointsSpent += (ushort)pointCost;
                    if (tier > entry.HighestTier)
                        entry.HighestTier = (ushort)tier;
                    progress[i] = entry;
                    return;
                }
            }

            progress.Add(new TalentTreeProgress
            {
                TreeId = treeId,
                PointsSpent = (ushort)pointCost,
                HighestTier = (ushort)tier,
                Padding = 0
            });
        }
    }
}
