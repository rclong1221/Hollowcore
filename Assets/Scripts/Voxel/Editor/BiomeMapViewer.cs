using UnityEngine;
using UnityEditor;
using DIG.Voxel.Biomes;

namespace DIG.Voxel.Editor
{
    public class BiomeMapViewer : EditorWindow
    {
        [MenuItem("DIG/World/Biome Map Viewer")]
        static void ShowWindow() => GetWindow<BiomeMapViewer>("Biome Map");
        
        private BiomeRegistry _registry;
        private int _seed = 12345;
        private float _scale = 0.001f;
        private Texture2D _texture;
        private int _size = 256;
        
        private void OnGUI()
        {
            _registry = (BiomeRegistry)EditorGUILayout.ObjectField("Registry", _registry, typeof(BiomeRegistry), false);
            _seed = EditorGUILayout.IntField("Seed", _seed);
            _scale = EditorGUILayout.Slider("Noise Scale", _scale, 0.0001f, 0.01f);
            
            if (GUILayout.Button("Generate Map"))
            {
                Generate();
            }
            
            if (_texture != null)
            {
                var rect = GUILayoutUtility.GetRect(_size, _size);
                GUI.DrawTexture(rect, _texture);
                
                // Legend
                if (_registry != null && _registry.Biomes != null)
                {
                    foreach (var b in _registry.Biomes)
                    {
                        if (b == null) continue;
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.ColorField(GUIContent.none, b.DebugColor, false, false, false, GUILayout.Width(20));
                        EditorGUILayout.LabelField(b.BiomeName);
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
        }
        
        private void Generate()
        {
            if (_registry == null) return;
            
            _texture = new Texture2D(_size, _size);
            var pixels = new Color[_size * _size];
            
            // Note: This matches BiomeLookup logic roughly
            for (int y = 0; y < _size; y++)
            {
                for (int x = 0; x < _size; x++)
                {
                    float u = (x - _size/2f) * 10f; // 10m per pixel
                    float v = (y - _size/2f) * 10f;
                    
                    float temp = Noise(u, v, _seed);
                    float hum = Noise(u, v * 1.3f, _seed + 2000);
                    
                    var biome = FindBiome(temp, hum);
                    pixels[y * _size + x] = biome?.DebugColor ?? Color.black;
                }
            }
            
            _texture.SetPixels(pixels);
            _texture.Apply();
        }
        
        private float Noise(float x, float y, int seed)
        {
            // Unity Mathf.PerlinNoise is [0,1], we need [-1,1] simulation
            // Or use proper Simplex 
            // Here just basic approx
            return Mathf.PerlinNoise((x * _scale) + seed, (y * _scale) + seed) * 2f - 1f;
        }
        
        private BiomeDefinition FindBiome(float temp, float hum)
        {
            foreach (var b in _registry.Biomes)
            {
                if (b == null) continue;
                if (temp >= b.MinTemperature && temp <= b.MaxTemperature &&
                    hum >= b.MinHumidity && hum <= b.MaxHumidity)
                {
                    return b;
                }
            }
            return _registry.FallbackBiome;
        }
    }
}
