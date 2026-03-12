using System.IO;
using UnityEditor;
using UnityEngine;
using DIG.Persistence;

namespace DIG.Persistence.Editor
{
    /// <summary>
    /// EPIC 16.15: Editor window for inspecting and debugging the persistence system.
    /// 4 tabs: Save Browser, Save Inspector, Migration Tester, Live State.
    /// </summary>
    public class PersistenceWorkstationWindow : EditorWindow
    {
        private int _selectedTab;
        private static readonly string[] TabNames = { "Browser", "Inspector", "Migration", "Live" };
        private Vector2 _scrollPos;
        private double _nextRepaintTime;
        private const double RepaintInterval = 0.5;

        // Module instances
        private SaveBrowserModule _browser;
        private SaveInspectorModule _inspector;
        private MigrationTesterModule _migration;
        private LiveStateModule _liveState;

        [MenuItem("DIG/Persistence Workstation")]
        public static void ShowWindow()
        {
            GetWindow<PersistenceWorkstationWindow>("Persistence Workstation");
        }

        private void OnEnable()
        {
            _browser = new SaveBrowserModule();
            _inspector = new SaveInspectorModule();
            _migration = new MigrationTesterModule();
            _liveState = new LiveStateModule();

            _browser.OnFileSelected += path =>
            {
                _inspector.LoadFile(path);
                _selectedTab = 1; // Switch to Inspector tab
                Repaint();
            };
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
                case 0: _browser?.OnGUI(); break;
                case 1: _inspector?.OnGUI(); break;
                case 2: _migration?.OnGUI(); break;
                case 3: _liveState?.OnGUI(); break;
            }

