using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using DIG.Voxel.Geology;

namespace DIG.Voxel.Editor.Tools
{
    public class SeedComparisonTool : EditorWindow
    {
        [MenuItem("DIG/World/Seed Comparison")]
        static void ShowWindow() => GetWindow<SeedComparisonTool>("Seeds");
        
        private WorldStructureConfig _config;
        private List<int> _seeds = new() { 12345, 54321, 11111, 99999 };
        private int _selectedLayerIndex = 0;
        private Texture2D[] _previews;
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Multi-Seed Visualization", EditorStyles.boldLabel);
            
            _config = (WorldStructureConfig)EditorGUILayout.ObjectField(
                "Config", _config, typeof(WorldStructureConfig), false);
            
            if (_config?.Layers != null && _config.Layers.Length > 0)
            {
                var layerNames = _config.Layers.Where(l => l != null).Select(l => l.LayerName).ToArray();
                if (layerNames.Length > 0)
                    _selectedLayerIndex = EditorGUILayout.Popup("Layer", _selectedLayerIndex, layerNames);
            }
            
            // Seed management
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Seeds", EditorStyles.boldLabel);
            
            for (int i = 0; i < _seeds.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _seeds[i] = EditorGUILayout.IntField($"Seed {i + 1}", _seeds[i]);
                if (GUILayout.Button("🎲", GUILayout.Width(25)))
                    _seeds[i] = UnityEngine.Random.Range(0, 999999);
                if (GUILayout.Button("X", GUILayout.Width(25)) && _seeds.Count > 1)
                {
                    _seeds.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Seed"))
                _seeds.Add(UnityEngine.Random.Range(0, 999999));
            if (GUILayout.Button("Generate All Previews"))
                GenerateAllPreviews();
            EditorGUILayout.EndHorizontal();
            
            // Show previews
            if (_previews != null && _previews.Length == _seeds.Count)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                for (int i = 0; i < _previews.Length; i++)
                {
                    if (_previews[i] != null)
                    {
                        EditorGUILayout.BeginVertical();
                        GUILayout.Label($"Seed: {_seeds[i]}");
                        GUILayout.Box(_previews[i], GUILayout.Width(150), GUILayout.Height(150));
                        EditorGUILayout.EndVertical();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private void GenerateAllPreviews()
        {
            if (_config == null || _config.Layers == null || _selectedLayerIndex >= _config.Layers.Length) return;
            
            var layer = _config.Layers[_selectedLayerIndex];
            if (layer == null) return;
            
            _previews = new Texture2D[_seeds.Count];
            float noiseScale = 0.05f; 
            
            if (layer.Type == LayerType.Hollow && layer.HollowProfile != null)
            {
                noiseScale = layer.HollowProfile.FloorNoiseScale;
            }
            else if (layer.Type == LayerType.Solid && layer.CaveProfile != null)
            {
                 // Estimation for solid layers
                 noiseScale = 0.05f;
            }
            
            for (int i = 0; i < _seeds.Count; i++)
            {
                _previews[i] = GeneratePreview(_seeds[i], noiseScale);
            }
        }
        
        private Texture2D GeneratePreview(int seed, float scale)
        {
            int res = 128;
            var tex = new Texture2D(res, res);
            
            for (int x = 0; x < res; x++)
            {
                for (int y = 0; y < res; y++)
                {
                    float val = Mathf.PerlinNoise(x * scale + seed, y * scale);
                    tex.SetPixel(x, y, new Color(val, val, val));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
