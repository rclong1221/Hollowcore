using UnityEditor;
using UnityEngine;
using DIG.Notifications.Config;

namespace DIG.Notifications.Editor.Modules
{
    /// <summary>
    /// EPIC 18.3: Style Browser module — browse NotificationStyleSO assets and inspect their settings.
    /// </summary>
    public class StyleBrowserModule : INotificationModule
    {
        private NotificationStyleSO[] _styles;
        private string[] _styleNames;
        private int _selectedIndex = -1;
        private Vector2 _scrollPos;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Notification Styles", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (GUILayout.Button("Refresh"))
                LoadStyles();

            if (_styles == null || _styles.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No NotificationStyleSO assets found.\nCreate via Assets > Create > DIG > Notifications > Style.",
                    MessageType.Info);
                if (_styles == null) LoadStyles();
                return;
            }

            EditorGUILayout.Space(4);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Style list
            for (int i = 0; i < _styles.Length; i++)
            {
                if (_styles[i] == null) continue;

                bool isSelected = i == _selectedIndex;
                var bgColor = GUI.backgroundColor;
                if (isSelected) GUI.backgroundColor = new Color(0.5f, 0.7f, 1f);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(_styles[i].name, isSelected ? EditorStyles.boldLabel : EditorStyles.label))
                {
                    _selectedIndex = i;
                    Selection.activeObject = _styles[i];
                }
                EditorGUILayout.EndHorizontal();

                if (isSelected)
                {
                    EditorGUI.indentLevel++;

                    // Colors
                    EditorGUILayout.ColorField("Background", _styles[i].BackgroundColor);
                    EditorGUILayout.ColorField("Border", _styles[i].BorderColor);
                    EditorGUILayout.ColorField("Title", _styles[i].TitleColor);
                    EditorGUILayout.ColorField("Body", _styles[i].BodyColor);
                    EditorGUILayout.ColorField("Icon Tint", _styles[i].IconTint);

                    EditorGUILayout.Space(2);

                    // Timing
                    EditorGUILayout.LabelField("Duration", $"{_styles[i].DefaultDuration:F1}s");
                    EditorGUILayout.LabelField("Animation", $"{_styles[i].AnimationDuration:F2}s");

                    // Audio
                    EditorGUILayout.LabelField("Sound", _styles[i].Sound != null ? _styles[i].Sound.name : "(none)");
                    EditorGUILayout.LabelField("Volume", $"{_styles[i].SoundVolume:P0}");

                    // Priority
                    EditorGUILayout.LabelField("Default Priority", _styles[i].DefaultPriority.ToString());

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                GUI.backgroundColor = bgColor;
            }

            EditorGUILayout.EndScrollView();

            // Master config
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Master Config", EditorStyles.boldLabel);
            var config = Resources.Load<NotificationConfigSO>("NotificationConfig");
            if (config != null)
            {
                EditorGUILayout.LabelField("Toast MaxVisible", config.ToastConfig.MaxVisible.ToString());
                EditorGUILayout.LabelField("Banner MaxVisible", config.BannerConfig.MaxVisible.ToString());
                EditorGUILayout.LabelField("Center MaxVisible", config.CenterConfig.MaxVisible.ToString());
                EditorGUILayout.LabelField("History Ring Size", config.HistoryRingSize.ToString());
                EditorGUILayout.LabelField("Unified Achievements", config.UseUnifiedAchievements.ToString());
                EditorGUILayout.LabelField("Unified LevelUp", config.UseUnifiedLevelUp.ToString());
                EditorGUILayout.LabelField("Unified Quests", config.UseUnifiedQuests.ToString());

                if (GUILayout.Button("Select Config"))
                    Selection.activeObject = config;
            }
            else
            {
                EditorGUILayout.HelpBox("NotificationConfig not found in Resources/.", MessageType.Warning);
            }
        }

        private void LoadStyles()
        {
            var guids = AssetDatabase.FindAssets("t:NotificationStyleSO");
            _styles = new NotificationStyleSO[guids.Length];
            _styleNames = new string[guids.Length];

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _styles[i] = AssetDatabase.LoadAssetAtPath<NotificationStyleSO>(path);
                _styleNames[i] = _styles[i] != null ? _styles[i].name : "(null)";
            }

            _selectedIndex = -1;
        }
    }
}
