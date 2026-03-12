using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DIG.Replay.Editor
{
    /// <summary>
    /// EPIC 18.10: Replay Workstation editor window.
    /// Tabs: Setup Wizard, Browser (list/manage .digreplay files), Recording Config (edit ReplayConfigSO),
    /// Camera Presets (manage CameraPresetSO assets).
    /// </summary>
    public class ReplayWorkstationWindow : EditorWindow
    {
        private int _selectedTab;
        private static readonly string[] TabNames =
            { "Setup Wizard", "Browser", "Recording Config", "Camera Presets" };

        // Setup Wizard state
        private Vector2 _wizardScroll;
        private bool _hasConfig;
        private bool _hasPreset;
        private bool _hasPrefab;
        private ReplayConfigSO _wizardConfig;
        private CameraPresetSO _wizardPreset;

        // Browser state
        private Vector2 _browserScroll;
        private string[] _replayFiles;
        private DateTime _lastRefresh;

        // Config state
        private ReplayConfigSO _configAsset;
        private SerializedObject _configSO;

        // Preset state
        private Vector2 _presetScroll;
        private CameraPresetSO[] _presets;

        // Paths
        private const string ResourcesDir = "Assets/Resources";
        private const string PrefabDir = "Assets/Prefabs/Replay";
        private const string ConfigPath = "Assets/Resources/ReplayConfig.asset";
        private const string DefaultPresetPath = "Assets/Resources/DefaultCameraPreset.asset";
        private const string PrefabPath = "Assets/Prefabs/Replay/ReplaySystem.prefab";

        [MenuItem("DIG/Replay Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<ReplayWorkstationWindow>("Replay Workstation");
            window.minSize = new Vector2(600, 450);
        }

        private void OnEnable()
        {
            RefreshReplayFiles();
            FindConfigAsset();
            RefreshPresets();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // Sidebar
            EditorGUILayout.BeginVertical("box", GUILayout.Width(140), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("Modules", EditorStyles.boldLabel);
            _selectedTab = GUILayout.SelectionGrid(_selectedTab, TabNames, 1);
            EditorGUILayout.EndVertical();

            // Content
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            switch (_selectedTab)
            {
                case 0: DrawSetupWizard(); break;
                case 1: DrawBrowser(); break;
                case 2: DrawRecordingConfig(); break;
                case 3: DrawCameraPresets(); break;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        // =============== SETUP WIZARD ===============

        private void DrawSetupWizard()
        {
            EditorGUILayout.LabelField("Setup Wizard", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Use this wizard to create the required assets for the Replay & Spectator system. " +
                "Green items are ready. Red items need to be created.",
                MessageType.Info);
            EditorGUILayout.Space(8);

            RefreshWizardStatus();

            _wizardScroll = EditorGUILayout.BeginScrollView(_wizardScroll);

            // === 1. ReplayConfigSO ===
            DrawStatusHeader("Replay Config", _hasConfig, "Resources/ReplayConfig");

            if (_hasConfig)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.ObjectField("Asset", _wizardConfig, typeof(ReplayConfigSO), false);
                EditorGUILayout.LabelField(
                    $"Tick Interval: {_wizardConfig.TickInterval}  |  " +
                    $"Max Duration: {_wizardConfig.MaxDurationMinutes} min  |  " +
                    $"Delta: {(_wizardConfig.DeltaEncoding ? "ON" : "OFF")}",
                    EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            else
            {
                if (GUILayout.Button("Create Replay Config", GUILayout.Height(24)))
                    WizardCreateConfig();
            }

            EditorGUILayout.Space(6);

            // === 2. Default Camera Preset ===
            DrawStatusHeader("Default Camera Preset", _hasPreset, "Resources/DefaultCameraPreset");

            if (_hasPreset)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.ObjectField("Asset", _wizardPreset, typeof(CameraPresetSO), false);
                EditorGUILayout.LabelField(
                    $"Mode: {_wizardPreset.Mode}  |  Speed: {_wizardPreset.MoveSpeed}  |  FOV: {_wizardPreset.FOV}",
                    EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            else
            {
                if (GUILayout.Button("Create Default Camera Preset", GUILayout.Height(24)))
                    WizardCreatePreset();
            }

            EditorGUILayout.Space(6);

            // === 3. ReplaySystem Prefab ===
            DrawStatusHeader("[ReplaySystem] Prefab", _hasPrefab, "Prefabs/Replay/ReplaySystem");

            if (_hasPrefab)
            {
                EditorGUI.indentLevel++;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);

                // Show component status
                if (prefab != null)
                {
                    bool hasPlayer = prefab.GetComponent<ReplayPlayer>() != null;
                    bool hasController = prefab.GetComponent<ReplayTimelineController>() != null;
                    bool hasView = prefab.GetComponent<UI.ReplayTimelineView>() != null;
                    bool hasCam = prefab.GetComponent<SpectatorCamera>() != null;

                    DrawComponentStatus("ReplayPlayer", hasPlayer);
                    DrawComponentStatus("ReplayTimelineController", hasController);
                    DrawComponentStatus("ReplayTimelineView", hasView);
                    DrawComponentStatus("SpectatorCamera", hasCam);
                }
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Creates a [ReplaySystem] prefab with all required components:\n" +
                    "ReplayPlayer, ReplayTimelineController, ReplayTimelineView, SpectatorCamera.\n" +
                    "Drag this prefab into your boot scene for replay/spectator support.",
                    MessageType.None);
                if (GUILayout.Button("Create [ReplaySystem] Prefab", GUILayout.Height(24)))
                    WizardCreatePrefab();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(12);

            // === Create All button ===
            int missing = (_hasConfig ? 0 : 1) + (_hasPreset ? 0 : 1) + (_hasPrefab ? 0 : 1);
            EditorGUI.BeginDisabledGroup(missing == 0);
            if (GUILayout.Button($"Create All Missing ({missing} remaining)", GUILayout.Height(32)))
                WizardCreateAll();
            EditorGUI.EndDisabledGroup();

            if (missing == 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "All assets are created. Drag the [ReplaySystem] prefab into your boot scene.\n" +
                    "Use the other workstation tabs to manage replays, edit config, and configure camera presets.",
                    MessageType.None);
            }
        }

        private void RefreshWizardStatus()
        {
            _wizardConfig = Resources.Load<ReplayConfigSO>("ReplayConfig");
            _hasConfig = _wizardConfig != null;

            // Check for any CameraPresetSO — prefer the default one
            _wizardPreset = AssetDatabase.LoadAssetAtPath<CameraPresetSO>(DefaultPresetPath);
            if (_wizardPreset == null)
            {
                var guids = AssetDatabase.FindAssets("t:CameraPresetSO");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _wizardPreset = AssetDatabase.LoadAssetAtPath<CameraPresetSO>(path);
                }
            }
            _hasPreset = _wizardPreset != null;

            _hasPrefab = File.Exists(
                Path.Combine(Application.dataPath, "../", PrefabPath));
        }

        private static void DrawStatusHeader(string label, bool exists, string location)
        {
            EditorGUILayout.BeginHorizontal();
            var icon = exists ? EditorGUIUtility.IconContent("TestPassed")
                              : EditorGUIUtility.IconContent("TestFailed");
            GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(location, EditorStyles.miniLabel, GUILayout.Width(250));
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawComponentStatus(string componentName, bool exists)
        {
            EditorGUILayout.BeginHorizontal();
            var icon = exists ? EditorGUIUtility.IconContent("TestPassed")
                              : EditorGUIUtility.IconContent("TestFailed");
            GUILayout.Label(icon, GUILayout.Width(16), GUILayout.Height(16));
            EditorGUILayout.LabelField(componentName, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void WizardCreateConfig()
        {
            EnsureDirectory(ResourcesDir);

            var config = CreateInstance<ReplayConfigSO>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
            Debug.Log("[ReplayWorkstation] Created ReplayConfig at " + ConfigPath);

            // Also update the config tab state
            _configAsset = config;
            _configSO = new SerializedObject(_configAsset);
        }

        private void WizardCreatePreset()
        {
            EnsureDirectory(ResourcesDir);

            var preset = CreateInstance<CameraPresetSO>();
            preset.PresetName = "Default";
            AssetDatabase.CreateAsset(preset, DefaultPresetPath);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(preset);
            Debug.Log("[ReplayWorkstation] Created Default Camera Preset at " + DefaultPresetPath);

            RefreshPresets();
        }

        private void WizardCreatePrefab()
        {
            EnsureDirectory(PrefabDir);

            var root = new GameObject("[ReplaySystem]");

            // Add all required components
            root.AddComponent<ReplayPlayer>();
            root.AddComponent<ReplayTimelineController>();
            root.AddComponent<UI.ReplayTimelineView>();
            var cam = root.AddComponent<SpectatorCamera>();

            // Save as prefab first
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            DestroyImmediate(root);

            // Wire serialized fields via SerializedObject
            WirePrefabReferences(prefab);

            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[ReplayWorkstation] Created [ReplaySystem] prefab at " + PrefabPath);
        }

        private void WirePrefabReferences(GameObject prefab)
        {
            if (prefab == null) return;

            // Wire SpectatorCamera._defaultPreset if we have one
            var spectatorCam = prefab.GetComponent<SpectatorCamera>();
            if (spectatorCam != null)
            {
                var camSO = new SerializedObject(spectatorCam);
                var presetProp = camSO.FindProperty("_defaultPreset");
                if (presetProp != null)
                {
                    // Use the wizard preset or find any existing preset
                    var preset = AssetDatabase.LoadAssetAtPath<CameraPresetSO>(DefaultPresetPath);
                    if (preset == null)
                    {
                        var guids = AssetDatabase.FindAssets("t:CameraPresetSO");
                        if (guids.Length > 0)
                        {
                            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                            preset = AssetDatabase.LoadAssetAtPath<CameraPresetSO>(path);
                        }
                    }

                    if (preset != null)
                    {
                        presetProp.objectReferenceValue = preset;
                        camSO.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            EditorUtility.SetDirty(prefab);
        }

        private void WizardCreateAll()
        {
            if (!_hasConfig) WizardCreateConfig();
            if (!_hasPreset) WizardCreatePreset();
            if (!_hasPrefab) WizardCreatePrefab();

            // Re-wire prefab in case preset was just created
            if (_hasPrefab || File.Exists(Path.Combine(Application.dataPath, "../", PrefabPath)))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab != null)
                    WirePrefabReferences(prefab);
            }

            AssetDatabase.Refresh();
        }

        // =============== BROWSER ===============

        private void DrawBrowser()
        {
            EditorGUILayout.LabelField("Replay Browser", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                RefreshReplayFiles();

            string replayDir = Path.Combine(Application.persistentDataPath, "Replays");
            if (GUILayout.Button("Open Folder", GUILayout.Width(100)))
            {
                if (Directory.Exists(replayDir))
                    EditorUtility.RevealInFinder(replayDir);
                else
                    EditorUtility.DisplayDialog("Replay Browser", "No Replays directory found yet.", "OK");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (_replayFiles == null || _replayFiles.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No .digreplay files found.\n" +
                    $"Directory: {replayDir}\n" +
                    "Start a recording in Play mode to generate replay files.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"{_replayFiles.Length} replay file(s) found:");
            EditorGUILayout.Space(2);

            _browserScroll = EditorGUILayout.BeginScrollView(_browserScroll);
            foreach (var file in _replayFiles)
            {
                EditorGUILayout.BeginHorizontal("box");

                string fileName = Path.GetFileName(file);
                var fileInfo = new FileInfo(file);

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(fileName, EditorStyles.boldLabel);

                string size = fileInfo.Length > 1024 * 1024
                    ? $"{fileInfo.Length / (1024f * 1024f):F1} MB"
                    : $"{fileInfo.Length / 1024f:F1} KB";
                EditorGUILayout.LabelField($"Size: {size}  |  Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm}");

                // Try to read header for metadata
                DrawFileMetadata(file);

                EditorGUILayout.EndVertical();

                if (GUILayout.Button("Delete", GUILayout.Width(60), GUILayout.Height(40)))
                {
                    if (EditorUtility.DisplayDialog("Delete Replay",
                        $"Delete {fileName}?", "Delete", "Cancel"))
                    {
                        File.Delete(file);
                        RefreshReplayFiles();
                        GUIUtility.ExitGUI();
                    }
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawFileMetadata(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs);

                if (fs.Length < 64) return;

                uint magic = br.ReadUInt32();
                if (magic != ReplayFileHeader.MagicValue) return;

                ushort version = br.ReadUInt16();
                ushort tickRate = br.ReadUInt16();
                long timestamp = br.ReadInt64();
                float duration = br.ReadSingle();
                uint totalFrames = br.ReadUInt32();
                ushort peakEntities = br.ReadUInt16();
                byte playerCount = br.ReadByte();

                string time = TimeSpan.FromSeconds(duration).ToString(@"mm\:ss");
                var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;

                EditorGUILayout.LabelField(
                    $"v{version}  |  {time}  |  {totalFrames} frames  |  " +
                    $"{peakEntities} entities  |  {playerCount} players  |  " +
                    $"Recorded: {date:yyyy-MM-dd HH:mm}",
                    EditorStyles.miniLabel);
            }
            catch
            {
                // Silently ignore malformed files
            }
        }

        private void RefreshReplayFiles()
        {
            string dir = Path.Combine(Application.persistentDataPath, "Replays");
            _replayFiles = Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*.digreplay")
                : Array.Empty<string>();
            Array.Sort(_replayFiles, (a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));
            _lastRefresh = DateTime.Now;
        }

        // =============== RECORDING CONFIG ===============

        private void DrawRecordingConfig()
        {
            EditorGUILayout.LabelField("Recording Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (_configAsset == null)
            {
                EditorGUILayout.HelpBox(
                    "No ReplayConfigSO found in the project.\n" +
                    "Create one via: Create > DIG > Replay > Replay Config\n" +
                    "Place it in a Resources/ folder named 'ReplayConfig' for auto-loading.",
                    MessageType.Warning);

                if (GUILayout.Button("Create Default Config"))
                {
                    CreateDefaultConfig();
                }
                return;
            }

            if (_configSO == null || _configSO.targetObject != _configAsset)
                _configSO = new SerializedObject(_configAsset);

            _configSO.Update();

            EditorGUILayout.ObjectField("Config Asset", _configAsset, typeof(ReplayConfigSO), false);
            EditorGUILayout.Space(4);

            // Draw all serialized properties
            var iterator = _configSO.GetIterator();
            iterator.NextVisible(true); // Skip m_Script
            while (iterator.NextVisible(false))
            {
                EditorGUILayout.PropertyField(iterator, true);
            }

            _configSO.ApplyModifiedProperties();

            // File size estimator
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("File Size Estimator", EditorStyles.boldLabel);
            int entityCount = EditorGUILayout.IntField("Estimated Entity Count", 50);
            float minutes = _configAsset.MaxDurationMinutes;
            int tickRate = 60;
            int interval = _configAsset.TickInterval;

            float totalFrames = minutes * 60f * tickRate / interval;
            float bytesPerEntity = 49f; // EntityComponentData size
            float bytesPerFrame = entityCount * (bytesPerEntity + 5); // +5 for EntitySnapshotHeader
            float totalBytes = totalFrames * bytesPerFrame;

            // Delta encoding reduces by ~70-80%
            if (_configAsset.DeltaEncoding)
                totalBytes *= 0.25f;

            float totalMB = totalBytes / (1024f * 1024f);
            EditorGUILayout.LabelField($"Estimated file size: {totalMB:F1} MB ({totalFrames:N0} frames)");
        }

        private void FindConfigAsset()
        {
            // Try Resources first
            _configAsset = Resources.Load<ReplayConfigSO>("ReplayConfig");

            if (_configAsset == null)
            {
                // Search project
                var guids = AssetDatabase.FindAssets("t:ReplayConfigSO");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _configAsset = AssetDatabase.LoadAssetAtPath<ReplayConfigSO>(path);
                }
            }
        }

        private void CreateDefaultConfig()
        {
            EnsureDirectory(ResourcesDir);

            var config = CreateInstance<ReplayConfigSO>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            _configAsset = config;
            _configSO = new SerializedObject(_configAsset);
        }

        // =============== CAMERA PRESETS ===============

        private void DrawCameraPresets()
        {
            EditorGUILayout.LabelField("Camera Presets", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                RefreshPresets();
            if (GUILayout.Button("Create New Preset", GUILayout.Width(140)))
                CreateNewPreset();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (_presets == null || _presets.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No CameraPresetSO assets found.\n" +
                    "Create one via: Create > DIG > Replay > Camera Preset",
                    MessageType.Info);
                return;
            }

            _presetScroll = EditorGUILayout.BeginScrollView(_presetScroll);
            foreach (var preset in _presets)
            {
                if (preset == null) continue;

                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(preset.PresetName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Mode: {preset.Mode}", GUILayout.Width(120));

                if (GUILayout.Button("Select", GUILayout.Width(60)))
                    Selection.activeObject = preset;
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    if (EditorUtility.DisplayDialog("Delete Preset",
                        $"Delete camera preset '{preset.PresetName}'?", "Delete", "Cancel"))
                    {
                        string path = AssetDatabase.GetAssetPath(preset);
                        AssetDatabase.DeleteAsset(path);
                        RefreshPresets();
                        GUIUtility.ExitGUI();
                    }
                }
                EditorGUILayout.EndHorizontal();

                // Show key fields
                EditorGUILayout.LabelField(
                    $"Speed: {preset.MoveSpeed}  |  FOV: {preset.FOV}  |  " +
                    $"Follow Dist: {preset.FollowDistance}  |  Orbit Speed: {preset.OrbitSpeed}",
                    EditorStyles.miniLabel);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            EditorGUILayout.EndScrollView();
        }

        private void RefreshPresets()
        {
            var guids = AssetDatabase.FindAssets("t:CameraPresetSO");
            _presets = new CameraPresetSO[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _presets[i] = AssetDatabase.LoadAssetAtPath<CameraPresetSO>(path);
            }
        }

        private void CreateNewPreset()
        {
            EnsureDirectory(ResourcesDir);

            var preset = CreateInstance<CameraPresetSO>();
            preset.PresetName = "New Preset";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{ResourcesDir}/CameraPreset.asset");
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = preset;
            RefreshPresets();
        }

        // =============== UTILITIES ===============

        private static void EnsureDirectory(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
