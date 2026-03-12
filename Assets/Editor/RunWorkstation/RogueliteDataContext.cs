#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using DIG.Roguelite;
using DIG.Roguelite.Rewards;
using DIG.Roguelite.Zones;

namespace DIG.Roguelite.Editor
{
    /// <summary>
    /// EPIC 23.7: Cached index of all roguelite ScriptableObjects in the project.
    /// Built once on demand, invalidated by RogueliteAssetPostprocessor.
    /// All RunWorkstation modules share a single instance via RunWorkstationWindow.
    /// Eliminates redundant AssetDatabase.FindAssets calls across modules.
    /// </summary>
    public class RogueliteDataContext
    {
        // Indexed collections — populated on Build(), never during OnGUI
        public RunConfigSO[] RunConfigs = System.Array.Empty<RunConfigSO>();
        public ZoneDefinitionSO[] ZoneDefinitions = System.Array.Empty<ZoneDefinitionSO>();
        public ZoneSequenceSO[] ZoneSequences = System.Array.Empty<ZoneSequenceSO>();
        public EncounterPoolSO[] EncounterPools = System.Array.Empty<EncounterPoolSO>();
        public SpawnDirectorConfigSO[] SpawnDirectorConfigs = System.Array.Empty<SpawnDirectorConfigSO>();
        public InteractablePoolSO[] InteractablePools = System.Array.Empty<InteractablePoolSO>();
        public RewardDefinitionSO[] RewardDefinitions = System.Array.Empty<RewardDefinitionSO>();
        public RewardPoolSO[] RewardPools = System.Array.Empty<RewardPoolSO>();
        public RunEventDefinitionSO[] RunEvents = System.Array.Empty<RunEventDefinitionSO>();
        public RunModifierPoolSO ModifierPool;
        public AscensionDefinitionSO AscensionDefinition;
        public MetaUnlockTreeSO MetaUnlockTree;

        // Quick lookups
        public Dictionary<int, ZoneDefinitionSO> ZoneById = new();
        public Dictionary<int, RewardDefinitionSO> RewardById = new();

        // Dependency graph (built lazily)
        public SODependencyGraph DependencyGraph;

        public bool IsBuilt { get; private set; }
        public double BuildTimestamp;

        /// <summary>Scans AssetDatabase and populates all arrays. ~50-200ms on large projects.</summary>
        public void Build()
        {
            RunConfigs = FindAll<RunConfigSO>();
            ZoneDefinitions = FindAll<ZoneDefinitionSO>();
            ZoneSequences = FindAll<ZoneSequenceSO>();
            EncounterPools = FindAll<EncounterPoolSO>();
            SpawnDirectorConfigs = FindAll<SpawnDirectorConfigSO>();
            InteractablePools = FindAll<InteractablePoolSO>();
            RewardDefinitions = FindAll<RewardDefinitionSO>();
            RewardPools = FindAll<RewardPoolSO>();
            RunEvents = FindAll<RunEventDefinitionSO>();

            // Singletons from Resources
            ModifierPool = Resources.Load<RunModifierPoolSO>("RunModifierPool");
            AscensionDefinition = Resources.Load<AscensionDefinitionSO>("AscensionDefinition");
            MetaUnlockTree = Resources.Load<MetaUnlockTreeSO>("MetaUnlockTree");

            // Quick lookups
            ZoneById.Clear();
            foreach (var z in ZoneDefinitions)
                if (z != null) ZoneById[z.ZoneId] = z;

            RewardById.Clear();
            foreach (var r in RewardDefinitions)
                if (r != null) RewardById[r.RewardId] = r;

            // Invalidate dependency graph (rebuild lazily)
            DependencyGraph = null;

            IsBuilt = true;
            BuildTimestamp = EditorApplication.timeSinceStartup;
        }

        /// <summary>Invalidate cache — next access triggers rebuild.</summary>
        public void Invalidate()
        {
            IsBuilt = false;
        }

        /// <summary>Ensure context is built. Call before querying.</summary>
        public void EnsureBuilt()
        {
            if (!IsBuilt) Build();
        }

        /// <summary>Lazily build and return the dependency graph.</summary>
        public SODependencyGraph GetDependencyGraph()
        {
            EnsureBuilt();
            if (DependencyGraph == null)
            {
                DependencyGraph = new SODependencyGraph();
                DependencyGraph.Build(this);
            }
            return DependencyGraph;
        }

        private static T[] FindAll<T>() where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            var results = new T[guids.Length];
            int count = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                    results[count++] = asset;
            }
            if (count < results.Length)
                System.Array.Resize(ref results, count);
            return results;
        }
    }
}
#endif
