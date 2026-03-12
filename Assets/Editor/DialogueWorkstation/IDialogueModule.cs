#if UNITY_EDITOR
namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 16.16: Module interface for DialogueWorkstationWindow tabs.
    /// Follows IAIWorkstationModule pattern.
    /// </summary>
    public interface IDialogueModule
    {
        void OnGUI();
        void OnSceneGUI(UnityEditor.SceneView sceneView);
    }
}
#endif
