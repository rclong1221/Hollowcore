#if UNITY_EDITOR
using UnityEditor;

namespace DIG.PvP.Editor
{
    /// <summary>
    /// EPIC 17.10: Interface for PvP Workstation editor modules.
    /// Same pattern as ILobbyWorkstationModule, ICinematicWorkstationModule.
    /// </summary>
    public interface IPvPWorkstationModule
    {
        string ModuleName { get; }
        void OnGUI();
        void OnSceneGUI(SceneView sceneView);
    }
}
#endif
