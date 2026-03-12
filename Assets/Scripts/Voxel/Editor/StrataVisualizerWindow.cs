using UnityEngine;
using UnityEditor;
using System.Linq;
using DIG.Voxel.Geology;

namespace DIG.Voxel.Editor
{
    /// <summary>
    /// Editor window for visualizing strata (rock layer) profiles.
    /// Shows vertical cross-section of geological layers.
    /// </summary>
    public class StrataVisualizerWindow : EditorWindow
    {
        [MenuItem("DIG/World/Strata Visualizer")]
        static void ShowWindow() => GetWindow<StrataVisualizerWindow>("Strata Visualizer");
        
        private StrataProfile _profile;
        private Vector2 _scrollPos;
        private float _previewDepth = 50f;
        private Vector3 _previewPosition = Vector3.zero;
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Strata Profile Visualizer", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Profile selection
            _profile = (StrataProfile)EditorGUILayout.ObjectField(
                "Strata Profile", _profile, typeof(StrataProfile), false);
            
            if (_profile == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a Strata Profile to visualize.\n\n" +
                    "Create one via: Right-click → Create → DIG → World → Strata Profile",
                    MessageType.Info);
                
                if (GUILayout.Button("Create New Strata Profile"))
                {
                    CreateNewProfile();
                }
                return;
            }
            
            EditorGUILayout.Space();
            
            // Drawing area
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            DrawStrataVisualization();
            
            EditorGUILayout.Space(10);
            
            DrawDepthTester();
            
            EditorGUILayout.Space(10);
            
            DrawLayerList();
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawStrataVisualization()
        {
            EditorGUILayout.LabelField("Cross-Section View", EditorStyles.boldLabel);
            
            if (_profile.Layers == null || _profile.Layers.Length == 0)
            {
                EditorGUILayout.HelpBox("No layers defined in profile.", MessageType.Warning);
                return;
            }
            
            float maxDepth = _profile.GetMaxDepth();
            float height = Mathf.Min(400, maxDepth * 2);
            
            var rect = GUILayoutUtility.GetRect(position.width - 40, height);
            
            // Background
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
            
            // Draw layers
            float barWidth = 80f;
            float labelOffset = 90f;
            
            foreach (var layer in _profile.Layers)
            {
                float yStart = rect.y + (layer.MinDepth / maxDepth) * rect.height;
                float yEnd = rect.y + (Mathf.Min(layer.MaxDepth, maxDepth) / maxDepth) * rect.height;
                float layerHeight = yEnd - yStart;
                
                if (layerHeight < 1) continue;
                
                // Layer bar
                Rect layerRect = new Rect(rect.x + 10, yStart, barWidth, layerHeight);
                EditorGUI.DrawRect(layerRect, layer.DebugColor);
                
                // Border
                Handles.color = Color.black;
                Handles.DrawWireDisc(layerRect.center, Vector3.forward, 0);
                
                // Label
                if (layerHeight > 16)
                {
                    var style = new GUIStyle(EditorStyles.miniLabel);
                    style.normal.textColor = GetContrastColor(layer.DebugColor);
                    GUI.Label(new Rect(rect.x + 15, yStart + 2, barWidth - 10, 16), 
                        layer.DisplayName ?? $"ID:{layer.MaterialID}", style);
                }
                
                // Depth label
                GUI.Label(new Rect(rect.x + labelOffset, yStart, 120, 16), 
                    $"{layer.MinDepth:F0}m - {layer.MaxDepth:F0}m", EditorStyles.miniLabel);
                
                // Noise influence indicator
                if (layer.NoiseInfluence > 0)
                {
                    GUI.Label(new Rect(rect.x + labelOffset + 100, yStart, 80, 16), 
                        $"±{layer.NoiseInfluence * 10:F0}m", EditorStyles.miniLabel);
                }
            }
            
            // Depth scale
            DrawDepthScale(rect, maxDepth);
        }
        
        private void DrawDepthScale(Rect rect, float maxDepth)
        {
            float scaleX = rect.x + rect.width - 40;
            
            Handles.color = Color.white;
            Handles.DrawLine(
                new Vector3(scaleX, rect.y, 0),
                new Vector3(scaleX, rect.y + rect.height, 0));
            
            // Tick marks every 25m
            for (float d = 0; d <= maxDepth; d += 25)
            {
                float y = rect.y + (d / maxDepth) * rect.height;
                Handles.DrawLine(
                    new Vector3(scaleX - 5, y, 0),
                    new Vector3(scaleX + 5, y, 0));
                GUI.Label(new Rect(scaleX + 8, y - 8, 40, 16), $"{d:F0}m", EditorStyles.miniLabel);
            }
        }
        
        private void DrawDepthTester()
        {
            EditorGUILayout.LabelField("Depth Tester", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            _previewDepth = EditorGUILayout.Slider("Test Depth", _previewDepth, 0, _profile.GetMaxDepth());
            EditorGUILayout.EndHorizontal();
            
            _previewPosition = EditorGUILayout.Vector3Field("World Position (noise)", _previewPosition);
            
            byte materialId = _profile.GetMaterialAtDepth(_previewDepth, _previewPosition);
            
            // Find layer info
            string layerName = "Unknown";
            Color layerColor = Color.gray;
            foreach (var layer in _profile.Layers)
            {
                if (layer.MaterialID == materialId)
                {
                    layerName = layer.DisplayName ?? $"Material {materialId}";
                    layerColor = layer.DebugColor;
                    break;
                }
            }
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Material at {_previewDepth:F1}m:", GUILayout.Width(120));
            
            var colorRect = GUILayoutUtility.GetRect(20, 16, GUILayout.Width(20));
            EditorGUI.DrawRect(colorRect, layerColor);
            
            EditorGUILayout.LabelField($"{layerName} (ID: {materialId})");
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawLayerList()
        {
            EditorGUILayout.LabelField("Layer Configuration", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Edit Profile"))
            {
                Selection.activeObject = _profile;
            }
            
            EditorGUILayout.Space();
            
            // Summary table
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Material", EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("Depth Range", EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("Blend", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Noise", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            
            foreach (var layer in _profile.Layers)
            {
                EditorGUILayout.BeginHorizontal();
                
                var colorRect = GUILayoutUtility.GetRect(16, 14, GUILayout.Width(16));
                EditorGUI.DrawRect(colorRect, layer.DebugColor);
                
                EditorGUILayout.LabelField(layer.DisplayName ?? $"ID:{layer.MaterialID}", GUILayout.Width(80));
                EditorGUILayout.LabelField($"{layer.MinDepth:F0} - {layer.MaxDepth:F0}m", GUILayout.Width(100));
                EditorGUILayout.LabelField($"{layer.BlendWidth:F0}m", GUILayout.Width(50));
                EditorGUILayout.LabelField($"{layer.NoiseInfluence:P0}", GUILayout.Width(50));
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void CreateNewProfile()
        {
            var profile = ScriptableObject.CreateInstance<StrataProfile>();
            
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Strata Profile", "NewStrataProfile", "asset",
                "Choose a location for the new strata profile");
            
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(profile, path);
                AssetDatabase.SaveAssets();
                _profile = profile;
                EditorGUIUtility.PingObject(profile);
            }
        }
        
        private Color GetContrastColor(Color bg)
        {
            float luminance = 0.299f * bg.r + 0.587f * bg.g + 0.114f * bg.b;
            return luminance > 0.5f ? Color.black : Color.white;
        }
    }
}
