using System;
using UnityEngine;

namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: Defines a single skill tree with all its nodes.
    /// Authored by designers via the Skill Tree Workstation editor.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Skill Tree/Skill Tree")]
    public class SkillTreeSO : ScriptableObject
    {
        public int TreeId;
        public string TreeName;
        [TextArea(2, 4)]
        public string Description;
        public string IconPath;
        public byte ClassRestriction;
        public int MaxPoints = 30;
        public SkillNodeDefinition[] Nodes = Array.Empty<SkillNodeDefinition>();
    }

    /// <summary>
    /// EPIC 17.1: Individual node definition within a skill tree.
    /// Serialized within SkillTreeSO.Nodes array.
    /// </summary>
    [Serializable]
    public struct SkillNodeDefinition
    {
        public int NodeId;
        public string Name;
        [TextArea(2, 4)]
        public string Description;
        public string IconPath;
        public int Tier;
        public int PointCost;
        public int TierPointsRequired;
        public int MaxRanks;
        public SkillNodeType NodeType;
        public int[] Prerequisites;
        public SkillBonusType BonusType;
        public float BonusValue;
        public int AbilityTypeId;
        public Vector2 EditorPosition;
    }
}
