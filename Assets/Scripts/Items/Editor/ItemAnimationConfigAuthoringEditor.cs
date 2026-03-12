using UnityEditor;
using UnityEngine;
using DIG.Items.Authoring;
using DIG.Items.Definitions;

namespace DIG.Items.Editor
{
    /// <summary>
    /// Custom editor for ItemAnimationConfigAuthoring.
    /// Provides migration UI from deprecated AnimationWeaponType to WeaponCategoryDefinition.
    /// </summary>
    [CustomEditor(typeof(ItemAnimationConfigAuthoring))]
    public class ItemAnimationConfigAuthoringEditor : UnityEditor.Editor
    {
        private SerializedProperty _categoryProp;
        private SerializedProperty _weaponTypeProp;

        private void OnEnable()
        {
            _categoryProp = serializedObject.FindProperty("Category");
            _weaponTypeProp = serializedObject.FindProperty("WeaponType");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            serializedObject.Update();
            
            // Migration UI - check if Category is null and WeaponType is not None (enum value 0)
            bool needsMigration = _categoryProp.objectReferenceValue == null && _weaponTypeProp.enumValueIndex != 0;
            
            if (needsMigration)
            {
                var script = (ItemAnimationConfigAuthoring)target;
                string legacyTypeName = _weaponTypeProp.enumNames[_weaponTypeProp.enumValueIndex];
                
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox($"Legacy WeaponType '{legacyTypeName}' detected. Migrate to 'Category' Asset?", MessageType.Warning);
                
                string categoryName = GetMigrationCategoryName(script.AnimatorItemID, legacyTypeName);
                if (GUILayout.Button($"Migrate to '{categoryName}.asset'"))
                {
                    Migrate(script, categoryName);
                }
            }
        }
        
        private void Migrate(ItemAnimationConfigAuthoring script, string categoryName)
        {
            Debug.Log($"[Migration] Mapping Legacy Type (ID:{script.AnimatorItemID}) to Category '{categoryName}'");
            
            // Find asset
            string[] guids = AssetDatabase.FindAssets($"t:WeaponCategoryDefinition {categoryName}");
            if (guids.Length == 0)
            {
                Debug.LogError($"[ItemAnimationConfigAuthoringEditor] Could not find WeaponCategoryDefinition named '{categoryName}'. Please create it first.");
                return;
            }
            
            // Prefer exact match
            string path = null;
            foreach (var guid in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(p).Equals(categoryName, System.StringComparison.OrdinalIgnoreCase))
                {
                    path = p;
                    break;
                }
            }
            
            if (path == null) path = AssetDatabase.GUIDToAssetPath(guids[0]); // Fallback
            
            var category = AssetDatabase.LoadAssetAtPath<WeaponCategoryDefinition>(path);
            if (category != null)
            {
                Undo.RecordObject(script, "Migrate Weapon Category");
                script.Category = category;
                EditorUtility.SetDirty(script);
                Debug.Log($"[Migration] Successfully assigned Category '{category.name}' to '{script.name}'");
            }
        }

        private string GetMigrationCategoryName(int animatorItemID, string legacyTypeName)
        {
            // Special overrides based on AnimatorItemID
            if (animatorItemID == 41) return "Grenade";
            if (animatorItemID == 24) return "Katana";
            if (animatorItemID == 23) return "Knife";
            if (animatorItemID == 22) return "Rifle";
            if (animatorItemID == 1) return "Pistol";
            if (animatorItemID == 4) return "Bow";
            
            // Default to legacy enum name
            return legacyTypeName;
        }
    }
}