            EditorGUILayout.EndScrollView();
        }
    }

    /// <summary>
    /// EPIC 16.15: Browse save files on disk. Lists all .dig files in the save directory.
    /// </summary>
    internal class SaveBrowserModule
    {
        public event System.Action<string> OnFileSelected;

        private string _saveDirectory;
        private FileInfo[] _saveFiles;
        private int _selectedIndex = -1;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Save File Browser", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Directory selector
            EditorGUILayout.BeginHorizontal();
            _saveDirectory = EditorGUILayout.TextField("Save Directory", _saveDirectory ?? GetDefaultSaveDir());
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                var path = EditorUtility.OpenFolderPanel("Select Save Directory", _saveDirectory, "");
                if (!string.IsNullOrEmpty(path)) _saveDirectory = path;
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Refresh"))
                RefreshFileList();

            EditorGUILayout.Space(8);

            if (_saveFiles == null || _saveFiles.Length == 0)
            {
                EditorGUILayout.HelpBox("No save files found. Click Refresh to scan the directory.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Found {_saveFiles.Length} save file(s):", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            for (int i = 0; i < _saveFiles.Length; i++)
            {
                var file = _saveFiles[i];
                bool isSelected = i == _selectedIndex;

                EditorGUILayout.BeginHorizontal(isSelected ? "SelectionRect" : "box");

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(file.Name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    $"Size: {FormatFileSize(file.Length)}  |  Modified: {file.LastWriteTime:yyyy-MM-dd HH:mm:ss}",
                    EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                if (GUILayout.Button("Inspect", GUILayout.Width(60), GUILayout.Height(36)))
                {
                    _selectedIndex = i;
                    OnFileSelected?.Invoke(file.FullName);
                }

                if (GUILayout.Button("Delete", GUILayout.Width(50), GUILayout.Height(36)))
                {
                    if (EditorUtility.DisplayDialog("Delete Save File",
                        $"Delete '{file.Name}'? This cannot be undone.", "Delete", "Cancel"))
                    {
                        file.Delete();
                        // Also delete sidecar
                        var sidecar = Path.ChangeExtension(file.FullName, ".json");
                        if (File.Exists(sidecar)) File.Delete(sidecar);
                        RefreshFileList();
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            // Show JSON sidecar preview for selected file
            if (_selectedIndex >= 0 && _selectedIndex < _saveFiles.Length)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Metadata (JSON Sidecar)", EditorStyles.boldLabel);
                var jsonPath = Path.ChangeExtension(_saveFiles[_selectedIndex].FullName, ".json");
                if (File.Exists(jsonPath))
                {
                    var json = File.ReadAllText(jsonPath);
                    EditorGUILayout.TextArea(json, GUILayout.Height(120));
                }
                else
                {
                    EditorGUILayout.HelpBox("No JSON sidecar found for this save file.", MessageType.Warning);
                }
            }
        }

        private void RefreshFileList()
        {
            if (string.IsNullOrEmpty(_saveDirectory) || !Directory.Exists(_saveDirectory))
            {
                _saveFiles = new FileInfo[0];
                return;
            }
            var dir = new DirectoryInfo(_saveDirectory);
            _saveFiles = dir.GetFiles("*.dig");
            System.Array.Sort(_saveFiles, (a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
            _selectedIndex = -1;
        }

        private static string GetDefaultSaveDir()
        {
            return Path.Combine(Application.persistentDataPath, "saves");
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / 1048576.0:F2} MB";
        }
    }

    /// <summary>
    /// EPIC 16.15: Inspect the binary contents of a save file.
    /// Reads header, validates CRC32, lists module blocks.
    /// </summary>
    internal class SaveInspectorModule
    {
        private string _loadedPath;
        private string _headerInfo;
        private string _moduleInfo;
        private bool _crcValid;
        private bool _loaded;

        public void LoadFile(string path)
        {
            _loadedPath = path;
            _loaded = false;
            _headerInfo = "";
            _moduleInfo = "";

            if (!File.Exists(path)) return;

            try
            {
                var bytes = File.ReadAllBytes(path);
                ParseHeader(bytes);
                _loaded = true;
            }
            catch (System.Exception e)
            {
                _headerInfo = $"Error reading file: {e.Message}";
            }
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Save File Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!_loaded)
            {
                EditorGUILayout.HelpBox("Select a file from the Browser tab to inspect it.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("File:", _loadedPath, EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            // CRC status
            var crcStyle = new GUIStyle(EditorStyles.boldLabel);
            crcStyle.normal.textColor = _crcValid ? Color.green : Color.red;
            EditorGUILayout.LabelField("CRC32:", _crcValid ? "VALID" : "INVALID / CORRUPTED", crcStyle);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Header", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(_headerInfo, GUILayout.Height(100));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Module Blocks", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(_moduleInfo, GUILayout.Height(200));
        }

        private void ParseHeader(byte[] data)
        {
            if (data.Length < SaveBinaryConstants.HeaderSize)
            {
                _headerInfo = $"File too small ({data.Length} bytes, need {SaveBinaryConstants.HeaderSize})";
                return;
            }

            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            var magic = reader.ReadUInt32();
            var magicStr = magic == SaveBinaryConstants.MagicPlayer ? "DIGS (Player)" :
                           magic == SaveBinaryConstants.MagicWorld ? "DIGW (World)" :
                           $"UNKNOWN (0x{magic:X8})";

            var version = reader.ReadInt32();
            var timestamp = reader.ReadInt64();
            var playerNameBytes = reader.ReadBytes(64);
            var playerName = System.Text.Encoding.UTF8.GetString(playerNameBytes).TrimEnd('\0');
            var storedCrc = reader.ReadUInt32();

            // Validate CRC
            _crcValid = SaveFileReader.ValidateCRC32(data);

            var moduleCount = reader.ReadUInt16();

            _headerInfo = $"Magic: {magicStr}\n" +
                          $"Format Version: {version}\n" +
                          $"Timestamp: {System.DateTimeOffset.FromUnixTimeSeconds(timestamp):yyyy-MM-dd HH:mm:ss UTC}\n" +
                          $"Player Name: \"{playerName}\"\n" +
                          $"Stored CRC32: 0x{storedCrc:X8}\n" +
                          $"Module Count: {moduleCount}";

            // Parse modules
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < moduleCount; i++)
            {
                if (ms.Position + SaveBinaryConstants.ModuleBlockHeaderSize > data.Length) break;

                var typeId = reader.ReadInt32();
                var moduleVersion = reader.ReadInt32();
                var dataLen = reader.ReadUInt16();

                var typeName = GetModuleName(typeId);
                sb.AppendLine($"[{i}] {typeName} (TypeId={typeId}, V{moduleVersion}, {dataLen} bytes)");

                // Skip data
                if (ms.Position + dataLen > data.Length) break;
                ms.Position += dataLen;
            }

            // Check EOF
            if (ms.Position + 4 <= data.Length)
            {
                var eof = reader.ReadUInt32();
                sb.AppendLine(eof == SaveBinaryConstants.EOFMarker
                    ? "\nEOF Marker: VALID (DEND)"
                    : $"\nEOF Marker: INVALID (0x{eof:X8})");
            }

            _moduleInfo = sb.ToString();
        }

        private static string GetModuleName(int typeId) => typeId switch
        {
            SaveModuleTypeIds.PlayerStats => "PlayerStats",
            SaveModuleTypeIds.Inventory => "Inventory",
            SaveModuleTypeIds.Equipment => "Equipment",
            SaveModuleTypeIds.Quests => "Quests",
            SaveModuleTypeIds.Crafting => "Crafting",
            SaveModuleTypeIds.World => "World",
            SaveModuleTypeIds.Settings => "Settings",
            SaveModuleTypeIds.StatusEffects => "StatusEffects",
            SaveModuleTypeIds.Survival => "Survival",
            SaveModuleTypeIds.Progression => "Progression",
            _ => $"Unknown({typeId})"
        };
    }

    /// <summary>
    /// EPIC 16.15: Test migration steps on save files.
    /// </summary>
    internal class MigrationTesterModule
    {
        private string _inputPath;
        private string _resultLog;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Migration Tester", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "Select a save file to test version migration.\n" +
                "This reads the file version and applies all registered IMigrationStep instances.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            _inputPath = EditorGUILayout.TextField("Save File", _inputPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFilePanel("Select Save File", "", "dig");
                if (!string.IsNullOrEmpty(path)) _inputPath = path;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Test Migration (Dry Run)"))
                RunMigrationTest(dryRun: true);
            if (GUILayout.Button("Apply Migration"))
                RunMigrationTest(dryRun: false);
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_resultLog))
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_resultLog, GUILayout.Height(200));
            }
        }

        private void RunMigrationTest(bool dryRun)
        {
            if (string.IsNullOrEmpty(_inputPath) || !File.Exists(_inputPath))
            {
                _resultLog = "Error: File not found.";
                return;
            }

            try
            {
                var bytes = File.ReadAllBytes(_inputPath);
                if (bytes.Length < SaveBinaryConstants.HeaderSize)
                {
                    _resultLog = "Error: File too small to be a valid save.";
                    return;
                }

                using var ms = new MemoryStream(bytes);
                using var reader = new BinaryReader(ms);
                reader.ReadUInt32(); // magic
                var fileVersion = reader.ReadInt32();

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"File Version: {fileVersion}");
                sb.AppendLine($"Target Version: {SaveBinaryConstants.CurrentFormatVersion}");
                sb.AppendLine($"Mode: {(dryRun ? "Dry Run" : "Apply")}");
                sb.AppendLine();

                if (fileVersion >= SaveBinaryConstants.CurrentFormatVersion)
                {
                    sb.AppendLine("File is already at or above target version. No migration needed.");
                }
                else
                {
                    var migrated = SaveMigrationRunner.MigrateToLatest(bytes, fileVersion, SaveBinaryConstants.CurrentFormatVersion);
                    sb.AppendLine($"Migration complete. Output size: {migrated.Length} bytes (was {bytes.Length}).");

                    if (!dryRun)
                    {
                        File.WriteAllBytes(_inputPath, migrated);
                        sb.AppendLine("File written successfully.");
                    }
                    else
                    {
                        sb.AppendLine("Dry run — no changes written.");
                    }
                }

                _resultLog = sb.ToString();
            }
            catch (System.Exception e)
            {
                _resultLog = $"Error: {e.Message}\n{e.StackTrace}";
            }
        }
    }

    /// <summary>
    /// EPIC 16.15: Live state view — shows in-play persistence state.
    /// </summary>
    internal class LiveStateModule
    {
        public void OnGUI()
        {
            EditorGUILayout.LabelField("Live Persistence State", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see live persistence state.", MessageType.Info);
                return;
            }

            // Find the server/local world
            Unity.Entities.World targetWorld = null;
            foreach (var world in Unity.Entities.World.All)
            {
                if ((world.Flags & Unity.Entities.WorldFlags.GameServer) != 0 ||
                    world.Name.Contains("Server") || world.Name.Contains("Local"))
                {
                    targetWorld = world;
                    break;
                }
            }

            if (targetWorld == null)
            {
                EditorGUILayout.HelpBox("No Server or Local world found.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField($"World: {targetWorld.Name}", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            // Try to find SaveManagerSingleton
            var em = targetWorld.EntityManager;
            var query = em.CreateEntityQuery(typeof(SaveManagerSingleton));
            if (query.CalculateEntityCount() == 0)
            {
                EditorGUILayout.HelpBox("SaveManagerSingleton not found. Is PersistenceBootstrapSystem running?", MessageType.Warning);
                return;
            }

            var entity = query.GetSingletonEntity();
            var manager = em.GetComponentObject<SaveManagerSingleton>(entity);

            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Save Directory: {manager.SaveDirectory}");
            EditorGUILayout.LabelField($"Initialized: {manager.IsInitialized}");
            if (manager.Config != null)
            {
                EditorGUILayout.LabelField($"Slot Count: {manager.Config.SaveSlotCount}");
                EditorGUILayout.LabelField($"Autosave Slot: {manager.Config.AutosaveSlot}");
                EditorGUILayout.LabelField($"Autosave Interval: {manager.Config.AutosaveIntervalSeconds}s");
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Elapsed Playtime: {manager.ElapsedPlaytime:F1}s");
            EditorGUILayout.LabelField($"Time Since Last Save: {manager.TimeSinceLastSave:F1}s");
            EditorGUILayout.LabelField($"Time Since Last Checkpoint: {manager.TimeSinceLastCheckpoint:F1}s");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Registered Modules", EditorStyles.boldLabel);
            if (manager.RegisteredModules != null)
            {
                foreach (var module in manager.RegisteredModules)
                {
                    EditorGUILayout.BeginHorizontal("box");
                    EditorGUILayout.LabelField($"[{module.TypeId}] {module.DisplayName}", GUILayout.Width(200));
                    EditorGUILayout.LabelField($"V{module.ModuleVersion}", GUILayout.Width(40));
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Save State Entities", EditorStyles.boldLabel);
            var saveStateQuery = em.CreateEntityQuery(typeof(SaveStateTag));
            var count = saveStateQuery.CalculateEntityCount();
            EditorGUILayout.LabelField($"Active SaveState children: {count}");

            // Quick actions
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Force Save (Slot 0)"))
            {
                var reqEntity = em.CreateEntity();
                em.AddComponentData(reqEntity, new SaveRequest
                {
                    SlotIndex = 0,
                    TriggerSource = SaveTriggerSource.Manual
                });
                Debug.Log("[Persistence Editor] Manual save triggered for slot 0.");
            }
            if (GUILayout.Button("Force Load (Slot 0)"))
            {
                var reqEntity = em.CreateEntity();
                em.AddComponentData(reqEntity, new LoadRequest { SlotIndex = 0 });
                Debug.Log("[Persistence Editor] Manual load triggered for slot 0.");
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Open Save Directory"))
            {
                if (Directory.Exists(manager.SaveDirectory))
                    EditorUtility.RevealInFinder(manager.SaveDirectory);
                else
                    Debug.LogWarning($"[Persistence Editor] Directory does not exist: {manager.SaveDirectory}");
            }
        }
    }
}
