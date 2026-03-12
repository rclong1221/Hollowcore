using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using DIG.Voxel.Debug;

namespace DIG.Voxel.Editor
{
    public class PerformanceDashboard : EditorWindow
    {
        [MenuItem("DIG/Voxel/Performance Dashboard")]
        static void ShowWindow() => GetWindow<PerformanceDashboard>("Perf Dashboard");
        
        private List<ProfilerTimingData> _timingsCache = new();
        private Dictionary<string, float> _timingsDict = new();
        private VoxelPerformanceBudget _budgetConfig;
        
        private void OnEnable()
        {
            // Try to load default budget
            _budgetConfig = Resources.Load<VoxelPerformanceBudget>("VoxelPerformanceBudget");
        }
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Voxel Performance", EditorStyles.boldLabel);
            
            _budgetConfig = (VoxelPerformanceBudget)EditorGUILayout.ObjectField("Budget Config", _budgetConfig, typeof(VoxelPerformanceBudget), false);
            
            if (_budgetConfig == null)
            {
                EditorGUILayout.HelpBox("No Budget Configuration selected. Standard thresholds will be used.", MessageType.Info);
                if (GUILayout.Button("Create Default Budget Config"))
                {
                    CreateDefaultBudget();
                }
            }
            
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode for live data.", MessageType.Info);
                return;
            }
            
            UpdateTimings();
            
            // Header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("System", GUILayout.Width(200));
            GUILayout.Label("Avg (ms)", GUILayout.Width(60));
            GUILayout.Label("Max (ms)", GUILayout.Width(60));
            GUILayout.Label("Budget", GUILayout.Width(60));
            GUILayout.Label("Calls/s", GUILayout.Width(60));
            GUILayout.Label("Status", GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            
            // Scroll view
            // (Assuming list isn't huge, standard loop is fine)
            
            float totalAvg = 0f;
            
            foreach (var data in _timingsCache)
            {
                DrawTimingRow(data);
                totalAvg += data.AvgMs;
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            
            // Total Frame Impact
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Total Tracked Impact: {totalAvg:F2} ms", EditorStyles.boldLabel);
            
            float frameBudget = 16.6f; // 60fps
            if (_budgetConfig != null) 
            {
                var totalBudget = _budgetConfig.GetBudget("Total");
                if (totalBudget.MaxAvgMs > 0) frameBudget = totalBudget.MaxAvgMs;
            }
            
            if (totalAvg > frameBudget)
            {
                var style = new GUIStyle(EditorStyles.helpBox);
                style.normal.textColor = Color.red;
                GUILayout.Label($"EXCEEDS BUDGET ({frameBudget}ms)", style);
            }
            else
            {
                GUILayout.Label("WITHIN BUDGET", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();
            
             // Actions
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Stats"))
                VoxelProfiler.Reset();
            if (GUILayout.Button("Log to Console"))
                VoxelProfiler.LogStats();
            if (GUILayout.Button("Export CSV"))
                ExportCSV();
            EditorGUILayout.EndHorizontal();
        }
        
        private void UpdateTimings()
        {
            _timingsCache.Clear();
            if (_timingsDict == null) _timingsDict = new Dictionary<string, float>();
            VoxelProfiler.PopulateTimings(_timingsDict);
            
            foreach (var kvp in _timingsDict)
            {
                float budget = 0f;
                if (_budgetConfig != null)
                {
                    budget = _budgetConfig.GetBudget(kvp.Key).MaxAvgMs;
                }
                
                _timingsCache.Add(new ProfilerTimingData
                {
                    Name = kvp.Key,
                    AvgMs = VoxelProfiler.GetAverageMs(kvp.Key),
                    MaxMs = VoxelProfiler.GetMaxMs(kvp.Key),
                    CallCount = VoxelProfiler.GetCallCount(kvp.Key),
                    BudgetMs = budget
                });
            }
            
            // Sort by Avg desc
            _timingsCache.Sort((a, b) => b.AvgMs.CompareTo(a.AvgMs));
        }
        
        private void DrawTimingRow(ProfilerTimingData data)
        {
            // Determine status
            bool overBudget = data.BudgetMs > 0 && data.AvgMs > data.BudgetMs;
            
            Color rowColor = overBudget ? new Color(1f, 0.8f, 0.8f) : GUI.backgroundColor;
            GUI.backgroundColor = rowColor;
            
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white; // Reset
            
            GUILayout.Label(data.Name, GUILayout.Width(200));
            GUILayout.Label(data.AvgMs.ToString("F2"), GUILayout.Width(60));
            GUILayout.Label(data.MaxMs.ToString("F2"), GUILayout.Width(60));
            
            string budgetStr = data.BudgetMs > 0 ? data.BudgetMs.ToString("F1") : "-";
            GUILayout.Label(budgetStr, GUILayout.Width(60));
            
            GUILayout.Label(data.CallCount.ToString("N0"), GUILayout.Width(60));
            
            var statusStyle = new GUIStyle(EditorStyles.label);
            if (overBudget) statusStyle.normal.textColor = Color.red;
            else statusStyle.normal.textColor = new Color(0, 0.5f, 0);
            
            GUILayout.Label(overBudget ? "OVER" : "OK", statusStyle, GUILayout.Width(60));
            
            EditorGUILayout.EndHorizontal();
        }
        
        private struct ProfilerTimingData
        {
            public string Name;
            public float AvgMs;
            public float MaxMs;
            public float BudgetMs;
            public int CallCount;
        }
        
        private void CreateDefaultBudget()
        {
            var asset = ScriptableObject.CreateInstance<VoxelPerformanceBudget>();
            string path = "Assets/Resources/VoxelPerformanceBudget.asset";
            
            // Ensure Resources folder exists
            if (!Directory.Exists("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
                
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            
            _budgetConfig = asset;
            EditorGUIUtility.PingObject(asset);
        }
        
        private void ExportCSV()
        {
            var path = EditorUtility.SaveFilePanel("Export Timings", "", "voxel_perf.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("System,AvgMs,MaxMs,Budget,CallCount");
            
            foreach (var t in _timingsCache)
            {
                sb.AppendLine($"{t.Name},{t.AvgMs:F3},{t.MaxMs:F3},{t.BudgetMs:F1},{t.CallCount}");
            }
            
            File.WriteAllText(path, sb.ToString());
            UnityEngine.Debug.Log($"[Perf] Exported to {path}");
        }
        
        private double _lastRepaint;

        private void OnInspectorUpdate()
        {
            // Throttled repaint at 4Hz max instead of every frame
            if (Application.isPlaying && EditorApplication.timeSinceStartup - _lastRepaint > 0.25)
            {
                _lastRepaint = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }
    }
}
