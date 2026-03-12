using System;
using System.Collections.Generic;
using UnityEngine;

namespace DIG.Roguelite.Rewards
{
    [Serializable]
    public struct RewardPoolEntry
    {
        public RewardDefinitionSO Reward;
        public float Weight;
        public byte MinRarity;                      // Filter: only include if rarity >= this
        public byte MaxRarity;                      // Filter: only include if rarity <= this
    }

    /// <summary>
    /// EPIC 23.5: Pool of rewards for choose-N-of-pool (zone clear) and shop inventory.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Roguelite/Reward Pool")]
    public class RewardPoolSO : ScriptableObject
    {
        public string PoolName;
        public int ChoiceCount = 3;                 // How many options to present
        public bool AllowDuplicates;
        public List<RewardPoolEntry> Entries = new();
    }
}
