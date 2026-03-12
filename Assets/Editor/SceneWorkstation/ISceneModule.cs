namespace DIG.SceneManagement.Editor.Modules
{
    /// <summary>
    /// EPIC 18.6: Module interface for Scene Workstation editor tabs.
    /// Follows ICraftingModule / IQuestModule pattern.
    /// </summary>
    public interface ISceneModule
    {
        void OnGUI();
        void OnSceneGUI(UnityEditor.SceneView sceneView);
    }
}
