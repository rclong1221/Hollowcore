using UnityEditor;

namespace DIG.Editor.SkillTreeWorkstation
{
    /// <summary>
    /// EPIC 17.1: Module interface for Skill Tree Workstation tabs.
    /// Follows IProgressionWorkstationModule pattern.
    /// </summary>
    public interface ISkillTreeWorkstationModule
    {
        void OnGUI();
        void OnSceneGUI(SceneView sceneView);
    }
}
