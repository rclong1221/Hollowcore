using UnityEngine;
using UnityEditor;

namespace DIG.Editor.EquipmentWorkstation
{
    public class EquipmentDebugModule : IEquipmentModule
    {
        private bool _isPlayMode;
        private string _log = "";
        private Vector2 _scrollPos;

        public void OnGUI()
        {
            _isPlayMode = Application.isPlaying;

            EditorGUILayout.LabelField("Runtime Debugger", EditorStyles.boldLabel);
            if (!_isPlayMode)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see live data.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            
            // Left: Status
            DrawStatusPanel();
            
            EditorGUILayout.Space();
            
            // Right: Actions & Log
            DrawActionPanel();
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatusPanel()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Width(250));
            EditorGUILayout.LabelField("Live State", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawStat("Active Slot:", "0 (Right Hand)");
            DrawStat("Item ID:", "4 (Bow)");
            DrawStat("State:", "Idle");
            DrawStat("Ammo:", "12 / 30");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("ECS Components", EditorStyles.boldLabel);
            DrawStat("Entities:", "4 Active");
            DrawStat("Systems:", "Running");
            
            EditorGUILayout.EndVertical();
        }

        private void DrawStat(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(100));
            GUILayout.Label(value, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawActionPanel()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("Override Controls", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Force Reload")) Log("Command: Force Reload");
            if (GUILayout.Button("Force Unequip")) Log("Command: Force Unequip");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Log", EditorStyles.boldLabel);
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, EditorStyles.helpBox, GUILayout.Height(200));
            GUILayout.Label(_log);
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.EndVertical();
        }

        private void Log(string msg)
        {
            _log += $"[{System.DateTime.Now:HH:mm:ss}] {msg}\n";
        }
    }
}
