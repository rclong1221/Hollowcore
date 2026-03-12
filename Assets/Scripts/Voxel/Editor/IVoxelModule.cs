using UnityEngine;
using UnityEditor;

namespace DIG.Voxel.Editor
{
    public interface IVoxelModule
    {
        void Initialize();
        void DrawGUI();
        void DrawSceneGUI(SceneView sceneView);
        void OnDestroy();
    }
}
