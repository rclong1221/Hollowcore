namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: UI state for a single skill tree, pushed to ITalentUIProvider.
    /// </summary>
    public struct TalentTreeUIState
    {
        public int TreeId;
        public string TreeName;
        public int AvailablePoints;
        public int SpentInTree;
        public int TotalSpent;
        public TalentNodeUIState[] Nodes;
    }

    /// <summary>
    /// EPIC 17.1: UI state for a single node within a skill tree.
    /// </summary>
    public struct TalentNodeUIState
    {
        public int NodeId;
        public string Name;
        public string Description;
        public string IconPath;
        public int Tier;
        public int CurrentRank;
        public int MaxRanks;
        public int PointCost;
        public SkillNodeType NodeType;
        public TalentNodeStatus Status;
        public int[] PrerequisiteNodeIds;
        public string BonusText;
        public float EditorX;
        public float EditorY;
    }

    public enum TalentNodeStatus : byte
    {
        Locked = 0,
        Available = 1,
        Allocated = 2,
        Maxed = 3
    }
}
