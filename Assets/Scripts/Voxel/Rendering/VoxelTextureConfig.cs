using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DIG.Voxel.Rendering
{
    [CreateAssetMenu(fileName = "VoxelTextureConfig", menuName = "DIG/Voxel/Texture Config")]
    public class VoxelTextureConfig : ScriptableObject
    {
        [Header("Settings")]
        public int TextureSize = 512;
        public FilterMode FilterMode = FilterMode.Bilinear;
        public int AnisoLevel = 4;

        [Header("Textures (Index = Material ID)")]
        [Tooltip("Index 0 is typically Air/Unused, but kept for alignment.")]
        public Texture2D[] Textures;

        [Header("Runtime")]
        [SerializeField] private Texture2DArray _textureArray;

        public Texture2DArray TextureArray => _textureArray;

        public void Rebuild()
        {
            if (Textures == null || Textures.Length == 0)
            {
                UnityEngine.Debug.LogError("[VoxelTextureConfig] No textures assigned!");
                return;
            }

            // Validate sizes
            foreach (var tex in Textures)
            {
                if (tex != null && (tex.width != TextureSize || tex.height != TextureSize))
                {
                    UnityEngine.Debug.LogError($"[VoxelTextureConfig] Texture {tex.name} is {tex.width}x{tex.height}, expected {TextureSize}x{TextureSize}!");
                    return;
                }
            }

            // Create Array
            _textureArray = new Texture2DArray(TextureSize, TextureSize, Textures.Length, TextureFormat.RGBA32, true);
            _textureArray.filterMode = FilterMode;
            _textureArray.anisoLevel = AnisoLevel;
            _textureArray.wrapMode = TextureWrapMode.Repeat;

            // Copy data
            for (int i = 0; i < Textures.Length; i++)
            {
                if (Textures[i] != null)
                {
                    Graphics.CopyTexture(Textures[i], 0, 0, _textureArray, i, 0);
                }
                else
                {
                    // Fill with error magenta if missing
                    var pixels = new Color[TextureSize * TextureSize];
                    for (int p = 0; p < pixels.Length; p++) pixels[p] = Color.magenta;
                    _textureArray.SetPixels(pixels, i, 0);
                    _textureArray.Apply(); // Apply only for this slice if manual set
                }
            }
            
            _textureArray.Apply(); // Apply all
            UnityEngine.Debug.Log($"[VoxelTextureConfig] Built Texture2DArray with {Textures.Length} slices.");
            
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
#endif
        }

        private void OnEnable()
        {
            // Rebuild if runtime array is missing but we have config?
            // Usually we want to bake this in editor.
        }
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(VoxelTextureConfig))]
    public class VoxelTextureConfigEditor : UnityEditor.Editor
    {
        private const int PREVIEW_SIZE = 48;
        private bool _foldoutTextures = true;
        
        public override void OnInspectorGUI()
        {
            VoxelTextureConfig config = (VoxelTextureConfig)target;
            
            EditorGUILayout.Space(5);
            
            // Settings section
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            config.TextureSize = EditorGUILayout.IntPopup("Texture Size", config.TextureSize, 
                new[] { "128", "256", "512", "1024" }, new[] { 128, 256, 512, 1024 });
            config.FilterMode = (FilterMode)EditorGUILayout.EnumPopup("Filter Mode", config.FilterMode);
            config.AnisoLevel = EditorGUILayout.IntSlider("Aniso Level", config.AnisoLevel, 0, 16);
            
            EditorGUILayout.Space(10);
            
            // Textures section with previews
            _foldoutTextures = EditorGUILayout.Foldout(_foldoutTextures, "Textures (Index = Material ID)", true);
            
            if (_foldoutTextures)
            {
                EditorGUI.indentLevel++;
                
                if (config.Textures == null)
                    config.Textures = new Texture2D[0];
                
                // Add/Remove buttons
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+ Add Slot", GUILayout.Width(80)))
                {
                    System.Array.Resize(ref config.Textures, config.Textures.Length + 1);
                    EditorUtility.SetDirty(config);
                }
                if (GUILayout.Button("- Remove", GUILayout.Width(80)) && config.Textures.Length > 0)
                {
                    System.Array.Resize(ref config.Textures, config.Textures.Length - 1);
                    EditorUtility.SetDirty(config);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(5);
                
                // Draw each texture slot with preview
                for (int i = 0; i < config.Textures.Length; i++)
                {
                    DrawTextureSlot(config, i);
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(10);
            
            // Build button
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("Build Texture Array", GUILayout.Height(30)))
            {
                config.Rebuild();
            }
            GUI.backgroundColor = Color.white;
            
            // Status
            if (config.TextureArray != null)
            {
                EditorGUILayout.HelpBox(
                    $"✓ Texture Array: {config.TextureArray.width}x{config.TextureArray.height}, {config.TextureArray.depth} slices",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Texture Array not built. Click 'Build Texture Array'.", MessageType.Warning);
            }
            
            if (GUI.changed)
            {
                EditorUtility.SetDirty(config);
            }
        }
        
        private void DrawTextureSlot(VoxelTextureConfig config, int index)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            // Texture preview
            Rect previewRect = GUILayoutUtility.GetRect(PREVIEW_SIZE, PREVIEW_SIZE, GUILayout.Width(PREVIEW_SIZE));
            if (config.Textures[index] != null)
            {
                EditorGUI.DrawPreviewTexture(previewRect, config.Textures[index]);
            }
            else
            {
                EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f));
                GUI.Label(previewRect, "?", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 });
            }
            
            EditorGUILayout.BeginVertical();
            
            // Label with material name
            string materialName = GetMaterialName(index);
            EditorGUILayout.LabelField($"[{index}] {materialName}", EditorStyles.boldLabel);
            
            // Texture field
            config.Textures[index] = (Texture2D)EditorGUILayout.ObjectField(
                config.Textures[index], typeof(Texture2D), false);
            
            // Validation
            if (config.Textures[index] != null)
            {
                var tex = config.Textures[index];
                if (tex.width != config.TextureSize || tex.height != config.TextureSize)
                {
                    EditorGUILayout.HelpBox($"⚠ Size: {tex.width}x{tex.height}", MessageType.Warning);
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
                case 5: return "Diamond";
                default: return $"Material";
            }
        }
    }
#endif
}
