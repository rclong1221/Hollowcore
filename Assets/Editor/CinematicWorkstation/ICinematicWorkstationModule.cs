#if UNITY_EDITOR
using UnityEditor;

namespace DIG.Cinematic.Editor
{
    /// <summary>
    /// EPIC 17.9: Module interface for Cinematic Workstation tabs.
    /// </summary>
    public interface ICinematicWorkstationModule
    {
        string ModuleName { get; }
        void OnGUI();
        void OnSceneGUI(SceneView sceneView);
    }
}
#endif
