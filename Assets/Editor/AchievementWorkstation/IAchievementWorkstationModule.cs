#if UNITY_EDITOR
using UnityEditor;

namespace DIG.Achievement.Editor
{
    /// <summary>
    /// EPIC 17.7: Interface for Achievement Workstation editor modules.
    /// Follows IProgressionWorkstationModule / IMapWorkstationModule pattern.
    /// </summary>
    public interface IAchievementWorkstationModule
    {
        string ModuleName { get; }
        void OnGUI();
        void OnSceneGUI(SceneView sceneView);
    }
}
#endif
