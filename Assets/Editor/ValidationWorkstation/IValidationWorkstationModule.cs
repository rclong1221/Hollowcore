using UnityEditor;

namespace DIG.Validation.Editor
{
    /// <summary>
    /// EPIC 17.11: Module interface for Validation Workstation tabs.
    /// </summary>
    public interface IValidationWorkstationModule
    {
        string ModuleName { get; }
        void OnGUI();
        void OnSceneGUI(SceneView sceneView);
    }
}
