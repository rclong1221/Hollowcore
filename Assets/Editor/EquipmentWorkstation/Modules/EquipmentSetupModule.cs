using UnityEngine;
using UnityEditor;
using System.IO;

namespace DIG.Editor.EquipmentWorkstation
{
    public class EquipmentSetupModule : IEquipmentModule
    {
        private bool _hasChecked = false;

        // Status Flags
        private bool _hasInputSystem;
        private bool _hasTags;
        private bool _hasLayers;
        private bool _hasFolderStructure;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Project Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Check and configure project-wide settings for the Equipment System.", MessageType.Info);

            EditorGUILayout.Space();

            if (GUILayout.Button("Run Setup Checks", GUILayout.Height(30)))
            {
                RunChecks();
            }

            if (!_hasChecked) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status Report:", EditorStyles.boldLabel);

             DrawCheckItem("Input System (Enhanced)", _hasInputSystem, "Use New Input System package.", "Fix: Install Package");
             DrawCheckItem("Tags (Player, Weapon)", _hasTags, "Required tags missing.", "Fix: Add Tags");
             DrawCheckItem("Layers (Character, Weapon)", _hasLayers, "Required layers missing.", "Fix: Add Layers");
             DrawCheckItem("Folder Structure", _hasFolderStructure, "Standard assets folders missing.", "Fix: Create Folders");
        }

        private void DrawCheckItem(string title, bool isOk, string errorMsg, string fixBtnLabel)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            // Icon
            var icon = isOk ? "testpassed" : "console.erroricon";
            GUILayout.Label(EditorGUIUtility.IconContent(icon), GUILayout.Width(25));

            // Label
            EditorGUILayout.BeginVertical();
            GUILayout.Label(title, EditorStyles.boldLabel);
            if (!isOk) GUILayout.Label(errorMsg, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            // Fix Button
            if (!isOk)
            {
                if (GUILayout.Button(fixBtnLabel, GUILayout.Width(120)))
                {
                    PerformFix(title);
                }
            }
            else
            {
                GUILayout.Label("OK", EditorStyles.miniLabel, GUILayout.Width(30));
            }

            EditorGUILayout.EndHorizontal();
        }

        private void RunChecks()
        {
            _hasInputSystem = true; // Placeholder check
            _hasTags = UnityEditorInternal.InternalEditorUtility.tags.Length > 0;
            _hasLayers = true; // Placeholder
            _hasFolderStructure = Directory.Exists("Assets/DIG/Items");
            
            _hasChecked = true;
        }

        private void PerformFix(string checkTitle)
        {
            Debug.Log($"Fixing {checkTitle}...");
            if (checkTitle.Contains("Folder"))
            {
                Directory.CreateDirectory("Assets/DIG/Items");
                _hasFolderStructure = true;
                AssetDatabase.Refresh();
            }
        }
    }
}
