using UnityEditor;
using UnityEngine;

namespace DIG.SceneManagement.Editor.Modules
{
    /// <summary>
    /// EPIC 18.6: Play-mode transition testing tool.
    /// Trigger transitions, fire events, and observe live state.
    /// </summary>
    public class TransitionTesterModule : ISceneModule
    {
        private string _targetState = "";
        private string _eventName = "";
        private Vector2 _logScroll;
        private readonly System.Collections.Generic.List<string> _log = new();

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Transition Tester", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to test scene transitions.",
                    MessageType.Info);
                return;
            }

            var service = SceneService.Instance;
            if (service == null)
            {
                EditorGUILayout.HelpBox(
                    "SceneService not found. Ensure a GameFlowDefinition exists in Resources/.",
                    MessageType.Warning);
                return;
            }

            // Live state display
            EditorGUILayout.LabelField("Live State", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Current State", service.CurrentState ?? "(null)");
            EditorGUILayout.LabelField("Is Loading", service.IsLoading.ToString());

            if (service.IsLoading)
            {
                var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                    GUILayout.Height(18), GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(rect, service.LoadProgress,
                    $"{service.LoadProgress:P0}");
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.Space(8);

            // Request transition
            EditorGUILayout.LabelField("Request Transition", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _targetState = EditorGUILayout.TextField("Target State", _targetState);
            EditorGUI.BeginDisabledGroup(service.IsLoading || string.IsNullOrEmpty(_targetState));
            if (GUILayout.Button("Go", GUILayout.Width(50)))
            {
                AddLog($"RequestTransition(\"{_targetState}\")");
                service.RequestTransition(_targetState);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Fire event
            EditorGUILayout.LabelField("Fire Event", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _eventName = EditorGUILayout.TextField("Event Name", _eventName);
            EditorGUI.BeginDisabledGroup(service.IsLoading || string.IsNullOrEmpty(_eventName));
            if (GUILayout.Button("Fire", GUILayout.Width(50)))
            {
                AddLog($"FireEvent(\"{_eventName}\")");
                service.FireEvent(_eventName);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Return to state
            EditorGUILayout.LabelField("Return To State", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(service.IsLoading);
            if (GUILayout.Button("Return to MainMenu"))
            {
                AddLog("ReturnToState(\"MainMenu\")");
                service.ReturnToState("MainMenu");
            }
            if (GUILayout.Button("Return to Lobby"))
            {
                AddLog("ReturnToState(\"Lobby\")");
                service.ReturnToState("Lobby");
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // Log panel
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            _logScroll = EditorGUILayout.BeginScrollView(_logScroll,
                GUILayout.Height(120));

            for (int i = _log.Count - 1; i >= 0; i--)
                EditorGUILayout.LabelField(_log[i], EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Clear Log", GUILayout.Width(80)))
                _log.Clear();
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private void AddLog(string msg)
        {
            _log.Add($"[{System.DateTime.Now:HH:mm:ss}] {msg}");
            if (_log.Count > 100)
                _log.RemoveAt(0);
        }
    }
}
