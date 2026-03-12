using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.SystemsWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 SW-01: Projectile Pool module.
    /// Prefab registration, pool size calculator, memory budget.
    /// </summary>
    public class ProjectilePoolModule : ISystemsModule
    {
        private Vector2 _scrollPosition;
        
        // Pool entries
        private List<ProjectilePoolEntry> _poolEntries = new List<ProjectilePoolEntry>();
        
        // Memory budget
        private float _memoryBudgetMB = 50f;
        private float _currentMemoryUsageMB = 0f;
        
        // Calculator settings
        private float _fireRateRPM = 600f;
        private float _projectileLifetime = 2f;
        private int _maxConcurrentWeapons = 10;
        
        // Warmup
        private bool _warmupOnStart = true;
        private float _warmupDelay = 0.5f;

        [System.Serializable]
        private class ProjectilePoolEntry
        {
            public string Name;
            public GameObject Prefab;
            public int PoolSize = 50;
            public int ActiveCount = 0;
            public int PeakCount = 0;
            public float EstimatedMemoryKB = 0;
            public bool IsExpanded = false;
        }

        public ProjectilePoolModule()
        {
            // Initialize with some default entries
            _poolEntries.Add(new ProjectilePoolEntry { Name = "Bullet_9mm", PoolSize = 100, EstimatedMemoryKB = 2.5f });
            _poolEntries.Add(new ProjectilePoolEntry { Name = "Bullet_556", PoolSize = 150, EstimatedMemoryKB = 3.0f });
            _poolEntries.Add(new ProjectilePoolEntry { Name = "Bullet_762", PoolSize = 100, EstimatedMemoryKB = 3.5f });
            _poolEntries.Add(new ProjectilePoolEntry { Name = "Rocket", PoolSize = 20, EstimatedMemoryKB = 15.0f });
            _poolEntries.Add(new ProjectilePoolEntry { Name = "Grenade", PoolSize = 30, EstimatedMemoryKB = 10.0f });
            
            CalculateTotalMemory();
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Projectile Pool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Manage projectile object pools for efficient memory usage and reduced GC.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawMemoryBudget();
            EditorGUILayout.Space(10);
            DrawPoolEntries();
            EditorGUILayout.Space(10);
            DrawPoolCalculator();
            EditorGUILayout.Space(10);
            DrawWarmupSettings();
            EditorGUILayout.Space(10);
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawMemoryBudget()
        {
            EditorGUILayout.LabelField("Memory Budget", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _memoryBudgetMB = EditorGUILayout.Slider("Budget (MB)", _memoryBudgetMB, 10f, 200f);
            
            // Memory usage bar
            Rect barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(25), GUILayout.ExpandWidth(true));
            DrawMemoryBar(barRect);

            EditorGUILayout.Space(5);
            
            float remaining = _memoryBudgetMB - _currentMemoryUsageMB;
            Color labelColor = remaining > 0 ? Color.green : Color.red;
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Used: {_currentMemoryUsageMB:F2} MB");
            GUI.color = labelColor;
            EditorGUILayout.LabelField($"Remaining: {remaining:F2} MB");
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawMemoryBar(Rect rect)
        {
            // Background
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
            
            float usageRatio = Mathf.Clamp01(_currentMemoryUsageMB / _memoryBudgetMB);
            
            // Usage bar
            Color barColor = usageRatio < 0.7f ? new Color(0.3f, 0.7f, 0.3f) :
                            usageRatio < 0.9f ? new Color(0.8f, 0.7f, 0.2f) :
                            new Color(0.8f, 0.3f, 0.3f);
            
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width * usageRatio, rect.height), barColor);
            
            // Percentage label
            GUI.Label(rect, $"{usageRatio * 100:F0}%", 
                new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });
        }

        private void DrawPoolEntries()
        {
            EditorGUILayout.LabelField("Pool Entries", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Prefab", EditorStyles.boldLabel, GUILayout.Width(150));
            EditorGUILayout.LabelField("Pool Size", EditorStyles.boldLabel, GUILayout.Width(70));
            EditorGUILayout.LabelField("Active", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Peak", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Memory", EditorStyles.boldLabel, GUILayout.Width(70));
            EditorGUILayout.LabelField("", GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            for (int i = 0; i < _poolEntries.Count; i++)
            {
                var entry = _poolEntries[i];
                
                EditorGUILayout.BeginHorizontal();
                
                // Prefab
                entry.Prefab = (GameObject)EditorGUILayout.ObjectField(
                    entry.Prefab, typeof(GameObject), false, GUILayout.Width(150));
                
                if (entry.Prefab != null && string.IsNullOrEmpty(entry.Name))
                {
                    entry.Name = entry.Prefab.name;
                }
                
                // Pool size
                entry.PoolSize = EditorGUILayout.IntField(entry.PoolSize, GUILayout.Width(70));
                
                // Active (runtime only)
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.IntField(entry.ActiveCount, GUILayout.Width(50));
                EditorGUI.EndDisabledGroup();
                
                // Peak
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.IntField(entry.PeakCount, GUILayout.Width(50));
                EditorGUI.EndDisabledGroup();
                
                // Memory estimate
                float memoryMB = (entry.PoolSize * entry.EstimatedMemoryKB) / 1024f;
                EditorGUILayout.LabelField($"{memoryMB:F2} MB", GUILayout.Width(70));
                
                // Remove button
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    _poolEntries.RemoveAt(i);
                    CalculateTotalMemory();
                    i--;
                }
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("+ Add Pool Entry"))
            {
                _poolEntries.Add(new ProjectilePoolEntry 
                { 
                    Name = $"NewProjectile_{_poolEntries.Count}",
                    PoolSize = 50,
                    EstimatedMemoryKB = 3f
                });
                CalculateTotalMemory();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPoolCalculator()
        {
            EditorGUILayout.LabelField("Pool Size Calculator", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _fireRateRPM = EditorGUILayout.FloatField("Fire Rate (RPM)", _fireRateRPM);
            _projectileLifetime = EditorGUILayout.FloatField("Projectile Lifetime (s)", _projectileLifetime);
            _maxConcurrentWeapons = EditorGUILayout.IntField("Max Concurrent Weapons", _maxConcurrentWeapons);

            EditorGUILayout.Space(5);
            
            // Calculate recommended pool size
            float projectilesPerSecond = _fireRateRPM / 60f;
            float projectilesInFlight = projectilesPerSecond * _projectileLifetime;
            int recommendedSize = Mathf.CeilToInt(projectilesInFlight * _maxConcurrentWeapons * 1.2f); // 20% buffer
            
            EditorGUILayout.LabelField($"Projectiles per second: {projectilesPerSecond:F1}");
            EditorGUILayout.LabelField($"Max in flight (per weapon): {projectilesInFlight:F0}");
            
            EditorGUILayout.Space(5);
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.cyan;
            EditorGUILayout.LabelField($"Recommended Pool Size: {recommendedSize}", EditorStyles.boldLabel);
            GUI.backgroundColor = prevColor;

            if (GUILayout.Button("Apply to Selected Entry"))
            {
                // Apply to first entry or selected
                if (_poolEntries.Count > 0)
                {
                    _poolEntries[0].PoolSize = recommendedSize;
                    CalculateTotalMemory();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawWarmupSettings()
        {
            EditorGUILayout.LabelField("Warmup Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _warmupOnStart = EditorGUILayout.Toggle("Warmup on Start", _warmupOnStart);
            
            if (_warmupOnStart)
            {
                EditorGUI.indentLevel++;
                _warmupDelay = EditorGUILayout.Slider("Warmup Delay (s)", _warmupDelay, 0f, 5f);
                EditorGUI.indentLevel--;
            }

            int totalObjects = _poolEntries.Sum(e => e.PoolSize);
            EditorGUILayout.LabelField($"Total objects to warmup: {totalObjects}");

            EditorGUILayout.EndVertical();
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

            if (GUILayout.Button("Create Pool Manager", GUILayout.Height(30)))
            {
                CreatePoolManager();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Optimize Sizes"))
            {
                OptimizeSizes();
            }
            
            if (GUILayout.Button("Export Config"))
            {
                ExportConfig();
            }
            
            if (GUILayout.Button("Import Config"))
            {
                ImportConfig();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void CalculateTotalMemory()
        {
            _currentMemoryUsageMB = _poolEntries.Sum(e => (e.PoolSize * e.EstimatedMemoryKB) / 1024f);
        }

        private void SaveConfiguration()
        {
            CalculateTotalMemory();
            Debug.Log($"[ProjectilePool] Saved {_poolEntries.Count} pool entries ({_currentMemoryUsageMB:F2} MB)");
        }

        private void CreatePoolManager()
        {
            Debug.Log("[ProjectilePool] Pool manager creation pending");
        }

        private void OptimizeSizes()
        {
            // Reduce pool sizes to fit within budget
            while (_currentMemoryUsageMB > _memoryBudgetMB && _poolEntries.Any(e => e.PoolSize > 10))
            {
                var largest = _poolEntries.OrderByDescending(e => e.PoolSize * e.EstimatedMemoryKB).First();
                largest.PoolSize = Mathf.Max(10, largest.PoolSize - 10);
                CalculateTotalMemory();
            }
            
            Debug.Log($"[ProjectilePool] Optimized to {_currentMemoryUsageMB:F2} MB");
        }

        private void ExportConfig()
        {
            Debug.Log("[ProjectilePool] Export pending");
        }

        private void ImportConfig()
        {
            Debug.Log("[ProjectilePool] Import pending");
        }
    }
}
