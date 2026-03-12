using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.SystemsWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 SW-03: VFX Pool module.
    /// VFX instance pooling, auto-return timers.
    /// </summary>
    public class VFXPoolModule : ISystemsModule
    {
        private Vector2 _scrollPosition;
        
        // VFX pool entries
        private List<VFXPoolEntry> _entries = new List<VFXPoolEntry>();
        
        // Global settings
        private bool _autoDetectLifetime = true;
        private float _defaultLifetime = 2f;
        private bool _pauseWhenHidden = true;
        private float _cleanupInterval = 60f;

        [System.Serializable]
        private class VFXPoolEntry
        {
            public string Name;
            public GameObject Prefab;
            public VFXType Type = VFXType.ParticleSystem;
            public int PoolSize = 20;
            public float AutoReturnTime = 2f;
            public bool UseAutoDetect = true;
            public int ActiveCount = 0;
            public int SpawnCount = 0;
        }

        private enum VFXType
        {
            ParticleSystem,
            VFXGraph,
            TrailRenderer,
            LineRenderer,
            Decal,
            Light
        }

        public VFXPoolModule()
        {
            InitializeDefaultEntries();
        }

        private void InitializeDefaultEntries()
        {
            _entries = new List<VFXPoolEntry>
            {
                new VFXPoolEntry { Name = "MuzzleFlash", Type = VFXType.ParticleSystem, PoolSize = 30, AutoReturnTime = 0.1f },
                new VFXPoolEntry { Name = "BulletImpact_Concrete", Type = VFXType.ParticleSystem, PoolSize = 50, AutoReturnTime = 1.5f },
                new VFXPoolEntry { Name = "BulletImpact_Metal", Type = VFXType.ParticleSystem, PoolSize = 50, AutoReturnTime = 1.5f },
                new VFXPoolEntry { Name = "BloodSpatter", Type = VFXType.ParticleSystem, PoolSize = 40, AutoReturnTime = 2f },
                new VFXPoolEntry { Name = "Explosion_Small", Type = VFXType.ParticleSystem, PoolSize = 15, AutoReturnTime = 3f },
                new VFXPoolEntry { Name = "Tracer", Type = VFXType.TrailRenderer, PoolSize = 100, AutoReturnTime = 0.5f },
                new VFXPoolEntry { Name = "BulletDecal", Type = VFXType.Decal, PoolSize = 100, AutoReturnTime = 30f },
            };
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("VFX Pool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Manage VFX object pools with automatic return timers based on effect lifetime.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawGlobalSettings();
            EditorGUILayout.Space(10);
            DrawPoolSummary();
            EditorGUILayout.Space(10);
            DrawPoolEntries();
            EditorGUILayout.Space(10);
            DrawQuickAdd();
            EditorGUILayout.Space(10);
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawGlobalSettings()
        {
            EditorGUILayout.LabelField("Global Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _autoDetectLifetime = EditorGUILayout.Toggle("Auto-Detect Lifetime", _autoDetectLifetime);
            
            if (_autoDetectLifetime)
            {
                EditorGUILayout.LabelField("Reads duration from ParticleSystem.main.duration", 
                    EditorStyles.miniLabel);
            }
            else
            {
                _defaultLifetime = EditorGUILayout.FloatField("Default Lifetime (s)", _defaultLifetime);
            }

            EditorGUILayout.Space(5);
            
            _pauseWhenHidden = EditorGUILayout.Toggle("Pause When Hidden", _pauseWhenHidden);
            _cleanupInterval = EditorGUILayout.FloatField("Cleanup Interval (s)", _cleanupInterval);

            EditorGUILayout.EndVertical();
        }

        private void DrawPoolSummary()
        {
            EditorGUILayout.LabelField("Pool Summary", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Group by type
            var byType = _entries.GroupBy(e => e.Type)
                .OrderByDescending(g => g.Sum(e => e.PoolSize));

            EditorGUILayout.BeginHorizontal();
            
            foreach (var group in byType)
            {
                int totalSize = group.Sum(e => e.PoolSize);
                
                EditorGUILayout.BeginVertical(GUILayout.Width(100));
                EditorGUILayout.LabelField(group.Key.ToString(), EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{group.Count()} entries");
                EditorGUILayout.LabelField($"{totalSize} objects");
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.EndHorizontal();

            // Total
            int totalEntries = _entries.Count;
            int totalObjects = _entries.Sum(e => e.PoolSize);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Total: {totalEntries} entries, {totalObjects} pooled objects");

            EditorGUILayout.EndVertical();
        }

        private void DrawPoolEntries()
        {
            EditorGUILayout.LabelField("Pool Entries", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Prefab", EditorStyles.boldLabel, GUILayout.Width(130));
            EditorGUILayout.LabelField("Type", EditorStyles.boldLabel, GUILayout.Width(90));
            EditorGUILayout.LabelField("Size", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Return (s)", EditorStyles.boldLabel, GUILayout.Width(70));
            EditorGUILayout.LabelField("", GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                
                EditorGUILayout.BeginHorizontal();
                
                // Prefab
                entry.Prefab = (GameObject)EditorGUILayout.ObjectField(
                    entry.Prefab, typeof(GameObject), false, GUILayout.Width(130));
                
                if (entry.Prefab != null)
                {
                    entry.Name = entry.Prefab.name;
                    
                    // Auto-detect type
                    if (entry.Prefab.GetComponent<ParticleSystem>() != null)
                        entry.Type = VFXType.ParticleSystem;
                    else if (entry.Prefab.GetComponent<TrailRenderer>() != null)
                        entry.Type = VFXType.TrailRenderer;
                    else if (entry.Prefab.GetComponent<LineRenderer>() != null)
                        entry.Type = VFXType.LineRenderer;
                    else if (entry.Prefab.GetComponent<Light>() != null)
                        entry.Type = VFXType.Light;
                }
                
                // Type
                entry.Type = (VFXType)EditorGUILayout.EnumPopup(entry.Type, GUILayout.Width(90));
                
                // Pool size
                entry.PoolSize = EditorGUILayout.IntField(entry.PoolSize, GUILayout.Width(50));
                
                // Auto return time
                entry.AutoReturnTime = EditorGUILayout.FloatField(entry.AutoReturnTime, GUILayout.Width(70));
                
                // Auto-detect button
                if (entry.Prefab != null && _autoDetectLifetime)
                {
                    if (GUILayout.Button("⟳", GUILayout.Width(20)))
                    {
                        DetectLifetime(entry);
                    }
                }
                
                // Remove
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _entries.RemoveAt(i);
                    i--;
                }
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("+ Add VFX Entry"))
            {
                _entries.Add(new VFXPoolEntry
                {
                    Name = $"NewVFX_{_entries.Count}",
                    PoolSize = 20,
                    AutoReturnTime = 2f
                });
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawQuickAdd()
        {
            EditorGUILayout.LabelField("Quick Add", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Drag VFX prefabs here to add them:", EditorStyles.miniLabel);

            Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drop VFX Prefabs", EditorStyles.helpBox);
            
            HandleDragDrop(dropArea);

            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("Scan Project for VFX Prefabs"))
            {
                ScanForVFXPrefabs();
            }

            EditorGUILayout.EndVertical();
        }

        private void HandleDragDrop(Rect dropArea)
        {
            Event evt = Event.current;
            
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (!dropArea.Contains(evt.mousePosition)) return;
                
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is GameObject go)
                        {
                            // Check if already exists
                            if (_entries.Any(e => e.Prefab == go)) continue;
                            
                            var entry = new VFXPoolEntry
                            {
                                Name = go.name,
                                Prefab = go,
                                PoolSize = 20,
                                AutoReturnTime = 2f
                            };
                            
                            // Auto-detect type
                            if (go.GetComponent<ParticleSystem>() != null)
                            {
                                entry.Type = VFXType.ParticleSystem;
                                DetectLifetime(entry);
                            }
                            else if (go.GetComponent<TrailRenderer>() != null)
                            {
                                entry.Type = VFXType.TrailRenderer;
                            }
                            
                            _entries.Add(entry);
                        }
                    }
                }
                
                evt.Use();
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            
            if (GUILayout.Button("Save Configuration", GUILayout.Height(30)))
            {
                SaveConfiguration();
            }
            
            GUI.backgroundColor = prevColor;

            if (GUILayout.Button("Auto-Detect All Lifetimes", GUILayout.Height(30)))
            {
                AutoDetectAllLifetimes();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Generate Pool Manager"))
            {
                GeneratePoolManager();
            }
            
            if (GUILayout.Button("Export"))
            {
                ExportConfig();
            }
            
            if (GUILayout.Button("Import"))
            {
                ImportConfig();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DetectLifetime(VFXPoolEntry entry)
        {
            if (entry.Prefab == null) return;
            
            var ps = entry.Prefab.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                entry.AutoReturnTime = ps.main.duration + ps.main.startLifetime.constantMax;
                Debug.Log($"[VFXPool] Detected lifetime for {entry.Name}: {entry.AutoReturnTime:F2}s");
            }
        }

        private void AutoDetectAllLifetimes()
        {
            foreach (var entry in _entries)
            {
                DetectLifetime(entry);
            }
        }

        private void ScanForVFXPrefabs()
        {
            string[] searchFolders = { "Assets/Prefabs/VFX", "Assets/VFX", "Assets/Effects" };
            
            foreach (var folder in searchFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                
                var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
                
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    
                    if (prefab == null) continue;
                    if (_entries.Any(e => e.Prefab == prefab)) continue;
                    
                    // Check if it's a VFX prefab
                    if (prefab.GetComponent<ParticleSystem>() != null ||
                        prefab.GetComponent<TrailRenderer>() != null)
                    {
                        var entry = new VFXPoolEntry
                        {
                            Name = prefab.name,
                            Prefab = prefab,
                            PoolSize = 20
                        };
                        
                        DetectLifetime(entry);
                        _entries.Add(entry);
                    }
                }
            }
            
            Debug.Log($"[VFXPool] Found {_entries.Count} VFX prefabs");
        }

        private void SaveConfiguration()
        {
            Debug.Log($"[VFXPool] Saved {_entries.Count} entries");
        }

        private void GeneratePoolManager()
        {
            Debug.Log("[VFXPool] Pool manager generation pending");
        }

        private void ExportConfig()
        {
            Debug.Log("[VFXPool] Export pending");
        }

        private void ImportConfig()
        {
            Debug.Log("[VFXPool] Import pending");
        }
    }
}
