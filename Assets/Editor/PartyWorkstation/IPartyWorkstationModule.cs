using UnityEditor;

namespace DIG.Editor.PartyWorkstation
{
    /// <summary>
    /// EPIC 17.2: Module interface for Party Workstation tabs.
    /// </summary>
    public interface IPartyWorkstationModule
    {
        void OnGUI();
        void OnSceneGUI(SceneView sceneView);
    }
}
