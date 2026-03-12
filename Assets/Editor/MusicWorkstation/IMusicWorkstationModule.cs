#if UNITY_EDITOR
using UnityEditor;

namespace DIG.Music.Editor
{
    /// <summary>
    /// EPIC 17.5: Module interface for Music Workstation window.
    /// </summary>
    public interface IMusicWorkstationModule
    {
        string ModuleName { get; }
        void OnGUI();
        void OnSceneGUI(SceneView sceneView);
    }
}
#endif
