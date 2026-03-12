using UnityEditor;

namespace DIG.Weather.Editor
{
    public interface IWeatherWorkstationModule
    {
        void OnGUI();
        void OnSceneGUI(SceneView sceneView);
    }
}
