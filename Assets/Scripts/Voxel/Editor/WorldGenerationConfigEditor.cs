using UnityEngine;
using UnityEditor;
using DIG.Voxel.Core;
using DIG.Voxel.Geology;

namespace DIG.Voxel.Editor
{
    /// <summary>
    /// Custom inspector for WorldGenerationConfig with quick setup and validation.
    /// </summary>
    [CustomEditor(typeof(WorldGenerationConfig))]
    public class WorldGenerationConfigEditor : UnityEditor.Editor
    {
        private SerializedProperty _groundLevel;
        private SerializedProperty _seed;
        private SerializedProperty _strataProfile;
        private SerializedProperty _depthCurve;
        private SerializedProperty _oreDefinitions;
        private SerializedProperty _terrainNoiseScale;
        private SerializedProperty _terrainNoiseAmplitude;
        private SerializedProperty _enableOres;
        private SerializedProperty _enableStrata;
        
        private bool _showQuickSetup = true;
        private bool _showValidation = true;
        
        private void OnEnable()
        {
            _groundLevel = serializedObject.FindProperty("GroundLevel");
            _seed = serializedObject.FindProperty("Seed");
            _strataProfile = serializedObject.FindProperty("StrataProfile");
            _depthCurve = serializedObject.FindProperty("DepthCurve");
            _oreDefinitions = serializedObject.FindProperty("OreDefinitions");
            _terrainNoiseScale = serializedObject.FindProperty("TerrainNoiseScale");
            _terrainNoiseAmplitude = serializedObject.FindProperty("TerrainNoiseAmplitude");
            _enableOres = serializedObject.FindProperty("EnableOres");
            _enableStrata = serializedObject.FindProperty("EnableStrata");
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            DrawQuickSetup();
            
            EditorGUILayout.Space(10);
            
            DrawMainProperties();
            
            EditorGUILayout.Space(10);
            
            DrawValidation();
            
            EditorGUILayout.Space(10);
            
            DrawActions();
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawQuickSetup()
        {
            _showQuickSetup = EditorGUILayout.Foldout(_showQuickSetup, "Quick Setup", true, EditorStyles.foldoutHeader);
            
            if (!_showQuickSetup) return;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("1. Create Required Assets", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Strata Profile"))
            {
                CreateAsset<StrataProfile>("NewStrataProfile");
            }
            if (GUILayout.Button("Create Depth Curve"))
            {
                CreateAsset<DepthValueCurve>("NewDepthCurve");
            }
            EditorGUILayout.EndHorizontal();
            
            if (GUILayout.Button("Create Sample Ore (Iron)"))
            {
                CreateSampleOre("Iron", 3, 10, 80, OreRarity.Common, 0.6f);
            }
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("2. Assign to Config", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_strataProfile);
            EditorGUILayout.PropertyField(_depthCurve);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("3. Add Ore Definitions", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Auto-Find All Ores in Project"))
            {
                AutoFindOres();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawMainProperties()
        {
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.PropertyField(_groundLevel);
            EditorGUILayout.PropertyField(_seed);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.PropertyField(_terrainNoiseScale);
            EditorGUILayout.PropertyField(_terrainNoiseAmplitude);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.PropertyField(_enableStrata);
            EditorGUILayout.PropertyField(_enableOres);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.PropertyField(_oreDefinitions, true);
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawValidation()
        {
            _showValidation = EditorGUILayout.Foldout(_showValidation, "Validation", true, EditorStyles.foldoutHeader);
            
            if (!_showValidation) return;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            bool hasErrors = false;
            
            // Check strata
            if (_strataProfile.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Strata Profile not assigned. Rock layers will use default stone.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField("✓ Strata Profile assigned", EditorStyles.miniLabel);
            }
            
            // Check ores
            if (_oreDefinitions.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No ore definitions. Ore veins will not generate.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"✓ {_oreDefinitions.arraySize} ore types configured", EditorStyles.miniLabel);
                
                // Check for duplicate material IDs
                var config = (WorldGenerationConfig)target;
                if (config.OreDefinitions != null)
                {
                    var ids = new System.Collections.Generic.HashSet<byte>();
                    foreach (var ore in config.OreDefinitions)
                    {
                        if (ore == null) continue;
                        if (!ids.Add(ore.MaterialID))
                        {
                            EditorGUILayout.HelpBox($"Duplicate MaterialID {ore.MaterialID} found!", MessageType.Error);
                            hasErrors = true;
                        }
                    }
                }
            }
            
            // Check Resources location
            string assetPath = AssetDatabase.GetAssetPath(target);
            if (!assetPath.Contains("Resources"))
            {
                EditorGUILayout.HelpBox(
                    "Config not in Resources folder!\n" +
                    "Move to Assets/.../Resources/WorldGenerationConfig.asset",
                    MessageType.Error);
                hasErrors = true;
            }
            else if (!assetPath.EndsWith("WorldGenerationConfig.asset"))
            {
                EditorGUILayout.HelpBox(
                    "Config should be named 'WorldGenerationConfig.asset' for auto-loading.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField("✓ Correctly placed in Resources", EditorStyles.miniLabel);
            }
            
            if (!hasErrors)
            {
                EditorGUILayout.LabelField("✓ Configuration valid", EditorStyles.boldLabel);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawActions()
        {
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Open Strata Visualizer"))
            {
                EditorWindow.GetWindow<StrataVisualizerWindow>("Strata Visualizer");
            }
            
            if (GUILayout.Button("Open Ore Distribution"))
            {
                EditorWindow.GetWindow<OreDistributionWindow>("Ore Distribution");
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Randomize Seed"))
            {
                _seed.intValue = (int)(Random.value * int.MaxValue);
            }
        }
        
        private void CreateAsset<T>(string defaultName) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            
            string path = EditorUtility.SaveFilePanelInProject(
                $"Create {typeof(T).Name}", defaultName, "asset",
                $"Choose location for new {typeof(T).Name}");
            
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(asset);
                
                // Auto-assign if appropriate
                if (typeof(T) == typeof(StrataProfile))
                    _strataProfile.objectReferenceValue = asset;
                else if (typeof(T) == typeof(DepthValueCurve))
                    _depthCurve.objectReferenceValue = asset;
            }
        }
        
        private void CreateSampleOre(string name, byte materialId, float minDepth, float maxDepth, OreRarity rarity, float threshold)
        {
            var ore = ScriptableObject.CreateInstance<OreDefinition>();
            ore.OreName = name;
            ore.MaterialID = materialId;
            ore.MinDepth = minDepth;
            ore.MaxDepth = maxDepth;
            ore.Rarity = rarity;
            ore.Threshold = threshold;
            
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Ore Definition", $"{name}Ore", "asset",
                "Choose location for new ore definition");
            
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(ore, path);
                AssetDatabase.SaveAssets();
                
                // Add to array
                _oreDefinitions.arraySize++;
                _oreDefinitions.GetArrayElementAtIndex(_oreDefinitions.arraySize - 1).objectReferenceValue = ore;
                
                EditorGUIUtility.PingObject(ore);
            }
        }
        
        private void AutoFindOres()
        {
            var ores = Resources.LoadAll<OreDefinition>("");
            
            if (ores.Length == 0)
            {
                EditorUtility.DisplayDialog("No Ores Found", 
                    "No OreDefinition assets found in Resources folders.", "OK");
                return;
            }
            
            _oreDefinitions.arraySize = ores.Length;
            for (int i = 0; i < ores.Length; i++)
            {
                _oreDefinitions.GetArrayElementAtIndex(i).objectReferenceValue = ores[i];
            }
            
            EditorUtility.DisplayDialog("Ores Found", 
                $"Found and assigned {ores.Length} ore definitions.", "OK");
        }
        
        // Static method for menu item
        [MenuItem("DIG/World/Create World Generation Config")]
        static void CreateWorldConfig()
        {
            var config = ScriptableObject.CreateInstance<WorldGenerationConfig>();
            
            // Ensure Resources folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            
            string path = "Assets/Resources/WorldGenerationConfig.asset";
            
            if (AssetDatabase.LoadAssetAtPath<WorldGenerationConfig>(path) != null)
            {
                if (!EditorUtility.DisplayDialog("Config Exists", 
                    "WorldGenerationConfig already exists. Overwrite?", "Overwrite", "Cancel"))
                {
                    return;
                }
            }
            
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }
    }
}

