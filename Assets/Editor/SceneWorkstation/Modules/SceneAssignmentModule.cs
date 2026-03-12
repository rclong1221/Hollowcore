using UnityEditor;
using UnityEngine;

namespace DIG.SceneManagement.Editor.Modules
{
    /// <summary>
    /// EPIC 18.6: Lists all states from a GameFlowDefinitionSO and shows
    /// their scene assignments with validation warnings.
    /// </summary>
    public class SceneAssignmentModule : ISceneModule
    {
        private GameFlowDefinitionSO _flowDef;
        private Vector2 _scrollPos;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Scene Assignment", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _flowDef = (GameFlowDefinitionSO)EditorGUILayout.ObjectField(
                "Flow Definition", _flowDef, typeof(GameFlowDefinitionSO), false);

            if (_flowDef == null)
            {
                EditorGUILayout.HelpBox("Assign a GameFlowDefinitionSO to inspect scene assignments.",
                    MessageType.Info);
                return;
            }

            if (_flowDef.States == null || _flowDef.States.Length == 0)
            {
                EditorGUILayout.HelpBox("No states defined.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(4);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            for (int i = 0; i < _flowDef.States.Length; i++)
            {
                var state = _flowDef.States[i];
                DrawStateEntry(state, i);
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();

            // Validation summary
            EditorGUILayout.Space(8);
            if (GUILayout.Button("Validate All", GUILayout.Height(28)))
                RunValidation();
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private void DrawStateEntry(GameFlowState state, int index)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField($"[{index}] {state.StateId ?? "(unnamed)"}", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;

            // Primary scene
            string sceneName = state.Scene != null ? state.Scene.DisplayName : "(none)";
            string loadMode = state.Scene != null ? state.Scene.LoadMode.ToString() : "-";
            EditorGUILayout.LabelField("Primary Scene", $"{sceneName} [{loadMode}]");

            // Validation: scene missing
            if (state.Scene == null && !state.RequiresNetwork)
            {
                EditorGUILayout.HelpBox("No scene assigned. Non-network states need a scene.", MessageType.Warning);
            }
            else if (state.Scene != null && state.Scene.LoadMode != SceneLoadMode.SubScene)
            {
                // Check Build Settings
                if (!string.IsNullOrEmpty(state.Scene.SceneName) && !IsSceneInBuild(state.Scene.SceneName))
                {
                    EditorGUILayout.HelpBox(
                        $"Scene '{state.Scene.SceneName}' is NOT in Build Settings.",
                        MessageType.Error);
                }
            }

            // SubScene GUIDs
            if (state.Scene != null && state.Scene.LoadMode == SceneLoadMode.SubScene)
            {
                int guidCount = state.Scene.SubSceneGuids != null ? state.Scene.SubSceneGuids.Length : 0;
                EditorGUILayout.LabelField("SubScene GUIDs", guidCount.ToString());
                if (guidCount == 0)
                    EditorGUILayout.HelpBox("SubScene mode but no GUIDs assigned.", MessageType.Warning);
            }

            // Additive scenes
            int additiveCount = state.AdditiveScenes != null ? state.AdditiveScenes.Length : 0;
            EditorGUILayout.LabelField("Additive Scenes", additiveCount.ToString());
            if (state.AdditiveScenes != null)
            {
                EditorGUI.indentLevel++;
                for (int j = 0; j < state.AdditiveScenes.Length; j++)
                {
                    var addScene = state.AdditiveScenes[j];
                    string name = addScene != null ? addScene.DisplayName : "(null)";
                    EditorGUILayout.LabelField($"  [{j}]", name);
                }
                EditorGUI.indentLevel--;
            }

            // Network flag
            if (state.RequiresNetwork)
                EditorGUILayout.LabelField("", "Requires Network (GameBootstrap)");

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void RunValidation()
        {
            int warnings = 0;
            int errors = 0;

            for (int i = 0; i < _flowDef.States.Length; i++)
            {
                var state = _flowDef.States[i];

                if (string.IsNullOrEmpty(state.StateId))
                {
                    Debug.LogWarning($"[SceneAssignment] State [{i}] has empty StateId.");
                    warnings++;
                }

                if (state.Scene == null && !state.RequiresNetwork)
                {
                    Debug.LogWarning($"[SceneAssignment] State '{state.StateId}' has no scene and is not network.");
                    warnings++;
                }

                if (state.Scene != null && state.Scene.LoadMode != SceneLoadMode.SubScene &&
                    !string.IsNullOrEmpty(state.Scene.SceneName) && !IsSceneInBuild(state.Scene.SceneName))
                {
                    Debug.LogError($"[SceneAssignment] State '{state.StateId}': scene '{state.Scene.SceneName}' not in Build Settings.");
                    errors++;
                }
            }

            // Check transitions reference valid states
            if (_flowDef.Transitions != null)
            {
                var stateIds = new System.Collections.Generic.HashSet<string>();
                for (int i = 0; i < _flowDef.States.Length; i++)
                    stateIds.Add(_flowDef.States[i].StateId);

                for (int i = 0; i < _flowDef.Transitions.Length; i++)
                {
                    var t = _flowDef.Transitions[i];
                    if (!stateIds.Contains(t.FromState))
                    {
                        Debug.LogWarning($"[SceneAssignment] Transition [{i}] FromState '{t.FromState}' not found.");
                        warnings++;
                    }
                    if (!stateIds.Contains(t.ToState))
                    {
                        Debug.LogWarning($"[SceneAssignment] Transition [{i}] ToState '{t.ToState}' not found.");
                        warnings++;
                    }
                }
            }

            Debug.Log($"[SceneAssignment] Validation complete: {errors} error(s), {warnings} warning(s).");
        }

        private static bool IsSceneInBuild(string sceneName)
        {
            var scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (!scenes[i].enabled) continue;
                // Match by name (last path segment without extension)
                string path = scenes[i].path;
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name == sceneName)
                    return true;
            }
            return false;
        }
    }
}
