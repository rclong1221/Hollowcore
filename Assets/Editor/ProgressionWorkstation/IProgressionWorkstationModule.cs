using UnityEditor;

namespace DIG.Progression.Editor
{
    /// <summary>
    /// EPIC 16.14: Interface for Progression Workstation editor modules.
    /// </summary>
    public interface IProgressionWorkstationModule
    {
        void OnGUI();
        void OnSceneGUI(SceneView sceneView);
    }
}
