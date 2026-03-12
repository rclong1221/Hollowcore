namespace DIG.Crafting.Editor.Modules
{
    /// <summary>
    /// EPIC 16.13: Module interface for CraftingWorkstationWindow tabs.
    /// Follows IAIWorkstationModule / IQuestModule pattern.
    /// </summary>
    public interface ICraftingModule
    {
        void OnGUI();
        void OnSceneGUI(UnityEditor.SceneView sceneView);
    }
}
