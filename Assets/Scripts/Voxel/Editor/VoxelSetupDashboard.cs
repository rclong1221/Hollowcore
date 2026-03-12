using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using DIG.Voxel.Core;
using DIG.Voxel.Rendering;
using DIG.Voxel.Geology;

namespace DIG.Voxel.Editor
{
    /// <summary>
    /// Unified dashboard for voxel system setup and validation.
    /// Shows status of all required assets and provides one-click setup.
    /// </summary>
    public class VoxelSetupDashboard : EditorWindow
    {
        private Vector2 _scrollPos;
        private bool _showEpic8 = true;
        private bool _showEpic9 = true;
        private bool _showEpic10 = true;
        
        // Cached status
        private SetupStatus _status;
        private double _lastRefresh;
        
        [MenuItem("DIG/Quick Setup/Open Setup Dashboard", priority = -100)]
        public static void ShowWindow()
        {
            var window = GetWindow<VoxelSetupDashboard>("Voxel Setup Dashboard");
            window.minSize = new Vector2(400, 500);
        }
        
        private void OnEnable()
        {
            RefreshStatus();
        }
        
        private void OnFocus()
        {
            RefreshStatus();
        }
        
        private void OnGUI()
        {
            // Removed auto-refresh - now only refreshes on OnEnable, OnFocus, or manual button click
            // This prevents AssetDatabase.FindAssets() from being called every 2 seconds

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            DrawHeader();
            EditorGUILayout.Space(10);
            
            DrawOverallStatus();
            EditorGUILayout.Space(10);
            
            DrawEpic8Section();
            EditorGUILayout.Space(5);
            
            DrawEpic9Section();
            EditorGUILayout.Space(5);
            
            DrawEpic10Section();
            EditorGUILayout.Space(10);
            
            DrawQuickActions();
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Voxel System Setup Dashboard", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshStatus();
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawOverallStatus()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            int total = _status.TotalChecks;
            int passed = _status.PassedChecks;
            float ratio = total > 0 ? (float)passed / total : 0;
            
            // Progress bar
            var rect = EditorGUILayout.GetControlRect(false, 24);
            EditorGUI.ProgressBar(rect, ratio, $"Setup Progress: {passed}/{total} ({ratio * 100:F0}%)");
            
            EditorGUILayout.Space(5);
            
            // Status icon
            if (passed == total)
            {
                EditorGUILayout.HelpBox("✅ All systems configured! Ready to use.", MessageType.Info);
            }
            else if (passed >= total / 2)
            {
                EditorGUILayout.HelpBox("⚠️ Some optional systems not configured.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("❌ Required systems need configuration. Run Quick Setup.", MessageType.Error);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawEpic8Section()
        {
            _showEpic8 = EditorGUILayout.BeginFoldoutHeaderGroup(_showEpic8, "Core Voxel System (Epic 8)");
            if (_showEpic8)
            {
                EditorGUI.indentLevel++;
                
                DrawStatusRow("Material Registry", _status.HasMaterialRegistry, 
                    "Defines all voxel material types", "VoxelMaterialRegistry");
                DrawStatusRow("Material Definitions", _status.MaterialCount > 0,
                    $"{_status.MaterialCount} materials defined", null);
                DrawStatusRow("Loot Prefabs", _status.LootPrefabCount > 0,
                    $"{_status.LootPrefabCount} loot prefabs", null);
                DrawStatusRow("Texture Config", _status.HasTextureConfig,
                    "Texture2DArray for terrain rendering", "VoxelTextureConfig");
                DrawStatusRow("Texture Array", _status.HasTextureArray,
                    "Built texture array ready", null);
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Setup All (Epic 8)", GUILayout.Width(150)))
                {
                    EditorApplication.ExecuteMenuItem("DIG/Quick Setup/Core/Create Complete Demo");
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawEpic9Section()
        {
            _showEpic9 = EditorGUILayout.BeginFoldoutHeaderGroup(_showEpic9, "Advanced Features (Epic 9)");
            if (_showEpic9)
            {
                EditorGUI.indentLevel++;
                
                DrawStatusRow("Visual Materials", _status.VisualMaterialCount > 0,
                    $"{_status.VisualMaterialCount} visual materials", null);
                DrawStatusRow("LOD Config", _status.HasLODConfig,
                    "Level of Detail settings", "VoxelLODConfig");
                DrawStatusRow("Profiler Config", _status.HasProfilerConfig,
                    "Performance profiling settings", "VoxelProfilerConfig");
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Setup All (Epic 9)", GUILayout.Width(150)))
                {
                    EditorApplication.ExecuteMenuItem("DIG/Quick Setup/Advanced/Create Complete Advanced Setup");
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawEpic10Section()
        {
            _showEpic10 = EditorGUILayout.BeginFoldoutHeaderGroup(_showEpic10, "Geology & Resources (Epic 10)");
            if (_showEpic10)
            {
                EditorGUI.indentLevel++;
                
                DrawStatusRow("World Generation Config", _status.HasWorldConfig,
                    "Master config for terrain generation", "WorldGenerationConfig");
                DrawStatusRow("Strata Profile", _status.HasStrataProfile,
                    "Rock layer configuration", "DefaultStrataProfile");
                DrawStatusRow("Ore Definitions", _status.OreCount > 0,
                    $"{_status.OreCount} ore types defined", null);
                DrawStatusRow("Depth Curve", _status.HasDepthCurve,
                    "Rarity curves by depth", "DefaultDepthCurve");
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Setup All (Epic 10)", GUILayout.Width(150)))
                {
                    EditorApplication.ExecuteMenuItem("DIG/Quick Setup/Generation/Create Complete Geology Setup");
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawStatusRow(string label, bool status, string detail, string assetName)
        {
            EditorGUILayout.BeginHorizontal();
            
            // Status icon
            GUILayout.Label(status ? "✅" : "❌", GUILayout.Width(20));
            
            // Label
            GUILayout.Label(label, GUILayout.Width(150));
            
            // Detail (gray)
            var style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = Color.gray;
            GUILayout.Label(detail, style);
            
            GUILayout.FlexibleSpace();
            
            // Locate button
            if (status && !string.IsNullOrEmpty(assetName))
            {
                if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    var asset = Resources.Load(assetName);
                    if (asset != null)
                    {
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                    }
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawQuickActions()
        {
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Complete Demo Setup\n(Epic 8 + 10)", GUILayout.Height(40)))
            {
                EditorApplication.ExecuteMenuItem("DIG/Quick Setup/Core/Create Complete Demo");
                EditorApplication.ExecuteMenuItem("DIG/Quick Setup/Generation/Create Complete Geology Setup");
                RefreshStatus();
            }
            
            if (GUILayout.Button("Validate All\nSystems", GUILayout.Height(40)))
            {
                EditorApplication.ExecuteMenuItem("DIG/Quick Setup/Core/Validate Current Setup");
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Strata\nVisualizer"))
            {
                GetWindow<StrataVisualizerWindow>("Strata Visualizer");
            }
            if (GUILayout.Button("Open Ore\nDistribution"))
            {
                GetWindow<OreDistributionWindow>("Ore Distribution");
            }
            if (GUILayout.Button("Open Streaming\nVisualizer"))
            {
                EditorApplication.ExecuteMenuItem("DIG/Voxel/Debug/Streaming Visualizer");
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void RefreshStatus()
        {
            _status = new SetupStatus();
            _lastRefresh = EditorApplication.timeSinceStartup;
            
            // Epic 8 checks
            var registry = Resources.Load<VoxelMaterialRegistry>("VoxelMaterialRegistry");
            _status.HasMaterialRegistry = registry != null;
            _status.MaterialCount = registry?.Materials?.Count ?? 0;
            
            // Count materials with loot
            if (registry?.Materials != null)
            {
                foreach (var mat in registry.Materials)
                {
                    if (mat != null && mat.LootPrefab != null)
                        _status.LootPrefabCount++;
                }
            }
            
            var texConfig = Resources.Load<VoxelTextureConfig>("VoxelTextureConfig");
            _status.HasTextureConfig = texConfig != null;
            _status.HasTextureArray = texConfig?.TextureArray != null;
            
            // Epic 9 checks
            string[] visualPaths = AssetDatabase.FindAssets("t:VoxelVisualMaterial", new[] { "Assets/Resources/VisualMaterials" });
            _status.VisualMaterialCount = visualPaths.Length;
            
            var lodConfig = Resources.Load<VoxelLODConfig>("VoxelLODConfig");
            _status.HasLODConfig = lodConfig != null;
            
            var profilerConfig = Resources.Load<VoxelProfilerConfig>("VoxelProfilerConfig");
            _status.HasProfilerConfig = profilerConfig != null;
            
            // Epic 10 checks
            var worldConfig = Resources.Load<WorldGenerationConfig>("WorldGenerationConfig");
            _status.HasWorldConfig = worldConfig != null;
            _status.HasStrataProfile = worldConfig?.StrataProfile != null;
            _status.HasDepthCurve = worldConfig?.DepthCurve != null;
            _status.OreCount = worldConfig?.OreDefinitions?.Length ?? 0;
            
            // Calculate totals (12 checks total now)
            _status.TotalChecks = 12;
            _status.PassedChecks = 0;
            // Epic 8
            if (_status.HasMaterialRegistry) _status.PassedChecks++;
            if (_status.MaterialCount > 0) _status.PassedChecks++;
            if (_status.LootPrefabCount > 0) _status.PassedChecks++;
            if (_status.HasTextureConfig) _status.PassedChecks++;
            if (_status.HasTextureArray) _status.PassedChecks++;
            // Epic 9
            if (_status.VisualMaterialCount > 0) _status.PassedChecks++;
            if (_status.HasLODConfig) _status.PassedChecks++;
            if (_status.HasProfilerConfig) _status.PassedChecks++;
            // Epic 10
            if (_status.HasWorldConfig) _status.PassedChecks++;
            if (_status.HasStrataProfile) _status.PassedChecks++;
            if (_status.HasDepthCurve) _status.PassedChecks++;
            if (_status.OreCount > 0) _status.PassedChecks++;
            
            Repaint();
        }
        
        private struct SetupStatus
        {
            // Epic 8
            public bool HasMaterialRegistry;
            public int MaterialCount;
            public int LootPrefabCount;
            public bool HasTextureConfig;
            public bool HasTextureArray;
            
            // Epic 9
            public int VisualMaterialCount;
            public bool HasLODConfig;
            public bool HasProfilerConfig;
            
            // Epic 10
            public bool HasWorldConfig;
            public bool HasStrataProfile;
            public bool HasDepthCurve;
            public int OreCount;
            
            // Summary
            public int TotalChecks;
            public int PassedChecks;
        }
    }
}
