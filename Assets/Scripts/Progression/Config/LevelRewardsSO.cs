using System;
using UnityEngine;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Per-level reward definitions.
    /// Configures gold, recipe unlocks, ability unlocks, bonus stat points, etc.
    /// that players receive on reaching specific levels.
    /// Loaded from Resources/LevelRewards by ProgressionBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelRewards", menuName = "DIG/Progression/Level Rewards", order = 2)]
    public class LevelRewardsSO : ScriptableObject
    {
        public LevelRewardEntryData[] Rewards;
    }

    /// <summary>
    /// EPIC 16.14: A single reward entry associated with a level threshold.
    /// </summary>
    [Serializable]
    public struct LevelRewardEntryData
    {
        [Tooltip("Level at which this reward is granted")]
        [Min(2)] public int Level;

        public LevelRewardType RewardType;

        [Tooltip("Context-dependent: gold amount, recipe ID, ability ID, etc.")]
        public int IntValue;

        [Tooltip("Context-dependent: float modifier, resource increase, etc.")]
        public float FloatValue;

        [Tooltip("Description shown in UI")]
        public string Description;
    }
}
