#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

namespace DIG.Voxel.Editor
{
    public class VoxelTextureManagerWindow : EditorWindow
    {
        private Rendering.VoxelTextureConfig _config;
        private Vector2 _scrollPosition;
        private bool _showPreview = true;
        private int _previewSize = 64;
        
        [MenuItem("DIG/Voxel/Texture Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<VoxelTextureManagerWindow>("Voxel Textures");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }
        
        private void OnEnable()
        {
            // Try to find existing config
            _config = Resources.Load<Rendering.VoxelTextureConfig>("VoxelTextureConfig");
            if (_config == null)
            {
                // Search in project
                var guids = AssetDatabase.FindAssets("t:VoxelTextureConfig");
                if (guids.Length > 0)
                {
                    _config = AssetDatabase.LoadAssetAtPath<Rendering.VoxelTextureConfig>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            
            // Header
            EditorGUILayout.LabelField("Voxel Texture Manager", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Manage the Texture2DArray used for voxel materials.\n" +
                "Each texture corresponds to a Material ID (0=Air, 1=Dirt, 2=Stone, etc.).",
                MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            // Config selection
            EditorGUILayout.BeginHorizontal();
            _config = (Rendering.VoxelTextureConfig)EditorGUILayout.ObjectField(
                "Texture Config", _config, typeof(Rendering.VoxelTextureConfig), false);
            
            if (GUILayout.Button("Create New", GUILayout.Width(100)))
            {
                CreateNewConfig();
            }
            EditorGUILayout.EndHorizontal();
            
            if (_config == null)
            {
                EditorGUILayout.HelpBox("No VoxelTextureConfig found. Create one or assign an existing asset.", MessageType.Warning);
                return;
            }
            
            EditorGUILayout.Space(10);
            
            // Settings
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            _config.TextureSize = EditorGUILayout.IntPopup("Texture Size", _config.TextureSize, 
                new[] { "128", "256", "512", "1024" }, new[] { 128, 256, 512, 1024 });
            _config.FilterMode = (FilterMode)EditorGUILayout.EnumPopup("Filter Mode", _config.FilterMode);
            _config.AnisoLevel = EditorGUILayout.IntSlider("Aniso Level", _config.AnisoLevel, 0, 16);
            
            EditorGUILayout.Space(10);
            
            // Textures list
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Textures (Index = Material ID)", EditorStyles.boldLabel);
            _showPreview = EditorGUILayout.Toggle("Preview", _showPreview, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));
            
            if (_config.Textures == null)
                _config.Textures = new Texture2D[0];
            
            for (int i = 0; i < _config.Textures.Length; i++)
            {
                DrawTextureSlot(i);
            }
            
            EditorGUILayout.EndScrollView();
            
            // Add/Remove buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Add Texture Slot"))
            {
                System.Array.Resize(ref _config.Textures, _config.Textures.Length + 1);
                EditorUtility.SetDirty(_config);
            }
            
            GUI.enabled = _config.Textures.Length > 0;
            if (GUILayout.Button("- Remove Last"))
            {
                System.Array.Resize(ref _config.Textures, _config.Textures.Length - 1);
                EditorUtility.SetDirty(_config);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // Build button
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
            if (GUILayout.Button("Build Texture Array", GUILayout.Height(30)))
            {
                _config.Rebuild();
                EditorUtility.SetDirty(_config);
                AssetDatabase.SaveAssets();
            }
            GUI.backgroundColor = Color.white;
            
            if (GUILayout.Button("Auto-Detect Textures", GUILayout.Height(30), GUILayout.Width(150)))
            {
                AutoDetectTextures();
            }
            EditorGUILayout.EndHorizontal();
            
            // Status
            if (_config.TextureArray != null)
            {
                EditorGUILayout.HelpBox(
                    $"✓ Texture Array built: {_config.TextureArray.width}x{_config.TextureArray.height}, {_config.TextureArray.depth} slices",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("Texture Array not built. Click 'Build Texture Array'.", MessageType.Warning);
            }
        }
        
        private void DrawTextureSlot(int index)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            // Preview
            if (_showPreview && _config.Textures[index] != null)
            {
                GUILayout.Label(_config.Textures[index], GUILayout.Width(_previewSize), GUILayout.Height(_previewSize));
            }
            
            EditorGUILayout.BeginVertical();
            
            // Label
            string label = GetMaterialName(index);
            EditorGUILayout.LabelField($"[{index}] {label}", EditorStyles.boldLabel);
            
            // Texture field
            _config.Textures[index] = (Texture2D)EditorGUILayout.ObjectField(
                _config.Textures[index], typeof(Texture2D), false, GUILayout.Height(18));
            
            // Validation
            if (_config.Textures[index] != null)
            {
                var tex = _config.Textures[index];
                if (tex.width != _config.TextureSize || tex.height != _config.TextureSize)
                {
                    EditorGUILayout.HelpBox($"Size mismatch: {tex.width}x{tex.height} != {_config.TextureSize}x{_config.TextureSize}", MessageType.Error);
                }
                if (!tex.isReadable)
                {
                    EditorGUILayout.HelpBox("Texture must be Read/Write Enabled", MessageType.Warning);
                }
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
        
        private string GetMaterialName(int index)
        {
            switch (index)
            {
                case 0: return "Air (unused)";
                case 1: return "Dirt";
                case 2: return "Stone";
                case 3: return "Iron Ore";
                case 4: return "Gold Ore";
                case 5: return "Diamond Ore";
                default: return $"Material {index}";
            }
        }
        
        private void CreateNewConfig()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Voxel Texture Config",
                "VoxelTextureConfig",
                "asset",
                "Choose location for the new Texture Config asset.");
            
            if (string.IsNullOrEmpty(path)) return;
            
            var config = ScriptableObject.CreateInstance<Rendering.VoxelTextureConfig>();
            config.TextureSize = 512;
            config.Textures = new Texture2D[6]; // Default slots for Air, Dirt, Stone, 3 Ores
            
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            
            _config = config;
            Selection.activeObject = config;
        }
        
        private void AutoDetectTextures()
        {
            // Look for textures in common locations
            string[] searchFolders = { "Assets/Textures/Voxel", "Assets/Textures/Materials", "Assets/Art/Textures" };
            string[] keywords = { "dirt", "stone", "rock", "iron", "gold", "ore", "grass", "sand" };
            
            var allTextures = AssetDatabase.FindAssets("t:Texture2D", searchFolders)
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<Texture2D>(path))
                .Where(t => t != null && t.width == _config.TextureSize && t.height == _config.TextureSize)
                .ToList();
            
            if (allTextures.Count == 0)
            {
                EditorUtility.DisplayDialog("Auto-Detect", 
                    $"No {_config.TextureSize}x{_config.TextureSize} textures found in:\n" + 
                    string.Join("\n", searchFolders), "OK");
                return;
            }
            
            // Try to match by name
            _config.Textures = new Texture2D[Mathf.Max(6, allTextures.Count)];
            
            foreach (var tex in allTextures)
            {
                string name = tex.name.ToLower();
                if (name.Contains("dirt") || name.Contains("grass"))
                    _config.Textures[1] = tex;
                else if (name.Contains("stone") || name.Contains("rock"))
                    _config.Textures[2] = tex;
                else if (name.Contains("iron"))
                    _config.Textures[3] = tex;
                else if (name.Contains("gold"))
                    _config.Textures[4] = tex;
                else if (name.Contains("diamond") || name.Contains("crystal"))
                    _config.Textures[5] = tex;
            }
            
            EditorUtility.SetDirty(_config);
            EditorUtility.DisplayDialog("Auto-Detect", 
                $"Found {allTextures.Count} textures. Review assignments and click 'Build Texture Array'.", "OK");
        }
    }
}
#endif
