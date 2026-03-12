using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.DebugWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 DW-02: Target Spawner module.
    /// Configurable target dummies, health display.
    /// </summary>
    public class TargetSpawnerModule : IDebugModule
    {
        private Vector2 _scrollPosition;
        
        // Target templates
        private List<TargetTemplate> _templates = new List<TargetTemplate>();
        private int _selectedTemplateIndex = 0;
        
        // Spawn settings
        private SpawnPattern _spawnPattern = SpawnPattern.Single;
        private int _spawnCount = 1;
        private float _spawnRadius = 5f;
        private float _spawnHeight = 0f;
        private Vector3 _spawnOffset = Vector3.zero;
        
        // Active targets
        private List<SpawnedTarget> _activeTargets = new List<SpawnedTarget>();
        
        // Display settings
        private bool _showHealthBars = true;
        private bool _showDamageNumbers = true;
        private bool _showHitMarkers = true;

        private enum SpawnPattern
        {
            Single,
            Line,
            Circle,
            Grid,
            Random
        }

        [System.Serializable]
        private class TargetTemplate
        {
            public string Name;
            public TargetType Type;
            public float Health = 100f;
            public float Armor = 0f;
            public float Size = 1f;
            public bool IsMoving = false;
            public float MoveSpeed = 2f;
            public Color DisplayColor = Color.red;
        }

        private enum TargetType
        {
            StaticDummy,
            MovingDummy,
            ArmoredDummy,
            HeadshotTarget,
            BodyTarget,
            CritZoneTarget
        }

        [System.Serializable]
        private class SpawnedTarget
        {
            public int Id;
            public string TemplateName;
            public Vector3 Position;
            public float CurrentHealth;
            public float MaxHealth;
            public int HitCount;
            public float TotalDamage;
        }

        public TargetSpawnerModule()
        {
            InitializeTemplates();
            InitializeSimulatedTargets();
        }

        private void InitializeTemplates()
        {
            _templates = new List<TargetTemplate>
            {
                new TargetTemplate { Name = "Basic Dummy", Type = TargetType.StaticDummy, Health = 100f, DisplayColor = Color.red },
                new TargetTemplate { Name = "Tank Dummy", Type = TargetType.ArmoredDummy, Health = 500f, Armor = 50f, Size = 1.5f, DisplayColor = Color.blue },
                new TargetTemplate { Name = "Headshot Target", Type = TargetType.HeadshotTarget, Health = 50f, Size = 0.5f, DisplayColor = Color.yellow },
                new TargetTemplate { Name = "Moving Target", Type = TargetType.MovingDummy, Health = 100f, IsMoving = true, MoveSpeed = 3f, DisplayColor = Color.green },
                new TargetTemplate { Name = "Body Target", Type = TargetType.BodyTarget, Health = 150f, DisplayColor = Color.cyan },
                new TargetTemplate { Name = "Crit Zone", Type = TargetType.CritZoneTarget, Health = 75f, DisplayColor = Color.magenta },
            };
        }

        private void InitializeSimulatedTargets()
        {
            _activeTargets = new List<SpawnedTarget>
            {
                new SpawnedTarget { Id = 1, TemplateName = "Basic Dummy", Position = new Vector3(5, 0, 10), CurrentHealth = 75, MaxHealth = 100, HitCount = 5, TotalDamage = 125 },
                new SpawnedTarget { Id = 2, TemplateName = "Tank Dummy", Position = new Vector3(-5, 0, 15), CurrentHealth = 420, MaxHealth = 500, HitCount = 8, TotalDamage = 80 },
            };
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Target Spawner", EditorStyles.boldLabel);
            
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to spawn and interact with targets.",
                    MessageType.Warning);
            }
            
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawTemplateSelector();
            EditorGUILayout.Space(10);
            DrawSpawnSettings();
            EditorGUILayout.Space(10);
            DrawSpawnControls();
            EditorGUILayout.Space(10);
            DrawActiveTargets();
            EditorGUILayout.Space(10);
            DrawDisplaySettings();

            EditorGUILayout.EndScrollView();
        }

        private void DrawTemplateSelector()
        {
            EditorGUILayout.LabelField("Target Templates", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Template grid
            EditorGUILayout.BeginHorizontal();
            
            for (int i = 0; i < _templates.Count; i++)
            {
                var template = _templates[i];
                bool isSelected = i == _selectedTemplateIndex;
                
                Color prevBg = GUI.backgroundColor;
                GUI.backgroundColor = isSelected ? template.DisplayColor : Color.gray;
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(100), GUILayout.Height(60));
                
                if (GUILayout.Button(template.Name, isSelected ? EditorStyles.boldLabel : EditorStyles.label))
                {
                    _selectedTemplateIndex = i;
                }
                
                EditorGUILayout.LabelField($"HP: {template.Health}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(template.Type.ToString(), EditorStyles.miniLabel);
                
                EditorGUILayout.EndVertical();
                
                GUI.backgroundColor = prevBg;
                
                if ((i + 1) % 3 == 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }
            
            EditorGUILayout.EndHorizontal();

            // Selected template details
            if (_selectedTemplateIndex >= 0 && _selectedTemplateIndex < _templates.Count)
            {
                var selected = _templates[_selectedTemplateIndex];
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Template Settings:", EditorStyles.boldLabel);
                
                selected.Name = EditorGUILayout.TextField("Name", selected.Name);
                selected.Health = EditorGUILayout.FloatField("Health", selected.Health);
                selected.Armor = EditorGUILayout.FloatField("Armor", selected.Armor);
                selected.Size = EditorGUILayout.Slider("Size", selected.Size, 0.5f, 3f);
                selected.IsMoving = EditorGUILayout.Toggle("Is Moving", selected.IsMoving);
                
                if (selected.IsMoving)
                {
                    selected.MoveSpeed = EditorGUILayout.Slider("Move Speed", selected.MoveSpeed, 1f, 10f);
                }
                
                selected.DisplayColor = EditorGUILayout.ColorField("Color", selected.DisplayColor);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSpawnSettings()
        {
            EditorGUILayout.LabelField("Spawn Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _spawnPattern = (SpawnPattern)EditorGUILayout.EnumPopup("Pattern", _spawnPattern);
            
            switch (_spawnPattern)
            {
                case SpawnPattern.Single:
                    _spawnOffset = EditorGUILayout.Vector3Field("Position Offset", _spawnOffset);
                    break;
                    
                case SpawnPattern.Line:
                    _spawnCount = EditorGUILayout.IntSlider("Count", _spawnCount, 2, 10);
                    _spawnRadius = EditorGUILayout.Slider("Spacing", _spawnRadius, 1f, 10f);
                    break;
                    
                case SpawnPattern.Circle:
                    _spawnCount = EditorGUILayout.IntSlider("Count", _spawnCount, 3, 12);
                    _spawnRadius = EditorGUILayout.Slider("Radius", _spawnRadius, 3f, 20f);
                    break;
                    
                case SpawnPattern.Grid:
                    _spawnCount = EditorGUILayout.IntSlider("Rows/Cols", _spawnCount, 2, 5);
                    _spawnRadius = EditorGUILayout.Slider("Spacing", _spawnRadius, 2f, 8f);
                    break;
                    
                case SpawnPattern.Random:
                    _spawnCount = EditorGUILayout.IntSlider("Count", _spawnCount, 1, 20);
                    _spawnRadius = EditorGUILayout.Slider("Radius", _spawnRadius, 5f, 30f);
                    break;
            }
            
            _spawnHeight = EditorGUILayout.Slider("Height", _spawnHeight, 0f, 10f);

            // Pattern preview
            Rect previewRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(80), GUILayout.ExpandWidth(true));
            DrawPatternPreview(previewRect);

            EditorGUILayout.EndVertical();
        }

        private void DrawPatternPreview(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
            
            Vector2 center = rect.center;
            float scale = Mathf.Min(rect.width, rect.height) / (_spawnRadius * 2.5f);
            
            // Draw spawn positions based on pattern
            List<Vector2> positions = new List<Vector2>();
            
            switch (_spawnPattern)
            {
                case SpawnPattern.Single:
                    positions.Add(center);
                    break;
                    
                case SpawnPattern.Line:
                    for (int i = 0; i < _spawnCount; i++)
                    {
                        float x = center.x + (i - (_spawnCount - 1) / 2f) * _spawnRadius * scale * 0.5f;
                        positions.Add(new Vector2(x, center.y));
                    }
                    break;
                    
                case SpawnPattern.Circle:
                    for (int i = 0; i < _spawnCount; i++)
                    {
                        float angle = (i / (float)_spawnCount) * Mathf.PI * 2f;
                        float x = center.x + Mathf.Cos(angle) * _spawnRadius * scale * 0.3f;
                        float y = center.y + Mathf.Sin(angle) * _spawnRadius * scale * 0.3f;
                        positions.Add(new Vector2(x, y));
                    }
                    break;
                    
                case SpawnPattern.Grid:
                    for (int row = 0; row < _spawnCount; row++)
                    {
                        for (int col = 0; col < _spawnCount; col++)
                        {
                            float x = center.x + (col - (_spawnCount - 1) / 2f) * _spawnRadius * scale * 0.3f;
                            float y = center.y + (row - (_spawnCount - 1) / 2f) * _spawnRadius * scale * 0.3f;
                            positions.Add(new Vector2(x, y));
                        }
                    }
                    break;
                    
                case SpawnPattern.Random:
                    for (int i = 0; i < _spawnCount; i++)
                    {
                        // Use deterministic "random" for preview
                        float angle = (i * 137.5f) * Mathf.Deg2Rad;
                        float r = Mathf.Sqrt(i / (float)_spawnCount) * _spawnRadius * scale * 0.35f;
                        positions.Add(new Vector2(center.x + Mathf.Cos(angle) * r, center.y + Mathf.Sin(angle) * r));
                    }
                    break;
            }
            
            // Draw positions
            Color templateColor = _templates[_selectedTemplateIndex].DisplayColor;
            foreach (var pos in positions)
            {
                EditorGUI.DrawRect(new Rect(pos.x - 4, pos.y - 4, 8, 8), templateColor);
            }
            
            // Draw player position indicator
            EditorGUI.DrawRect(new Rect(center.x - 3, rect.yMax - 15, 6, 10), Color.green);
            GUI.Label(new Rect(center.x - 15, rect.yMax - 12, 30, 12), "You", EditorStyles.miniLabel);
        }

        private void DrawSpawnControls()
        {
            EditorGUILayout.BeginHorizontal();
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            
            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            
            if (GUILayout.Button("Spawn Targets", GUILayout.Height(35)))
            {
                SpawnTargets();
            }
            
            EditorGUI.EndDisabledGroup();
            
            GUI.backgroundColor = prevColor;
            
            if (GUILayout.Button("Clear All", GUILayout.Height(35), GUILayout.Width(100)))
            {
                ClearAllTargets();
            }
            
            if (GUILayout.Button("Reset Health", GUILayout.Height(35), GUILayout.Width(100)))
            {
                ResetTargetHealth();
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawActiveTargets()
        {
            EditorGUILayout.LabelField($"Active Targets ({_activeTargets.Count})", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_activeTargets.Count == 0)
            {
                EditorGUILayout.LabelField("No active targets.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                // Header
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("ID", EditorStyles.boldLabel, GUILayout.Width(30));
                EditorGUILayout.LabelField("Template", EditorStyles.boldLabel, GUILayout.Width(100));
                EditorGUILayout.LabelField("Health", EditorStyles.boldLabel, GUILayout.Width(150));
                EditorGUILayout.LabelField("Hits", EditorStyles.boldLabel, GUILayout.Width(50));
                EditorGUILayout.LabelField("Damage", EditorStyles.boldLabel, GUILayout.Width(70));
                EditorGUILayout.LabelField("", GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();

                foreach (var target in _activeTargets)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    EditorGUILayout.LabelField($"#{target.Id}", GUILayout.Width(30));
                    EditorGUILayout.LabelField(target.TemplateName, GUILayout.Width(100));
                    
                    // Health bar
                    Rect healthRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                        GUILayout.Width(150), GUILayout.Height(18));
                    DrawHealthBar(healthRect, target.CurrentHealth, target.MaxHealth);
                    
                    EditorGUILayout.LabelField(target.HitCount.ToString(), GUILayout.Width(50));
                    EditorGUILayout.LabelField($"{target.TotalDamage:F0}", GUILayout.Width(70));
                    
                    if (GUILayout.Button("Focus", GUILayout.Width(50)))
                    {
                        FocusTarget(target);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                // Summary
                EditorGUILayout.Space(5);
                int totalHits = _activeTargets.Sum(t => t.HitCount);
                float totalDamage = _activeTargets.Sum(t => t.TotalDamage);
                EditorGUILayout.LabelField($"Total: {totalHits} hits, {totalDamage:F0} damage", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawHealthBar(Rect rect, float current, float max)
        {
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
            
            float ratio = Mathf.Clamp01(current / max);
            Color barColor = ratio > 0.5f ? Color.green : ratio > 0.25f ? Color.yellow : Color.red;
            
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width * ratio, rect.height), barColor);
            
            GUI.Label(rect, $"{current:F0}/{max:F0}", 
                new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });
        }

        private void DrawDisplaySettings()
        {
            EditorGUILayout.LabelField("Display Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            _showHealthBars = EditorGUILayout.Toggle("Health Bars", _showHealthBars);
            _showDamageNumbers = EditorGUILayout.Toggle("Damage Numbers", _showDamageNumbers);
            _showHitMarkers = EditorGUILayout.Toggle("Hit Markers", _showHitMarkers);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void SpawnTargets()
        {
            var template = _templates[_selectedTemplateIndex];
            int count = _spawnPattern == SpawnPattern.Single ? 1 : 
                       _spawnPattern == SpawnPattern.Grid ? _spawnCount * _spawnCount : _spawnCount;
            
            for (int i = 0; i < count; i++)
            {
                _activeTargets.Add(new SpawnedTarget
                {
                    Id = _activeTargets.Count + 1,
                    TemplateName = template.Name,
                    CurrentHealth = template.Health,
                    MaxHealth = template.Health
                });
            }
            
            Debug.Log($"[TargetSpawner] Spawned {count} targets ({template.Name})");
        }

        private void ClearAllTargets()
        {
            _activeTargets.Clear();
            Debug.Log("[TargetSpawner] Cleared all targets");
        }

        private void ResetTargetHealth()
        {
            foreach (var target in _activeTargets)
            {
                target.CurrentHealth = target.MaxHealth;
                target.HitCount = 0;
                target.TotalDamage = 0;
            }
            Debug.Log("[TargetSpawner] Reset target health");
        }

        private void FocusTarget(SpawnedTarget target)
        {
            Debug.Log($"[TargetSpawner] Focus target #{target.Id}");
        }
    }
}
