#if UNITY_EDITOR
using UnityEditor;

namespace DIG.Map.Editor
{
    /// <summary>
    /// EPIC 17.6: Module interface for Map Workstation window.
    /// </summary>
    public interface IMapWorkstationModule
    {
        string ModuleName { get; }
        void OnGUI();
        void OnSceneGUI(SceneView sceneView);
    }
}
#endif
