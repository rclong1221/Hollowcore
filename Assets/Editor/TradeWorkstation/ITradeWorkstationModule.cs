using UnityEditor;

namespace DIG.Editor.TradeWorkstation
{
    /// <summary>
    /// EPIC 17.3: Module interface for Trade Workstation tabs.
    /// </summary>
    public interface ITradeWorkstationModule
    {
        void OnGUI();
        void OnSceneGUI(SceneView sceneView);
    }
}
