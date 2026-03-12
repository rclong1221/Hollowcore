using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using DIG.Voxel.Geology;

namespace DIG.Voxel.Editor
{
    /// <summary>
    /// Editor window for visualizing ore distribution by depth.
    /// Shows probability curves for each ore type.
    /// </summary>
    public class OreDistributionWindow : EditorWindow
    {
        [MenuItem("DIG/World/Ore Distribution")]
        static void ShowWindow() => GetWindow<OreDistributionWindow>("Ore Distribution");
        
        private OreDefinition[] _ores;
        private DepthValueCurve _depthCurve;
        private Vector2 _scrollPos;
        private float _maxDepth = 200f;
        
        // Graph colors
        private static readonly Color[] _lineColors = {
            new Color(1f, 0.3f, 0.3f),   // Red
            new Color(0.3f, 1f, 0.3f),   // Green
            new Color(0.3f, 0.5f, 1f),   // Blue
            new Color(1f, 1f, 0.3f),     // Yellow
            new Color(0.3f, 1f, 1f),     // Cyan
            new Color(1f, 0.3f, 1f),     // Magenta
            new Color(1f, 0.6f, 0.3f),   // Orange
            new Color(0.6f, 0.3f, 1f)    // Purple
        };
        
        private void OnEnable()
        {
            RefreshOreList();
        }
        
        private void RefreshOreList()
        {
            _ores = Resources.LoadAll<OreDefinition>("");
            _depthCurve = Resources.Load<DepthValueCurve>("DepthValueCurve");
        }
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Ore Distribution Viewer", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Toolbar
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
            {
                RefreshOreList();
            }
            
            _maxDepth = EditorGUILayout.Slider("Max Depth", _maxDepth, 50, 500);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            if (_ores == null || _ores.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No ore definitions found in Resources.\n\n" +
                    "Create ores via: Right-click → Create → DIG → World → Ore Definition\n" +
                    "Then place them in a Resources folder.",
                    MessageType.Info);
                return;
            }
            
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            DrawDistributionGraph();
            
            EditorGUILayout.Space(10);
            
            DrawLegend();
            
            EditorGUILayout.Space(10);
            
            DrawOreTable();
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawDistributionGraph()
        {
            EditorGUILayout.LabelField("Spawn Probability by Depth", EditorStyles.boldLabel);
            
            var rect = GUILayoutUtility.GetRect(position.width - 40, 200);
            
            // Background
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
            
            // Grid
            Handles.color = new Color(0.3f, 0.3f, 0.3f);
            
            // Vertical grid (depth)
            for (float d = 0; d <= _maxDepth; d += 50)
            {
                float x = rect.x + (d / _maxDepth) * rect.width;
                Handles.DrawLine(
                    new Vector3(x, rect.y, 0),
                    new Vector3(x, rect.y + rect.height, 0));
            }
            
            // Horizontal grid (probability)
            for (float p = 0; p <= 1; p += 0.25f)
            {
                float y = rect.y + rect.height - (p * rect.height);
                Handles.DrawLine(
                    new Vector3(rect.x, y, 0),
                    new Vector3(rect.x + rect.width, y, 0));
            }
            
            // Draw axes
            Handles.color = Color.white;
            // X axis
            Handles.DrawLine(
                new Vector3(rect.x, rect.y + rect.height, 0),
                new Vector3(rect.x + rect.width, rect.y + rect.height, 0));
            // Y axis
            Handles.DrawLine(
                new Vector3(rect.x, rect.y, 0),
                new Vector3(rect.x, rect.y + rect.height, 0));
            
            // Axis labels
            GUI.Label(new Rect(rect.x + rect.width / 2 - 30, rect.y + rect.height + 2, 60, 16), 
                "Depth (m)", EditorStyles.centeredGreyMiniLabel);
            
            // Depth tick labels
            for (float d = 0; d <= _maxDepth; d += 50)
            {
                float x = rect.x + (d / _maxDepth) * rect.width;
                GUI.Label(new Rect(x - 15, rect.y + rect.height + 2, 30, 14), 
                    $"{d:F0}", EditorStyles.centeredGreyMiniLabel);
            }
            
            // Draw ore curves
            int oreIndex = 0;
            foreach (var ore in _ores)
            {
                if (ore == null) continue;
                
                Color lineColor = ore.DebugColor != default ? ore.DebugColor : _lineColors[oreIndex % _lineColors.Length];
                Handles.color = lineColor;
                
                Vector3 prevPoint = Vector3.zero;
                bool hasStarted = false;
                
                for (float depth = 0; depth <= _maxDepth; depth += 2)
                {
                    float probability = CalculateSpawnProbability(ore, depth);
                    
                    float x = rect.x + (depth / _maxDepth) * rect.width;
                    float y = rect.y + rect.height - (probability * rect.height);
                    
                    Vector3 point = new Vector3(x, y, 0);
                    
                    if (hasStarted)
                    {
                        Handles.DrawLine(prevPoint, point);
                    }
                    
                    prevPoint = point;
                    hasStarted = true;
                }
                
                oreIndex++;
            }
        }
        
