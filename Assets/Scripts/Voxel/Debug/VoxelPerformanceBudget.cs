using UnityEngine;
using System;
using System.Linq;

namespace DIG.Voxel.Debug
{
    [CreateAssetMenu(menuName = "DIG/Voxel/Performance Budget")]
    public class VoxelPerformanceBudget : ScriptableObject
    {
        [Serializable]
        public struct BudgetEntry
        {
            public string SystemName;
            public float MaxAvgMs;
            public float MaxPeakMs;
        }
        
        public BudgetEntry[] Budgets = new BudgetEntry[]
        {
            new BudgetEntry { SystemName = "ChunkGeneration", MaxAvgMs = 2f, MaxPeakMs = 5f },
            new BudgetEntry { SystemName = "MeshGeneration", MaxAvgMs = 3f, MaxPeakMs = 8f },
            new BudgetEntry { SystemName = "ColliderUpdate", MaxAvgMs = 1f, MaxPeakMs = 3f },
            new BudgetEntry { SystemName = "LODSystem", MaxAvgMs = 0.5f, MaxPeakMs = 1f },
            new BudgetEntry { SystemName = "Total", MaxAvgMs = 10f, MaxPeakMs = 16.6f },
        };
        
        public BudgetEntry GetBudget(string name)
        {
            // Exact match
            foreach (var b in Budgets)
            {
                if (b.SystemName == name) return b;
            }
            
            // Contains match (e.g. "ChunkGenerationSystem" matches "ChunkGeneration")
            foreach (var b in Budgets)
            {
                if (name.Contains(b.SystemName)) return b;
            }
            
            return default;
        }
    }
}
