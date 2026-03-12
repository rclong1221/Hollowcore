using System.Collections.Generic;
using UnityEngine;

namespace DIG.Loot.Definitions
{
    /// <summary>
    /// EPIC 16.6: Root loot table ScriptableObject.
    /// Contains pools and roll configuration. Assigned to enemy prefabs via LootTableAuthoring.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Loot/Loot Table", order = 0)]
    public class LootTableSO : ScriptableObject
    {
        [Tooltip("Human-readable name for editor tooling.")]
        public string TableName;

        [Tooltip("Number of guaranteed rolls (always performed).")]
        [Min(0)]
        public int GuaranteedRolls = 1;

        [Tooltip("Number of bonus rolls attempted.")]
        [Min(0)]
        public int BonusRolls;

        [Tooltip("Chance (0-1) for each bonus roll to succeed.")]
        [Range(0f, 1f)]
        public float BonusRollChance = 0.5f;

        [Tooltip("Pools to roll against. Each pool is rolled independently.")]
        public List<LootPoolSO> Pools = new();

        [Tooltip("Conditions that must be met for this table to drop anything.")]
        public LootTableCondition[] Conditions;
    }
}
