using UnityEditor;
using UnityEngine;
using DIG.DeathCamera;

namespace DIG.DeathCamera.Editor
{
    /// <summary>
    /// EPIC 18.13: Editor window for death camera system.
    /// 4 tabs: Config Editor, Presets, Runtime Debug, Authority Gate.
    /// </summary>
    public class DeathCameraWorkstationWindow : EditorWindow
    {
        private int _selectedTab;
        private static readonly string[] TabNames = { "Config", "Presets", "Runtime", "Authority" };
        private Vector2 _scrollPos;
        private double _nextRepaintTime;
        private const double RepaintInterval = 0.25;

        private DeathCameraConfigSO _selectedConfig;
        private DeathCameraPresetSO _selectedPreset;
        private UnityEditor.Editor _configEditor;
        private UnityEditor.Editor _presetEditor;

        [MenuItem("DIG/Death Camera Workstation")]
        public static void ShowWindow()
        {
            GetWindow<DeathCameraWorkstationWindow>("Death Camera Workstation");
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying && EditorApplication.timeSinceStartup >= _nextRepaintTime)
            {
                _nextRepaintTime = EditorApplication.timeSinceStartup + RepaintInterval;
                Repaint();
            }
        }

        private void OnGUI()
        {
            _selectedTab = GUILayout.Toolbar(_selectedTab, TabNames);
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_selectedTab)
            {
                case 0: DrawConfigEditor(); break;
                case 1: DrawPresets(); break;
                case 2: DrawRuntimeDebug(); break;
                case 3: DrawAuthorityGate(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        // ====================================================================
        // Tab 0: Config Editor
        // ====================================================================

        private void DrawConfigEditor()
        {
            EditorGUILayout.LabelField("Death Camera Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _selectedConfig = (DeathCameraConfigSO)EditorGUILayout.ObjectField(
                "Config Asset", _selectedConfig, typeof(DeathCameraConfigSO), false);

            if (_selectedConfig == null)
            {
                EditorGUILayout.HelpBox(
                    "Select or create a DeathCameraConfigSO asset.\nCreate via: Assets > Create > DIG > Death Camera > Config",
                    MessageType.Info);

                if (GUILayout.Button("Create New Config"))
                {
                    CreateNewConfig();
                }
                return;
            }

            EditorGUILayout.Space(8);

            // Draw the embedded inspector for the SO
            if (_configEditor == null || _configEditor.target != _selectedConfig)
            {
                if (_configEditor != null)
                    DestroyImmediate(_configEditor);
                _configEditor = UnityEditor.Editor.CreateEditor(_selectedConfig);
            }

            _configEditor.OnInspectorGUI();

            EditorGUILayout.Space(8);

            // Phase sequence visualization
            EditorGUILayout.LabelField("Phase Sequence Preview", EditorStyles.boldLabel);
            DrawPhaseSequence(_selectedConfig);

            EditorGUILayout.Space(4);

            // Quick-toggle buttons
            EditorGUILayout.LabelField("Quick Toggles", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawToggleButton("Kill Cam", ref _selectedConfig.KillCamEnabled);
            DrawToggleButton("Death Recap", ref _selectedConfig.DeathRecapEnabled);
            DrawToggleButton("Spectator", ref _selectedConfig.SpectatorEnabled);
            EditorGUILayout.EndHorizontal();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(_selectedConfig);
            }
        }

        // ====================================================================
        // Tab 1: Presets
        // ====================================================================

        private void DrawPresets()
        {
            EditorGUILayout.LabelField("Game Mode Presets", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _selectedPreset = (DeathCameraPresetSO)EditorGUILayout.ObjectField(
                "Preset Asset", _selectedPreset, typeof(DeathCameraPresetSO), false);

            if (_selectedPreset == null)
            {
                EditorGUILayout.HelpBox(
                    "Select or create a DeathCameraPresetSO asset.\nCreate via: Assets > Create > DIG > Death Camera > Preset",
                    MessageType.Info);

                if (GUILayout.Button("Create New Preset"))
                {
                    CreateNewPreset();
                }
                return;
            }

            EditorGUILayout.Space(8);

            if (_presetEditor == null || _presetEditor.target != _selectedPreset)
            {
                if (_presetEditor != null)
                    DestroyImmediate(_presetEditor);
                _presetEditor = UnityEditor.Editor.CreateEditor(_selectedPreset);
            }

            _presetEditor.OnInspectorGUI();

            EditorGUILayout.Space(8);

            // Preview: apply preset to a config and show result
            EditorGUILayout.LabelField("Preset Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Presets apply partial overrides on top of a base config at runtime.\n" +
                "Only fields where Override=true will be applied.",
                MessageType.Info);

            if (_selectedConfig != null)
            {
                if (GUILayout.Button("Preview: Apply to Selected Config"))
                {
                    var preview = Instantiate(_selectedConfig);
                    _selectedPreset.ApplyTo(preview);
                    Debug.Log($"[DeathCamera] Preset '{_selectedPreset.name}' applied to copy of '{_selectedConfig.name}'.\n" +
                        $"Kill Cam: {preview.KillCamEnabled} ({preview.KillCamDuration}s)\n" +
                        $"Spectator: {preview.SpectatorEnabled} (TPS: {preview.AllowTPSOrbit}, Iso: {preview.AllowIsometric}, TD: {preview.AllowTopDown}, IsoRot: {preview.AllowIsometricRotatable}, Free: {preview.AllowFreeCam})");
                    DestroyImmediate(preview);
                }
            }

            if (GUI.changed && _selectedPreset != null)
            {
                EditorUtility.SetDirty(_selectedPreset);
            }
        }

        // ====================================================================
        // Tab 2: Runtime Debug
        // ====================================================================

        private void DrawRuntimeDebug()
        {
            EditorGUILayout.LabelField("Runtime State", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see live runtime data.", MessageType.Info);
                return;
            }

            // Orchestrator state
            EditorGUILayout.LabelField("Orchestrator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Active", DeathCameraOrchestrator.IsActive.ToString());

            if (DeathCameraOrchestrator.IsActive)
            {
                EditorGUILayout.LabelField("Current Phase", DeathCameraOrchestrator.CurrentPhaseType.ToString());
                EditorGUILayout.LabelField("Phase Index", DeathCameraOrchestrator.CurrentPhaseIndex.ToString());
                EditorGUILayout.LabelField("Respawn Timer", $"{DeathCameraOrchestrator.RespawnTimeRemaining:F2}s");

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Context", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Killer", DeathCameraOrchestrator.ContextKillerName ?? "N/A");
                EditorGUILayout.LabelField("Kill Position", DeathCameraOrchestrator.ContextKillPosition.ToString());
                EditorGUILayout.LabelField("Alive Players", DeathCameraOrchestrator.AlivePlayerCount.ToString());

                EditorGUILayout.Space(4);

                // Manual phase advance
                EditorGUILayout.LabelField("Manual Controls", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Skip Phase"))
                {
                    DeathCameraOrchestrator.EditorSkipCurrentPhase();
                }
                if (GUILayout.Button("Force End"))
                {
                    DeathCameraOrchestrator.EditorForceEnd();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8);

            // Camera mode info
            EditorGUILayout.LabelField("Camera", EditorStyles.boldLabel);
            if (DIG.CameraSystem.CameraModeProvider.HasInstance)
            {
                var activeMode = DIG.CameraSystem.CameraModeProvider.Instance.ActiveCamera;
                EditorGUILayout.LabelField("Active Camera Mode", activeMode != null ? activeMode.GetType().Name : "None");
            }
            else
            {
                EditorGUILayout.LabelField("CameraModeProvider", "Not available");
            }

            if (DIG.CameraSystem.CameraTransitionManager.HasInstance)
            {
                EditorGUILayout.LabelField("Is Transitioning", DIG.CameraSystem.CameraTransitionManager.Instance.IsTransitioning.ToString());
            }

            // UI state
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("UI", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("DeathRecapView", DeathRecapView.Instance != null ? "Active" : "None");
            EditorGUILayout.LabelField("SpectatorHUDView", SpectatorHUDView.Instance != null ? "Active" : "None");
        }

        // ====================================================================
        // Tab 3: Authority Gate
        // ====================================================================

        private void DrawAuthorityGate()
        {
            EditorGUILayout.LabelField("Camera Authority Gate", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Current state
            var overridden = CameraAuthorityGate.IsOverridden;
            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = overridden ? new Color(1f, 0.3f, 0.3f) : new Color(0.3f, 1f, 0.3f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = prevColor;

            EditorGUILayout.LabelField("Status", overridden ? "OVERRIDDEN" : "NORMAL", EditorStyles.boldLabel);

            if (overridden)
            {
                EditorGUILayout.LabelField("Owner", CameraAuthorityGate.CurrentOwner ?? "Unknown");
                EditorGUILayout.LabelField("Priority", CameraAuthorityGate.CurrentPriority.ToString());
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // Priority reference
            EditorGUILayout.LabelField("Priority Levels", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("0", "Gameplay (normal cameras)");
            EditorGUILayout.LabelField("10", "Death Camera");
            EditorGUILayout.LabelField("20", "Cutscene");
            EditorGUILayout.LabelField("30", "Editor / Debug");

            EditorGUILayout.Space(8);

            // Manual controls
            EditorGUILayout.LabelField("Manual Controls", EditorStyles.boldLabel);

            if (overridden)
            {
                if (GUILayout.Button("Force Release"))
                {
                    CameraAuthorityGate.ForceRelease();
                    Debug.Log("[DeathCamera] Authority force-released from editor.");
                }
            }
            else
            {
                if (GUILayout.Button("Test Acquire (Editor, Priority 30)"))
                {
                    bool result = CameraAuthorityGate.Acquire("EditorDebug", 30);
                    Debug.Log($"[DeathCamera] Test acquire: {result}");
                }
            }
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private static void DrawPhaseSequence(DeathCameraConfigSO config)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("Death", GUILayout.Width(40));

            for (int i = 0; i < config.PhaseSequence.Length; i++)
            {
                var phase = config.PhaseSequence[i];
                bool enabled = config.IsPhaseEnabled(phase);

                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = enabled ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.5f, 0.5f, 0.5f);

                GUILayout.Button(phase.ToString(), GUILayout.Width(100));

                GUI.backgroundColor = prevBg;

                if (i < config.PhaseSequence.Length - 1)
                    EditorGUILayout.LabelField("\u2192", GUILayout.Width(20));
            }

            EditorGUILayout.LabelField("\u2192", GUILayout.Width(20));
            EditorGUILayout.LabelField("Respawn", GUILayout.Width(60));

            EditorGUILayout.EndHorizontal();
        }

        private static void DrawToggleButton(string label, ref bool value)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = value ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.8f, 0.3f, 0.3f);
            if (GUILayout.Button(value ? $"{label}: ON" : $"{label}: OFF"))
            {
                value = !value;
            }
            GUI.backgroundColor = prev;
        }

        private static void CreateNewConfig()
        {
            var asset = CreateInstance<DeathCameraConfigSO>();
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Death Camera Config", "DeathCameraConfig", "asset", "Choose save location");
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = asset;
            }
        }

        private static void CreateNewPreset()
        {
            var asset = CreateInstance<DeathCameraPresetSO>();
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Death Camera Preset", "DeathCameraPreset", "asset", "Choose save location");
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = asset;
            }
        }

        private void OnDestroy()
        {
            if (_configEditor != null)
                DestroyImmediate(_configEditor);
            if (_presetEditor != null)
                DestroyImmediate(_presetEditor);
        }
    }
}
