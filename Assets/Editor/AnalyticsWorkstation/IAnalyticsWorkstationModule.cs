using UnityEditor;

namespace DIG.Analytics.Editor
{
    public interface IAnalyticsWorkstationModule
    {
        void OnGUI();
        void OnSceneGUI(SceneView sceneView);
    }
}
