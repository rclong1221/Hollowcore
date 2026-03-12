using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using DIG.Weapons.Data;
using DIG.Weapons.Authoring;

namespace DIG.Editor.EquipmentWorkstation.Modules
{
    /// <summary>
    /// Weapon Templates module for Equipment Workstation.
    /// Manages weapon template assets and applies them to prefabs.
    /// </summary>
    public class WeaponTemplatesModule : IEquipmentModule
    {
        private const string TemplateFolder = "Assets/Data/WeaponTemplates";
        
        private WeaponTemplateAsset _selectedTemplate;
        private GameObject _targetPrefab;
        private Vector2 _scrollPosition;
        private List<WeaponTemplateAsset> _availableTemplates = new List<WeaponTemplateAsset>();

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Weapon Templates", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Create and manage weapon templates. Apply templates to quickly configure new weapons.", MessageType.Info);
            EditorGUILayout.Space(10);

            // Refresh templates - only on explicit button click, not auto-load
            if (GUILayout.Button("Refresh Templates", EditorStyles.miniButton))
            {
                LoadTemplates();
            }

            EditorGUILayout.Space(5);

            // Create default templates button
            if (GUILayout.Button("Create Default Templates", GUILayout.Height(25)))
            {
                CreateDefaultTemplates();
                LoadTemplates();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Available Templates", EditorStyles.boldLabel);

            // Template list
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, EditorStyles.helpBox, GUILayout.Height(150));

            if (_availableTemplates.Count == 0)
            {
                EditorGUILayout.HelpBox("No templates found. Click 'Create Default Templates' to generate them.", MessageType.Warning);
            }
            else
            {
                foreach (var template in _availableTemplates)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    bool isSelected = _selectedTemplate == template;
                    GUI.backgroundColor = isSelected ? Color.cyan : Color.white;
                    
                    if (GUILayout.Button(template.TemplateName, GUILayout.Height(25)))
                    {
                        _selectedTemplate = template;
                    }
                    
                    GUI.backgroundColor = Color.white;
                    
                    if (GUILayout.Button("Edit", EditorStyles.miniButton, GUILayout.Width(40)))
                    {
                        Selection.activeObject = template;
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // Selected template preview
            if (_selectedTemplate != null)
            {
                EditorGUILayout.LabelField("Selected Template Preview", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(_selectedTemplate.GetSummary(), MessageType.None);
            }

            EditorGUILayout.Space(10);

            // Apply to prefab section
            EditorGUILayout.LabelField("Apply Template to Prefab", EditorStyles.boldLabel);
            
            _targetPrefab = (GameObject)EditorGUILayout.ObjectField("Target Weapon", _targetPrefab, typeof(GameObject), true);

            EditorGUI.BeginDisabledGroup(_selectedTemplate == null || _targetPrefab == null);
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Apply Template", GUILayout.Height(30)))
            {
                ApplyTemplateToTarget();
            }
            GUI.backgroundColor = Color.white;
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(10);

            // Quick apply buttons
            EditorGUILayout.LabelField("Quick Apply (to selected prefab)", EditorStyles.boldLabel);
            
            if (_targetPrefab != null)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Pistol")) ApplyQuickTemplate(WeaponCategory.Pistol);
                if (GUILayout.Button("Rifle")) ApplyQuickTemplate(WeaponCategory.Rifle);
                if (GUILayout.Button("SMG")) ApplyQuickTemplate(WeaponCategory.SMG);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Shotgun")) ApplyQuickTemplate(WeaponCategory.Shotgun);
                if (GUILayout.Button("Sniper")) ApplyQuickTemplate(WeaponCategory.Sniper);
                if (GUILayout.Button("LMG")) ApplyQuickTemplate(WeaponCategory.LMG);
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("Select a target prefab to use quick apply.", MessageType.Info);
            }
        }

        private void LoadTemplates()
        {
            _availableTemplates.Clear();
            
            string[] guids = AssetDatabase.FindAssets("t:WeaponTemplateAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var template = AssetDatabase.LoadAssetAtPath<WeaponTemplateAsset>(path);
                if (template != null)
                {
                    _availableTemplates.Add(template);
                }
            }

            _availableTemplates = _availableTemplates.OrderBy(t => t.Category).ToList();
            
            Debug.Log($"[WeaponTemplates] Loaded {_availableTemplates.Count} templates");
        }

        private void CreateDefaultTemplates()
        {
            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder(TemplateFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Data"))
                {
                    AssetDatabase.CreateFolder("Assets", "Data");
                }
                AssetDatabase.CreateFolder("Assets/Data", "WeaponTemplates");
            }

            CreateTemplateIfNotExists(WeaponCategory.Pistol);
            CreateTemplateIfNotExists(WeaponCategory.Rifle);
            CreateTemplateIfNotExists(WeaponCategory.SMG);
            CreateTemplateIfNotExists(WeaponCategory.Shotgun);
            CreateTemplateIfNotExists(WeaponCategory.Sniper);
            CreateTemplateIfNotExists(WeaponCategory.LMG);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[WeaponTemplates] Default templates created in {TemplateFolder}");
        }

        private void CreateTemplateIfNotExists(WeaponCategory category)
        {
            string assetPath = $"{TemplateFolder}/{category}Template.asset";

            if (AssetDatabase.LoadAssetAtPath<WeaponTemplateAsset>(assetPath) != null)
                return;

            var template = ScriptableObject.CreateInstance<WeaponTemplateAsset>();
            template.Category = category;
            template.ApplyCategoryDefaults();

            AssetDatabase.CreateAsset(template, assetPath);
        }

        private void ApplyTemplateToTarget()
        {
            if (_selectedTemplate == null || _targetPrefab == null) return;

            var authoring = _targetPrefab.GetComponent<WeaponAuthoring>();
            if (authoring == null)
            {
                authoring = _targetPrefab.AddComponent<WeaponAuthoring>();
            }

            ApplyTemplate(_selectedTemplate, authoring);
        }

        private void ApplyQuickTemplate(WeaponCategory category)
        {
            if (_targetPrefab == null) return;

            var authoring = _targetPrefab.GetComponent<WeaponAuthoring>();
            if (authoring == null)
            {
                authoring = _targetPrefab.AddComponent<WeaponAuthoring>();
            }

            // Create temporary template
            var tempTemplate = ScriptableObject.CreateInstance<WeaponTemplateAsset>();
            tempTemplate.Category = category;
            tempTemplate.ApplyCategoryDefaults();

            ApplyTemplate(tempTemplate, authoring);

            Object.DestroyImmediate(tempTemplate);
        }

        private void ApplyTemplate(WeaponTemplateAsset template, WeaponAuthoring authoring)
        {
            Undo.RecordObject(authoring, $"Apply Template: {template.TemplateName}");

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

            Debug.Log($"[WeaponTemplates] Applied '{template.TemplateName}' to {authoring.gameObject.name}");
        }
    }
}
