using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: Reads talent state from ECS and drives the talent UI via TalentUIRegistry.
    /// Follows CombatUIBridgeSystem / DialogueUIBridgeSystem pattern.
    /// Runs in PresentationSystemGroup, Client|Local only.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TalentUIBridgeSystem : SystemBase
    {
        private int _diagnosticFrames;
        private int _lastAllocCount;

        protected override void OnCreate()
        {
            _diagnosticFrames = 120;
        }

        protected override void OnUpdate()
        {
            if (_diagnosticFrames > 0)
            {
                _diagnosticFrames--;
                if (_diagnosticFrames == 0 && !TalentUIRegistry.HasTalentUI)
                    Debug.LogWarning("[TalentUIBridge] No ITalentUIProvider registered after 120 frames.");
            }

            if (!TalentUIRegistry.HasTalentUI) return;
            if (!SystemAPI.HasSingleton<SkillTreeRegistrySingleton>()) return;

            var registry = SystemAPI.GetSingleton<SkillTreeRegistrySingleton>();
            ref var blob = ref registry.Registry.Value;

            // Find local player's talent data
            foreach (var (link, entity) in
                SystemAPI.Query<RefRO<TalentLink>>().WithEntityAccess())
            {
                if (!EntityManager.HasComponent<GhostOwnerIsLocal>(entity)) continue;

                var talentChild = link.ValueRO.TalentChild;
                if (talentChild == Entity.Null) continue;
                if (!SystemAPI.HasComponent<TalentState>(talentChild)) continue;

                var state = SystemAPI.GetComponent<TalentState>(talentChild);
                var allocations = SystemAPI.GetBuffer<TalentAllocation>(talentChild);
                var treeProgress = SystemAPI.GetBuffer<TalentTreeProgress>(talentChild);

                // Build UI state for each tree
                for (int t = 0; t < blob.Trees.Length; t++)
                {
                    ref var tree = ref blob.Trees[t];
                    int treeSpent = GetTreeSpent(treeProgress, tree.TreeId);

                    var nodes = new TalentNodeUIState[tree.Nodes.Length];
                    for (int n = 0; n < tree.Nodes.Length; n++)
                    {
                        ref var node = ref tree.Nodes[n];
                        int ranks = CountAllocations(allocations, (ushort)tree.TreeId, (ushort)node.NodeId);

                        var status = TalentNodeStatus.Locked;
                        if (ranks >= node.MaxRanks)
                            status = TalentNodeStatus.Maxed;
                        else if (ranks > 0)
                            status = TalentNodeStatus.Allocated;
                        else if (PrerequisitesMet(allocations, ref node) &&
                                 treeSpent >= node.TierPointsRequired &&
                                 (state.TotalTalentPoints - state.SpentTalentPoints) >= node.PointCost)
                            status = TalentNodeStatus.Available;

                        int[] prereqs = null;
                        int prereqCount = 0;
                        if (node.PrereqNodeId0 >= 0) prereqCount++;
                        if (node.PrereqNodeId1 >= 0) prereqCount++;
                        if (node.PrereqNodeId2 >= 0) prereqCount++;
                        if (prereqCount > 0)
                        {
                            prereqs = new int[prereqCount];
                            int pi = 0;
                            if (node.PrereqNodeId0 >= 0) prereqs[pi++] = node.PrereqNodeId0;
                            if (node.PrereqNodeId1 >= 0) prereqs[pi++] = node.PrereqNodeId1;
                            if (node.PrereqNodeId2 >= 0) prereqs[pi++] = node.PrereqNodeId2;
                        }

                        string bonusText = node.PassiveBonus.BonusType != SkillBonusType.None
                            ? $"{node.PassiveBonus.BonusType} +{node.PassiveBonus.Value * ranks}"
                            : "";

                        nodes[n] = new TalentNodeUIState
                        {
                            NodeId = node.NodeId,
                            Name = $"Node {node.NodeId}",
                            Description = "",
                            IconPath = "",
                            Tier = node.Tier,
                            CurrentRank = ranks,
                            MaxRanks = node.MaxRanks,
                            PointCost = node.PointCost,
                            NodeType = node.NodeType,
                            Status = status,
                            PrerequisiteNodeIds = prereqs,
                            BonusText = bonusText,
                            EditorX = node.EditorX,
                            EditorY = node.EditorY
                        };
                    }

                    var uiState = new TalentTreeUIState
                    {
                        TreeId = tree.TreeId,
                        TreeName = tree.Name.ToString(),
                        AvailablePoints = state.TotalTalentPoints - state.SpentTalentPoints,
                        SpentInTree = treeSpent,
                        TotalSpent = state.SpentTalentPoints,
                        Nodes = nodes
                    };

                    if (allocations.Length != _lastAllocCount)
                    {
                        TalentUIRegistry.TalentUI.UpdateNodeStates(uiState);
                        _lastAllocCount = allocations.Length;
                    }
                }

                break; // Only process local player
            }
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

        private static int GetTreeSpent(DynamicBuffer<TalentTreeProgress> progress, int treeId)
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
    }
}
