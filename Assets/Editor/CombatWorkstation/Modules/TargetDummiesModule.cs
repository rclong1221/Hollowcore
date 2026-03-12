using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.CombatWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 CB-06: Target Dummies module.
    /// Spawn test targets with health bars, damage logging.
    /// </summary>
    public class TargetDummiesModule : ICombatModule
    {
        private Vector2 _scrollPosition;
        
        // Spawn settings
        private GameObject _dummyPrefab;
        private int _spawnCount = 1;
        private float _spawnSpacing = 3f;
        private SpawnPattern _spawnPattern = SpawnPattern.Line;
        private Vector3 _spawnOrigin = Vector3.zero;
        
        // Dummy settings
        private float _dummyHealth = 1000f;
        private bool _immortal = false;
        private bool _showHealthBar = true;
        private bool _showDamageNumbers = true;
        private bool _autoRespawn = true;
        private float _respawnDelay = 3f;
        
        // Active dummies
        private List<SpawnedDummy> _activeDummies = new List<SpawnedDummy>();
        
        // Damage log
        private List<DummyDamageLog> _damageLog = new List<DummyDamageLog>();
        private Vector2 _logScrollPosition;
        private float _totalDamageReceived = 0f;
        private int _totalHitsReceived = 0;

        private enum SpawnPattern { Single, Line, Circle, Grid, Random }

        private class SpawnedDummy
        {
            public GameObject Object;
            public float CurrentHealth;
            public float MaxHealth;
            public int HitCount;
            public float DamageReceived;
        }

        private class DummyDamageLog
        {
            public float Timestamp;
            public string DummyName;
            public float Damage;
            public string HitRegion;
            public bool WasCritical;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Target Dummies", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Spawn test targets for weapon testing. Configure health, damage logging, and respawn.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            
            // Left panel - spawn and settings
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            DrawPrefabSelection();
            EditorGUILayout.Space(10);
            DrawSpawnSettings();
            EditorGUILayout.Space(10);
            DrawDummySettings();
            EditorGUILayout.Space(10);
            DrawSpawnActions();
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // Right panel - active dummies and log
            EditorGUILayout.BeginVertical();
            DrawActiveDummies();
            EditorGUILayout.Space(10);
            DrawDamageLog();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPrefabSelection()
        {
            EditorGUILayout.LabelField("Dummy Prefab", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _dummyPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Prefab", _dummyPrefab, typeof(GameObject), false);

            if (_dummyPrefab == null)
            {
                EditorGUILayout.HelpBox(
                    "No prefab assigned. Use 'Create Default Dummy' or assign your own.",
                    MessageType.Warning);
                
                if (GUILayout.Button("Create Default Dummy Prefab"))
                {
                    CreateDefaultDummy();
                }
            }
            else
            {
                EditorGUILayout.LabelField($"Selected: {_dummyPrefab.name}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSpawnSettings()
        {
            EditorGUILayout.LabelField("Spawn Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _spawnPattern = (SpawnPattern)EditorGUILayout.EnumPopup("Pattern", _spawnPattern);
            
            if (_spawnPattern != SpawnPattern.Single)
            {
                _spawnCount = EditorGUILayout.IntSlider("Count", _spawnCount, 1, 20);
                _spawnSpacing = EditorGUILayout.Slider("Spacing", _spawnSpacing, 1f, 10f);
            }

            _spawnOrigin = EditorGUILayout.Vector3Field("Origin", _spawnOrigin);

            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Use Scene View Center"))
            {
                _spawnOrigin = SceneView.lastActiveSceneView?.pivot ?? Vector3.zero;
            }
            if (GUILayout.Button("Use Selection"))
            {
                if (Selection.activeTransform != null)
                {
                    _spawnOrigin = Selection.activeTransform.position;
                }
            }
            EditorGUILayout.EndHorizontal();

            // Pattern preview
            DrawPatternPreview();

            EditorGUILayout.EndVertical();
        }

        private void DrawPatternPreview()
        {
            EditorGUILayout.LabelField("Pattern Preview", EditorStyles.miniLabel);
            
            Rect previewRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(80), GUILayout.ExpandWidth(true));
            
            EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.15f, 0.15f));

            // Draw spawn positions
            var positions = CalculateSpawnPositions();
            float scale = 5f;
            Vector2 center = previewRect.center;

            foreach (var pos in positions)
            {
                float x = center.x + pos.x * scale;
                float y = center.y - pos.z * scale; // Invert Z for top-down view
                
                Rect dotRect = new Rect(x - 4, y - 4, 8, 8);
                EditorGUI.DrawRect(dotRect, Color.red);
            }

            // Draw origin
            EditorGUI.DrawRect(new Rect(center.x - 2, center.y - 2, 4, 4), Color.green);
        }

        private void DrawDummySettings()
        {
            EditorGUILayout.LabelField("Dummy Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _dummyHealth = EditorGUILayout.FloatField("Health", _dummyHealth);
            _immortal = EditorGUILayout.Toggle("Immortal (No Death)", _immortal);
            
            EditorGUILayout.Space(5);
            _showHealthBar = EditorGUILayout.Toggle("Show Health Bar", _showHealthBar);
            _showDamageNumbers = EditorGUILayout.Toggle("Show Damage Numbers", _showDamageNumbers);
            
            EditorGUILayout.Space(5);
            _autoRespawn = EditorGUILayout.Toggle("Auto Respawn", _autoRespawn);
            if (_autoRespawn)
            {
                _respawnDelay = EditorGUILayout.Slider("Respawn Delay", _respawnDelay, 0f, 10f);
            }

            // Quick presets
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("100 HP")) _dummyHealth = 100f;
            if (GUILayout.Button("500 HP")) _dummyHealth = 500f;
            if (GUILayout.Button("1000 HP")) _dummyHealth = 1000f;
            if (GUILayout.Button("∞ HP")) { _dummyHealth = 999999f; _immortal = true; }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSpawnActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginDisabledGroup(_dummyPrefab == null || !Application.isPlaying);

            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            
            if (GUILayout.Button("Spawn Dummies", GUILayout.Height(35)))
            {
                SpawnDummies();
            }
            
            GUI.backgroundColor = prevColor;

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.BeginHorizontal();
            
            EditorGUI.BeginDisabledGroup(_activeDummies.Count == 0);
            
            if (GUILayout.Button("Reset All Health"))
            {
                ResetAllHealth();
            }
            
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Destroy All"))
            {
                DestroyAllDummies();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play mode to spawn dummies.",
                    MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActiveDummies()
        {
            EditorGUILayout.LabelField($"Active Dummies ({_activeDummies.Count})", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));

            // Clean up destroyed dummies
            _activeDummies.RemoveAll(d => d.Object == null);

            if (_activeDummies.Count == 0)
            {
                EditorGUILayout.LabelField("No active dummies", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                foreach (var dummy in _activeDummies)
                {
                    DrawDummyStatus(dummy);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDummyStatus(SpawnedDummy dummy)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            EditorGUILayout.LabelField(dummy.Object.name, GUILayout.Width(100));

            // Health bar
            Rect healthRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(18), GUILayout.ExpandWidth(true));
            
            float healthPercent = dummy.CurrentHealth / dummy.MaxHealth;
            Color healthColor = healthPercent > 0.5f ? Color.green : 
                               healthPercent > 0.25f ? Color.yellow : Color.red;
            
            EditorGUI.DrawRect(healthRect, new Color(0.2f, 0.2f, 0.2f));
            EditorGUI.DrawRect(new Rect(healthRect.x, healthRect.y, 
                healthRect.width * healthPercent, healthRect.height), healthColor);
            
            GUI.Label(healthRect, $"{dummy.CurrentHealth:F0}/{dummy.MaxHealth:F0}", 
                new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });

            // Stats
            EditorGUILayout.LabelField($"{dummy.HitCount} hits", EditorStyles.miniLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField($"{dummy.DamageReceived:F0} dmg", EditorStyles.miniLabel, GUILayout.Width(60));

            // Actions
            if (GUILayout.Button("↻", GUILayout.Width(25)))
            {
                dummy.CurrentHealth = dummy.MaxHealth;
                dummy.HitCount = 0;
                dummy.DamageReceived = 0;
            }
            
            if (GUILayout.Button("×", GUILayout.Width(25)))
            {
                if (dummy.Object != null)
                {
                    Object.DestroyImmediate(dummy.Object);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDamageLog()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Damage Log", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                ClearLog();
            }
            if (GUILayout.Button("Export", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                ExportLog();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(150));

            // Stats bar
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Total: {_totalHitsReceived} hits, {_totalDamageReceived:F0} damage", 
                EditorStyles.miniLabel);
            if (_totalHitsReceived > 0)
            {
                EditorGUILayout.LabelField($"Avg: {_totalDamageReceived / _totalHitsReceived:F1} per hit", 
                    EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            _logScrollPosition = EditorGUILayout.BeginScrollView(_logScrollPosition);

            // Show last 20 entries
            int startIndex = Mathf.Max(0, _damageLog.Count - 20);
            for (int i = startIndex; i < _damageLog.Count; i++)
            {
                var entry = _damageLog[i];
                EditorGUILayout.BeginHorizontal();
                
                EditorGUILayout.LabelField($"[{entry.Timestamp:F2}s]", 
                    EditorStyles.miniLabel, GUILayout.Width(60));
                EditorGUILayout.LabelField(entry.DummyName, GUILayout.Width(80));
                
                Color prevColor = GUI.color;
                GUI.color = entry.WasCritical ? Color.yellow : Color.white;
                EditorGUILayout.LabelField($"{entry.Damage:F0}", 
                    EditorStyles.boldLabel, GUILayout.Width(50));
                GUI.color = prevColor;
                
                EditorGUILayout.LabelField(entry.HitRegion, EditorStyles.miniLabel, GUILayout.Width(50));
                if (entry.WasCritical)
                {
                    EditorGUILayout.LabelField("CRIT", EditorStyles.miniBoldLabel, GUILayout.Width(35));
                }
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private List<Vector3> CalculateSpawnPositions()
        {
            var positions = new List<Vector3>();
            int count = _spawnPattern == SpawnPattern.Single ? 1 : _spawnCount;

            switch (_spawnPattern)
            {
                case SpawnPattern.Single:
                    positions.Add(_spawnOrigin);
                    break;

                case SpawnPattern.Line:
                    float lineOffset = (count - 1) * _spawnSpacing * 0.5f;
                    for (int i = 0; i < count; i++)
                    {
                        positions.Add(_spawnOrigin + Vector3.right * (i * _spawnSpacing - lineOffset));
                    }
                    break;

                case SpawnPattern.Circle:
                    float radius = _spawnSpacing;
                    for (int i = 0; i < count; i++)
                    {
                        float angle = (i / (float)count) * Mathf.PI * 2;
                        positions.Add(_spawnOrigin + new Vector3(
                            Mathf.Cos(angle) * radius,
                            0,
                            Mathf.Sin(angle) * radius
                        ));
                    }
                    break;

                case SpawnPattern.Grid:
                    int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
                    int rows = Mathf.CeilToInt((float)count / cols);
                    float gridOffsetX = (cols - 1) * _spawnSpacing * 0.5f;
                    float gridOffsetZ = (rows - 1) * _spawnSpacing * 0.5f;
                    
                    for (int i = 0; i < count; i++)
                    {
                        int col = i % cols;
                        int row = i / cols;
                        positions.Add(_spawnOrigin + new Vector3(
                            col * _spawnSpacing - gridOffsetX,
                            0,
                            row * _spawnSpacing - gridOffsetZ
                        ));
                    }
                    break;

                case SpawnPattern.Random:
                    for (int i = 0; i < count; i++)
                    {
                        positions.Add(_spawnOrigin + new Vector3(
                            Random.Range(-_spawnSpacing * 2, _spawnSpacing * 2),
                            0,
                            Random.Range(-_spawnSpacing * 2, _spawnSpacing * 2)
                        ));
                    }
                    break;
            }

            return positions;
        }

        private void SpawnDummies()
        {
            if (_dummyPrefab == null) return;

            var positions = CalculateSpawnPositions();

            foreach (var pos in positions)
            {
                var dummy = Object.Instantiate(_dummyPrefab, pos, Quaternion.identity);
                dummy.name = $"TargetDummy_{_activeDummies.Count}";

                _activeDummies.Add(new SpawnedDummy
                {
                    Object = dummy,
                    CurrentHealth = _dummyHealth,
                    MaxHealth = _dummyHealth,
                    HitCount = 0,
                    DamageReceived = 0
                });
            }

            Debug.Log($"[TargetDummies] Spawned {positions.Count} dummies");
        }

        private void CreateDefaultDummy()
        {
            // Create a simple capsule dummy
            var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.name = "TargetDummy_Default";
            dummy.transform.localScale = new Vector3(1, 2, 1);

            // Save as prefab
            string path = "Assets/Prefabs/Debug/TargetDummy_Default.prefab";
            
            // Ensure directory exists
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            _dummyPrefab = PrefabUtility.SaveAsPrefabAsset(dummy, path);
            Object.DestroyImmediate(dummy);
            
            AssetDatabase.Refresh();
            Debug.Log($"[TargetDummies] Created default dummy prefab at {path}");
        }

        private void ResetAllHealth()
        {
            foreach (var dummy in _activeDummies)
            {
                dummy.CurrentHealth = dummy.MaxHealth;
                dummy.HitCount = 0;
                dummy.DamageReceived = 0;
            }
            Debug.Log("[TargetDummies] Reset all dummy health");
        }

        private void DestroyAllDummies()
        {
            foreach (var dummy in _activeDummies)
            {
                if (dummy.Object != null)
                {
                    Object.DestroyImmediate(dummy.Object);
                }
            }
            _activeDummies.Clear();
            Debug.Log("[TargetDummies] Destroyed all dummies");
        }

        private void ClearLog()
        {
            _damageLog.Clear();
            _totalDamageReceived = 0;
            _totalHitsReceived = 0;
        }

        private void ExportLog()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Timestamp,Dummy,Damage,Region,IsCritical");

            foreach (var entry in _damageLog)
            {
                sb.AppendLine($"{entry.Timestamp:F2},{entry.DummyName},{entry.Damage:F1}," +
                    $"{entry.HitRegion},{entry.WasCritical}");
            }

            string path = EditorUtility.SaveFilePanel("Export Damage Log", "", 
                $"DummyLog_{System.DateTime.Now:yyyyMMdd_HHmmss}", "csv");
            
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, sb.ToString());
                Debug.Log($"[TargetDummies] Exported log to {path}");
            }
        }

        // Called from damage system to log hits
        public void LogHit(string dummyName, float damage, string region, bool isCritical)
        {
            _damageLog.Add(new DummyDamageLog
            {
                Timestamp = Time.time,
                DummyName = dummyName,
                Damage = damage,
                HitRegion = region,
                WasCritical = isCritical
            });

            _totalDamageReceived += damage;
            _totalHitsReceived++;

            // Update dummy stats
            var dummy = _activeDummies.FirstOrDefault(d => d.Object != null && d.Object.name == dummyName);
            if (dummy != null)
            {
                dummy.HitCount++;
                dummy.DamageReceived += damage;
                if (!_immortal)
                {
                    dummy.CurrentHealth -= damage;
                }
            }
        }
    }
}
