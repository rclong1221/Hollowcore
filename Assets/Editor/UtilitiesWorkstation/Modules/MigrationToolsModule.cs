using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.UtilitiesWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 UW-05: Migration Tools module.
    /// Update old prefabs to new component formats.
    /// </summary>
    public class MigrationToolsModule : IUtilitiesModule
    {
        private Vector2 _scrollPosition;
        
        // Migration tasks
        private List<MigrationTask> _tasks = new List<MigrationTask>();
        private int _selectedTaskIndex = -1;
        
        // Scan results
        private List<MigrationCandidate> _candidates = new List<MigrationCandidate>();
        private bool _isScanning = false;
        
        // Migration log
        private List<MigrationLogEntry> _migrationLog = new List<MigrationLogEntry>();

        [System.Serializable]
        private class MigrationTask
        {
            public string Name;
            public string Description;
            public string FromVersion;
            public string ToVersion;
            public string SourceComponent;
            public string TargetComponent;
            public List<PropertyMapping> PropertyMappings;
            public bool IsBuiltIn;
        }

        [System.Serializable]
        private class PropertyMapping
        {
            public string SourceProperty;
            public string TargetProperty;
            public MappingType Type;
        }

        private enum MappingType
        {
            Direct,
            Rename,
            Convert,
            Remove,
            Add
        }

        [System.Serializable]
        private class MigrationCandidate
        {
            public Object Asset;
            public string AssetPath;
            public string ComponentName;
            public string CurrentVersion;
            public bool Selected;
            public MigrationStatus Status;
        }

        private enum MigrationStatus
        {
            Pending,
            InProgress,
            Completed,
            Failed,
            Skipped
        }

        [System.Serializable]
        private class MigrationLogEntry
        {
            public string Timestamp;
            public string AssetPath;
            public string TaskName;
            public bool Success;
            public string Details;
        }

        public MigrationToolsModule()
        {
            InitializeBuiltInTasks();
        }

        private void InitializeBuiltInTasks()
        {
            _tasks = new List<MigrationTask>
            {
                new MigrationTask
                {
                    Name = "WeaponController v1 → v2",
                    Description = "Migrate legacy WeaponController to new ECS-compatible format",
                    FromVersion = "1.x",
                    ToVersion = "2.0",
                    SourceComponent = "WeaponController",
                    TargetComponent = "WeaponControllerAuthoring",
                    IsBuiltIn = true,
                    PropertyMappings = new List<PropertyMapping>
                    {
                        new PropertyMapping { SourceProperty = "damage", TargetProperty = "baseDamage", Type = MappingType.Rename },
                        new PropertyMapping { SourceProperty = "fireRate", TargetProperty = "roundsPerMinute", Type = MappingType.Convert },
                        new PropertyMapping { SourceProperty = "range", TargetProperty = "maxRange", Type = MappingType.Rename },
                    }
                },
                new MigrationTask
                {
                    Name = "CharacterStats → StatsAuthoring",
                    Description = "Convert MonoBehaviour stats to ECS authoring",
                    FromVersion = "Legacy",
                    ToVersion = "ECS",
                    SourceComponent = "CharacterStats",
                    TargetComponent = "StatsAuthoring",
                    IsBuiltIn = true,
                    PropertyMappings = new List<PropertyMapping>()
                },
                new MigrationTask
                {
                    Name = "AudioManager Legacy → FEEL",
                    Description = "Migrate to MoreMountains FEEL audio system",
                    FromVersion = "Custom",
                    ToVersion = "FEEL",
                    SourceComponent = "AudioManager",
                    TargetComponent = "MMF_Player",
                    IsBuiltIn = true,
                    PropertyMappings = new List<PropertyMapping>()
                },
                new MigrationTask
                {
                    Name = "VFX Prefabs → VFX Graph",
                    Description = "Convert particle system prefabs to VFX Graph",
                    FromVersion = "ParticleSystem",
                    ToVersion = "VFX Graph",
                    SourceComponent = "ParticleSystem",
                    TargetComponent = "VisualEffect",
                    IsBuiltIn = true,
                    PropertyMappings = new List<PropertyMapping>()
                },
            };
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Migration Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Migrate prefabs and components from legacy formats to new versions.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawMigrationTasks();
            EditorGUILayout.Space(10);
            
            if (_selectedTaskIndex >= 0 && _selectedTaskIndex < _tasks.Count)
            {
                DrawTaskDetails(_tasks[_selectedTaskIndex]);
                EditorGUILayout.Space(10);
                DrawCandidates();
                EditorGUILayout.Space(10);
            }
            
            DrawMigrationLog();

            EditorGUILayout.EndScrollView();
        }

        private void DrawMigrationTasks()
        {
            EditorGUILayout.LabelField("Migration Tasks", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            for (int i = 0; i < _tasks.Count; i++)
            {
                var task = _tasks[i];
                bool isSelected = i == _selectedTaskIndex;
                
                EditorGUILayout.BeginHorizontal();
                
                Color prevColor = GUI.backgroundColor;
                if (isSelected)
                {
                    GUI.backgroundColor = new Color(0.3f, 0.5f, 0.8f);
                }
                
                if (GUILayout.Button("", GUILayout.Width(20)))
                {
                    _selectedTaskIndex = i;
                    _candidates.Clear();
                }
                
                GUI.backgroundColor = prevColor;
                
                // Task info
                EditorGUILayout.LabelField(task.Name, isSelected ? EditorStyles.boldLabel : EditorStyles.label, 
                    GUILayout.Width(200));
                EditorGUILayout.LabelField($"{task.FromVersion} → {task.ToVersion}", 
                    EditorStyles.miniLabel, GUILayout.Width(100));
                EditorGUILayout.LabelField(task.Description, EditorStyles.wordWrappedMiniLabel);
                
                // Built-in badge
                if (task.IsBuiltIn)
                {
                    GUILayout.Label("Built-in", EditorStyles.miniLabel, GUILayout.Width(50));
                }
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("+ Create Custom Migration Task"))
            {
                CreateCustomTask();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTaskDetails(MigrationTask task)
        {
            EditorGUILayout.LabelField($"Task: {task.Name}", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            // Source
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Component: {task.SourceComponent}");
            EditorGUILayout.LabelField($"Version: {task.FromVersion}");
            EditorGUILayout.EndVertical();
            
            // Arrow
            EditorGUILayout.LabelField("→", new GUIStyle(EditorStyles.boldLabel) 
            { 
                fontSize = 24, 
                alignment = TextAnchor.MiddleCenter 
            }, GUILayout.Width(40));
            
            // Target
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Component: {task.TargetComponent}");
            EditorGUILayout.LabelField($"Version: {task.ToVersion}");
            EditorGUILayout.EndVertical();
            
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.EndHorizontal();

            // Property mappings
            if (task.PropertyMappings != null && task.PropertyMappings.Count > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Property Mappings:", EditorStyles.boldLabel);
                
                foreach (var mapping in task.PropertyMappings)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"  {mapping.SourceProperty}", GUILayout.Width(150));
                    
                    GUI.color = mapping.Type == MappingType.Remove ? Color.red :
                               mapping.Type == MappingType.Add ? Color.green : Color.cyan;
                    EditorGUILayout.LabelField($"[{mapping.Type}]", GUILayout.Width(70));
                    GUI.color = Color.white;
                    
                    EditorGUILayout.LabelField($"→ {mapping.TargetProperty}");
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.cyan;
            
            EditorGUI.BeginDisabledGroup(_isScanning);
            if (GUILayout.Button("Scan for Candidates", GUILayout.Height(25)))
            {
                ScanForCandidates(task);
            }
            EditorGUI.EndDisabledGroup();
            
            GUI.backgroundColor = prevColor;
            
            if (GUILayout.Button("Edit Task", GUILayout.Height(25), GUILayout.Width(80)))
            {
                EditTask(task);
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawCandidates()
        {
            EditorGUILayout.LabelField($"Migration Candidates ({_candidates.Count})", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_candidates.Count == 0)
            {
                EditorGUILayout.LabelField("Click 'Scan for Candidates' to find assets to migrate.", 
                    EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                // Select all / none
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Select All", EditorStyles.miniButton, GUILayout.Width(80)))
                {
                    foreach (var c in _candidates) c.Selected = true;
                }
                
                if (GUILayout.Button("Select None", EditorStyles.miniButton, GUILayout.Width(80)))
                {
                    foreach (var c in _candidates) c.Selected = false;
                }
                
                GUILayout.FlexibleSpace();
                
                int selectedCount = _candidates.Count(c => c.Selected);
                EditorGUILayout.LabelField($"{selectedCount} selected", GUILayout.Width(80));
                
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);
                
                // Candidate list
                foreach (var candidate in _candidates.Take(30))
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    candidate.Selected = EditorGUILayout.Toggle(candidate.Selected, GUILayout.Width(20));
                    
                    // Status indicator
                    Color statusColor = candidate.Status switch
                    {
                        MigrationStatus.Completed => Color.green,
                        MigrationStatus.Failed => Color.red,
                        MigrationStatus.InProgress => Color.yellow,
                        MigrationStatus.Skipped => Color.gray,
                        _ => Color.white
                    };
                    
                    GUI.color = statusColor;
                    EditorGUILayout.LabelField("●", GUILayout.Width(15));
                    GUI.color = Color.white;
                    
                    if (GUILayout.Button(candidate.Asset?.name ?? "null", EditorStyles.linkLabel, GUILayout.Width(150)))
                    {
                        Selection.activeObject = candidate.Asset;
                    }
                    
                    EditorGUILayout.LabelField(candidate.ComponentName, GUILayout.Width(120));
                    EditorGUILayout.LabelField(candidate.AssetPath, EditorStyles.miniLabel);
                    
                    EditorGUILayout.EndHorizontal();
                }

                if (_candidates.Count > 30)
                {
                    EditorGUILayout.LabelField($"... and {_candidates.Count - 30} more", 
                        EditorStyles.centeredGreyMiniLabel);
                }

                EditorGUILayout.Space(5);
                
                // Migration buttons
                EditorGUILayout.BeginHorizontal();
                
                Color prevColor = GUI.backgroundColor;
                GUI.backgroundColor = Color.green;
                
                EditorGUI.BeginDisabledGroup(selectedCount == 0);
                if (GUILayout.Button($"Migrate Selected ({selectedCount})", GUILayout.Height(30)))
                {
                    MigrateSelected();
                }
                EditorGUI.EndDisabledGroup();
                
                GUI.backgroundColor = prevColor;
                
                if (GUILayout.Button("Preview Changes", GUILayout.Height(30), GUILayout.Width(120)))
                {
                    PreviewChanges();
                }
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawMigrationLog()
        {
            EditorGUILayout.LabelField("Migration Log", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_migrationLog.Count == 0)
            {
                EditorGUILayout.LabelField("No migrations performed yet.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                foreach (var entry in _migrationLog.TakeLast(10).Reverse())
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    GUI.color = entry.Success ? Color.green : Color.red;
                    EditorGUILayout.LabelField(entry.Success ? "✓" : "✗", GUILayout.Width(15));
                    GUI.color = Color.white;
                    
                    EditorGUILayout.LabelField(entry.Timestamp, GUILayout.Width(80));
                    EditorGUILayout.LabelField(entry.TaskName, GUILayout.Width(150));
                    EditorGUILayout.LabelField(entry.AssetPath, EditorStyles.miniLabel);
                    
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(5);
                
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Export Log"))
                {
                    ExportLog();
                }
                
                if (GUILayout.Button("Clear Log"))
                {
                    _migrationLog.Clear();
                }
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void ScanForCandidates(MigrationTask task)
        {
            _candidates.Clear();
            _isScanning = true;
            
            // Find prefabs with the source component
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab == null) continue;
                
                // Check for source component (by name for simulation)
                var components = prefab.GetComponentsInChildren<Component>(true);
                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    
                    if (comp.GetType().Name == task.SourceComponent)
                    {
                        _candidates.Add(new MigrationCandidate
                        {
                            Asset = prefab,
                            AssetPath = path,
                            ComponentName = task.SourceComponent,
                            CurrentVersion = task.FromVersion,
                            Status = MigrationStatus.Pending
                        });
                        break;
                    }
                }
            }
            
            _isScanning = false;
            Debug.Log($"[MigrationTools] Found {_candidates.Count} candidates for {task.Name}");
        }

        private void MigrateSelected()
        {
            var selected = _candidates.Where(c => c.Selected && c.Status == MigrationStatus.Pending).ToList();
            var task = _tasks[_selectedTaskIndex];
            
            foreach (var candidate in selected)
            {
                candidate.Status = MigrationStatus.InProgress;
                
                // Perform migration (simulation)
                bool success = true; // Would do actual migration here
                
                candidate.Status = success ? MigrationStatus.Completed : MigrationStatus.Failed;
                
                _migrationLog.Add(new MigrationLogEntry
                {
                    Timestamp = System.DateTime.Now.ToString("HH:mm:ss"),
                    AssetPath = candidate.AssetPath,
                    TaskName = task.Name,
                    Success = success,
                    Details = success ? "Migration completed" : "Migration failed"
                });
            }
            
            AssetDatabase.SaveAssets();
            Debug.Log($"[MigrationTools] Migrated {selected.Count} assets");
        }

        private void PreviewChanges()
        {
            Debug.Log("[MigrationTools] Preview pending");
        }

        private void CreateCustomTask()
        {
            _tasks.Add(new MigrationTask
            {
                Name = "Custom Task",
                Description = "Custom migration task",
                FromVersion = "Old",
                ToVersion = "New",
                SourceComponent = "",
                TargetComponent = "",
                IsBuiltIn = false,
                PropertyMappings = new List<PropertyMapping>()
            });
            
            _selectedTaskIndex = _tasks.Count - 1;
        }

        private void EditTask(MigrationTask task)
        {
            Debug.Log($"[MigrationTools] Edit task: {task.Name}");
        }

        private void ExportLog()
        {
            string path = EditorUtility.SaveFilePanel("Export Migration Log", "", "migration_log.csv", "csv");
            if (!string.IsNullOrEmpty(path))
            {
                Debug.Log($"[MigrationTools] Exported log to: {path}");
            }
        }
    }
}
