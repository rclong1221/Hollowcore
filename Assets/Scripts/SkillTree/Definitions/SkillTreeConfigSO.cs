using UnityEngine;

namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: Global skill tree configuration.
    /// Place in Resources/SkillTreeConfig for bootstrap loading.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Skill Tree/Config")]
    public class SkillTreeConfigSO : ScriptableObject
    {
        [Tooltip("Maximum number of skill trees a player can have active simultaneously.")]
        public int MaxTreesPerPlayer = 3;

        [Tooltip("Lifetime cap on total talent points (e.g., 60 at level 50).")]
        public int MaxTotalTalentPoints = 60;

        [Tooltip("Allow players to respec talent points for gold.")]
        public bool AllowRespec = true;

        [Tooltip("Show locked abilities grayed out in the UI for preview.")]
        public bool PreviewUnlockedAbilities = true;
    }
}
