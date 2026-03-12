using UnityEngine;
using UnityEditor;
using DIG.Voxel.Geology;

namespace DIG.Voxel.Editor.Tools
{
    public class HollowEarthPreviewer : EditorWindow
    {
        [MenuItem("DIG/World/Hollow Earth Previewer")]
        static void ShowWindow() => GetWindow<HollowEarthPreviewer>("Hollow Preview");
        
        private HollowEarthProfile _profile;
        private Texture2D _floorHeightmap;
        private Texture2D _ceilingHeightmap;
        private Texture2D _crossSection;
        private int _resolution = 256;
        private int _seed = 12345;
        
        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _profile = (HollowEarthProfile)EditorGUILayout.ObjectField(
                _profile, typeof(HollowEarthProfile), false, GUILayout.Width(200));
            _seed = EditorGUILayout.IntField("Seed", _seed, GUILayout.Width(150));
            
            if (GUILayout.Button("Generate", EditorStyles.toolbarButton))
                GeneratePreview();
            
            EditorGUILayout.EndHorizontal();
            
            if (_profile == null)
            {
                EditorGUILayout.HelpBox("Select a HollowEarthProfile to preview", MessageType.Info);
                return;
            }
            
            // Dimensions info
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Dimensions", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Height: {_profile.AverageHeight}m ± {_profile.HeightVariation}m");
            EditorGUILayout.LabelField($"Floor: {_profile.FloorWidth}m × {_profile.FloorLength}m");
            EditorGUILayout.LabelField($"Area: {(_profile.FloorWidth * _profile.FloorLength / 1_000_000f):F2} km²");
            EditorGUILayout.LabelField($"Volume: {(_profile.FloorWidth * _profile.FloorLength * _profile.AverageHeight / 1_000_000_000f):F3} km³");
            EditorGUILayout.EndVertical();
            
            // Preview images
            if (_floorHeightmap != null)
            {
                EditorGUILayout.BeginHorizontal();
                
                EditorGUILayout.BeginVertical();
                GUILayout.Label("Floor Terrain (Overhead)");
                GUILayout.Box(_floorHeightmap, GUILayout.Width(200), GUILayout.Height(200));
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.BeginVertical();
                GUILayout.Label("Ceiling Terrain (Overhead)");
                GUILayout.Box(_ceilingHeightmap, GUILayout.Width(200), GUILayout.Height(200));
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.EndHorizontal();
                
                GUILayout.Space(10);
                GUILayout.Label("Cross Section (Side View - Z Slice)");
                GUILayout.Box(_crossSection, GUILayout.Width(400), GUILayout.Height(150));
            }
            
            // Feature preview
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Features Configured", EditorStyles.boldLabel);
            DrawFeatureRow("Stalactites", _profile.HasStalactites);
            DrawFeatureRow("Stalagmites", _profile.HasStalagmites);
            DrawFeatureRow("Pillars", _profile.GeneratePillars);
            DrawFeatureRow("Underground Lakes", _profile.HasUndergroundLakes);
            DrawFeatureRow("Crystal Formations", _profile.HasCrystalFormations);
            DrawFeatureRow("Lava Flows", _profile.HasLavaFlows);
            DrawFeatureRow("Floating Islands", _profile.HasFloatingIslands);
            EditorGUILayout.EndVertical();
        }
        
        private void DrawFeatureRow(string name, bool enabled)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(enabled ? "✅" : "❌", GUILayout.Width(25));
            GUILayout.Label(name);
            EditorGUILayout.EndHorizontal();
        }
        
        private void GeneratePreview()
        {
            if (_profile == null) return;
            
            // Generate floor heightmap
            _floorHeightmap = new Texture2D(_resolution, _resolution);
            _ceilingHeightmap = new Texture2D(_resolution, _resolution);
            
            for (int x = 0; x < _resolution; x++)
            {
                for (int z = 0; z < _resolution; z++)
                {
                    float worldX = (x / (float)_resolution - 0.5f) * _profile.FloorWidth;
                    float worldZ = (z / (float)_resolution - 0.5f) * _profile.FloorLength;
                    
                    // Floor height using profile settings
                    float floorNoise = Mathf.PerlinNoise(
                        (worldX * _profile.FloorNoiseScale) + _seed,
                        (worldZ * _profile.FloorNoiseScale));
                    
                    // Ceiling height
                    float ceilingNoise = Mathf.PerlinNoise(
                        (worldX * _profile.CeilingNoiseScale) + _seed + 1000,
                        (worldZ * _profile.CeilingNoiseScale));
                    
                    _floorHeightmap.SetPixel(x, z, new Color(floorNoise, floorNoise, floorNoise));
                    _ceilingHeightmap.SetPixel(x, z, new Color(ceilingNoise, ceilingNoise, ceilingNoise));
                }
            }
            
            _floorHeightmap.Apply();
            _ceilingHeightmap.Apply();
            
            // Generate cross section
            GenerateCrossSection();
        }
        
        private void GenerateCrossSection()
        {
            int width = 400;
            int height = 150;
            _crossSection = new Texture2D(width, height);
            
            // Clear background
            Color bg = new Color(0.1f, 0.1f, 0.15f);
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    _crossSection.SetPixel(x, y, bg);
            
            float heightScale = height / (_profile.AverageHeight + _profile.HeightVariation * 2 + 50);
            
            for (int x = 0; x < width; x++)
            {
                float worldX = (x / (float)width - 0.5f) * _profile.FloorWidth;
                
                // Sample floor and ceiling at this X (Z=0)
                float floorNoise = Mathf.PerlinNoise((worldX * _profile.FloorNoiseScale) + _seed, 0);
                float floorHeight = floorNoise * _profile.FloorAmplitude;
                
                float ceilingNoise = Mathf.PerlinNoise((worldX * _profile.CeilingNoiseScale) + _seed + 1000, 0);
                // Map 0-1 noise to actual ceiling height variation derived from profile
                float ceilingHeight = _profile.AverageHeight + (ceilingNoise - 0.5f) * _profile.HeightVariation * 2;
                
                int floorY = Mathf.Clamp((int)(floorHeight * heightScale) + 10, 0, height - 1);
                int ceilingY = Mathf.Clamp((int)(ceilingHeight * heightScale) + 10, 0, height - 1);
                
                // Draw floor surface
                for (int y = 0; y < floorY; y++)
                    _crossSection.SetPixel(x, y, new Color(0.4f, 0.3f, 0.2f));
                
                // Draw ceiling (rock above hollow)
                for (int y = ceilingY; y < height; y++)
                    _crossSection.SetPixel(x, y, new Color(0.3f, 0.3f, 0.35f));
                
                // Draw air space (Visual only)
                for (int y = floorY; y < ceilingY; y++)
                    _crossSection.SetPixel(x, y, new Color(0.2f, 0.3f, 0.4f, 0.3f));
            }
            
            _crossSection.Apply();
        }
    }
}
