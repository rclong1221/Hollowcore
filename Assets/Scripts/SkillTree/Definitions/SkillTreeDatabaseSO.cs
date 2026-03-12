using System.Collections.Generic;
using UnityEngine;

namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: Root registry of all skill trees.
    /// Place in Resources/SkillTreeDatabase for bootstrap loading.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Skill Tree/Skill Tree Database")]
    public class SkillTreeDatabaseSO : ScriptableObject
    {
        public List<SkillTreeSO> Trees = new();
        public int TalentPointsPerLevel = 1;
        public int RespecBaseCost = 100;
        [Range(1f, 3f)]
        public float RespecCostMultiplier = 1.5f;
        public int RespecCostCap = 1000;
    }
}
