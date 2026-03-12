#if UNITY_EDITOR
using System;
using DIG.Accessibility;
using DIG.Accessibility.Config;
using DIG.Accessibility.Visual;
using DIG.Widgets.Config;
using UnityEditor;
using UnityEngine;

namespace DIG.Accessibility.Editor
{
    /// <summary>
    /// EPIC 18.12: Editor window for accessibility testing and CVAA compliance review.
    /// Colorblind simulation preview, text scale preview, screen reader test,
    /// difficulty tuner, and CVAA compliance checklist.
    /// </summary>
    public class AccessibilityWorkstationWindow : EditorWindow
    {
        private int _selectedTab;
        private readonly string[] _tabNames = { "Colorblind Preview", "Screen Reader", "CVAA Checklist", "Difficulty Tuner" };

        // Colorblind preview state
        private ColorblindMode _previewMode = ColorblindMode.None;
        private float _previewIntensity = 1f;

        // Screen reader test state
        private string _testSpeechText = "Hello, this is a screen reader test.";

        // Difficulty tuner state
        private float _tunerEnemyHP = 1f;
        private float _tunerEnemyDmg = 1f;
        private float _tunerTiming = 1f;
        private float _tunerResource = 1f;
        private RespawnPenalty _tunerRespawn = RespawnPenalty.Normal;

        // Scroll
        private Vector2 _scrollPos;

