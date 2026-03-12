using System;
using System.Collections.Generic;
using UnityEngine;

namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Weighted pool of enemy types for a zone's spawn director.
    /// Entries are filtered by difficulty and alive-count caps at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "EncounterPool", menuName = "DIG/Roguelite/Encounter Pool", order = 12)]
    public class EncounterPoolSO : ScriptableObject
    {
        public string PoolName;
        public List<EncounterPoolEntry> Entries = new();

        [Tooltip("Boss encounter profile for Boss/TriggerThenBoss zones. Null = use Entries.")]
        public ScriptableObject BossProfile;
    }

    [Serializable]
    public struct EncounterPoolEntry
    {
        [Tooltip("Enemy ghost prefab.")]
        public GameObject EnemyPrefab;

        [Tooltip("Display name for editor tooling and simulation.")]
        public string DisplayName;

        [Tooltip("Selection weight. Higher = more likely to be picked by the director.")]
        public float Weight;

        [Tooltip("Spawn credit cost. Director 'buys' spawns with its budget.")]
        public int SpawnCost;

        [Tooltip("Minimum effective difficulty before this entry appears. 0 = always.")]
        public float MinDifficulty;

        [Tooltip("Maximum effective difficulty before this entry is removed. 0 = no limit.")]
        public float MaxDifficulty;

        [Tooltip("Can this entry spawn as an Elite variant?")]
        public bool CanBeElite;

        [Tooltip("Maximum concurrent alive count for this entry type. 0 = unlimited.")]
        public int MaxAlive;
    }
}
