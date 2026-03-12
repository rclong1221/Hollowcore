using UnityEditor;
using UnityEngine;
using DIG.CameraSystem.Authoring;
using System.Collections.Generic;

namespace DIG.CameraSystem.Editor
{
    /// <summary>
    /// EPIC 14.9 Phase 4 - Camera System Setup Wizard
    /// Multi-step wizard for configuring the camera system on player prefabs.
    /// </summary>
    public class CameraSetupWizard : EditorWindow
    {
        // Wizard state
        private int _currentStep = 0;
        private const int TOTAL_STEPS = 3;

        // Step 1: Camera Style
        private CameraStyleTemplate _selectedStyle = CameraStyleTemplate.DIG_ThirdPerson;

        // Step 2: Configuration
        private CameraConfig _selectedConfig;

        // Step 3: Player Setup
        private GameObject _playerPrefab;
        private Camera _targetCamera;
        private bool _addAuthoringComponent = true;
        private bool _registerWithProvider = true;

        // Scroll position
        private Vector2 _scrollPos;

        // Validation
        private List<string> _validationMessages = new List<string>();

        public enum CameraStyleTemplate
        {
            DIG_ThirdPerson,
            ARPG_Isometric,
            TopDown,
            RotatableIsometric,
            Custom
        }

        [MenuItem("DIG/Wizards/Camera Setup Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<CameraSetupWizard>("Camera Setup Wizard");
            window.minSize = new Vector2(500, 400);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            DrawHeader();
            EditorGUILayout.Space(10);

            DrawProgressBar();
            EditorGUILayout.Space(20);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_currentStep)
            {
                case 0: DrawStep1_CameraStyle(); break;
                case 1: DrawStep2_Configuration(); break;
                case 2: DrawStep3_PlayerSetup(); break;
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(20);
            DrawNavigationButtons();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Camera System Setup Wizard", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Configure camera system for DIG or ARPG game styles.", EditorStyles.helpBox);
        }

