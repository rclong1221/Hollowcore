using UnityEngine;
using UnityEditor;
using DIG.Voxel.Geology;
using System.Linq;

namespace DIG.Voxel.Editor.Tools
{
    /// <summary>
    /// Visual editor for the multi-layer world structure.
    /// Shows cross-section view of all layers, statistics, and validation.
    /// </summary>
    public class WorldLayerEditor : EditorWindow
    {
        private WorldStructureConfig _config;
        private Vector2 _scrollPos;
        private Vector2 _layerListScroll;
        private float _pixelsPerMeter = 0.05f;
        private int _selectedLayerIndex = -1;
        
        // Cache
        private GUIStyle _headerStyle;
        private GUIStyle _statStyle;
        private bool _stylesInitialized;
        
        [MenuItem("DIG/World/World Layer Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<WorldLayerEditor>("World Layers");
            window.minSize = new Vector2(800, 500);
        }
        
        private void InitStyles()
        {
            if (_stylesInitialized) return;
            
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };
            
            _statStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 4, 4)
            };
            
            _stylesInitialized = true;
        }
        
        private void OnGUI()
        {
            InitStyles();
            
            // Toolbar
            DrawToolbar();
            
            if (_config == null || _config.Layers == null || _config.Layers.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "Assign a WorldStructureConfig to visualize and edit the world layer structure.\n\n" +
                    "Quick Start:\n" +
                    "1. Go to DIG/Quick Setup/Generation/Create Complete Cave Setup\n" +
                    "2. Drag the created WorldStructureConfig here",
                    MessageType.Info);
                return;
            }
            
            EditorGUILayout.BeginHorizontal();
            
            // Left panel: Layer list
            DrawLayerList();
            
            // Center: Visual cross-section
            DrawCrossSection();
            
            // Right: Selected layer inspector
            DrawLayerInspector();
            
            EditorGUILayout.EndHorizontal();
            
            // Footer: Statistics
            DrawStatistics();
        }
        
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            EditorGUILayout.LabelField("Config:", GUILayout.Width(45));
            _config = (WorldStructureConfig)EditorGUILayout.ObjectField(
                _config, typeof(WorldStructureConfig), false, GUILayout.Width(200));
            
            GUILayout.Space(20);
            
            if (_config != null)
            {
                if (GUILayout.Button("Add Solid Layer", EditorStyles.toolbarButton, GUILayout.Width(100)))
                    AddLayer(LayerType.Solid);
                if (GUILayout.Button("Add Hollow Layer", EditorStyles.toolbarButton, GUILayout.Width(100)))
                    AddLayer(LayerType.Hollow);
                
                GUILayout.Space(10);
                
                if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    ValidateConfig();
            }
            
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.LabelField("Zoom:", GUILayout.Width(40));
            _pixelsPerMeter = EditorGUILayout.Slider(_pixelsPerMeter, 0.01f, 0.2f, GUILayout.Width(150));
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawLayerList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(180));
            
            EditorGUILayout.LabelField("Layers", _headerStyle);
            
            _layerListScroll = EditorGUILayout.BeginScrollView(_layerListScroll, GUILayout.Height(position.height - 150));
            
            for (int i = 0; i < _config.Layers.Length; i++)
            {
                var layer = _config.Layers[i];
                if (layer == null) continue;
                
                bool isSelected = _selectedLayerIndex == i;
                var bgColor = isSelected ? new Color(0.3f, 0.5f, 0.8f, 0.3f) : Color.clear;
                
                EditorGUILayout.BeginHorizontal(isSelected ? EditorStyles.helpBox : GUIStyle.none);
                
                // Type icon
                string icon = layer.Type == LayerType.Hollow ? "🌋" : "🪨";
                EditorGUILayout.LabelField(icon, GUILayout.Width(25));
                
                // Layer color indicator
                var colorRect = GUILayoutUtility.GetRect(12, 16, GUILayout.Width(12));
                EditorGUI.DrawRect(colorRect, layer.DebugColor);
                
                // Name button
                if (GUILayout.Button(layer.LayerName, EditorStyles.label))
                {
                    _selectedLayerIndex = i;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
        
        private void DrawCrossSection()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            
            EditorGUILayout.LabelField("Cross Section", _headerStyle);
            
            var rect = GUILayoutUtility.GetRect(300, position.height - 150, GUILayout.ExpandWidth(true));
            
            // Background
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.15f));
            
            // Draw border
            Handles.color = new Color(0.3f, 0.3f, 0.35f);
            Handles.DrawLine(new Vector3(rect.x, rect.y), new Vector3(rect.xMax, rect.y));
            Handles.DrawLine(new Vector3(rect.x, rect.yMax), new Vector3(rect.xMax, rect.yMax));
            Handles.DrawLine(new Vector3(rect.x, rect.y), new Vector3(rect.x, rect.yMax));
            Handles.DrawLine(new Vector3(rect.xMax, rect.y), new Vector3(rect.xMax, rect.yMax));
            
            // Ground level line
            float groundY = rect.y + 30;
            EditorGUI.DrawRect(new Rect(rect.x, groundY, rect.width, 2), Color.green);
            GUI.Label(new Rect(rect.x + 5, groundY - 18, 100, 18), "Ground (Y=0)", EditorStyles.miniLabel);
            
            // Draw each layer
            float currentY = groundY + 2;
            float centerX = rect.x + rect.width * 0.1f;
            float layerWidth = rect.width * 0.7f;
            
            foreach (var layer in _config.Layers)
            {
                if (layer == null) continue;
                
                float layerHeight = Mathf.Max(layer.Thickness * _pixelsPerMeter, 15f);
                
                // Layer color based on type
                Color layerColor = layer.Type == LayerType.Hollow 
                    ? new Color(layer.DebugColor.r, layer.DebugColor.g, layer.DebugColor.b, 0.7f) 
                    : new Color(layer.DebugColor.r * 0.6f, layer.DebugColor.g * 0.6f, layer.DebugColor.b * 0.6f, 0.8f);
                
                // Calculate width based on layer area (normalized)
                float normalizedWidth = Mathf.Clamp(layer.AreaWidth / 5000f, 0.3f, 1f);
                float drawWidth = layerWidth * normalizedWidth;
                float drawX = centerX + (layerWidth - drawWidth) * 0.5f;
                
                var layerRect = new Rect(drawX, currentY, drawWidth, layerHeight);
                EditorGUI.DrawRect(layerRect, layerColor);
                
                // Hollow earth special styling (show as open space)
                if (layer.Type == LayerType.Hollow)
                {
                    var innerRect = new Rect(layerRect.x + 5, layerRect.y + 5, layerRect.width - 10, layerRect.height - 10);
                    EditorGUI.DrawRect(innerRect, new Color(0.15f, 0.15f, 0.2f, 0.9f));
                }
                
                // Layer label
                var labelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = Color.white },
                    fontStyle = FontStyle.Bold
                };
                
                string label = $"{layer.LayerName}";
                if (layer.Type == LayerType.Hollow && layer.HollowProfile != null)
                    label += $" ({layer.HollowProfile.AverageHeight}m)";
                else
                    label += $" ({layer.Thickness}m)";
                
                GUI.Label(new Rect(drawX + 5, currentY + layerHeight / 2 - 8, 200, 16), label, labelStyle);
                
                // Depth marker on right
                string depthLabel = $"{layer.TopDepth}m";
                GUI.Label(new Rect(rect.xMax - 55, currentY + 2, 50, 16), depthLabel, EditorStyles.miniLabel);
                
                currentY += layerHeight + 2;
            }
            
            // Total depth marker
            float totalDepth = _config.GetTotalDepth();
            GUI.Label(new Rect(rect.xMax - 70, currentY, 65, 16), $"-{totalDepth}m", EditorStyles.miniLabel);
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawLayerInspector()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(250));
            
            if (_selectedLayerIndex < 0 || _selectedLayerIndex >= _config.Layers.Length)
            {
                EditorGUILayout.HelpBox("Select a layer to inspect", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }
            
            var layer = _config.Layers[_selectedLayerIndex];
            if (layer == null)
            {
                EditorGUILayout.HelpBox("Selected layer is null", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }
            
            EditorGUILayout.LabelField(layer.LayerName, _headerStyle);
            EditorGUILayout.Space(5);
            
            // Quick stats
            EditorGUILayout.BeginVertical(_statStyle);
            EditorGUILayout.LabelField($"Type: {layer.Type}");
            EditorGUILayout.LabelField($"Depth: {layer.TopDepth}m to {layer.BottomDepth}m");
            EditorGUILayout.LabelField($"Thickness: {layer.Thickness}m");
            EditorGUILayout.LabelField($"Area: {layer.AreaKm2:F2} km²");
            EditorGUILayout.LabelField($"Playtime: {layer.TargetPlaytimeMinutes} min");
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            // Edit in inspector
            if (GUILayout.Button("Select in Inspector"))
            {
                Selection.activeObject = layer;
            }
            
            // Type-specific info
            EditorGUILayout.Space(5);
            if (layer.Type == LayerType.Hollow && layer.HollowProfile != null)
            {
                EditorGUILayout.LabelField("Hollow Earth", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Height: {layer.HollowProfile.AverageHeight}m ± {layer.HollowProfile.HeightVariation}m");
                EditorGUILayout.LabelField($"Pillars: {(layer.HollowProfile.GeneratePillars ? "Yes" : "No")}");
                EditorGUILayout.LabelField($"Stalactites: {(layer.HollowProfile.HasStalactites ? "Yes" : "No")}");
                EditorGUILayout.LabelField($"Lakes: {(layer.HollowProfile.HasUndergroundLakes ? "Yes" : "No")}");
                EditorGUILayout.LabelField($"Light: {layer.HollowProfile.LightSource}");
                
                if (GUILayout.Button("Edit Hollow Profile"))
                    Selection.activeObject = layer.HollowProfile;
            }
            else if (layer.Type == LayerType.Solid && layer.CaveProfile != null)
            {
                EditorGUILayout.LabelField("Solid Layer", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Swiss Cheese: {(layer.CaveProfile.EnableSwissCheese ? "Yes" : "No")}");
                EditorGUILayout.LabelField($"Spaghetti: {(layer.CaveProfile.EnableSpaghetti ? "Yes" : "No")}");
                EditorGUILayout.LabelField($"Noodles: {(layer.CaveProfile.EnableNoodles ? "Yes" : "No")}");
                EditorGUILayout.LabelField($"Caverns: {(layer.CaveProfile.EnableCaverns ? "Yes" : "No")}");
                
                if (GUILayout.Button("Edit Cave Profile"))
                    Selection.activeObject = layer.CaveProfile;
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawStatistics()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            float totalDepth = _config.GetTotalDepth();
            int solidLayers = _config.SolidLayerCount;
            int hollowLayers = _config.HollowLayerCount;
            float totalPlaytime = _config.TotalPlaytimeMinutes;
            
            GUILayout.Label($"📏 Total Depth: {totalDepth:N0}m");
            GUILayout.Space(20);
            GUILayout.Label($"🪨 Solid: {solidLayers}");
            GUILayout.Space(20);
            GUILayout.Label($"🌋 Hollow: {hollowLayers}");
            GUILayout.Space(20);
            GUILayout.Label($"⏱️ Est. Playtime: {totalPlaytime / 60:F1}h ({totalPlaytime:N0} min)");
            
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void AddLayer(LayerType type)
        {
            // Determine new layer depth
            float newTop = 0f;
            if (_config.Layers != null && _config.Layers.Length > 0)
            {
                var lastLayer = _config.Layers[_config.Layers.Length - 1];
                if (lastLayer != null)
                    newTop = lastLayer.BottomDepth;
            }
            
            // Create new layer asset
            var newLayer = ScriptableObject.CreateInstance<WorldLayerDefinition>();
            newLayer.LayerName = type == LayerType.Hollow ? "New Hollow" : "New Solid";
            newLayer.Type = type;
            newLayer.LayerIndex = _config.Layers?.Length ?? 0;
            newLayer.TopDepth = newTop;
            newLayer.BottomDepth = newTop - (type == LayerType.Hollow ? 500f : 400f);
            newLayer.AreaWidth = 2000f;
            newLayer.AreaLength = 2000f;
            newLayer.DebugColor = type == LayerType.Hollow ? Color.cyan : Color.gray;
            
            // Save asset
            string path = EditorUtility.SaveFilePanelInProject(
                "Save New Layer",
                $"Layer_{newLayer.LayerIndex:D2}_{newLayer.LayerName}",
                "asset",
                "Save the new layer definition");
            
            if (string.IsNullOrEmpty(path)) return;
            
            AssetDatabase.CreateAsset(newLayer, path);
            
            // Add to config
            var layers = _config.Layers?.ToList() ?? new System.Collections.Generic.List<WorldLayerDefinition>();
            layers.Add(newLayer);
            _config.Layers = layers.ToArray();
            
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
        }
        
        private void ValidateConfig()
        {
            var errors = new System.Collections.Generic.List<string>();
            var warnings = new System.Collections.Generic.List<string>();
            
            if (_config.Layers == null || _config.Layers.Length == 0)
            {
                errors.Add("No layers defined");
            }
            else
            {
                float lastBottom = 0;
                for (int i = 0; i < _config.Layers.Length; i++)
                {
                    var layer = _config.Layers[i];
                    if (layer == null)
                    {
                        errors.Add($"Layer {i} is null");
                        continue;
                    }
                    
                    // Check ordering
                    if (i > 0 && layer.TopDepth > lastBottom)
                    {
                        errors.Add($"Layer gap between {_config.Layers[i - 1].LayerName} and {layer.LayerName}");
                    }
                    
                    // Check profiles
                    if (layer.Type == LayerType.Hollow && layer.HollowProfile == null)
                        errors.Add($"Hollow layer '{layer.LayerName}' missing HollowEarthProfile");
                    
                    if (layer.Type == LayerType.Solid && layer.CaveProfile == null)
                        warnings.Add($"Solid layer '{layer.LayerName}' has no CaveProfile (no caves)");
                    
                    lastBottom = layer.BottomDepth;
                }
            }
            
            // Show results
            if (errors.Count == 0 && warnings.Count == 0)
            {
                EditorUtility.DisplayDialog("Validation Passed", "✅ World structure is valid!", "OK");
            }
            else
            {
                string message = "";
                if (errors.Count > 0)
                    message += "ERRORS:\n" + string.Join("\n", errors.Select(e => "❌ " + e)) + "\n\n";
                if (warnings.Count > 0)
                    message += "WARNINGS:\n" + string.Join("\n", warnings.Select(w => "⚠️ " + w));
                
                EditorUtility.DisplayDialog("Validation Results", message, "OK");
            }
        }
    }
}
