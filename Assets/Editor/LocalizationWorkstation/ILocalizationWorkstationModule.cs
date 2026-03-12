using UnityEditor;

namespace DIG.Localization.Editor
{
    public interface ILocalizationWorkstationModule
    {
        void OnGUI();
        void OnSceneGUI(SceneView sceneView);
    }
}
