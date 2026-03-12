using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SettingsService = DIG.Settings.Core.SettingsService;

namespace DIG.Editor.SettingsWorkstation
{
    /// <summary>
    /// EPIC 18.2: Editor window for debugging the Settings system.
    /// Tabs: Pages, Managers, PlayerPrefs.
    /// </summary>
    public class SettingsWorkstationWindow : EditorWindow
    {
        private enum Tab { Pages, Managers, PlayerPrefs }

        [MenuItem("DIG/Settings Workstation")]
        private static void ShowWindow()
        {
            var window = GetWindow<SettingsWorkstationWindow>("Settings Workstation");
            window.minSize = new Vector2(400, 300);
        }

        private Tab _activeTab = Tab.Pages;
        private Vector2 _scrollPos;

        // Cached GUIStyles (avoid allocation per OnGUI)
        private GUIStyle _dirtyStyle;
        private GUIStyle _cleanStyle;

        // Known PlayerPrefs keys used by settings pages
        private static readonly string[] KnownPrefsKeys =
        {
            "WindowMode",
            "Settings_FOV",
            "Audio_MasterVolume",
            "Audio_MusicVolume",
            "Audio_SFXVolume",
            "Audio_VoiceVolume",
            "Audio_AmbientVolume",
            "Settings_MouseSensitivity",
            "Settings_InvertY",
            "Targeting_AllowLock",
            "Targeting_AllowAssist",
            "Targeting_ShowIndicator",
            "Settings_ShowDamageNumbers",
            "Widget_FontScale",
            "Widget_Colorblind",
            "Widget_ReducedMotion",
            "Widget_HighContrast",
            "Widget_SizeScale",
            "AudioAccess_SoundRadar",
            "AudioAccess_DirSubs",
            "AudioAccess_SubScale",
            "HealthBar_Preset",
            "HealthBar_FadeTimeout",
            "HealthBar_MaxDistance",
            "HealthBar_ShowNames",
            "HealthBar_ShowLevels",
        };

        private void OnGUI()
        {
            DrawToolbar();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_activeTab)
            {
                case Tab.Pages:
                    DrawPagesTab();
                    break;
                case Tab.Managers:
                    DrawManagersTab();
                    break;
                case Tab.PlayerPrefs:
                    DrawPlayerPrefsTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Toggle(_activeTab == Tab.Pages, "Pages", EditorStyles.toolbarButton))
                _activeTab = Tab.Pages;
            if (GUILayout.Toggle(_activeTab == Tab.Managers, "Managers", EditorStyles.toolbarButton))
                _activeTab = Tab.Managers;
            if (GUILayout.Toggle(_activeTab == Tab.PlayerPrefs, "PlayerPrefs", EditorStyles.toolbarButton))
                _activeTab = Tab.PlayerPrefs;

            GUILayout.FlexibleSpace();

            if (Application.isPlaying)
            {
                if (GUILayout.Button("Open Settings", EditorStyles.toolbarButton))
                    SettingsService.Open();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPagesTab()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see registered settings pages.", MessageType.Info);
                return;
            }

