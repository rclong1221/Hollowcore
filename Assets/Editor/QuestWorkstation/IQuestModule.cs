using Unity.Entities;

namespace DIG.Quest.Editor
{
    /// <summary>
    /// EPIC 16.12: Interface for Quest Workstation tab modules.
    /// Follows IAIWorkstationModule pattern.
    /// </summary>
    public interface IQuestModule
    {
        void OnGUI();
        void OnSceneGUI(UnityEditor.SceneView sceneView);
    }
}