        private void DrawProgressBar()
        {
            EditorGUILayout.BeginHorizontal();
            string[] stepNames = { "Camera Style", "Configuration", "Player Setup" };
            for (int i = 0; i < TOTAL_STEPS; i++)
            {
                var style = i == _currentStep ? EditorStyles.toolbarButton : EditorStyles.miniButtonMid;
                var color = i < _currentStep ? Color.green : (i == _currentStep ? Color.cyan : Color.gray);
                GUI.backgroundColor = color;
                GUILayout.Button(stepNames[i], style, GUILayout.ExpandWidth(true));
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        #region Step 1: Camera Style

        private void DrawStep1_CameraStyle()
        {
            EditorGUILayout.LabelField("Step 1: Select Camera Style", EditorStyles.largeLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Choose the camera style that matches your game. This determines the camera behavior and input transformation.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            _selectedStyle = (CameraStyleTemplate)EditorGUILayout.EnumPopup("Camera Style", _selectedStyle);

            EditorGUILayout.Space(10);

            // Show style description
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            switch (_selectedStyle)
            {
                case CameraStyleTemplate.DIG_ThirdPerson:
                    EditorGUILayout.LabelField("DIG Third-Person (Default)", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("WoW-style orbit camera with mouse look.");
                    EditorGUILayout.LabelField("- Camera follows behind character");
                    EditorGUILayout.LabelField("- Hold RMB to orbit camera");
                    EditorGUILayout.LabelField("- Movement is camera-relative");
                    EditorGUILayout.LabelField("- Aim at screen center");
                    break;

                case CameraStyleTemplate.ARPG_Isometric:
                    EditorGUILayout.LabelField("ARPG Isometric", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Diablo/Hades style fixed-angle camera.");
                    EditorGUILayout.LabelField("- Fixed 45-60 degree angle");
                    EditorGUILayout.LabelField("- No mouse orbit");
                    EditorGUILayout.LabelField("- Movement transformed for isometric view");
                    EditorGUILayout.LabelField("- Aim toward mouse cursor");
                    break;

                case CameraStyleTemplate.TopDown:
                    EditorGUILayout.LabelField("Top-Down", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Near-vertical camera for twin-stick games.");
                    EditorGUILayout.LabelField("- Camera looks straight down (or slight angle)");
                    EditorGUILayout.LabelField("- World-aligned movement (WASD = cardinal directions)");
                    EditorGUILayout.LabelField("- Aim toward mouse cursor");
                    break;

                case CameraStyleTemplate.RotatableIsometric:
                    EditorGUILayout.LabelField("Rotatable Isometric", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Isometric with Q/E rotation support.");
                    EditorGUILayout.LabelField("- Fixed angle isometric view");
                    EditorGUILayout.LabelField("- Press Q/E to rotate camera 45 degrees");
                    EditorGUILayout.LabelField("- Movement follows camera rotation");
                    break;

                case CameraStyleTemplate.Custom:
                    EditorGUILayout.LabelField("Custom", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Create your own camera configuration.");
                    EditorGUILayout.LabelField("- Select or create a CameraConfig asset");
                    EditorGUILayout.LabelField("- Full control over all settings");
                    break;
            }
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Step 2: Configuration

        private void DrawStep2_Configuration()
        {
            EditorGUILayout.LabelField("Step 2: Camera Configuration", EditorStyles.largeLabel);
            EditorGUILayout.Space(10);

            if (_selectedStyle == CameraStyleTemplate.Custom)
            {
                EditorGUILayout.HelpBox(
                    "Select an existing CameraConfig asset or create a new one.",
                    MessageType.Info);

                EditorGUILayout.Space(10);

                _selectedConfig = (CameraConfig)EditorGUILayout.ObjectField(
                    "Camera Config",
                    _selectedConfig,
                    typeof(CameraConfig),
                    false);

                EditorGUILayout.Space(10);

                if (GUILayout.Button("Create New CameraConfig Asset"))
                {
                    string path = EditorUtility.SaveFilePanelInProject(
                        "Create Camera Config",
                        "NewCameraConfig",
                        "asset",
                        "Choose where to save the camera config.");

                    if (!string.IsNullOrEmpty(path))
                    {
                        var newConfig = ScriptableObject.CreateInstance<CameraConfig>();
                        AssetDatabase.CreateAsset(newConfig, path);
                        AssetDatabase.SaveAssets();
                        _selectedConfig = newConfig;
                        Selection.activeObject = newConfig;
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"A preset configuration will be created for {_selectedStyle}.",
                    MessageType.Info);

                EditorGUILayout.Space(10);

                // Show what config will be used/created
                string configName = GetPresetConfigName();
                EditorGUILayout.LabelField("Config Name:", configName);

                // Check if preset already exists
                string configPath = $"{CameraConfigPresetCreator.GetConfigFolder()}/{configName}.asset";
                var existingConfig = AssetDatabase.LoadAssetAtPath<CameraConfig>(configPath);

                if (existingConfig != null)
                {
                    EditorGUILayout.LabelField("Status:", "Using existing preset");
                    _selectedConfig = existingConfig;

                    if (GUILayout.Button("View Config"))
                    {
                        Selection.activeObject = existingConfig;
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Status:", "Will create new preset");

                    if (GUILayout.Button("Create Preset Now"))
                    {
                        _selectedConfig = CreatePresetForStyle(_selectedStyle);
                        Selection.activeObject = _selectedConfig;
                    }
                }
            }

            // Show config preview if we have one
            if (_selectedConfig != null)
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.LabelField("Configuration Preview", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Mode: {_selectedConfig.CameraMode}");
                EditorGUILayout.LabelField($"Follow Smoothing: {_selectedConfig.FollowSmoothing}");
                EditorGUILayout.LabelField($"Zoom Range: {_selectedConfig.ZoomMin} - {_selectedConfig.ZoomMax}");
                EditorGUILayout.LabelField($"FOV: {_selectedConfig.FieldOfView}");
                EditorGUILayout.EndVertical();
            }
        }

        private string GetPresetConfigName()
        {
            switch (_selectedStyle)
            {
                case CameraStyleTemplate.DIG_ThirdPerson:
                    return "CameraConfig_DIG";
                case CameraStyleTemplate.ARPG_Isometric:
                    return "CameraConfig_ARPG";
                case CameraStyleTemplate.TopDown:
                    return "CameraConfig_TopDown";
                case CameraStyleTemplate.RotatableIsometric:
                    return "CameraConfig_RotatableIso";
                default:
                    return "CameraConfig_Custom";
            }
        }

        private CameraConfig CreatePresetForStyle(CameraStyleTemplate style)
        {
            switch (style)
            {
                case CameraStyleTemplate.DIG_ThirdPerson:
                    return CameraConfigPresetCreator.CreateDIGPreset();
                case CameraStyleTemplate.ARPG_Isometric:
                    return CameraConfigPresetCreator.CreateARPGPreset();
                case CameraStyleTemplate.TopDown:
                    return CameraConfigPresetCreator.CreateTopDownPreset();
                case CameraStyleTemplate.RotatableIsometric:
                    return CameraConfigPresetCreator.CreateRotatableIsometricPreset();
                default:
                    return CameraConfigPresetCreator.CreateDIGPreset();
            }
        }

        #endregion

        #region Step 3: Player Setup

        private void DrawStep3_PlayerSetup()
        {
            EditorGUILayout.LabelField("Step 3: Player Prefab Setup", EditorStyles.largeLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Assign your player prefab and camera to complete the setup.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            _playerPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Player Prefab",
                _playerPrefab,
                typeof(GameObject),
                false);

            _targetCamera = (Camera)EditorGUILayout.ObjectField(
                "Target Camera (Optional)",
                _targetCamera,
                typeof(Camera),
                true);

            EditorGUILayout.Space(10);

            _addAuthoringComponent = EditorGUILayout.Toggle("Add CameraSystemAuthoring", _addAuthoringComponent);
            _registerWithProvider = EditorGUILayout.Toggle("Register with CameraModeProvider", _registerWithProvider);

            EditorGUILayout.Space(20);

            // Validation
            _validationMessages.Clear();
            ValidateStep3();

            if (_validationMessages.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Validation Issues:", EditorStyles.boldLabel);
                foreach (var msg in _validationMessages)
                {
                    EditorGUILayout.LabelField("- " + msg, EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);

            // Setup button
            GUI.enabled = _validationMessages.Count == 0 && _playerPrefab != null;
            if (GUILayout.Button("Complete Setup", GUILayout.Height(30)))
            {
                PerformSetup();
            }
            GUI.enabled = true;
        }

        private void ValidateStep3()
        {
            if (_playerPrefab == null)
            {
                _validationMessages.Add("Player prefab is required");
            }
            else
            {
                // Check if it's a prefab
                if (PrefabUtility.GetPrefabAssetType(_playerPrefab) == PrefabAssetType.NotAPrefab)
                {
                    _validationMessages.Add("Selected object is not a prefab");
                }

                // Check if already has authoring
                if (_addAuthoringComponent && _playerPrefab.GetComponent<CameraSystemAuthoring>() != null)
                {
                    _validationMessages.Add("Prefab already has CameraSystemAuthoring (will update config)");
                }
            }

            if (_selectedConfig == null)
            {
                _validationMessages.Add("No camera config selected (go back to Step 2)");
            }
        }

        private void PerformSetup()
        {
            // Ensure config exists
            if (_selectedConfig == null)
            {
                _selectedConfig = CreatePresetForStyle(_selectedStyle);
            }

            // Open prefab for editing
            string prefabPath = AssetDatabase.GetAssetPath(_playerPrefab);
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                // Add or update authoring component
                var authoring = prefabRoot.GetComponent<CameraSystemAuthoring>();
                if (authoring == null && _addAuthoringComponent)
                {
                    authoring = prefabRoot.AddComponent<CameraSystemAuthoring>();
                }

                if (authoring != null)
                {
                    authoring.CameraConfig = _selectedConfig;
                    authoring.RegisterWithProvider = _registerWithProvider;
                    authoring.TargetCamera = _targetCamera;
                    EditorUtility.SetDirty(authoring);
                }

                // Save prefab
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

                EditorUtility.DisplayDialog("Setup Complete",
                    $"Camera system configured successfully!\n\n" +
                    $"Prefab: {_playerPrefab.name}\n" +
                    $"Config: {_selectedConfig.name}\n" +
                    $"Mode: {_selectedConfig.CameraMode}",
                    "OK");

                // Select the config
                Selection.activeObject = _selectedConfig;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        #endregion

        #region Navigation

        private void DrawNavigationButtons()
        {
            EditorGUILayout.BeginHorizontal();

            // Back button
            GUI.enabled = _currentStep > 0;
            if (GUILayout.Button("< Back", GUILayout.Width(100)))
            {
                _currentStep--;
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            // Next button (or skip for final step)
            if (_currentStep < TOTAL_STEPS - 1)
            {
                if (GUILayout.Button("Next >", GUILayout.Width(100)))
                {
                    // Ensure config is created/selected before proceeding from step 2
                    if (_currentStep == 1 && _selectedConfig == null && _selectedStyle != CameraStyleTemplate.Custom)
                    {
                        _selectedConfig = CreatePresetForStyle(_selectedStyle);
                    }
                    _currentStep++;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion
    }
}
