namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: Interface for MonoBehaviour UI adapters that display talent trees.
    /// Register via TalentUIRegistry.RegisterTalentUI() in OnEnable.
    /// </summary>
    public interface ITalentUIProvider
    {
        void OpenTalentTree(TalentTreeUIState state);
        void UpdateNodeStates(TalentTreeUIState state);
        void CloseTalentTree();
        void ShowRespecConfirm(int goldCost, int treeId);
    }
}