        [MenuItem("DIG/Accessibility Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<AccessibilityWorkstationWindow>("Accessibility Workstation");
            window.minSize = new Vector2(500, 400);
        }

        private void OnGUI()
        {
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
            EditorGUILayout.Space(8);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_selectedTab)
            {
                case 0: DrawColorblindPreview(); break;
                case 1: DrawScreenReaderTest(); break;
                case 2: DrawCVAAChecklist(); break;
                case 3: DrawDifficultyTuner(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        // ── Tab 1: Colorblind Preview ───────────────────────────────

        private void DrawColorblindPreview()
        {
            EditorGUILayout.LabelField("Colorblind Simulation Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select a colorblind mode to simulate how the game appears to affected players.\n" +
                "This applies a simulation filter in the Scene/Game view when in Play Mode.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            _previewMode = (ColorblindMode)EditorGUILayout.EnumPopup("Simulation Mode", _previewMode);
            _previewIntensity = EditorGUILayout.Slider("Intensity", _previewIntensity, 0f, 1f);

            EditorGUILayout.Space(8);

            if (GUILayout.Button("Apply to Game View"))
            {
                if (ColorblindFilter.ActiveInstance != null)
                {
                    ColorblindFilter.ActiveInstance.SetMode(_previewMode);
                    ColorblindFilter.ActiveInstance.SetIntensity(_previewIntensity);
                    EditorUtility.DisplayDialog("Applied",
                        $"Colorblind filter set to {_previewMode} at {_previewIntensity:P0} intensity.", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Not Available",
                        "ColorblindFilter renderer feature not found on the active URP Renderer.\n" +
                        "Add ColorblindFilter to your URP Renderer Asset.", "OK");
                }
            }

            if (GUILayout.Button("Reset to None"))
            {
                _previewMode = ColorblindMode.None;
                if (ColorblindFilter.ActiveInstance != null)
                    ColorblindFilter.ActiveInstance.SetMode(ColorblindMode.None);
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Color Palette Preview", EditorStyles.boldLabel);

            // Show a grid of common game colors to preview the filter's effect
            EditorGUILayout.BeginHorizontal();
            DrawColorSwatch("Red (Danger)", Color.red);
            DrawColorSwatch("Green (Health)", Color.green);
            DrawColorSwatch("Blue (Mana)", Color.blue);
            DrawColorSwatch("Yellow (Gold)", Color.yellow);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawColorSwatch("Orange (Fire)", new Color(1f, 0.5f, 0f));
            DrawColorSwatch("Cyan (Ice)", Color.cyan);
            DrawColorSwatch("Purple (Arcane)", new Color(0.6f, 0f, 0.8f));
            DrawColorSwatch("White (Text)", Color.white);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawColorSwatch(string label, Color color)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            EditorGUILayout.ColorField(GUIContent.none, color, false, false, false, GUILayout.Height(30));
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        // ── Tab 2: Screen Reader Test ───────────────────────────────

        private void DrawScreenReaderTest()
        {
            EditorGUILayout.LabelField("Screen Reader Test", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Test the platform TTS (Text-to-Speech) integration.\n" +
                "macOS uses the 'say' command. Windows uses SAPI5 via PowerShell.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            _testSpeechText = EditorGUILayout.TextField("Text to Speak", _testSpeechText);

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Speak"))
            {
                ScreenReaderBridge.SetEnabled(true);
                ScreenReaderBridge.Speak(_testSpeechText, SpeechPriority.High);
            }

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Quick Tests", EditorStyles.boldLabel);

            if (GUILayout.Button("\"Menu opened\""))
            {
                ScreenReaderBridge.SetEnabled(true);
                ScreenReaderBridge.Speak("Menu opened", SpeechPriority.Normal);
            }

            if (GUILayout.Button("\"Level Up! You reached level 10.\""))
            {
                ScreenReaderBridge.SetEnabled(true);
                ScreenReaderBridge.Speak("Level Up! You reached level 10.", SpeechPriority.High);
            }

            if (GUILayout.Button("\"Item acquired: Legendary Sword\""))
            {
                ScreenReaderBridge.SetEnabled(true);
                ScreenReaderBridge.Speak("Item acquired: Legendary Sword", SpeechPriority.Normal);
            }
        }

        // ── Tab 3: CVAA Checklist ───────────────────────────────────

        private void DrawCVAAChecklist()
        {
            EditorGUILayout.LabelField("CVAA / WCAG Compliance Checklist", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Review accessibility feature coverage against CVAA (21st Century Communications and Video Accessibility Act) " +
                "and WCAG (Web Content Accessibility Guidelines) requirements.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            DrawChecklistItem("Colorblind Support", true,
                "GPU Daltonization filter for Protanopia/Deuteranopia/Tritanopia");
            DrawChecklistItem("Text Scaling", true,
                "Global text scale 0.8x–2.0x via TextScaleService + WidgetAccessibilityManager");
            DrawChecklistItem("High Contrast Mode", true,
                "Theme swap + outline thickness increase");
            DrawChecklistItem("Screen Reader / TTS", true,
                "Platform TTS via ScreenReaderBridge (macOS: say, Windows: SAPI5)");
            DrawChecklistItem("Remappable Controls", true,
                "Full key remapping via KeybindService");
            DrawChecklistItem("One-Handed Presets", true,
                "Left-hand and right-hand keybind profiles");
            DrawChecklistItem("Hold-to-Toggle", true,
                "Sprint, Aim, Crouch configurable as toggle instead of hold");
            DrawChecklistItem("Input Timing Adjustments", true,
                "Configurable double-tap window, hold threshold, input buffer");
            DrawChecklistItem("Aim Assist", true,
                "Configurable strength scaling targeting cone and magnetism");
            DrawChecklistItem("Difficulty Modifiers", true,
                "Independent enemy HP, damage, timing, resource multipliers");
            DrawChecklistItem("Subtitles & Captions", true,
                "Directional subtitles with configurable size and background opacity");
            DrawChecklistItem("Sound Radar / Visual Audio", true,
                "HUD radar showing sound source directions");
            DrawChecklistItem("Mono Audio", true,
                "Stereo-to-mono downmix via AudioMixer");
            DrawChecklistItem("Reduced Motion", true,
                "Camera shake/bob reduction via MotionIntensitySettings");
            DrawChecklistItem("Simplified HUD", true,
                "Hide non-essential HUD elements");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("15 / 15 features implemented", EditorStyles.boldLabel);
        }

        private void DrawChecklistItem(string feature, bool implemented, string description)
        {
            EditorGUILayout.BeginHorizontal();
            var icon = implemented ? "\u2713" : "\u2717"; // ✓ or ✗
            var style = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                fontStyle = implemented ? FontStyle.Normal : FontStyle.Bold
            };
            string color = implemented ? "green" : "red";
            EditorGUILayout.LabelField($"<color={color}>{icon}</color> {feature}", style, GUILayout.Width(250));
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();
        }

        // ── Tab 4: Difficulty Tuner ─────────────────────────────────

        private void DrawDifficultyTuner()
        {
            EditorGUILayout.LabelField("Difficulty Modifier Tuner", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Preview difficulty modifier values. In Play Mode with AccessibilityService active, " +
                "these are synced to the ECS DifficultyModifiers singleton.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            // Load current values from service if available
            if (Application.isPlaying && AccessibilityService.HasInstance)
            {
                if (GUILayout.Button("Load from AccessibilityService"))
                {
                    var svc = AccessibilityService.Instance;
                    _tunerEnemyHP = svc.EnemyHPMultiplier;
                    _tunerEnemyDmg = svc.EnemyDamageMultiplier;
                    _tunerTiming = svc.TimingWindowMultiplier;
                    _tunerResource = svc.ResourceGainMultiplier;
                    _tunerRespawn = svc.RespawnPenalty;
                }
            }

            EditorGUILayout.Space(4);

            _tunerEnemyHP = EditorGUILayout.Slider("Enemy HP Multiplier", _tunerEnemyHP, 0.25f, 2f);
            _tunerEnemyDmg = EditorGUILayout.Slider("Enemy Damage Multiplier", _tunerEnemyDmg, 0.25f, 2f);
            _tunerTiming = EditorGUILayout.Slider("Timing Window Multiplier", _tunerTiming, 0.5f, 2f);
            _tunerResource = EditorGUILayout.Slider("Resource Gain Multiplier", _tunerResource, 0.5f, 3f);
            _tunerRespawn = (RespawnPenalty)EditorGUILayout.EnumPopup("Respawn Penalty", _tunerRespawn);

            EditorGUILayout.Space(8);

            // Projected values
            EditorGUILayout.LabelField("Projected Values", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Enemy with 100 HP", $"→ {100f * _tunerEnemyHP:F0} HP");
            EditorGUILayout.LabelField("Attack dealing 25 damage", $"→ {25f * _tunerEnemyDmg:F1} damage");
            EditorGUILayout.LabelField("Dodge window 0.3s", $"→ {0.3f * _tunerTiming:F2}s");
            EditorGUILayout.LabelField("XP reward 100", $"→ {100f * _tunerResource:F0} XP");
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(8);

            if (Application.isPlaying && AccessibilityService.HasInstance)
            {
                if (GUILayout.Button("Apply to AccessibilityService"))
                {
                    var svc = AccessibilityService.Instance;
                    svc.SetEnemyHPMultiplier(_tunerEnemyHP);
                    svc.SetEnemyDamageMultiplier(_tunerEnemyDmg);
                    svc.SetTimingWindowMultiplier(_tunerTiming);
                    svc.SetResourceGainMultiplier(_tunerResource);
                    svc.SetRespawnPenalty(_tunerRespawn);
                    Debug.Log("[AccessibilityWorkstation] Difficulty modifiers applied.");
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Enter Play Mode to apply difficulty modifiers live.", MessageType.Warning);
            }
        }
    }
}
#endif
