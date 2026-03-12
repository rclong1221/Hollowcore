using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using DIG.Weapons.Authoring;
using DIG.Items;

namespace DIG.Editor.OpsiveExtractor
{
    /// <summary>
    /// Converts OPSIVE weapon/item prefabs into ECS-compatible prefabs.
    ///
    /// This tool:
    /// 1. Takes an OPSIVE CharacterItem prefab
    /// 2. Extracts all relevant configuration using reflection
    /// 3. Creates a new prefab with ECS authoring components
    /// 4. Preserves mesh/material references for visuals
    /// 5. Adds appropriate ECS components (WeaponAuthoring, ItemAuthoring, etc.)
    ///
    /// The resulting prefab can be used directly with Unity's entity baking system.
    /// </summary>
    public class OpsivePrefabConverter : EditorWindow
    {
        private GameObject _sourcePrefab;
        private string _outputFolder = "Assets/Prefabs/Items/Converted";
        private bool _preserveHierarchy = true;
        private bool _addAnimationRelay = true;
        private Vector2 _scrollPosition;

        private ConversionResult _lastResult;

        [MenuItem("Tools/DIG/OPSIVE Prefab Converter")]
        public static void ShowWindow()
        {
            var window = GetWindow<OpsivePrefabConverter>("Prefab Converter");
            window.minSize = new Vector2(450, 400);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("OPSIVE → ECS Prefab Converter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Converts OPSIVE weapon/item prefabs to ECS-compatible prefabs.\n\n" +
                "• Extracts configuration data\n" +
                "• Preserves visual components (mesh, materials)\n" +
                "• Adds ECS authoring components\n" +
                "• Optionally adds animation event relay",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Source prefab
            _sourcePrefab = (GameObject)EditorGUILayout.ObjectField(
                "Source OPSIVE Prefab",
                _sourcePrefab,
                typeof(GameObject),
                false);

            EditorGUILayout.Space(5);

            // Output folder
            using (new EditorGUILayout.HorizontalScope())
            {
                _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var selected = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        // Convert to relative path
                        var relativePath = GetProjectRelativePath(selected);
                        if (!string.IsNullOrEmpty(relativePath))
                        {
                            _outputFolder = relativePath;
                        }
                    }
                }
            }

            EditorGUILayout.Space(5);

            // Options
            _preserveHierarchy = EditorGUILayout.Toggle("Preserve Hierarchy", _preserveHierarchy);
            _addAnimationRelay = EditorGUILayout.Toggle("Add Animation Relay", _addAnimationRelay);

            EditorGUILayout.Space(15);

            // Convert button
            using (new EditorGUI.DisabledScope(_sourcePrefab == null))
            {
                if (GUILayout.Button("Convert Prefab", GUILayout.Height(35)))
                {
                    ConvertPrefab();
                }
            }

            EditorGUILayout.Space(10);

            // Batch conversion
            EditorGUILayout.LabelField("Batch Conversion", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Convert All in Folder"))
                {
                    BatchConvertFolder();
                }

                if (GUILayout.Button("Convert Selected"))
                {
                    BatchConvertSelected();
                }
            }

            EditorGUILayout.Space(5);
            
            // One-click convert all Opsive weapons
            if (GUILayout.Button("🔫 Convert ALL Opsive Weapons", GUILayout.Height(30)))
            {
                ConvertAllOpsiveWeapons();
            }
            EditorGUILayout.HelpBox("Scans entire project for Opsive weapon prefabs and converts any that don't have an _ECS version yet.", MessageType.None);

            // Display last result
            if (_lastResult != null)
            {
                EditorGUILayout.Space(15);
                DrawConversionResult();
            }
        }

        private void ConvertPrefab()
        {
            if (_sourcePrefab == null)
            {
                Debug.LogWarning("[PrefabConverter] No source prefab specified");
                return;
            }

            _lastResult = ConvertSinglePrefab(_sourcePrefab);

            if (_lastResult.Success)
            {
                Debug.Log($"[PrefabConverter] Successfully converted: {_lastResult.OutputPath}");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(_lastResult.OutputPath);
            }
            else
            {
                Debug.LogError($"[PrefabConverter] Conversion failed: {_lastResult.ErrorMessage}");
            }
        }

