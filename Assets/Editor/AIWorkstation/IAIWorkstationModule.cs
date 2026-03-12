using Unity.Entities;

namespace DIG.Editor.AIWorkstation
{
    /// <summary>
    /// Interface for AI Workstation tab modules.
    /// Each module renders its own tab content and optionally draws Scene view gizmos.
    /// </summary>
    public interface IAIWorkstationModule
    {
        void OnGUI();
        void OnSceneGUI(UnityEditor.SceneView sceneView);
        void OnEntityChanged(Entity entity, EntityManager entityManager);
    }
}
