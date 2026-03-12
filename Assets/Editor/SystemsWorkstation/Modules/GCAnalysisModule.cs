using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.SystemsWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 SW-05: GC Analysis module.
    /// Allocation tracking, spike detection.
    /// </summary>
    public class GCAnalysisModule : ISystemsModule
    {
        private Vector2 _scrollPosition;
        
        // Monitoring state
        private bool _isMonitoring = false;
        private float _updateInterval = 1f;
        private double _lastUpdateTime = 0;
        
        // GC data
        private List<GCSnapshot> _snapshots = new List<GCSnapshot>();
        private List<AllocationSource> _allocationSources = new List<AllocationSource>();
        private const int MAX_SNAPSHOTS = 300;
        
        // Thresholds
        private float _spikeThresholdKB = 100f;
        private float _warningThresholdMB = 50f;
        private float _criticalThresholdMB = 100f;
        
        // Simulated current values
        private float _currentHeapMB = 45f;
        private float _allocPerFrameKB = 2.5f;
        private int _gcCollectCount = 0;
        private float _lastGCTime = 0;

        [System.Serializable]
        private class GCSnapshot
        {
            public float Time;
            public float HeapSizeMB;
            public float AllocatedKB;
            public bool WasGCCollect;
        }

        [System.Serializable]
        private class AllocationSource
        {
            public string Name;
            public string Location;
            public float AllocPerFrameKB;
            public int CallCount;
            public AllocationSeverity Severity;
        }

        private enum AllocationSeverity
        {
            Low,
            Medium,
            High,
            Critical
        }

        public GCAnalysisModule()
        {
            InitializeSimulatedData();
        }

        private void InitializeSimulatedData()
        {
            _allocationSources = new List<AllocationSource>
            {
                new AllocationSource { Name = "String.Format", Location = "DamageNumbers.cs:45", AllocPerFrameKB = 0.8f, CallCount = 12, Severity = AllocationSeverity.Medium },
                new AllocationSource { Name = "new List<>", Location = "EnemyManager.cs:112", AllocPerFrameKB = 2.1f, CallCount = 3, Severity = AllocationSeverity.High },
                new AllocationSource { Name = "GameObject.Instantiate", Location = "ProjectileSpawner.cs:78", AllocPerFrameKB = 5.2f, CallCount = 8, Severity = AllocationSeverity.Critical },
                new AllocationSource { Name = "ToString()", Location = "UIManager.cs:234", AllocPerFrameKB = 0.3f, CallCount = 24, Severity = AllocationSeverity.Low },
                new AllocationSource { Name = "Lambda capture", Location = "EventSystem.cs:89", AllocPerFrameKB = 0.5f, CallCount = 6, Severity = AllocationSeverity.Medium },
                new AllocationSource { Name = "Boxing int", Location = "StatsDisplay.cs:156", AllocPerFrameKB = 0.2f, CallCount = 45, Severity = AllocationSeverity.Low },
                new AllocationSource { Name = "LINQ ToList()", Location = "InventorySystem.cs:201", AllocPerFrameKB = 1.5f, CallCount = 2, Severity = AllocationSeverity.High },
            };

            // Initialize history
            for (int i = 0; i < 60; i++)
            {
                _snapshots.Add(new GCSnapshot
                {
                    Time = i * 0.5f,
                    HeapSizeMB = 40f + Random.Range(-5f, 10f),
                    AllocatedKB = Random.Range(1f, 5f),
                    WasGCCollect = Random.value > 0.95f
                });
            }
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("GC Analysis", EditorStyles.boldLabel);
            
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode for live GC analysis. Showing simulated data.",
                    MessageType.Info);
            }
            
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawMonitorControls();
            EditorGUILayout.Space(10);
            DrawCurrentStats();
            EditorGUILayout.Space(10);
            DrawMemoryGraph();
            EditorGUILayout.Space(10);
            DrawAllocationSources();
            EditorGUILayout.Space(10);
            DrawSpikes();
            EditorGUILayout.Space(10);
            DrawRecommendations();
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
            
            EditorGUILayout.LabelField("Interval:", GUILayout.Width(50));
            _updateInterval = EditorGUILayout.Slider(_updateInterval, 0.1f, 5f, GUILayout.Width(100));
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Force GC", EditorStyles.toolbarButton))
            {
                System.GC.Collect();
                _gcCollectCount++;
            }
            
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
            {
                _snapshots.Clear();
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCurrentStats()
        {
            EditorGUILayout.LabelField("Current Status", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            // Heap size
            EditorGUILayout.BeginVertical(GUILayout.Width(120));
            EditorGUILayout.LabelField("Heap Size", EditorStyles.centeredGreyMiniLabel);
            GUI.color = _currentHeapMB > _criticalThresholdMB ? Color.red :
                       _currentHeapMB > _warningThresholdMB ? Color.yellow : Color.green;
            EditorGUILayout.LabelField($"{_currentHeapMB:F1} MB", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter });
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();
            
            // Alloc per frame
            EditorGUILayout.BeginVertical(GUILayout.Width(120));
            EditorGUILayout.LabelField("Alloc/Frame", EditorStyles.centeredGreyMiniLabel);
            GUI.color = _allocPerFrameKB > 10f ? Color.red :
                       _allocPerFrameKB > 5f ? Color.yellow : Color.green;
            EditorGUILayout.LabelField($"{_allocPerFrameKB:F1} KB", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter });
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();
            
            // GC collections
            EditorGUILayout.BeginVertical(GUILayout.Width(120));
            EditorGUILayout.LabelField("GC Collections", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField(_gcCollectCount.ToString(), 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.EndVertical();
            
            // Time since last GC
            EditorGUILayout.BeginVertical(GUILayout.Width(120));
            EditorGUILayout.LabelField("Last GC", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField($"{_lastGCTime:F1}s ago", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();

            // Threshold settings
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Thresholds:", GUILayout.Width(70));
            _spikeThresholdKB = EditorGUILayout.FloatField("Spike (KB)", _spikeThresholdKB, GUILayout.Width(120));
            _warningThresholdMB = EditorGUILayout.FloatField("Warn (MB)", _warningThresholdMB, GUILayout.Width(120));
            _criticalThresholdMB = EditorGUILayout.FloatField("Crit (MB)", _criticalThresholdMB, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawMemoryGraph()
        {
            EditorGUILayout.LabelField("Memory Over Time", EditorStyles.boldLabel);
            
            Rect graphRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(120), GUILayout.ExpandWidth(true));
            
            DrawGraph(graphRect);
        }

        private void DrawGraph(Rect rect)
        {
            // Background
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));

            // Grid lines
            Handles.color = new Color(0.25f, 0.25f, 0.25f, 0.5f);
            for (int i = 1; i < 4; i++)
            {
                float y = rect.y + rect.height * (i / 4f);
                Handles.DrawLine(new Vector3(rect.x, y), new Vector3(rect.xMax, y));
            }

            if (_snapshots.Count < 2) return;

            float maxHeap = _snapshots.Max(s => s.HeapSizeMB) * 1.1f;
            float maxAlloc = _snapshots.Max(s => s.AllocatedKB) * 1.1f;

            // Draw heap line
            Handles.color = new Color(0.3f, 0.7f, 0.9f);
            for (int i = 1; i < _snapshots.Count; i++)
            {
                float x1 = rect.x + ((i - 1) / (float)MAX_SNAPSHOTS) * rect.width;
                float x2 = rect.x + (i / (float)MAX_SNAPSHOTS) * rect.width;
                float y1 = rect.yMax - (_snapshots[i - 1].HeapSizeMB / maxHeap) * rect.height;
                float y2 = rect.yMax - (_snapshots[i].HeapSizeMB / maxHeap) * rect.height;
                
                Handles.DrawLine(new Vector3(x1, y1), new Vector3(x2, y2));
            }

            // Draw allocation spikes
            Handles.color = new Color(0.9f, 0.5f, 0.2f);
            for (int i = 0; i < _snapshots.Count; i++)
            {
                if (_snapshots[i].AllocatedKB > _spikeThresholdKB)
                {
                    float x = rect.x + (i / (float)MAX_SNAPSHOTS) * rect.width;
                    float height = (_snapshots[i].AllocatedKB / maxAlloc) * rect.height * 0.5f;
                    EditorGUI.DrawRect(new Rect(x - 1, rect.yMax - height, 2, height), 
                        new Color(0.9f, 0.5f, 0.2f, 0.7f));
                }
            }

            // Draw GC markers
            for (int i = 0; i < _snapshots.Count; i++)
            {
                if (_snapshots[i].WasGCCollect)
                {
                    float x = rect.x + (i / (float)MAX_SNAPSHOTS) * rect.width;
                    Handles.color = Color.red;
                    Handles.DrawLine(new Vector3(x, rect.y), new Vector3(x, rect.yMax));
                }
            }

            // Legend
            GUI.Label(new Rect(rect.x + 5, rect.y + 2, 80, 16), "— Heap", EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + 5, rect.y + 16, 80, 16), "█ Alloc Spike", EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + 5, rect.y + 30, 80, 16), "| GC Collect", EditorStyles.miniLabel);
            
            GUI.Label(new Rect(rect.xMax - 50, rect.y + 2, 50, 16), $"{maxHeap:F0} MB", EditorStyles.miniLabel);
        }

        private void DrawAllocationSources()
        {
            EditorGUILayout.LabelField("Top Allocation Sources", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel, GUILayout.Width(150));
            EditorGUILayout.LabelField("Location", EditorStyles.boldLabel, GUILayout.Width(180));
            EditorGUILayout.LabelField("KB/Frame", EditorStyles.boldLabel, GUILayout.Width(70));
            EditorGUILayout.LabelField("Calls", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Severity", EditorStyles.boldLabel, GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            foreach (var source in _allocationSources.OrderByDescending(s => s.AllocPerFrameKB))
            {
                EditorGUILayout.BeginHorizontal();
                
                EditorGUILayout.LabelField(source.Name, GUILayout.Width(150));
                
                if (GUILayout.Button(source.Location, EditorStyles.miniLabel, GUILayout.Width(180)))
                {
                    // Would open file at location
                    Debug.Log($"[GCAnalysis] Open: {source.Location}");
                }
                
                EditorGUILayout.LabelField($"{source.AllocPerFrameKB:F1}", GUILayout.Width(70));
                EditorGUILayout.LabelField(source.CallCount.ToString(), GUILayout.Width(50));
                
                Color severityColor = source.Severity switch
                {
                    AllocationSeverity.Critical => Color.red,
                    AllocationSeverity.High => new Color(1f, 0.6f, 0f),
                    AllocationSeverity.Medium => Color.yellow,
                    _ => Color.green
                };
                
                GUI.color = severityColor;
                EditorGUILayout.LabelField(source.Severity.ToString(), GUILayout.Width(70));
                GUI.color = Color.white;
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSpikes()
        {
            var spikes = _snapshots.Where(s => s.AllocatedKB > _spikeThresholdKB).ToList();
            
            if (spikes.Count == 0) return;
            
            EditorGUILayout.LabelField($"⚠ Allocation Spikes ({spikes.Count})", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            int showCount = Mathf.Min(5, spikes.Count);
            foreach (var spike in spikes.TakeLast(showCount))
            {
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField($"  {spike.Time:F1}s: {spike.AllocatedKB:F1} KB allocated");
                GUI.color = Color.white;
            }

            if (spikes.Count > showCount)
            {
                EditorGUILayout.LabelField($"  ... and {spikes.Count - showCount} more", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRecommendations()
        {
            EditorGUILayout.LabelField("Recommendations", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var criticalSources = _allocationSources.Where(s => s.Severity == AllocationSeverity.Critical).ToList();
            var highSources = _allocationSources.Where(s => s.Severity == AllocationSeverity.High).ToList();

            if (criticalSources.Count > 0)
            {
                EditorGUILayout.LabelField("🔴 Critical:", EditorStyles.boldLabel);
                foreach (var source in criticalSources)
                {
                    string fix = source.Name switch
                    {
                        "GameObject.Instantiate" => "→ Use object pooling instead",
                        "new List<>" => "→ Reuse collection with Clear()",
                        _ => "→ Review and optimize"
                    };
                    EditorGUILayout.LabelField($"  {source.Name} {fix}");
                }
            }

            if (highSources.Count > 0)
            {
                EditorGUILayout.LabelField("🟠 High Priority:", EditorStyles.boldLabel);
                foreach (var source in highSources)
                {
                    string fix = source.Name switch
                    {
                        "LINQ ToList()" => "→ Use foreach or cached list",
                        "String.Format" => "→ Use StringBuilder or string interpolation cache",
                        _ => "→ Consider caching"
                    };
                    EditorGUILayout.LabelField($"  {source.Name} {fix}");
                }
            }

            if (criticalSources.Count == 0 && highSources.Count == 0)
            {
                EditorGUILayout.LabelField("✅ No critical allocation issues detected", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Deep Profile"))
            {
                DeepProfile();
            }
            
            if (GUILayout.Button("Export Report"))
            {
                ExportReport();
            }
            
            if (GUILayout.Button("Open Profiler"))
            {
                EditorApplication.ExecuteMenuItem("Window/Analysis/Profiler");
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void UpdateSimulation()
        {
            // Simulate allocations
            _allocPerFrameKB = Random.Range(1f, 8f);
            _currentHeapMB += _allocPerFrameKB / 1024f;
            _lastGCTime += _updateInterval;
            
            // Simulate occasional GC
            bool wasGC = false;
            if (_currentHeapMB > 60f || Random.value > 0.95f)
            {
                _currentHeapMB = 40f + Random.Range(0f, 10f);
                _gcCollectCount++;
                _lastGCTime = 0f;
                wasGC = true;
            }
            
            _snapshots.Add(new GCSnapshot
            {
                Time = (float)EditorApplication.timeSinceStartup,
                HeapSizeMB = _currentHeapMB,
                AllocatedKB = _allocPerFrameKB,
                WasGCCollect = wasGC
            });
            
            if (_snapshots.Count > MAX_SNAPSHOTS)
            {
                _snapshots.RemoveAt(0);
            }
        }

        private void DeepProfile()
        {
            Debug.Log("[GCAnalysis] Deep profile pending");
        }

        private void ExportReport()
        {
            Debug.Log("[GCAnalysis] Report export pending");
        }
    }
}
