using UnityEngine;
using UnityEditor;
using DIG.Weapons.Authoring;
using DIG.Weapons.Data;

namespace DIG.Editor.WeaponTools
{
    /// <summary>
    /// Custom inspector for WeaponAuthoring that adds template support.
    /// </summary>
    [CustomEditor(typeof(WeaponAuthoring))]
    public class WeaponAuthoringEditor : UnityEditor.Editor
    {
        private WeaponTemplateAsset selectedTemplate;
        private bool showTemplateSection = true;

        public override void OnInspectorGUI()
        {
            // Template section at top
            DrawTemplateSection();

            EditorGUILayout.Space(10);

            // Default inspector
            DrawDefaultInspector();
        }

        private void DrawTemplateSection()
        {
            var authoring = (WeaponAuthoring)target;

            showTemplateSection = EditorGUILayout.BeginFoldoutHeaderGroup(showTemplateSection, "Template Quick Setup");

            if (showTemplateSection)
            {
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.HelpBox(
                    "Select a template to quickly configure this weapon with preset values.",
                    MessageType.Info);

                EditorGUILayout.Space(5);

                // Template field
                selectedTemplate = (WeaponTemplateAsset)EditorGUILayout.ObjectField(
                    "Template",
                    selectedTemplate,
                    typeof(WeaponTemplateAsset),
                    false);

                // Quick template buttons
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Quick Templates:", EditorStyles.miniLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Pistol"))
                {
                    ApplyQuickTemplate(WeaponCategory.Pistol, authoring);
                }
                if (GUILayout.Button("Rifle"))
                {
                    ApplyQuickTemplate(WeaponCategory.Rifle, authoring);
                }
                if (GUILayout.Button("SMG"))
                {
                    ApplyQuickTemplate(WeaponCategory.SMG, authoring);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Shotgun"))
                {
                    ApplyQuickTemplate(WeaponCategory.Shotgun, authoring);
                }
                if (GUILayout.Button("Sniper"))
                {
                    ApplyQuickTemplate(WeaponCategory.Sniper, authoring);
                }
                if (GUILayout.Button("LMG"))
                {
                    ApplyQuickTemplate(WeaponCategory.LMG, authoring);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                // Apply selected template
                EditorGUI.BeginDisabledGroup(selectedTemplate == null);
                GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                if (GUILayout.Button("Apply Selected Template", GUILayout.Height(25)))
                {
                    ApplyTemplate(selectedTemplate, authoring);
                }
                GUI.backgroundColor = Color.white;
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void ApplyQuickTemplate(WeaponCategory category, WeaponAuthoring authoring)
        {
            Undo.RecordObject(authoring, $"Apply {category} Template");

            // Create temporary template with category defaults
            var tempTemplate = ScriptableObject.CreateInstance<WeaponTemplateAsset>();
            tempTemplate.Category = category;
            tempTemplate.ApplyCategoryDefaults();

            ApplyTemplate(tempTemplate, authoring);

            DestroyImmediate(tempTemplate);
        }

        private void ApplyTemplate(WeaponTemplateAsset template, WeaponAuthoring authoring)
        {
            if (template == null) return;

            Undo.RecordObject(authoring, $"Apply Template: {template.TemplateName}");

            // Apply values
            authoring.Type = WeaponType.Shootable;
            authoring.ClipSize = template.ClipSize;
            authoring.StartingAmmo = template.ClipSize;
            authoring.ReserveAmmo = template.StartingReserveAmmo;
            authoring.FireRate = template.FireRate;
            authoring.Damage = template.BaseDamage;
            authoring.SpreadAngle = template.BaseSpread;
            authoring.RecoilAmount = template.VerticalRecoil;
            authoring.ReloadTime = template.ReloadTime;
            authoring.Range = template.EffectiveRange;
            authoring.IsAutomatic = template.FireMode == FireMode.Automatic;

            EditorUtility.SetDirty(authoring);

            Debug.Log($"[WeaponAuthoring] Applied template: {template.TemplateName}\n" +
                      $"Clip: {template.ClipSize}, FireRate: {template.FireRate}, Damage: {template.BaseDamage}");
        }
    }
}
