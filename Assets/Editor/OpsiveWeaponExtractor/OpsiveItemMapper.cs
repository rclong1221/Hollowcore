using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Reflection;
using DIG.Items;

namespace DIG.Editor.OpsiveExtractor
{
    /// <summary>
    /// Maps OPSIVE ItemType/ItemDefinition ScriptableObjects to DIG ECS ItemDefinition components.
    ///
    /// OPSIVE uses ScriptableObject-based item definitions stored as assets.
    /// DIG uses IComponentData structs baked at edit time.
    ///
    /// This tool:
    /// 1. Scans for OPSIVE ItemType/ItemDefinitionBase assets
    /// 2. Extracts relevant metadata (name, category, stack settings)
    /// 3. Creates a mapping registry for runtime reference
    /// 4. Generates ECS-compatible item definitions
    /// </summary>
    public class OpsiveItemMapper : EditorWindow
    {
        private Vector2 _scrollPosition;
        private List<OpsiveItemEntry> _discoveredItems = new List<OpsiveItemEntry>();
        private bool _showDiscoveredItems = true;

        // Mapping registry that can be saved as an asset
        private OpsiveItemMappingRegistry _registry;

        [MenuItem("Tools/DIG/OPSIVE Item Mapper")]
        public static void ShowWindow()
        {
            var window = GetWindow<OpsiveItemMapper>("OPSIVE Item Mapper");
            window.minSize = new Vector2(500, 400);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("OPSIVE → ECS Item Mapper", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Scans for OPSIVE ItemType/ItemDefinition assets and creates a mapping registry for ECS items.\n\n" +
                "The registry maps OPSIVE item IDs to ECS ItemTypeId values.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Registry field
            _registry = (OpsiveItemMappingRegistry)EditorGUILayout.ObjectField(
                "Mapping Registry",
                _registry,
                typeof(OpsiveItemMappingRegistry),
                false);

            EditorGUILayout.Space(5);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create New Registry"))
                {
                    CreateNewRegistry();
                }

                using (new EditorGUI.DisabledScope(_registry == null))
                {
                    if (GUILayout.Button("Load Existing"))
                    {
                        LoadRegistry();
                    }
                }
            }

            EditorGUILayout.Space(10);

            // Scan buttons
            if (GUILayout.Button("Scan for OPSIVE Items", GUILayout.Height(30)))
            {
                ScanForOpsiveItems();
            }

            EditorGUILayout.Space(10);

            // Display discovered items
            if (_discoveredItems.Count > 0)
            {
                _showDiscoveredItems = EditorGUILayout.Foldout(_showDiscoveredItems,
                    $"Discovered Items ({_discoveredItems.Count})", true);

                if (_showDiscoveredItems)
                {
                    _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(250));
                    DrawDiscoveredItems();
                    EditorGUILayout.EndScrollView();
                }

                EditorGUILayout.Space(10);

                using (new EditorGUI.DisabledScope(_registry == null))
                {
                    if (GUILayout.Button("Add All to Registry", GUILayout.Height(30)))
                    {
                        AddAllToRegistry();
                    }

                    if (GUILayout.Button("Generate ECS Item Definitions", GUILayout.Height(30)))
                    {
                        GenerateEcsItemDefinitions();
                    }
                }
            }
        }

