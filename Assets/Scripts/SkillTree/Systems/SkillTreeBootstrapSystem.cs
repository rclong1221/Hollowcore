using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: Loads SkillTreeDatabaseSO and SkillTreeConfigSO from Resources/,
    /// builds BlobAssets, creates SkillTreeRegistrySingleton. Runs once, then self-disables.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class SkillTreeBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;

            var databaseSO = Resources.Load<SkillTreeDatabaseSO>("SkillTreeDatabase");
            var configSO = Resources.Load<SkillTreeConfigSO>("SkillTreeConfig");

            if (databaseSO == null)
            {
                Debug.LogWarning("[SkillTreeBootstrap] No SkillTreeDatabase found at Resources/SkillTreeDatabase. Using defaults.");
                databaseSO = ScriptableObject.CreateInstance<SkillTreeDatabaseSO>();
            }

            if (configSO == null)
            {
                Debug.LogWarning("[SkillTreeBootstrap] No SkillTreeConfig found at Resources/SkillTreeConfig. Using defaults.");
                configSO = ScriptableObject.CreateInstance<SkillTreeConfigSO>();
            }

            var singleton = new SkillTreeRegistrySingleton
            {
                Registry = BuildRegistryBlob(databaseSO)
            };

            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, singleton);

            // Store config as a separate singleton
            EntityManager.AddComponentData(entity, new SkillTreeConfigData
            {
                MaxTreesPerPlayer = configSO.MaxTreesPerPlayer,
                MaxTotalTalentPoints = configSO.MaxTotalTalentPoints,
                AllowRespec = configSO.AllowRespec
            });

#if UNITY_EDITOR
            EntityManager.SetName(entity, "SkillTreeRegistry");
#endif

            int treeCount = databaseSO.Trees != null ? databaseSO.Trees.Count : 0;
            Debug.Log($"[SkillTreeBootstrap] Loaded {treeCount} skill trees, TalentPointsPerLevel={databaseSO.TalentPointsPerLevel}");

            _initialized = true;
            Enabled = false;
        }

        protected override void OnDestroy()
        {
            if (!_initialized) return;

            foreach (var registry in SystemAPI.Query<RefRO<SkillTreeRegistrySingleton>>())
            {
                if (registry.ValueRO.Registry.IsCreated)
                    registry.ValueRO.Registry.Dispose();
            }
        }

        private static BlobAssetReference<SkillTreeRegistryBlob> BuildRegistryBlob(SkillTreeDatabaseSO so)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<SkillTreeRegistryBlob>();

            root.TalentPointsPerLevel = so.TalentPointsPerLevel;
            root.RespecBaseCost = so.RespecBaseCost;
            root.RespecCostMultiplier = so.RespecCostMultiplier;
            root.RespecCostCap = so.RespecCostCap;

            int treeCount = so.Trees != null ? so.Trees.Count : 0;
            var treesArray = builder.Allocate(ref root.Trees, treeCount);

            for (int t = 0; t < treeCount; t++)
            {
                var treeSO = so.Trees[t];
                if (treeSO == null) continue;

                treesArray[t].TreeId = treeSO.TreeId;
                builder.AllocateString(ref treesArray[t].Name, treeSO.TreeName ?? "");
                treesArray[t].MaxPoints = treeSO.MaxPoints;
                treesArray[t].ClassRestriction = treeSO.ClassRestriction;

                int nodeCount = treeSO.Nodes != null ? treeSO.Nodes.Length : 0;
                var nodesArray = builder.Allocate(ref treesArray[t].Nodes, nodeCount);

                for (int n = 0; n < nodeCount; n++)
                {
                    ref var nodeDef = ref treeSO.Nodes[n];
                    nodesArray[n] = new SkillNodeBlob
                    {
                        NodeId = nodeDef.NodeId,
                        Tier = nodeDef.Tier,
                        PointCost = nodeDef.PointCost,
                        TierPointsRequired = nodeDef.TierPointsRequired,
                        NodeType = nodeDef.NodeType,
                        MaxRanks = nodeDef.MaxRanks > 0 ? nodeDef.MaxRanks : 1,
                        PassiveBonus = new SkillPassiveBonus
                        {
                            BonusType = nodeDef.BonusType,
                            Value = nodeDef.BonusValue
                        },
                        AbilityTypeId = nodeDef.AbilityTypeId,
                        PrereqNodeId0 = nodeDef.Prerequisites != null && nodeDef.Prerequisites.Length > 0 ? nodeDef.Prerequisites[0] : -1,
                        PrereqNodeId1 = nodeDef.Prerequisites != null && nodeDef.Prerequisites.Length > 1 ? nodeDef.Prerequisites[1] : -1,
                        PrereqNodeId2 = nodeDef.Prerequisites != null && nodeDef.Prerequisites.Length > 2 ? nodeDef.Prerequisites[2] : -1,
                        EditorX = nodeDef.EditorPosition.x,
                        EditorY = nodeDef.EditorPosition.y
                    };
                }
            }

            var result = builder.CreateBlobAssetReference<SkillTreeRegistryBlob>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }
    }

    /// <summary>Runtime config singleton created by bootstrap.</summary>
    public struct SkillTreeConfigData : IComponentData
    {
        public int MaxTreesPerPlayer;
        public int MaxTotalTalentPoints;
        public bool AllowRespec;
    }
}