            var pages = SettingsService.Pages;
            if (pages.Count == 0)
            {
                EditorGUILayout.HelpBox("No settings pages registered. Open the Settings screen first.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField($"Registered Pages ({pages.Count})", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            for (int i = 0; i < pages.Count; i++)
            {
                var page = pages[i];
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                EditorGUILayout.LabelField(page.DisplayName, EditorStyles.boldLabel, GUILayout.Width(120));
                EditorGUILayout.LabelField($"ID: {page.PageId}", GUILayout.Width(120));
                EditorGUILayout.LabelField($"Order: {page.SortOrder}", GUILayout.Width(80));

                if (page.HasUnsavedChanges)
                {
                    if (_dirtyStyle == null)
                    {
                        _dirtyStyle = new GUIStyle(EditorStyles.label);
                        _dirtyStyle.normal.textColor = new Color(1f, 0.6f, 0f);
                    }
                    EditorGUILayout.LabelField("DIRTY", _dirtyStyle, GUILayout.Width(50));
                }
                else
                {
                    if (_cleanStyle == null)
                    {
                        _cleanStyle = new GUIStyle(EditorStyles.label);
                        _cleanStyle.normal.textColor = new Color(0.4f, 0.8f, 0.4f);
                    }
                    EditorGUILayout.LabelField("Clean", _cleanStyle, GUILayout.Width(50));
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"Settings Open: {SettingsService.IsOpen}");
        }

        private void DrawManagersTab()
        {
            EditorGUILayout.LabelField("Settings Manager Singletons", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see live manager values.", MessageType.Info);
                return;
            }

            // WidgetAccessibilityManager
            DrawManagerSection("Widget Accessibility", DIG.Widgets.Config.WidgetAccessibilityManager.HasInstance, () =>
            {
                var m = DIG.Widgets.Config.WidgetAccessibilityManager.Instance;
                EditorGUILayout.LabelField("Font Scale", m.FontScale.ToString("F2"));
                EditorGUILayout.LabelField("Colorblind", m.Colorblind.ToString());
                EditorGUILayout.LabelField("Reduced Motion", m.ReducedMotion.ToString());
                EditorGUILayout.LabelField("High Contrast", m.HighContrast.ToString());
                EditorGUILayout.LabelField("Widget Size", m.WidgetSizeScale.ToString("F2"));
            });

            // TargetLockSettingsManager
            DrawManagerSection("Target Lock Settings", true, () =>
            {
                var m = DIG.Targeting.TargetLockSettingsManager.Instance;
                EditorGUILayout.LabelField("Allow Target Lock", m.AllowTargetLock.ToString());
                EditorGUILayout.LabelField("Allow Aim Assist", m.AllowAimAssist.ToString());
                EditorGUILayout.LabelField("Show Indicator", m.ShowIndicator.ToString());
            });

            // HealthBarSettingsManager
            DrawManagerSection("Health Bar Settings", DIG.Combat.UI.HealthBarSettingsManager.HasInstance, () =>
            {
                var m = DIG.Combat.UI.HealthBarSettingsManager.Instance;
                EditorGUILayout.LabelField("Mode", m.CurrentMode.ToString());
                EditorGUILayout.LabelField("Show Name", m.ShowName.ToString());
                EditorGUILayout.LabelField("Show Level", m.ShowLevel.ToString());
                EditorGUILayout.LabelField("Fade Timeout", m.FadeTimeout.ToString("F1"));
            });

            // MotionIntensitySettings
            DrawManagerSection("Motion Intensity", DIG.Core.Settings.MotionIntensitySettings.HasInstance, () =>
            {
                var m = DIG.Core.Settings.MotionIntensitySettings.Instance;
                EditorGUILayout.LabelField("Global Intensity", m.GlobalIntensity.ToString("F2"));
                EditorGUILayout.LabelField("Platform Tier", m.CurrentTier.ToString());
            });
        }

        private void DrawManagerSection(string name, bool hasInstance, System.Action drawFields)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(name, EditorStyles.boldLabel);

            if (hasInstance)
            {
                EditorGUI.indentLevel++;
                drawFields();
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.LabelField("Not instantiated", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void DrawPlayerPrefsTab()
        {
            EditorGUILayout.LabelField("Known Settings PlayerPrefs Keys", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            for (int i = 0; i < KnownPrefsKeys.Length; i++)
            {
                string key = KnownPrefsKeys[i];
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(key, GUILayout.Width(220));

                if (PlayerPrefs.HasKey(key))
                {
                    // Try float first, then int
                    float fVal = PlayerPrefs.GetFloat(key, float.MinValue);
                    if (fVal != float.MinValue)
                    {
                        EditorGUILayout.LabelField(fVal.ToString("F3"), GUILayout.Width(100));
                    }
                    else
                    {
                        int iVal = PlayerPrefs.GetInt(key, int.MinValue);
                        EditorGUILayout.LabelField(iVal.ToString(), GUILayout.Width(100));
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("(not set)", EditorStyles.miniLabel, GUILayout.Width(100));
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8);

            if (GUILayout.Button("Clear All Settings PlayerPrefs"))
            {
                if (EditorUtility.DisplayDialog("Clear Settings",
                    "Delete all known settings PlayerPrefs keys?\nThis cannot be undone.",
                    "Clear", "Cancel"))
                {
                    for (int i = 0; i < KnownPrefsKeys.Length; i++)
                        PlayerPrefs.DeleteKey(KnownPrefsKeys[i]);
                    PlayerPrefs.Save();
                    Debug.Log("[SettingsWorkstation] All known settings PlayerPrefs cleared.");
                }
            }
        }
    }
}
