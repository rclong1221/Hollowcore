using UnityEngine;
using UnityEditor;
using DIG.Weapons.Data;

namespace DIG.Editor.WeaponTools
{
    /// <summary>
    /// Custom inspector for WeaponTemplateAsset.
    /// </summary>
    [CustomEditor(typeof(WeaponTemplateAsset))]
    public class WeaponTemplateAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var template = (WeaponTemplateAsset)target;

            // Category change detection
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            // Apply defaults button
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Category Defaults"))
            {
                Undo.RecordObject(template, "Apply Category Defaults");
                template.ApplyCategoryDefaults();
                EditorUtility.SetDirty(template);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Summary preview
            EditorGUILayout.LabelField("Summary Preview:", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(template.GetSummary(), MessageType.None);
        }
    }
}
