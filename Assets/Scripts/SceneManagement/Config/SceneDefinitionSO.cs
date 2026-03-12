using UnityEngine;

namespace DIG.SceneManagement
{
    /// <summary>
    /// EPIC 18.6: Defines a single loadable scene — standard Unity scene or ECS SubScene.
    /// Designers assign these to GameFlowStates to control what loads per game phase.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Scene Management/Scene Definition")]
    public class SceneDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for this scene definition.")]
        public string SceneId;

        [Tooltip("Human-readable name shown in loading screens and editor tools.")]
        public string DisplayName;

        [Header("Scene Reference")]
        [Tooltip("Scene name exactly as it appears in Build Settings.")]
        public string SceneName;

        [Tooltip("How this scene is loaded at runtime.")]
        public SceneLoadMode LoadMode = SceneLoadMode.Single;

        [Header("SubScene (ECS)")]
        [Tooltip("SubScene GUIDs to load into ECS worlds when LoadMode = SubScene.")]
        public string[] SubSceneGuids;

        [Tooltip("SubScene GUIDs that must finish loading before the scene is considered ready.")]
        public string[] RequiredSubscenes;

        [Header("Behavior")]
        [Tooltip("Unload the previous scene before loading this one (Single mode only).")]
        public bool UnloadPrevious = true;

        [Tooltip("Minimum seconds the loading screen stays visible even if load is instant.")]
        [Min(0f)] public float MinLoadTimeSeconds = 0.5f;
    }
}
