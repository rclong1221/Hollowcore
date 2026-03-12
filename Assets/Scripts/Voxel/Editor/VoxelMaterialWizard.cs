using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DIG.Voxel.Core;
using DIG.Voxel.Rendering;

namespace DIG.Voxel.Editor
{
    public class VoxelMaterialWizard : EditorWindow
    {
        [MenuItem("DIG/Voxel/Material Creator Wizard")]
        static void ShowWindow() => GetWindow<VoxelMaterialWizard>("Material Wizard");
        
        // Step 1: Basic Info
        private string _materialName = "NewMaterial";
        private byte _materialID = 10;
        
        // Step 2: Textures (auto-detected)
        private Texture2D _albedo;
        private Texture2D _normal;
        private Texture2D _height;
        private Texture2D _detailAlbedo;
        private Texture2D _detailNormal;
        private bool _texturesValid = false;
        
        // Step 3: Properties (Gameplay & Visual)
        private float _hardness = 1f;
        private bool _isMineable = true;
        private float _smoothness = 0.3f;
        private float _metallic = 0f;
        
        // Step 4: Loot
        private bool _generateLootPrefab = true;
        private Mesh _lootMesh;
        private Color _lootColor = Color.gray;
        
        // Preview
        private PreviewRenderUtility _preview;
        