        private float CalculateSpawnProbability(OreDefinition ore, float depth)
        {
            // Outside depth range = 0
            if (depth < ore.MinDepth || depth > ore.MaxDepth)
                return 0f;
            
            // Base probability from threshold (inverted: lower threshold = higher spawn chance)
            float baseProbability = 1f - ore.Threshold;
            
            // Apply depth curve if available
            if (_depthCurve != null)
            {
                float curveMultiplier = _depthCurve.GetProbability(ore.Rarity, depth);
                baseProbability *= curveMultiplier;
            }
            
            return Mathf.Clamp01(baseProbability);
        }
        
        private void DrawLegend()
        {
            EditorGUILayout.LabelField("Legend", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            int oreIndex = 0;
            foreach (var ore in _ores)
            {
                if (ore == null) continue;
                
                EditorGUILayout.BeginHorizontal();
                
                Color lineColor = ore.DebugColor != default ? ore.DebugColor : _lineColors[oreIndex % _lineColors.Length];
                var colorRect = GUILayoutUtility.GetRect(20, 14, GUILayout.Width(20));
                EditorGUI.DrawRect(colorRect, lineColor);
                
                EditorGUILayout.LabelField($"{ore.OreName} ({ore.MinDepth:F0}m - {ore.MaxDepth:F0}m)", GUILayout.Width(200));
                EditorGUILayout.LabelField($"[{ore.Rarity}]", GUILayout.Width(80));
                EditorGUILayout.LabelField($"Threshold: {ore.Threshold:P0}", GUILayout.Width(100));
                
                EditorGUILayout.EndHorizontal();
                
                oreIndex++;
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawOreTable()
        {
            EditorGUILayout.LabelField("Ore Configuration", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Ore", EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("ID", EditorStyles.boldLabel, GUILayout.Width(40));
            EditorGUILayout.LabelField("Depth", EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("Rarity", EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField("Vein Scale", EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField("Warping", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            
            foreach (var ore in _ores)
            {
                if (ore == null) continue;
                
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button(ore.OreName, EditorStyles.linkLabel, GUILayout.Width(100)))
                {
                    Selection.activeObject = ore;
                    EditorGUIUtility.PingObject(ore);
                }
                
                EditorGUILayout.LabelField(ore.MaterialID.ToString(), GUILayout.Width(40));
                EditorGUILayout.LabelField($"{ore.MinDepth:F0} - {ore.MaxDepth:F0}m", GUILayout.Width(100));
                EditorGUILayout.LabelField(ore.Rarity.ToString(), GUILayout.Width(80));
                EditorGUILayout.LabelField(ore.NoiseScale.ToString("F2"), GUILayout.Width(80));
                EditorGUILayout.LabelField(ore.DomainWarping ? "✓" : "-", GUILayout.Width(60));
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Create New Ore Definition"))
            {
                CreateNewOre();
            }
        }
        
        private void CreateNewOre()
        {
            var ore = ScriptableObject.CreateInstance<OreDefinition>();
            ore.OreName = "New Ore";
            ore.MaterialID = 50; // Default to unused range
            
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Ore Definition", "NewOre", "asset",
                "Choose a location for the new ore definition");
            
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(ore, path);
                AssetDatabase.SaveAssets();
                RefreshOreList();
                EditorGUIUtility.PingObject(ore);
            }
        }
    }
}