        private void CreateNewRegistry()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Item Mapping Registry",
                "OpsiveItemMappingRegistry",
                "asset",
                "Choose a location for the item mapping registry");

            if (string.IsNullOrEmpty(path)) return;

            _registry = ScriptableObject.CreateInstance<OpsiveItemMappingRegistry>();
            AssetDatabase.CreateAsset(_registry, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[OpsiveItemMapper] Created registry at {path}");
        }

        private void LoadRegistry()
        {
            if (_registry == null) return;

            // Registry is already loaded via ObjectField
            Debug.Log($"[OpsiveItemMapper] Loaded registry with {_registry.Mappings.Count} entries");
        }

        private void ScanForOpsiveItems()
        {
            _discoveredItems.Clear();

            // Find all ScriptableObjects that might be OPSIVE item definitions
            var guids = AssetDatabase.FindAssets("t:ScriptableObject");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                if (asset == null) continue;

                var type = asset.GetType();

                // Check if it's an OPSIVE ItemDefinitionBase or ItemType
                if (IsOpsiveItemType(type))
                {
                    var entry = ExtractItemData(asset, path);
                    if (entry != null)
                    {
                        _discoveredItems.Add(entry);
                    }
                }
            }

            Debug.Log($"[OpsiveItemMapper] Found {_discoveredItems.Count} OPSIVE item definitions");
        }

        private bool IsOpsiveItemType(Type type)
        {
            // Check type hierarchy for OPSIVE item types
            var currentType = type;
            while (currentType != null)
            {
                var typeName = currentType.Name;
                if (typeName == "ItemDefinitionBase" ||
                    typeName == "ItemType" ||
                    typeName.Contains("ItemDefinition"))
                {
                    // Verify it's in Opsive namespace
                    if (currentType.Namespace != null &&
                        currentType.Namespace.StartsWith("Opsive"))
                    {
                        return true;
                    }
                }
                currentType = currentType.BaseType;
            }
            return false;
        }

        private OpsiveItemEntry ExtractItemData(ScriptableObject asset, string path)
        {
            var entry = new OpsiveItemEntry
            {
                AssetPath = path,
                Asset = asset,
                OpsiveName = asset.name
            };

            var type = asset.GetType();

            // Try to get common properties via reflection
            entry.DisplayName = GetStringProperty(asset, "name") ?? asset.name;

            // Get unique ID if available
            var idProp = type.GetProperty("ID",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (idProp != null)
            {
                var id = idProp.GetValue(asset);
                if (id is uint uid)
                    entry.OpsiveId = (int)uid;
                else if (id is int iid)
                    entry.OpsiveId = iid;
            }

            // Try to determine category
            entry.Category = DetermineCategory(asset, type);

            // Get stack settings
            entry.IsStackable = GetBoolProperty(asset, "IsStackable") ??
                               GetBoolProperty(asset, "m_Stackable") ?? false;
            entry.MaxStack = GetIntProperty(asset, "MaxStack") ??
                            GetIntProperty(asset, "m_MaxStack") ?? 1;

            // Generate ECS type ID (hash of name for consistency)
            entry.EcsItemTypeId = entry.OpsiveName.GetHashCode();

            return entry;
        }

        private ItemCategory DetermineCategory(ScriptableObject asset, Type type)
        {
            // Check type name for hints
            var typeName = type.Name.ToLower();

            if (typeName.Contains("weapon") || typeName.Contains("gun") ||
                typeName.Contains("shootable") || typeName.Contains("melee"))
                return ItemCategory.Weapon;

            if (typeName.Contains("ammo") || typeName.Contains("bullet") ||
                typeName.Contains("clip") || typeName.Contains("magazine"))
                return ItemCategory.Ammo;

            if (typeName.Contains("consumable") || typeName.Contains("health") ||
                typeName.Contains("potion"))
                return ItemCategory.Consumable;

            if (typeName.Contains("armor") || typeName.Contains("helmet") ||
                typeName.Contains("equipment"))
                return ItemCategory.Equipment;

            // Check Category property if exists
            var categoryProp = GetPropertyValue(asset, "Category");
            if (categoryProp != null)
            {
                var catName = categoryProp.ToString().ToLower();
                if (catName.Contains("weapon")) return ItemCategory.Weapon;
                if (catName.Contains("ammo")) return ItemCategory.Ammo;
                if (catName.Contains("consumable")) return ItemCategory.Consumable;
                if (catName.Contains("equipment")) return ItemCategory.Equipment;
            }

            // Default based on asset name
            var assetName = asset.name.ToLower();
            if (assetName.Contains("rifle") || assetName.Contains("pistol") ||
                assetName.Contains("sword") || assetName.Contains("gun"))
                return ItemCategory.Weapon;

            return ItemCategory.None;
        }

        private void DrawDiscoveredItems()
        {
            EditorGUI.indentLevel++;

            foreach (var item in _discoveredItems)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(item.OpsiveName, GUILayout.Width(150));
                    EditorGUILayout.LabelField(item.Category.ToString(), GUILayout.Width(80));
                    EditorGUILayout.LabelField($"ID: {item.EcsItemTypeId}", GUILayout.Width(120));

                    if (GUILayout.Button("Select", GUILayout.Width(50)))
                    {
                        Selection.activeObject = item.Asset;
                        EditorGUIUtility.PingObject(item.Asset);
                    }
                }
            }

            EditorGUI.indentLevel--;
        }

        private void AddAllToRegistry()
        {
            if (_registry == null)
            {
                Debug.LogWarning("[OpsiveItemMapper] No registry selected");
                return;
            }

            Undo.RecordObject(_registry, "Add Items to Registry");

            foreach (var item in _discoveredItems)
            {
                // Check if already in registry
                var existing = _registry.Mappings.Find(m => m.OpsiveName == item.OpsiveName);
                if (existing == null)
                {
                    _registry.Mappings.Add(new OpsiveItemMapping
                    {
                        OpsiveName = item.OpsiveName,
                        OpsiveId = item.OpsiveId,
                        EcsItemTypeId = item.EcsItemTypeId,
                        Category = item.Category,
                        DisplayName = item.DisplayName,
                        IsStackable = item.IsStackable,
                        MaxStack = item.MaxStack,
                        AssetPath = item.AssetPath
                    });
                }
            }

            EditorUtility.SetDirty(_registry);
            AssetDatabase.SaveAssets();

            Debug.Log($"[OpsiveItemMapper] Added {_discoveredItems.Count} items to registry");
        }

        private void GenerateEcsItemDefinitions()
        {
            if (_registry == null || _registry.Mappings.Count == 0)
            {
                Debug.LogWarning("[OpsiveItemMapper] No items in registry");
                return;
            }

            // Generate a static class with item type constants
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("// Auto-generated by OpsiveItemMapper");
            sb.AppendLine("// DO NOT EDIT MANUALLY");
            sb.AppendLine();
            sb.AppendLine("namespace DIG.Items");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Item type IDs mapped from OPSIVE item definitions.");
            sb.AppendLine("    /// Use these constants when creating or querying items.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class ItemTypeIds");
            sb.AppendLine("    {");

            foreach (var mapping in _registry.Mappings)
            {
                var safeName = SanitizeIdentifier(mapping.OpsiveName);
                sb.AppendLine($"        /// <summary>{mapping.DisplayName} ({mapping.Category})</summary>");
                sb.AppendLine($"        public const int {safeName} = {mapping.EcsItemTypeId};");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            // Write to file
            var path = "Assets/Scripts/Items/Generated/ItemTypeIds.cs";
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            System.IO.File.WriteAllText(path, sb.ToString());
            AssetDatabase.Refresh();

            Debug.Log($"[OpsiveItemMapper] Generated ItemTypeIds.cs with {_registry.Mappings.Count} entries");
        }

        private string SanitizeIdentifier(string name)
        {
            // Convert to valid C# identifier
            var result = new System.Text.StringBuilder();

            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c))
                    result.Append(c);
                else if (c == ' ' || c == '-' || c == '_')
                    result.Append('_');
            }

            // Ensure doesn't start with digit
            var str = result.ToString();
            if (str.Length > 0 && char.IsDigit(str[0]))
                str = "_" + str;

            return str;
        }

        // ============================================
        // REFLECTION HELPERS
        // ============================================

        private string GetStringProperty(object obj, string propName)
        {
            var type = obj.GetType();
            var prop = type.GetProperty(propName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var value = prop?.GetValue(obj);
            return value?.ToString();
        }

        private bool? GetBoolProperty(object obj, string propName)
        {
            var type = obj.GetType();

            // Try property first
            var prop = type.GetProperty(propName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null)
            {
                var value = prop.GetValue(obj);
                if (value is bool b) return b;
            }

            // Try field
            var field = type.GetField(propName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                var value = field.GetValue(obj);
                if (value is bool b) return b;
            }

            return null;
        }

        private int? GetIntProperty(object obj, string propName)
        {
            var type = obj.GetType();

            var prop = type.GetProperty(propName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null)
            {
                var value = prop.GetValue(obj);
                if (value is int i) return i;
            }

            var field = type.GetField(propName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                var value = field.GetValue(obj);
                if (value is int i) return i;
            }

            return null;
        }

        private object GetPropertyValue(object obj, string propName)
        {
            var type = obj.GetType();
            var prop = type.GetProperty(propName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return prop?.GetValue(obj);
        }

        // ============================================
        // DATA STRUCTURES
        // ============================================

        private class OpsiveItemEntry
        {
            public string AssetPath;
            public ScriptableObject Asset;
            public string OpsiveName;
            public string DisplayName;
            public int OpsiveId;
            public int EcsItemTypeId;
            public ItemCategory Category;
            public bool IsStackable;
            public int MaxStack;
        }
    }

    /// <summary>
    /// A single mapping entry from OPSIVE item to ECS item.
    /// </summary>
    [Serializable]
    public class OpsiveItemMapping
    {
        public string OpsiveName;
        public int OpsiveId;
        public int EcsItemTypeId;
        public ItemCategory Category;
        public string DisplayName;
        public bool IsStackable;
        public int MaxStack;
        public string AssetPath;
    }

    /// <summary>
    /// ScriptableObject registry storing all OPSIVE to ECS item mappings.
    /// Save this as an asset to persist mappings between sessions.
    /// </summary>
    [CreateAssetMenu(fileName = "OpsiveItemMappingRegistry", menuName = "DIG/OPSIVE Item Mapping Registry")]
    public class OpsiveItemMappingRegistry : ScriptableObject
    {
        public List<OpsiveItemMapping> Mappings = new List<OpsiveItemMapping>();

        /// <summary>
        /// Get ECS item type ID from OPSIVE item name.
        /// </summary>
        public int GetEcsItemTypeId(string opsiveName)
        {
            var mapping = Mappings.Find(m => m.OpsiveName == opsiveName);
            return mapping?.EcsItemTypeId ?? 0;
        }

        /// <summary>
        /// Get mapping by OPSIVE ID.
        /// </summary>
        public OpsiveItemMapping GetByOpsiveId(int opsiveId)
        {
            return Mappings.Find(m => m.OpsiveId == opsiveId);
        }

        /// <summary>
        /// Get mapping by ECS item type ID.
        /// </summary>
        public OpsiveItemMapping GetByEcsTypeId(int ecsTypeId)
        {
            return Mappings.Find(m => m.EcsItemTypeId == ecsTypeId);
        }
    }
}