        private ConversionResult ConvertSinglePrefab(GameObject source)
        {
            var result = new ConversionResult { SourceName = source.name };

            try
            {
                // Ensure output folder exists
                if (!AssetDatabase.IsValidFolder(_outputFolder))
                {
                    CreateFolderRecursive(_outputFolder);
                }

                // Determine item type
                var itemType = DetermineItemType(source);
                result.DetectedType = itemType.ToString();

                // Create a copy of the prefab
                var outputPath = $"{_outputFolder}/{source.name}_ECS.prefab";
                result.OutputPath = outputPath;

                // Instantiate, modify, save as new prefab
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);

                try
                {
                    // Remove OPSIVE components (they'll cause issues in ECS)
                    RemoveOpsiveComponents(instance, result);

                    // Add ECS authoring components
                    AddEcsAuthoringComponents(instance, source, itemType, result);

                    // Add animation relay if requested
                    if (_addAnimationRelay)
                    {
                        AddAnimationRelay(instance, result);
                    }

                    // Clean up unnecessary components
                    CleanupComponents(instance, result);

                    // Save as new prefab
                    var savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, outputPath);

                    if (savedPrefab != null)
                    {
                        result.Success = true;
                        result.ConvertedPrefab = savedPrefab;
                    }
                    else
                    {
                        result.ErrorMessage = "Failed to save prefab";
                    }
                }
                finally
                {
                    // Always destroy the temporary instance
                    DestroyImmediate(instance);
                }
            }
            catch (System.Exception e)
            {
                result.Success = false;
                result.ErrorMessage = e.Message;
                Debug.LogException(e);
            }

            return result;
        }

        private ItemTypeEnum DetermineItemType(GameObject source)
        {
            // Check for OPSIVE action components
            if (HasComponentByName(source, "ShootableAction"))
                return ItemTypeEnum.Shootable;

            if (HasComponentByName(source, "MeleeAction"))
                return ItemTypeEnum.Melee;

            if (HasComponentByName(source, "ThrowableAction"))
                return ItemTypeEnum.Throwable;

            if (HasComponentByName(source, "ShieldAction"))
                return ItemTypeEnum.Shield;

            if (HasComponentByName(source, "MagicAction"))
                return ItemTypeEnum.Magic;

            // Check name-based heuristics
            var name = source.name.ToLower();
            if (name.Contains("gun") || name.Contains("rifle") || name.Contains("pistol"))
                return ItemTypeEnum.Shootable;

            if (name.Contains("sword") || name.Contains("axe") || name.Contains("knife"))
                return ItemTypeEnum.Melee;

            if (name.Contains("grenade") || name.Contains("bomb"))
                return ItemTypeEnum.Throwable;

            if (name.Contains("shield"))
                return ItemTypeEnum.Shield;

            return ItemTypeEnum.Generic;
        }

        private void RemoveOpsiveComponents(GameObject instance, ConversionResult result)
        {
            var componentsToRemove = new List<Component>();

            // Find all OPSIVE components
            foreach (var component in instance.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;

                var type = component.GetType();
                var ns = type.Namespace ?? "";

                if (ns.StartsWith("Opsive"))
                {
                    componentsToRemove.Add(component);
                    result.RemovedComponents.Add(type.Name);
                }
            }

            // Remove them (in reverse to handle dependencies)
            componentsToRemove.Reverse();
            foreach (var component in componentsToRemove)
            {
                DestroyImmediate(component);
            }
        }

        private void AddEcsAuthoringComponents(GameObject instance, GameObject source,
            ItemTypeEnum itemType, ConversionResult result)
        {
            // Extract data from original OPSIVE prefab
            var extractedData = ExtractOpsiveData(source, itemType);

            // Add appropriate authoring component
            switch (itemType)
            {
                case ItemTypeEnum.Shootable:
                case ItemTypeEnum.Melee:
                case ItemTypeEnum.Throwable:
                case ItemTypeEnum.Shield:
                    var weaponAuthoring = instance.AddComponent<WeaponAuthoring>();
                    ApplyWeaponData(weaponAuthoring, extractedData, itemType);
                    result.AddedComponents.Add("WeaponAuthoring");
                    break;

                case ItemTypeEnum.Generic:
                case ItemTypeEnum.Magic:
                default:
                    // Add generic item authoring (you may need to create this)
                    result.AddedComponents.Add("(Generic item - manual setup needed)");
                    break;
            }
        }

        private ExtractedItemData ExtractOpsiveData(GameObject source, ItemTypeEnum itemType)
        {
            var data = new ExtractedItemData();

            var shootableAction = FindComponentByTypeName(source, "ShootableAction");
            var meleeAction = FindComponentByTypeName(source, "MeleeAction");
            var throwableAction = FindComponentByTypeName(source, "ThrowableAction");

            if (shootableAction != null)
            {
                // Extract shootable data (similar to OpsiveWeaponExtractorWindow)
                var clipModuleGroup = GetFieldValue(shootableAction, "m_ClipModuleGroup");
                if (clipModuleGroup != null)
                {
                    var clipModule = GetPropertyValue(clipModuleGroup, "FirstEnabledModule");
                    if (clipModule != null)
                    {
                        data.ClipSize = GetPropertyValueInt(clipModule, "ClipSize", 30);
                    }
                }

                var shooterModuleGroup = GetFieldValue(shootableAction, "m_ShooterModuleGroup");
                if (shooterModuleGroup != null)
                {
                    var shooter = GetPropertyValue(shooterModuleGroup, "FirstEnabledModule");
                    if (shooter != null)
                    {
                        data.UseHitscan = shooter.GetType().Name.Contains("Hitscan");
                        data.Spread = GetFieldValueFloat(shooter, "m_Spread", 0.01f);
                        data.Range = GetFieldValueFloat(shooter, "m_HitscanFireRange", 100f);
                        if (data.Range > 10000f) data.Range = 100f;
                    }
                }

                var impactModuleGroup = GetFieldValue(shootableAction, "m_ImpactModuleGroup");
                if (impactModuleGroup != null)
                {
                    var impact = GetPropertyValue(impactModuleGroup, "FirstEnabledModule");
                    if (impact != null)
                    {
                        data.Damage = GetFieldValueFloat(impact, "m_DamageAmount", 20f);
                    }
                }
            }

            if (meleeAction != null)
            {
                data.MeleeDamage = GetFieldValueFloat(meleeAction, "m_DamageAmount", 50f);
                data.MeleeRange = GetFieldValueFloat(meleeAction, "m_HitboxExtents", 2f);
            }

            if (throwableAction != null)
            {
                data.MinThrowForce = GetFieldValueFloat(throwableAction, "m_MinVelocity", 10f);
                data.MaxThrowForce = GetFieldValueFloat(throwableAction, "m_MaxVelocity", 30f);
            }

            return data;
        }

        private void ApplyWeaponData(WeaponAuthoring authoring, ExtractedItemData data, ItemTypeEnum itemType)
        {
            authoring.Type = itemType switch
            {
                ItemTypeEnum.Shootable => WeaponType.Shootable,
                ItemTypeEnum.Melee => WeaponType.Melee,
                ItemTypeEnum.Throwable => WeaponType.Throwable,
                ItemTypeEnum.Shield => WeaponType.Shield,
                _ => WeaponType.None
            };

            // Apply common values
            authoring.ClipSize = data.ClipSize;
            authoring.StartingAmmo = data.ClipSize;
            authoring.ReserveAmmo = data.ClipSize * 3;

            // Apply type-specific values
            switch (itemType)
            {
                case ItemTypeEnum.Shootable:
                    authoring.Damage = data.Damage;
                    authoring.Range = data.Range;
                    authoring.SpreadAngle = data.Spread;
                    authoring.UseHitscan = data.UseHitscan;
                    authoring.FireRate = data.FireRate > 0 ? data.FireRate : 10f;
                    break;

                case ItemTypeEnum.Melee:
                    authoring.MeleeDamage = data.MeleeDamage;
                    authoring.MeleeRange = data.MeleeRange;
                    break;

                case ItemTypeEnum.Throwable:
                    authoring.MinThrowForce = data.MinThrowForce;
                    authoring.MaxThrowForce = data.MaxThrowForce;
                    break;
            }
        }

        private void AddAnimationRelay(GameObject instance, ConversionResult result)
        {
            // Find the object with the Animator
            var animator = instance.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                var relayType = System.Type.GetType("DIG.Weapons.Animation.OpsiveWeaponAnimationEventRelay, Assembly-CSharp");
                if (relayType != null)
                {
                    if (animator.gameObject.GetComponent(relayType) == null)
                    {
                        animator.gameObject.AddComponent(relayType);
                        result.AddedComponents.Add("OpsiveWeaponAnimationEventRelay");
                    }
                }
                else
                {
                    result.Warnings.Add("OpsiveWeaponAnimationEventRelay type not found");
                }
            }
        }

        private void CleanupComponents(GameObject instance, ConversionResult result)
        {
            // Remove any orphaned components that reference removed OPSIVE types
            var allComponents = instance.GetComponentsInChildren<Component>(true);

            foreach (var component in allComponents)
            {
                if (component == null)
                {
                    // This indicates a missing script reference
                    result.Warnings.Add("Found missing script reference (cleaned up)");
                }
            }

            // Use Unity's built-in cleanup for missing scripts
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(instance);

            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            }
        }

        private void BatchConvertFolder()
        {
            var folderPath = EditorUtility.OpenFolderPanel("Select Folder with OPSIVE Prefabs", "Assets", "");
            if (string.IsNullOrEmpty(folderPath)) return;

            // Convert to relative path
            folderPath = GetProjectRelativePath(folderPath);

            var guids = AssetDatabase.FindAssets("t:GameObject", new[] { folderPath });
            int converted = 0;
            int failed = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab != null && HasAnyOpsiveComponent(prefab))
                {
                    var result = ConvertSinglePrefab(prefab);
                    if (result.Success)
                        converted++;
                    else
                        failed++;
                }
            }

            Debug.Log($"[PrefabConverter] Batch conversion complete. Converted: {converted}, Failed: {failed}");
        }

        private void BatchConvertSelected()
        {
            var selectedObjects = Selection.gameObjects;
            int converted = 0;
            int failed = 0;

            foreach (var go in selectedObjects)
            {
                if (PrefabUtility.IsPartOfPrefabAsset(go) && HasAnyOpsiveComponent(go))
                {
                    var result = ConvertSinglePrefab(go);
                    if (result.Success)
                        converted++;
                    else
                        failed++;
                }
            }

            Debug.Log($"[PrefabConverter] Batch conversion complete. Converted: {converted}, Failed: {failed}");
        }

        private void DrawConversionResult()
        {
            EditorGUILayout.LabelField("Last Conversion Result", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Source", _lastResult.SourceName);
            EditorGUILayout.LabelField("Type", _lastResult.DetectedType);
            EditorGUILayout.LabelField("Status", _lastResult.Success ? "Success" : "Failed");

            if (!_lastResult.Success)
            {
                EditorGUILayout.HelpBox(_lastResult.ErrorMessage, MessageType.Error);
            }

            if (_lastResult.RemovedComponents.Count > 0)
            {
                EditorGUILayout.LabelField($"Removed: {string.Join(", ", _lastResult.RemovedComponents)}");
            }

            if (_lastResult.AddedComponents.Count > 0)
            {
                EditorGUILayout.LabelField($"Added: {string.Join(", ", _lastResult.AddedComponents)}");
            }

            if (_lastResult.Warnings.Count > 0)
            {
                foreach (var warning in _lastResult.Warnings)
                {
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }
            }

            if (_lastResult.Success && _lastResult.ConvertedPrefab != null)
            {
                if (GUILayout.Button("Select Converted Prefab"))
                {
                    Selection.activeObject = _lastResult.ConvertedPrefab;
                    EditorGUIUtility.PingObject(_lastResult.ConvertedPrefab);
                }
            }

            EditorGUI.indentLevel--;
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        private void CreateFolderRecursive(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private bool HasComponentByName(GameObject go, string typeName)
        {
            return FindComponentByTypeName(go, typeName) != null;
        }

        private bool HasAnyOpsiveComponent(GameObject go)
        {
            foreach (var component in go.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                var ns = component.GetType().Namespace ?? "";
                if (ns.StartsWith("Opsive")) return true;
            }
            return false;
        }

        private Component FindComponentByTypeName(GameObject go, string typeName)
        {
            foreach (var component in go.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                if (component.GetType().Name == typeName)
                    return component;
            }
            return null;
        }

        private object GetFieldValue(object obj, string fieldName)
        {
            if (obj == null) return null;
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return field?.GetValue(obj);
        }

        private object GetPropertyValue(object obj, string propName)
        {
            if (obj == null) return null;
            var prop = obj.GetType().GetProperty(propName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return prop?.GetValue(obj);
        }

        private float GetFieldValueFloat(object obj, string fieldName, float defaultValue)
        {
            var value = GetFieldValue(obj, fieldName);
            if (value is float f) return f;
            if (value is double d) return (float)d;
            return defaultValue;
        }

        private int GetPropertyValueInt(object obj, string propName, int defaultValue)
        {
            var value = GetPropertyValue(obj, propName);
            if (value is int i) return i;
            return defaultValue;
        }

        private string GetProjectRelativePath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return "";
            
            absolutePath = absolutePath.Replace("\\", "/");
            if (absolutePath.StartsWith(Application.dataPath))
            {
                return "Assets" + absolutePath.Substring(Application.dataPath.Length);
            }
                
            var projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath)?.Replace("\\", "/");
            if (projectRoot != null && absolutePath.StartsWith(projectRoot))
            {
                return absolutePath.Substring(projectRoot.Length + 1);
            }
                
            return absolutePath;
        }

        // ============================================
        // DATA STRUCTURES
        // ============================================

        private enum ItemTypeEnum
        {
            Generic,
            Shootable,
            Melee,
            Throwable,
            Shield,
            Magic
        }

        private class ExtractedItemData
        {
            public int ClipSize = 30;
            public float FireRate = 10f;
            public float Damage = 20f;
            public float Range = 100f;
            public float Spread = 2f;
            public bool UseHitscan = true;

            public float MeleeDamage = 50f;
            public float MeleeRange = 2f;

            public float MinThrowForce = 10f;
            public float MaxThrowForce = 30f;

            // Animator Item ID from Opsive's CharacterItem.m_AnimatorItemID
            public int AnimatorItemID = 1;
        }

        private class ConversionResult
        {
            public bool Success;
            public string SourceName;
            public string DetectedType;
            public string OutputPath;
            public string ErrorMessage;
            public GameObject ConvertedPrefab;
            public List<string> RemovedComponents = new List<string>();
            public List<string> AddedComponents = new List<string>();
            public List<string> Warnings = new List<string>();
        }

        /// <summary>
        /// Scans the entire project for Opsive weapon prefabs and converts any missing ones.
        /// </summary>
        private void ConvertAllOpsiveWeapons()
        {
            // Find all GameObjects in the project
            var guids = AssetDatabase.FindAssets("t:GameObject");
            int converted = 0;
            int skipped = 0;
            int failed = 0;

            EditorUtility.DisplayProgressBar("Converting Opsive Weapons", "Scanning...", 0f);

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    
                    // Skip if not in OPSIVE folder
                    if (!path.Contains("OPSIVE")) continue;
                    
                    // Skip if not a prefab
                    if (!path.EndsWith(".prefab")) continue;
                    
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null) continue;

                    // Check if it has Opsive weapon components
                    bool hasShootable = HasOpsiveComponent(go, "ShootableAction");
                    bool hasMelee = HasOpsiveComponent(go, "MeleeAction");
                    bool hasThrowable = HasOpsiveComponent(go, "ThrowableAction");

                    if (!hasShootable && !hasMelee && !hasThrowable)
                        continue;

                    // Check if already converted
                    string outputName = go.name + "_ECS.prefab";
                    string outputPath = System.IO.Path.Combine(_outputFolder, outputName);
                    
                    if (System.IO.File.Exists(outputPath))
                    {
                        skipped++;
                        continue;
                    }

                    EditorUtility.DisplayProgressBar("Converting Opsive Weapons", $"Converting {go.name}...", (float)i / guids.Length);

                    var result = ConvertSinglePrefab(go);
                    if (result.Success)
                    {
                        converted++;
                        Debug.Log($"[PrefabConverter] Converted: {result.OutputPath}");
                    }
                    else
                    {
                        failed++;
                        Debug.LogWarning($"[PrefabConverter] Failed to convert {go.name}: {result.ErrorMessage}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            Debug.Log($"[PrefabConverter] Batch complete! Converted: {converted}, Skipped (already exists): {skipped}, Failed: {failed}");
            EditorUtility.DisplayDialog("Conversion Complete",
                $"Converted: {converted}\nSkipped (already exists): {skipped}\nFailed: {failed}",
                "OK");
        }

        private bool HasOpsiveComponent(GameObject go, string componentName)
        {
            var components = go.GetComponentsInChildren<Component>(true);
            foreach (var comp in components)
            {
                if (comp == null) continue;
                if (comp.GetType().Name == componentName)
                    return true;
            }
            return false;
        }
    }
}
