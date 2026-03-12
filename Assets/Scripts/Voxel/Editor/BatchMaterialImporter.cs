using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DIG.Voxel.Core;
using DIG.Voxel.Rendering;

namespace DIG.Voxel.Editor
{
    public class BatchMaterialImporter : EditorWindow
    {
        [MenuItem("DIG/Voxel/Batch Material Import")]
        static void ShowWindow() => GetWindow<BatchMaterialImporter>("Batch Import");
        
        private DefaultAsset _rootFolder;
        private byte _startingID = 20;
        private bool _generateLoot = true;
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Batch Material Importer", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "Expected folder structure:\n" +
                "  /Materials\n" +
                "    /Stone\n" +
                "      Stone_albedo.png\n" +
                "      Stone_normal.png\n" +
                "    /Iron\n" +
                "      Iron_albedo.png\n" +
                "      Iron_normal.png",
                MessageType.Info);
            
            _rootFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "Root Folder", _rootFolder, typeof(DefaultAsset), false);
            
            if (_rootFolder != null)
            {
                string path = AssetDatabase.GetAssetPath(_rootFolder);
                if (!AssetDatabase.IsValidFolder(path))
                {
                    EditorGUILayout.HelpBox("Selected object is not a folder", MessageType.Error);
                    return;
                }

                var subdirs = Directory.GetDirectories(path);
                
                EditorGUILayout.Space();
                _startingID = (byte)EditorGUILayout.IntField("Starting ID", _startingID);
                _generateLoot = EditorGUILayout.Toggle("Generate Loot", _generateLoot);
                
                EditorGUILayout.LabelField($"Found {subdirs.Length} material folders");
                
                if (GUILayout.Button($"Import {subdirs.Length} Materials"))
                {
                    ImportAll(subdirs);
                }
            }
        }
        
        private void ImportAll(string[] folders)
        {
            int imported = 0;
            int errors = 0;
            int skipped = 0;
            
            var registry = Resources.Load<VoxelMaterialRegistry>("VoxelMaterialRegistry");
            var usedIDs = new HashSet<byte>();
            
            if (registry != null)
            {
                foreach (var m in registry.Materials)
                    if (m != null) usedIDs.Add(m.MaterialID);
            }
            
            byte currentID = _startingID;
            
            foreach (var folder in folders)
            {
                try
                {
                    // Find next available ID
                    while (usedIDs.Contains(currentID) && currentID < 255)
                        currentID++;
                    
                    if (currentID >= 255)
                    {
                        UnityEngine.Debug.LogError("Ran out of Material IDs!");
                        break;
                    }
                    
                    if (ImportMaterialFromFolder(folder, currentID))
                    {
                        imported++;
                        usedIDs.Add(currentID);
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogError($"Failed to import {folder}: {e.Message}");
                    errors++;
                }
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            UnityEngine.Debug.Log($"[BatchImport] Imported {imported} materials, {skipped} skipped, {errors} errors");
        }
        
        private bool ImportMaterialFromFolder(string folderPath, byte id)
        {
            string materialName = Path.GetFileName(folderPath);
            
            // cleanup path slashes
            folderPath = folderPath.Replace("\\", "/");
            
            // Check if already exists?
            if (File.Exists(folderPath + "/" + materialName + "_Def.asset"))
            {
                UnityEngine.Debug.LogWarning($"Skipping {materialName} - already exists");
                return false;
            }
            
            // Find textures
            var textures = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
            
            Texture2D albedo = null, normal = null, height = null, detailAlbedo = null, detailNormal = null;
            
            foreach (var guid in textures)
            {
                var texPath = AssetDatabase.GUIDToAssetPath(guid);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                
                string name = tex.name.ToLower();
                if (name.Contains("albedo") || name.Contains("diffuse") || name.Contains("_d") || name.Contains("_c"))
                    albedo = tex;
                else if (name.Contains("normal") || name.Contains("nrm") || name.Contains("_n"))
                    normal = tex;
                else if (name.Contains("height") || name.Contains("displacement") || name.Contains("_h"))
                    height = tex;
                else if (name.Contains("detail"))
                {
                    if (name.Contains("nrm")) detailNormal = tex;
                    else detailAlbedo = tex;
                }
            }
            
            if (albedo == null)
            {
                UnityEngine.Debug.LogWarning($"Skipping {materialName} - No albedo texture found");
                return false;
            }
            
            // 1. Create Visual Material
            var visualMat = ScriptableObject.CreateInstance<VoxelVisualMaterial>();
            visualMat.MaterialID = id;
            visualMat.DisplayName = materialName;
            visualMat.Albedo = albedo;
            visualMat.Normal = normal;
            visualMat.HeightMap = height;
            visualMat.DetailAlbedo = detailAlbedo;
            visualMat.DetailNormal = detailNormal;
            
            AssetDatabase.CreateAsset(visualMat, folderPath + "/" + materialName + "_Visual.asset");
            
            // 2. Create Definition
            var matDef = ScriptableObject.CreateInstance<VoxelMaterialDefinition>();
            matDef.MaterialID = id;
            matDef.MaterialName = materialName;
            matDef.VisualMaterial = visualMat;
            matDef.TextureArrayIndex = id;
            
            // 3. Loot
            if (_generateLoot)
            {
                var lootPrefab = CreateLootPrefab(folderPath, materialName, albedo);
                matDef.LootPrefab = lootPrefab;
            }
            
            AssetDatabase.CreateAsset(matDef, folderPath + "/" + materialName + "_Def.asset");
            
            // 4. Registry
            var registry = Resources.Load<VoxelMaterialRegistry>("VoxelMaterialRegistry");
            if (registry != null)
            {
                registry.Materials.Add(matDef);
                EditorUtility.SetDirty(registry);
            }
            
            return true;
        }
        
        private GameObject CreateLootPrefab(string folderPath, string materialName, Texture2D albedo)
        {
            var lootGo = new GameObject(materialName + "_Loot");
            
            // Mesh
            var meshFilter = lootGo.AddComponent<MeshFilter>();
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meshFilter.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(go);
            
            // Renderer
            var renderer = lootGo.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.mainTexture = albedo;
            
            string matPath = folderPath + "/" + materialName + "_LootMat.mat";
            AssetDatabase.CreateAsset(mat, matPath);
            renderer.sharedMaterial = mat;
            
            // Physics
            lootGo.AddComponent<BoxCollider>();
            var rb = lootGo.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            
            // Save prefab
            string path = folderPath + "/" + materialName + "_Loot.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(lootGo, path);
            
            DestroyImmediate(lootGo);
            return prefab;
        }
    }
}
