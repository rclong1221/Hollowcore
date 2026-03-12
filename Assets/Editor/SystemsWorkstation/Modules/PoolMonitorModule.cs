using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.SystemsWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 SW-04: Pool Monitor module.
    /// Runtime pool stats, utilization graphs.
    /// </summary>
    public class PoolMonitorModule : ISystemsModule
    {
        private Vector2 _scrollPosition;
        
        // Monitoring state
        private bool _isMonitoring = false;
        private float _updateInterval = 0.5f;
        private double _lastUpdateTime = 0;
        
        // Pool data (simulated for editor)
        private List<PoolStats> _poolStats = new List<PoolStats>();
        private List<float> _utilizationHistory = new List<float>();
        private const int MAX_HISTORY = 100;
        
        // Filters
        private string _searchFilter = "";
        private PoolCategory _categoryFilter = PoolCategory.All;
        private bool _showOnlyActive = false;
        private float _alertThreshold = 0.8f;

        [System.Serializable]
        private class PoolStats
        {
            public string Name;
            public PoolCategory Category;
            public int PoolSize;
            public int ActiveCount;
            public int PeakCount;
            public float Utilization => PoolSize > 0 ? (float)ActiveCount / PoolSize : 0f;
            public int SpawnsPerSecond;
            public int ReturnsPerSecond;
            public float AverageActiveTime;
            public List<float> UtilizationHistory = new List<float>();
        }

        private enum PoolCategory
        {
            All,
            Projectiles,
            VFX,
            Entities,
            Audio,
            UI
        }

        public PoolMonitorModule()
        {
            InitializeSimulatedData();
        }

        private void InitializeSimulatedData()
        {
            _poolStats = new List<PoolStats>
            {
                new PoolStats { Name = "Bullet_9mm", Category = PoolCategory.Projectiles, PoolSize = 100, ActiveCount = 45, PeakCount = 78 },
                new PoolStats { Name = "Bullet_556", Category = PoolCategory.Projectiles, PoolSize = 150, ActiveCount = 62, PeakCount = 120 },
                new PoolStats { Name = "MuzzleFlash", Category = PoolCategory.VFX, PoolSize = 30, ActiveCount = 8, PeakCount = 22 },
                new PoolStats { Name = "BulletImpact", Category = PoolCategory.VFX, PoolSize = 50, ActiveCount = 23, PeakCount = 45 },
                new PoolStats { Name = "BloodSpatter", Category = PoolCategory.VFX, PoolSize = 40, ActiveCount = 15, PeakCount = 38 },
                new PoolStats { Name = "Enemy_Basic", Category = PoolCategory.Entities, PoolSize = 30, ActiveCount = 18, PeakCount = 28 },
                new PoolStats { Name = "DamageNumber", Category = PoolCategory.UI, PoolSize = 50, ActiveCount = 12, PeakCount = 35 },
                new PoolStats { Name = "SoundOneShot", Category = PoolCategory.Audio, PoolSize = 20, ActiveCount = 5, PeakCount = 18 },
            };

            // Initialize history
            foreach (var stats in _poolStats)
            {
                for (int i = 0; i < MAX_HISTORY; i++)
                {
                    stats.UtilizationHistory.Add(Random.Range(0f, stats.Utilization * 1.2f));
                }
            }
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Pool Monitor", EditorStyles.boldLabel);
            
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode for live monitoring. Showing simulated data.",
                    MessageType.Info);
            }
            
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawMonitorControls();
            EditorGUILayout.Space(10);
            DrawOverviewStats();
            EditorGUILayout.Space(10);
            DrawUtilizationGraph();
            EditorGUILayout.Space(10);
            DrawPoolTable();
            EditorGUILayout.Space(10);
            DrawAlerts();
            EditorGUILayout.Space(10);
            DrawActions();

            EditorGUILayout.EndScrollView();

            // Update simulation
            if (_isMonitoring && EditorApplication.timeSinceStartup - _lastUpdateTime > _updateInterval)
            {
                UpdateSimulation();
                _lastUpdateTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void Repaint()
        {
            EditorWindow.GetWindow<SystemsWorkstationWindow>()?.Repaint();
        }

        private void DrawMonitorControls()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = _isMonitoring ? Color.red : Color.green;
            
            if (GUILayout.Button(_isMonitoring ? "■ Stop" : "● Monitor", 
                EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                _isMonitoring = !_isMonitoring;
            }
            
            GUI.backgroundColor = prevColor;
            
            GUILayout.Space(10);
            
            EditorGUILayout.LabelField("Update:", GUILayout.Width(50));
            _updateInterval = EditorGUILayout.Slider(_updateInterval, 0.1f, 2f, GUILayout.Width(100));
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Reset Stats", EditorStyles.toolbarButton))
            {
                ResetStats();
            }
            
            if (GUILayout.Button("Export CSV", EditorStyles.toolbarButton))
            {
                ExportCSV();
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawOverviewStats()
        {
            EditorGUILayout.LabelField("Overview", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            // Total pools
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            EditorGUILayout.LabelField("Total Pools", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField(_poolStats.Count.ToString(), 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.EndVertical();
            
            // Total capacity
            int totalCapacity = _poolStats.Sum(p => p.PoolSize);
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            EditorGUILayout.LabelField("Capacity", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField(totalCapacity.ToString(), 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.EndVertical();
            
            // Active objects
            int totalActive = _poolStats.Sum(p => p.ActiveCount);
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            EditorGUILayout.LabelField("Active", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField(totalActive.ToString(), 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.EndVertical();
            
            // Overall utilization
            float overallUtil = totalCapacity > 0 ? (float)totalActive / totalCapacity : 0f;
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            EditorGUILayout.LabelField("Utilization", EditorStyles.centeredGreyMiniLabel);
            GUI.color = GetUtilizationColor(overallUtil);
            EditorGUILayout.LabelField($"{overallUtil * 100:F0}%", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleCenter });
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();
            
            // Alerts count
            int alertCount = _poolStats.Count(p => p.Utilization > _alertThreshold);
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            EditorGUILayout.LabelField("Alerts", EditorStyles.centeredGreyMiniLabel);
            GUI.color = alertCount > 0 ? Color.red : Color.green;
            EditorGUILayout.LabelField(alertCount.ToString(), 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleCenter });
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawUtilizationGraph()
        {
            EditorGUILayout.LabelField("Utilization Over Time", EditorStyles.boldLabel);
            
            Rect graphRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(100), GUILayout.ExpandWidth(true));
            
            DrawGraph(graphRect);
        }

        private void DrawGraph(Rect rect)
        {
            // Background
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

            // Grid lines
            Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            for (int i = 1; i < 4; i++)
            {
                float y = rect.y + rect.height * (i / 4f);
                Handles.DrawLine(new Vector3(rect.x, y), new Vector3(rect.xMax, y));
            }

            // Threshold line
            float thresholdY = rect.yMax - _alertThreshold * rect.height;
            Handles.color = new Color(1f, 0.5f, 0.2f, 0.5f);
            Handles.DrawLine(new Vector3(rect.x, thresholdY), new Vector3(rect.xMax, thresholdY));

            // Draw each pool's history
            Color[] poolColors = { Color.green, Color.cyan, Color.yellow, Color.magenta, Color.red };
            
            for (int p = 0; p < Mathf.Min(_poolStats.Count, 5); p++)
            {
                var stats = _poolStats[p];
                if (stats.UtilizationHistory.Count < 2) continue;
                
                Handles.color = poolColors[p % poolColors.Length];
                
                for (int i = 1; i < stats.UtilizationHistory.Count; i++)
                {
                    float x1 = rect.x + ((i - 1) / (float)MAX_HISTORY) * rect.width;
                    float x2 = rect.x + (i / (float)MAX_HISTORY) * rect.width;
                    float y1 = rect.yMax - Mathf.Clamp01(stats.UtilizationHistory[i - 1]) * rect.height;
                    float y2 = rect.yMax - Mathf.Clamp01(stats.UtilizationHistory[i]) * rect.height;
                    
                    Handles.DrawLine(new Vector3(x1, y1), new Vector3(x2, y2));
                }
            }

            // Labels
            GUI.Label(new Rect(rect.x + 5, rect.y + 2, 50, 16), "100%", EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + 5, rect.yMax - 16, 50, 16), "0%", EditorStyles.miniLabel);
        }

        private void DrawPoolTable()
        {
            EditorGUILayout.LabelField("Pool Details", EditorStyles.boldLabel);
            
            // Filters
            EditorGUILayout.BeginHorizontal();
            _searchFilter = EditorGUILayout.TextField("Search", _searchFilter, GUILayout.Width(200));
            _categoryFilter = (PoolCategory)EditorGUILayout.EnumPopup(_categoryFilter, GUILayout.Width(100));
            _showOnlyActive = EditorGUILayout.Toggle("Active Only", _showOnlyActive);
            _alertThreshold = EditorGUILayout.Slider("Alert @", _alertThreshold, 0.5f, 1f, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Pool Name", EditorStyles.boldLabel, GUILayout.Width(120));
            EditorGUILayout.LabelField("Category", EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField("Active/Size", EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField("Peak", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Util", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Graph", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // Filtered entries
            var filtered = _poolStats
                .Where(p => string.IsNullOrEmpty(_searchFilter) || 
                           p.Name.ToLower().Contains(_searchFilter.ToLower()))
                .Where(p => _categoryFilter == PoolCategory.All || p.Category == _categoryFilter)
                .Where(p => !_showOnlyActive || p.ActiveCount > 0)
                .OrderByDescending(p => p.Utilization);

            foreach (var stats in filtered)
            {
                EditorGUILayout.BeginHorizontal();
                
                // Name with alert indicator
                Color textColor = stats.Utilization > _alertThreshold ? Color.red : Color.white;
                GUI.color = textColor;
                EditorGUILayout.LabelField(stats.Name, GUILayout.Width(120));
                GUI.color = Color.white;
                
                // Category
                EditorGUILayout.LabelField(stats.Category.ToString(), GUILayout.Width(80));
                
                // Active/Size
                EditorGUILayout.LabelField($"{stats.ActiveCount}/{stats.PoolSize}", GUILayout.Width(80));
                
                // Peak
                EditorGUILayout.LabelField(stats.PeakCount.ToString(), GUILayout.Width(50));
                
                // Utilization
                GUI.color = GetUtilizationColor(stats.Utilization);
                EditorGUILayout.LabelField($"{stats.Utilization * 100:F0}%", GUILayout.Width(50));
                GUI.color = Color.white;
                
                // Mini graph
                Rect miniGraphRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                    GUILayout.Height(16), GUILayout.ExpandWidth(true));
                DrawMiniGraph(miniGraphRect, stats);
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawMiniGraph(Rect rect, PoolStats stats)
        {
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
            
            if (stats.UtilizationHistory.Count < 2) return;
            
            Color barColor = GetUtilizationColor(stats.Utilization);
            
            int showCount = Mathf.Min(30, stats.UtilizationHistory.Count);
            float barWidth = rect.width / showCount;
            
            for (int i = 0; i < showCount; i++)
            {
                int histIndex = stats.UtilizationHistory.Count - showCount + i;
                float util = Mathf.Clamp01(stats.UtilizationHistory[histIndex]);
                float barHeight = util * rect.height;
                
                EditorGUI.DrawRect(new Rect(
                    rect.x + i * barWidth,
                    rect.yMax - barHeight,
                    barWidth - 1,
                    barHeight), barColor);
            }
        }

        private Color GetUtilizationColor(float util)
        {
            if (util < 0.5f) return Color.green;
            if (util < 0.8f) return Color.yellow;
            return Color.red;
        }

        private void DrawAlerts()
        {
            var alerts = _poolStats.Where(p => p.Utilization > _alertThreshold).ToList();
            
            if (alerts.Count == 0) return;
            
            EditorGUILayout.LabelField("⚠ Alerts", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            foreach (var alert in alerts)
            {
                EditorGUILayout.BeginHorizontal();
                
                GUI.color = Color.red;
                EditorGUILayout.LabelField($"⚠ {alert.Name}: {alert.Utilization * 100:F0}% utilization", 
                    EditorStyles.wordWrappedLabel);
                GUI.color = Color.white;
                
                if (GUILayout.Button("Expand", GUILayout.Width(60)))
                {
                    alert.PoolSize = Mathf.CeilToInt(alert.PoolSize * 1.5f);
                }
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Auto-Optimize All"))
            {
                AutoOptimize();
            }
            
            if (GUILayout.Button("Clear History"))
            {
                ClearHistory();
            }
            
            if (GUILayout.Button("Take Snapshot"))
            {
                TakeSnapshot();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void UpdateSimulation()
        {
            foreach (var stats in _poolStats)
            {
                // Simulate activity
                stats.ActiveCount = Mathf.Clamp(
                    stats.ActiveCount + Random.Range(-3, 4),
                    0, stats.PoolSize);
                
                stats.PeakCount = Mathf.Max(stats.PeakCount, stats.ActiveCount);
                
                // Update history
                stats.UtilizationHistory.Add(stats.Utilization);
                if (stats.UtilizationHistory.Count > MAX_HISTORY)
                {
                    stats.UtilizationHistory.RemoveAt(0);
                }
            }
        }

        private void ResetStats()
        {
            foreach (var stats in _poolStats)
            {
                stats.PeakCount = stats.ActiveCount;
                stats.UtilizationHistory.Clear();
            }
        }

        private void ExportCSV()
        {
            Debug.Log("[PoolMonitor] CSV export pending");
        }

        private void AutoOptimize()
        {
            foreach (var stats in _poolStats)
            {
                if (stats.Utilization > 0.9f)
                {
                    stats.PoolSize = Mathf.CeilToInt(stats.PoolSize * 1.5f);
                }
                else if (stats.Utilization < 0.3f && stats.PoolSize > 10)
                {
                    stats.PoolSize = Mathf.Max(10, Mathf.CeilToInt(stats.PoolSize * 0.7f));
                }
            }
            Debug.Log("[PoolMonitor] Auto-optimization complete");
        }

        private void ClearHistory()
        {
            foreach (var stats in _poolStats)
            {
                stats.UtilizationHistory.Clear();
            }
        }

        private void TakeSnapshot()
        {
            Debug.Log("[PoolMonitor] Snapshot saved");
        }
    }
}