        private int _currentStep = 0;
        private string[] _stepNames = { "Basic Info", "Textures", "Properties", "Loot", "Review" };
        private Vector2 _scrollPos;
        
        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            // Step tabs
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < _stepNames.Length; i++)
            {
                GUI.enabled = i <= _currentStep;
                if (GUILayout.Toggle(_currentStep == i, _stepNames[i], "Button"))
                    _currentStep = i;
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            switch (_currentStep)
            {
                case 0: DrawBasicInfo(); break;
                case 1: DrawTextures(); break;
                case 2: DrawProperties(); break;
                case 3: DrawLoot(); break;
                case 4: DrawReview(); break;
            }
            
            EditorGUILayout.Space(10);
            
            // Navigation
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (_currentStep > 0 && GUILayout.Button("← Back", GUILayout.Width(80)))
                _currentStep--;
            
            if (_currentStep < _stepNames.Length - 1)
            {
                if (GUILayout.Button("Next →", GUILayout.Width(80)))
                    _currentStep++;
            }
            else
            {
                if (GUILayout.Button("Create Material", GUILayout.Width(120)))
                    CreateMaterial();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawBasicInfo()
        {
            EditorGUILayout.LabelField("Step 1: Basic Information", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            _materialName = EditorGUILayout.TextField("Material Name", _materialName);
            _materialID = (byte)EditorGUILayout.IntSlider("Material ID", _materialID, 1, 255);
            
            // Validate unique ID
            var registry = Resources.Load<VoxelMaterialRegistry>("VoxelMaterialRegistry");
            if (registry != null)
            {
                bool idUsed = registry.Materials.Any(m => m != null && m.MaterialID == _materialID);
                if (idUsed)
                {
                    EditorGUILayout.HelpBox($"Material ID {_materialID} is already in use!", MessageType.Error);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Registry not found. Will create temporary definition.", MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawTextures()
        {
            EditorGUILayout.LabelField("Step 2: Textures", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "Drag textures anywhere - they'll be auto-assigned based on filename:\n" +
                "- *_albedo, *_diffuse, *_color → Albedo\n" +
                "- *_normal, *_nrm → Normal\n" +
                "- *_height, *_displacement → Height\n" +
                "- *_detail, *detail_nrm → Detail",
                MessageType.Info);
            
            // Drop area
            var dropRect = GUILayoutUtility.GetRect(100, 80);
            GUI.Box(dropRect, "Drop Textures Here\n(Auto-detect type)");
            HandleTextureDrop(dropRect);
            
            EditorGUILayout.Space();
            
            // Show assigned textures with small previews
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawTextureField("Albedo", ref _albedo, true);
            DrawTextureField("Normal", ref _normal, false);
            DrawTextureField("Height", ref _height, false);
            DrawTextureField("Detail Albedo", ref _detailAlbedo, false);
            DrawTextureField("Detail Normal", ref _detailNormal, false);
            EditorGUILayout.EndVertical();
            
            _texturesValid = _albedo != null;
            
            if (!_texturesValid)
            {
                EditorGUILayout.HelpBox("At least Albedo texture is required.", MessageType.Warning);
            }
            else
            {
                // Auto-set loot color if albedo is present
                if (_albedo != null && _lootColor == Color.gray)
                {
                    // Sample center pixel
                    // Cannot ReadPixels from Texture2D directly unless readable, but we can try
                    // Just skipping auto-color for now to avoid complexity
                }
            }
        }
        
        private void DrawTextureField(string label, ref Texture2D tex, bool required)
        {
            EditorGUILayout.BeginHorizontal();
            
            // Preview
            var previewRect = GUILayoutUtility.GetRect(50, 50, GUILayout.Width(50));
            if (tex != null)
                EditorGUI.DrawPreviewTexture(previewRect, tex);
            else
                EditorGUI.DrawRect(previewRect, Color.gray);
            
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(label + (required ? " *" : ""));
            
            tex = (Texture2D)EditorGUILayout.ObjectField(tex, typeof(Texture2D), false, GUILayout.Width(200));
            
            if (tex != null && GUILayout.Button("Clear", GUILayout.Width(50)))
                tex = null;
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
        
        private void HandleTextureDrop(Rect dropRect)
        {
            Event evt = Event.current;
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (!dropRect.Contains(evt.mousePosition)) return;
                
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is Texture2D tex)
                        {
                            AssignTextureByName(tex);
                        }
                    }
                }
            }
        }
        
        private void AssignTextureByName(Texture2D tex)
        {
            string name = tex.name.ToLower();
            
            if (name.Contains("albedo") || name.Contains("diffuse") || name.Contains("color") || name.Contains("_d") || name.Contains("_c"))
                _albedo = tex;
            else if (name.Contains("normal") || name.Contains("_n") || name.Contains("nrm"))
                _normal = tex;
            else if (name.Contains("height") || name.Contains("_h") || name.Contains("displacement"))
                _height = tex;
            else if (name.Contains("detail"))
            {
                if (name.Contains("norm") || name.Contains("nrm"))
                    _detailNormal = tex;
                else
                    _detailAlbedo = tex;
            }
            else
                _albedo = tex;  // Default to albedo
        }
        
        private void DrawProperties()
        {
            EditorGUILayout.LabelField("Step 3: Properties", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Gameplay", EditorStyles.boldLabel);
            _hardness = EditorGUILayout.Slider("Hardness", _hardness, 0.1f, 10f);
            EditorGUILayout.HelpBox($"Mining time = 1 / {_hardness:F1} seconds", MessageType.None);
            
            _isMineable = EditorGUILayout.Toggle("Is Mineable", _isMineable);
            
            if (!_isMineable)
            {
                EditorGUILayout.HelpBox("This material cannot be destroyed (e.g., bedrock).", MessageType.Info);
            }
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Visual Surface", EditorStyles.boldLabel);
            _smoothness = EditorGUILayout.Slider("Smoothness", _smoothness, 0f, 1f);
            _metallic = EditorGUILayout.Slider("Metallic", _metallic, 0f, 1f);
            EditorGUILayout.EndVertical();
        }
        
        private void DrawLoot()
        {
            EditorGUILayout.LabelField("Step 4: Loot Configuration", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            if (!_isMineable)
            {
                EditorGUILayout.HelpBox("Loot not applicable - material is not mineable.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }
            
            _generateLootPrefab = EditorGUILayout.Toggle("Generate Loot Prefab", _generateLootPrefab);
            
            if (_generateLootPrefab)
            {
                _lootMesh = (Mesh)EditorGUILayout.ObjectField("Loot Mesh", _lootMesh, typeof(Mesh), false);
                
                if (_lootMesh == null)
                {
                    EditorGUILayout.HelpBox("Leave empty to use default cube mesh.", MessageType.None);
                }
                
                _lootColor = EditorGUILayout.ColorField("Loot Color", _lootColor);
            }
            EditorGUILayout.EndVertical();
        }
        
        private void DrawReview()
        {
            EditorGUILayout.LabelField("Step 5: Review & Create", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Material Name:", _materialName);
            EditorGUILayout.LabelField("Material ID:", _materialID.ToString());
            EditorGUILayout.LabelField("Visuals:");
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Albedo:", _albedo?.name ?? "(none)");
            EditorGUILayout.LabelField("Normal:", _normal?.name ?? "(none)");
            EditorGUILayout.LabelField("Surface:", $"S:{_smoothness:F1} M:{_metallic:F1}");
            EditorGUI.indentLevel--;
            
            EditorGUILayout.LabelField("Gameplay:");
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Hardness:", _hardness.ToString("F1"));
            EditorGUILayout.LabelField("Mineable:", _isMineable.ToString());
            EditorGUILayout.LabelField("Gen Loot:", _generateLootPrefab.ToString());
            EditorGUI.indentLevel--;
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // Validation
            var issues = ValidateAll();
            if (issues.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Issues to fix:\n" + string.Join("\n", issues.Select(i => "• " + i)),
                    MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox("Ready to create! Files will be saved in Assets/Content/VoxelMaterials/" + _materialName, MessageType.Info);
            }
        }
        
        private List<string> ValidateAll()
        {
            var issues = new List<string>();
            
            if (string.IsNullOrEmpty(_materialName))
                issues.Add("Material name is empty");
            if (_albedo == null)
                issues.Add("Albedo texture is required");
            
            // Check ID uniqueness
            var registry = Resources.Load<VoxelMaterialRegistry>("VoxelMaterialRegistry");
            if (registry != null && registry.Materials.Any(m => m != null && m.MaterialID == _materialID))
                issues.Add($"Material ID {_materialID} is already in use");
            
            return issues;
        }
        
        private void CreateMaterial()
        {
            var issues = ValidateAll();
            if (issues.Count > 0)
            {
                EditorUtility.DisplayDialog("Cannot Create", 
                    "Fix issues on Review step first.", "OK");
                return;
            }
            
            string folderPath = "Assets/Content/VoxelMaterials/" + _materialName;
            Directory.CreateDirectory(folderPath);
            
            // 1. Create Visual Material
            var visualMat = ScriptableObject.CreateInstance<VoxelVisualMaterial>();
            visualMat.MaterialID = _materialID;
            visualMat.DisplayName = _materialName;
            visualMat.Albedo = _albedo;
            visualMat.Normal = _normal;
            visualMat.HeightMap = _height;
            visualMat.DetailAlbedo = _detailAlbedo;
            visualMat.DetailNormal = _detailNormal;
            visualMat.Smoothness = _smoothness;
            visualMat.Metallic = _metallic;
            visualMat.Tint = Color.white;
            
            AssetDatabase.CreateAsset(visualMat, folderPath + "/" + _materialName + "_Visual.asset");
            
            // 2. Create Definition
            var matDef = ScriptableObject.CreateInstance<VoxelMaterialDefinition>();
            matDef.MaterialID = _materialID;
            matDef.MaterialName = _materialName;
            matDef.Hardness = _hardness;
            matDef.IsMineable = _isMineable;
            matDef.VisualMaterial = visualMat; // Link!
            matDef.TextureArrayIndex = _materialID; // Default mapping
            
            // 3. Generate loot
            if (_generateLootPrefab && _isMineable)
            {
                var lootPrefab = CreateLootPrefab(folderPath);
                matDef.LootPrefab = lootPrefab;
            }
            
            AssetDatabase.CreateAsset(matDef, folderPath + "/" + _materialName + "_Def.asset");
            
            // 4. Add to Registry
            var registry = Resources.Load<VoxelMaterialRegistry>("VoxelMaterialRegistry");
            if (registry != null)
            {
                if (!registry.Materials.Contains(matDef))
                {
                    registry.Materials.Add(matDef);
                    registry.Materials.Sort((a, b) => a.MaterialID.CompareTo(b.MaterialID));
                    EditorUtility.SetDirty(registry);
                }
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            UnityEngine.Debug.Log($"[Wizard] Successfully created material: {_materialName}");
            
            // Ping
            EditorGUIUtility.PingObject(matDef);
            Close();
        }
        
        private GameObject CreateLootPrefab(string folderPath)
        {
            var lootGo = new GameObject(_materialName + "_Loot");
            
            // Mesh
            var meshFilter = lootGo.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = _lootMesh ?? GetDefaultCubeMesh();
            
            // Renderer
            var renderer = lootGo.AddComponent<MeshRenderer>();
            
            // Reuse albedo for loot material
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (_albedo != null)
                mat.mainTexture = _albedo;
            mat.color = _lootColor;
            
            AssetDatabase.CreateAsset(mat, folderPath + "/" + _materialName + "_LootMat.mat");
            renderer.sharedMaterial = mat;
            
            // Physics
            lootGo.AddComponent<BoxCollider>();
            var rb = lootGo.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            
            // Save prefab
            string path = folderPath + "/" + _materialName + "_Loot.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(lootGo, path);
            
            DestroyImmediate(lootGo);
            
            return prefab;
        }
        
        private Mesh GetDefaultCubeMesh()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(go);
            return mesh;
        }
    }
}
