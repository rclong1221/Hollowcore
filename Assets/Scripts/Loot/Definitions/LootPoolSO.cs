using System.Collections.Generic;
using UnityEngine;

namespace DIG.Loot.Definitions
{
    /// <summary>
    /// EPIC 16.6: A pool of loot entries that can be rolled.
    /// Multiple pools compose a LootTableSO.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Loot/Loot Pool", order = 1)]
    public class LootPoolSO : ScriptableObject
    {
        [Tooltip("Entries in this pool, each with a weight and drop chance.")]
        public List<LootPoolEntry> Entries = new();

        [Tooltip("If false, duplicate items from this pool are re-rolled.")]
        public bool AllowDuplicates = true;

        [Tooltip("Relative weight of this pool when the table has multiple pools.")]
        [Min(0f)]
        public float PoolWeight = 1f;

        [Tooltip("Minimum number of drops from this pool per roll.")]
        [Min(0)]
        public int MinDrops = 1;

        [Tooltip("Maximum number of drops from this pool per roll.")]
        [Min(1)]
        public int MaxDrops = 1;
    }
}
