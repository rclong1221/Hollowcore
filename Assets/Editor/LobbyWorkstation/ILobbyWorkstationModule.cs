using UnityEditor;

namespace DIG.Lobby.Editor
{
    /// <summary>
    /// EPIC 17.4: Module interface for Lobby Workstation editor window.
    /// Follows IPartyWorkstationModule pattern.
    /// </summary>
    public interface ILobbyWorkstationModule
    {
        string ModuleName { get; }
        void OnGUI();
        void OnSceneGUI(SceneView sceneView);
    }
}
