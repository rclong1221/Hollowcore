using Unity.Entities;
using UnityEditor;

namespace Hollowcore.Editor.ChassisWorkstation
{
    public interface IChassisModule
    {
        void OnGUI();
        void OnSceneGUI(SceneView sceneView);
        void OnEntityChanged(Entity entity, EntityManager entityManager);
    }
}
