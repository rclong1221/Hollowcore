using UnityEditor;
using UnityEngine;
using DIG.UI.Tutorial;

namespace DIG.Tutorial.Editor.Modules
{
    /// <summary>
    /// EPIC 18.4: Trigger Map module — lists all TutorialTriggerAuthoring in scene,
    /// shows their SequenceId bindings, and allows pinging GameObjects.
    /// </summary>
    public class TriggerMapModule : ITutorialModule
    {
        private TutorialTriggerAuthoring[] _triggers;
        private Vector2 _scrollPos;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Tutorial Trigger Map", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (GUILayout.Button("Refresh"))
                LoadTriggers();

            if (_triggers == null || _triggers.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No TutorialTriggerAuthoring found in scene.\nAdd one to a trigger volume GameObject.",
                    MessageType.Info);
                if (_triggers == null) LoadTriggers();
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Found {_triggers.Length} tutorial trigger(s) in scene");
            EditorGUILayout.Space(4);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            foreach (var trigger in _triggers)
            {
                if (trigger == null) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();

                // Ping button
                if (GUILayout.Button("Ping", GUILayout.Width(40)))
                {
                    EditorGUIUtility.PingObject(trigger.gameObject);
                    Selection.activeGameObject = trigger.gameObject;
                }

                // Name
                EditorGUILayout.LabelField(trigger.gameObject.name, EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Header", trigger.Header);
                EditorGUILayout.LabelField("Sequence ID", string.IsNullOrEmpty(trigger.SequenceId) ? "(none)" : trigger.SequenceId);
                EditorGUILayout.LabelField("OneTime", trigger.OneTime.ToString());

                // Position
                var pos = trigger.transform.position;
                EditorGUILayout.LabelField("Position", $"({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        private void LoadTriggers()
        {
            _triggers = Object.FindObjectsByType<TutorialTriggerAuthoring>(FindObjectsSortMode.None);
        }
    }
}
