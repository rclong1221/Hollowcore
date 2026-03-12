using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DIG.Voxel.Editor
{
    /// <summary>
    /// Editor window for building Texture2DArrays from folders of textures.
    /// Supports auto-detection of texture types by naming convention.
    /// </summary>
    public class TextureArrayBuilder : EditorWindow
    {
        private DefaultAsset _sourceFolder;
        private int _textureSize = 512;
        private string _outputPath = "Assets/Resources/VoxelTextures";
        private bool _generateMipmaps = true;
        private FilterMode _filterMode = FilterMode.Bilinear;
        private int _anisoLevel = 4;
        
        private Dictionary<string, List<Texture2D>> _categorizedTextures = new Dictionary<string, List<Texture2D>>();
        private Vector2 _scrollPosition;
        
        [MenuItem("DIG/Voxel/Texture Array Builder")]
        public static void ShowWindow()
        {
            var window = GetWindow<TextureArrayBuilder>("Texture Array Builder");
            window.minSize = new Vector2(400, 500);
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Texture Array Builder", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "Build Texture2DArrays from a folder of textures.\n\n" +
                "Naming convention:\n" +
                "  MaterialName_albedo.png\n" +
                "  MaterialName_normal.png\n" +
                "  MaterialName_height.png\n\n" +
                "Arrays will be created for each texture type.",
                MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            // Source folder
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            _sourceFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "Source Folder", _sourceFolder, typeof(DefaultAsset), false);
            
            if (_sourceFolder != null)
            {
                string path = AssetDatabase.GetAssetPath(_sourceFolder);
                if (!AssetDatabase.IsValidFolder(path))
                {
                    EditorGUILayout.HelpBox("Please select a folder, not a file.", MessageType.Error);
                    _sourceFolder = null;
                }
            }
            
            EditorGUILayout.Space(10);
            
            // Settings
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            _textureSize = EditorGUILayout.IntPopup("Texture Size", _textureSize,
                new[] { "128", "256", "512", "1024", "2048" },
                new[] { 128, 256, 512, 1024, 2048 });
            _generateMipmaps = EditorGUILayout.Toggle("Generate Mipmaps", _generateMipmaps);
            _filterMode = (FilterMode)EditorGUILayout.EnumPopup("Filter Mode", _filterMode);
            _anisoLevel = EditorGUILayout.IntSlider("Aniso Level", _anisoLevel, 0, 16);
            _outputPath = EditorGUILayout.TextField("Output Path", _outputPath);
            
            EditorGUILayout.Space(10);
            
            // Scan button
            if (_sourceFolder != null)
            {
                if (GUILayout.Button("Scan Folder", GUILayout.Height(25)))
                {
                    ScanFolder(AssetDatabase.GetAssetPath(_sourceFolder));
                }
            }
            
            // Results
            if (_categorizedTextures.Count > 0)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Found Textures", EditorStyles.boldLabel);
                
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));
                
                foreach (var category in _categorizedTextures)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    EditorGUILayout.LabelField(category.Key.ToUpper(), EditorStyles.boldLabel, GUILayout.Width(80));
                    EditorGUILayout.LabelField($"{category.Value.Count} textures", GUILayout.Width(80));
                    
                    // Show first few texture names
                    var names = string.Join(", ", category.Value.Take(3).Select(t => t.name));
                    if (category.Value.Count > 3) names += "...";
                    EditorGUILayout.LabelField(names, EditorStyles.miniLabel);
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.EndScrollView();
                
                EditorGUILayout.Space(10);
                
                // Build button
                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
                if (GUILayout.Button("Build Texture Arrays", GUILayout.Height(35)))
                {
                    BuildTextureArrays();
                }
                GUI.backgroundColor = Color.white;
            }
        }
        
        private void ScanFolder(string folderPath)
        {
            _categorizedTextures.Clear();
            
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
            
            foreach (var guid in guids)
            {
                var texPath = AssetDatabase.GUIDToAssetPath(guid);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex == null) continue;
                
                string name = tex.name.ToLower();
                string category = DetectCategory(name);
                
                if (!_categorizedTextures.ContainsKey(category))
                {
                    _categorizedTextures[category] = new List<Texture2D>();
                }
                
                _categorizedTextures[category].Add(tex);
            }
            
            UnityEngine.Debug.Log($"[TextureArrayBuilder] Scanned {guids.Length} textures in {_categorizedTextures.Count} categories");
        }
        
        private string DetectCategory(string name)
        {
            if (name.Contains("albedo") || name.Contains("diffuse") || name.Contains("color") || name.Contains("_c"))
                return "albedo";
            if (name.Contains("normal") || name.Contains("nrm") || name.Contains("_n"))
                return "normal";
            if (name.Contains("height") || name.Contains("displacement") || name.Contains("bump") || name.Contains("_h"))
                return "height";
            if (name.Contains("ao") || name.Contains("occlusion"))
                return "ao";
            if (name.Contains("rough") || name.Contains("smoothness"))
                return "roughness";
            if (name.Contains("metal"))
                return "metallic";
            if (name.Contains("detail"))
                return "detail";
            
            return "unknown";
        }
        
        private void BuildTextureArrays()
        {
            // Ensure output directory exists
            if (!AssetDatabase.IsValidFolder(_outputPath))
            {
                string[] parts = _outputPath.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }
            
            int totalCreated = 0;
            
            foreach (var category in _categorizedTextures)
            {
                if (category.Value.Count == 0) continue;
                if (category.Key == "unknown") continue; // Skip unknown category
                
                var array = CreateTextureArray(category.Value, category.Key);
                if (array != null)
                {
                    string savePath = $"{_outputPath}/{category.Key.ToUpper()}_Array.asset";
                    AssetDatabase.CreateAsset(array, savePath);
                    totalCreated++;
                    UnityEngine.Debug.Log($"[TextureArrayBuilder] Created {savePath} with {category.Value.Count} slices");
                }
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog("Texture Array Builder", 
                $"Created {totalCreated} texture arrays in:\n{_outputPath}", "OK");
        }
        
        private Texture2DArray CreateTextureArray(List<Texture2D> textures, string category)
        {
            // Sort textures by name for consistent ordering
            textures = textures.OrderBy(t => t.name).ToList();
            
            // Determine format
            TextureFormat format = TextureFormat.RGBA32;
            if (category == "normal")
                format = TextureFormat.RGBA32; // Could use BC5 for normal maps
            
            var array = new Texture2DArray(_textureSize, _textureSize, textures.Count, format, _generateMipmaps);
            array.filterMode = _filterMode;
            array.anisoLevel = _anisoLevel;
            array.wrapMode = TextureWrapMode.Repeat;
            
            for (int i = 0; i < textures.Count; i++)
            {
                var tex = textures[i];
                
                if (tex.width == _textureSize && tex.height == _textureSize)
                {
                    // Direct copy if size matches
                    Graphics.CopyTexture(tex, 0, 0, array, i, 0);
                }
                else
                {
                    // Resize using RenderTexture
                    var resized = ResizeTexture(tex, _textureSize);
                    array.SetPixels(resized.GetPixels(), i, 0);
                }
            }
            
            array.Apply();
            return array;
        }
        
        private Texture2D ResizeTexture(Texture2D source, int size)
        {
            RenderTexture rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            RenderTexture.active = rt;
            
            Graphics.Blit(source, rt);
            
            Texture2D result = new Texture2D(size, size, TextureFormat.RGBA32, _generateMipmaps);
            result.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            result.Apply();
            
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            
            return result;
        }
    }
}
