using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Reflection;
using DIG.Weapons.Authoring;

namespace DIG.Editor.OpsiveExtractor
{
    /// <summary>
    /// Editor window to extract weapon configuration from OPSIVE ShootableAction prefabs
    /// and apply them to DIG WeaponAuthoring components.
    ///
    /// Usage:
    /// 1. Open via Tools > DIG > OPSIVE Weapon Extractor
    /// 2. Drag OPSIVE weapon prefab to "Source OPSIVE Weapon"
    /// 3. Drag your ECS weapon prefab to "Target ECS Weapon"
    /// 4. Click "Extract & Apply" to copy configuration
    ///
    /// This tool uses reflection to access OPSIVE properties without requiring
    /// direct assembly references, making it work even if OPSIVE code is in a separate assembly.
    /// </summary>
    public class OpsiveWeaponExtractorWindow : EditorWindow
    {
        private GameObject _sourceOpsiveWeapon;
        private GameObject _targetEcsWeapon;
        private Vector2 _scrollPosition;
        private bool _showExtractedValues = true;
        
        // Scan results
        private List<GameObject> _foundWeapons = new List<GameObject>();
        private bool _showScanResults = true;

        // Extracted values cache
        private ExtractedWeaponData _extractedData;

        [MenuItem("Tools/DIG/OPSIVE Weapon Extractor")]
        public static void ShowWindow()
        {
            var window = GetWindow<OpsiveWeaponExtractorWindow>("OPSIVE Extractor");
            window.minSize = new Vector2(400, 500);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("OPSIVE → ECS Weapon Extractor", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Extract weapon configuration from OPSIVE ShootableAction prefabs and apply to DIG WeaponAuthoring components.\n\n" +
                "This copies DATA only - no runtime dependencies on OPSIVE.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Source and Target fields
            EditorGUI.BeginChangeCheck();
            _sourceOpsiveWeapon = (GameObject)EditorGUILayout.ObjectField(
                "Source OPSIVE Weapon",
                _sourceOpsiveWeapon,
                typeof(GameObject),
                false);

            if (EditorGUI.EndChangeCheck() && _sourceOpsiveWeapon != null)
            {
                ExtractFromOpsive();
            }

            _targetEcsWeapon = (GameObject)EditorGUILayout.ObjectField(
                "Target ECS Weapon",
                _targetEcsWeapon,
                typeof(GameObject),
                false);

            EditorGUILayout.Space(10);

            // Extract button
            using (new EditorGUI.DisabledScope(_sourceOpsiveWeapon == null))
            {
                if (GUILayout.Button("Extract from OPSIVE", GUILayout.Height(30)))
                {
                    ExtractFromOpsive();
                }
            }

            // Show extracted values
            if (_extractedData != null)
            {
                EditorGUILayout.Space(10);
                _showExtractedValues = EditorGUILayout.Foldout(_showExtractedValues, "Extracted Values", true);

                if (_showExtractedValues)
                {
                    _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(250));
                    DrawExtractedValues();
                    EditorGUILayout.EndScrollView();
                }

                EditorGUILayout.Space(10);

                // Apply button
                using (new EditorGUI.DisabledScope(_targetEcsWeapon == null))
                {
                    if (GUILayout.Button("Apply to ECS Weapon", GUILayout.Height(30)))
                    {
                        ApplyToEcsWeapon();
                    }
                }

                // Combined extract and apply
                using (new EditorGUI.DisabledScope(_sourceOpsiveWeapon == null || _targetEcsWeapon == null))
                {
                    if (GUILayout.Button("Extract & Apply", GUILayout.Height(30)))
                    {
                        ExtractFromOpsive();
                        ApplyToEcsWeapon();
                    }
                }
            }

            EditorGUILayout.Space(20);

            // Batch processing section
            EditorGUILayout.LabelField("Batch Processing", EditorStyles.boldLabel);
            if (GUILayout.Button("Scan Project for OPSIVE Weapons"))
            {
                ScanForOpsiveWeapons();
            }

            if (_foundWeapons.Count > 0)
            {
                EditorGUILayout.Space(10);
                _showScanResults = EditorGUILayout.Foldout(_showScanResults, $"Scan Results ({_foundWeapons.Count})", true);

                if (_showScanResults)
                {
                    var scanScroll = EditorGUILayout.BeginScrollView(Vector2.zero, GUILayout.Height(200));
                    EditorGUI.indentLevel++;
                    
                    foreach (var weapon in _foundWeapons)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.ObjectField(weapon, typeof(GameObject), false);
                            if (GUILayout.Button("Set Source", GUILayout.Width(80)))
                            {
                                _sourceOpsiveWeapon = weapon;
                                ExtractFromOpsive();
                            }
                        }
                    }
                    
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndScrollView();
                    
                    EditorGUILayout.HelpBox("To batch convert these to ECS Prefabs, use Tools > DIG > OPSIVE Prefab Converter.", MessageType.Info);
                }
            }
        }

        private void ExtractFromOpsive()
        {
            if (_sourceOpsiveWeapon == null)
            {
                Debug.LogWarning("[OpsiveExtractor] No source weapon specified");
                return;
            }

            _extractedData = new ExtractedWeaponData();
            _extractedData.SourceName = _sourceOpsiveWeapon.name;

            // Find ShootableAction component using reflection
            var shootableAction = FindComponentByTypeName(_sourceOpsiveWeapon, "ShootableAction");

            if (shootableAction != null)
            {
                _extractedData.WeaponType = WeaponType.Shootable;
                ExtractShootableData(shootableAction);
            }
            else
            {
                // Try MeleeAction
                var meleeAction = FindComponentByTypeName(_sourceOpsiveWeapon, "MeleeAction");
                if (meleeAction != null)
                {
                    _extractedData.WeaponType = WeaponType.Melee;
                    ExtractMeleeData(meleeAction);
                }
                else
                {
                    // Try ThrowableAction
                    var throwableAction = FindComponentByTypeName(_sourceOpsiveWeapon, "ThrowableAction");
                    if (throwableAction != null)
                    {
                        _extractedData.WeaponType = WeaponType.Throwable;
                        ExtractThrowableData(throwableAction);
                    }
                    else
                    {
                        Debug.LogWarning($"[OpsiveExtractor] No OPSIVE action found on {_sourceOpsiveWeapon.name}");
                        _extractedData = null;
                        return;
                    }
                }
            }

            Debug.Log($"[OpsiveExtractor] Extracted data from {_sourceOpsiveWeapon.name}");
        }

        private void ExtractShootableData(Component shootableAction)
        {
            var type = shootableAction.GetType();

            // Get ClipSize from MainClipModule
            var clipModuleGroup = GetFieldValue(shootableAction, "m_ClipModuleGroup");
            if (clipModuleGroup != null)
            {
                var firstEnabledModule = GetPropertyValue(clipModuleGroup, "FirstEnabledModule");
                if (firstEnabledModule != null)
                {
                    _extractedData.ClipSize = GetPropertyValueInt(firstEnabledModule, "ClipSize", 30);
                }
            }

            // Get shooter module for spread, fire count, range
            var shooterModuleGroup = GetFieldValue(shootableAction, "m_ShooterModuleGroup");
            if (shooterModuleGroup != null)
            {
                var shooter = GetPropertyValue(shooterModuleGroup, "FirstEnabledModule");
                if (shooter != null)
                {
                    var shooterType = shooter.GetType();

                    // Check if it's a HitscanShooter
                    _extractedData.UseHitscan = shooterType.Name.Contains("Hitscan");

                    _extractedData.Spread = GetFieldValueFloat(shooter, "m_Spread", 0.01f);
                    _extractedData.FireCount = GetFieldValueInt(shooter, "m_FireCount", 1);
                    _extractedData.Range = GetFieldValueFloat(shooter, "m_HitscanFireRange", 100f);

                    // If range is MaxValue, use a sensible default
                    if (_extractedData.Range > 10000f)
                        _extractedData.Range = 100f;
                }
            }

            // Get UsableAction base properties for fire rate
            // Fire rate is often in a trigger module or as UseRate
            var triggerModuleGroup = GetFieldValue(shootableAction, "m_TriggerModuleGroup");
            if (triggerModuleGroup != null)
            {
                var trigger = GetPropertyValue(triggerModuleGroup, "FirstEnabledModule");
                if (trigger != null)
                {
                    // Try to get fire rate or use rate
                    float useRate = GetFieldValueFloat(trigger, "m_UseRate", 10f);
                    _extractedData.FireRate = useRate;

                    bool isAutomatic = GetFieldValueBool(trigger, "m_AutoUse", true);
                    _extractedData.IsAutomatic = isAutomatic;
                }
            }

            // Get reloader module for reload time
            var reloaderModuleGroup = GetFieldValue(shootableAction, "m_ReloaderModuleGroup");
            if (reloaderModuleGroup != null)
            {
                var reloader = GetPropertyValue(reloaderModuleGroup, "FirstEnabledModule");
                if (reloader != null)
                {
                    _extractedData.ReloadTime = GetFieldValueFloat(reloader, "m_ReloadDuration", 2f);
                }
            }

            // Get impact module for damage
            var impactModuleGroup = GetFieldValue(shootableAction, "m_ImpactModuleGroup");
            if (impactModuleGroup != null)
            {
                var impact = GetPropertyValue(impactModuleGroup, "FirstEnabledModule");
                if (impact != null)
                {
                    _extractedData.Damage = GetFieldValueFloat(impact, "m_DamageAmount", 20f);
                }
            }

            // Estimate recoil from fire effects or use default
            _extractedData.RecoilAmount = 5f; // Default, OPSIVE stores this differently
            _extractedData.RecoilRecovery = 10f;
        }

        private void ExtractMeleeData(Component meleeAction)
        {
            // Extract melee-specific data
            _extractedData.MeleeDamage = GetFieldValueFloat(meleeAction, "m_DamageAmount", 50f);
            _extractedData.MeleeRange = GetFieldValueFloat(meleeAction, "m_HitboxExtents", 2f);
            _extractedData.AttackSpeed = GetFieldValueFloat(meleeAction, "m_AttackRate", 2f);

            // Combo settings if available
            _extractedData.ComboCount = GetFieldValueInt(meleeAction, "m_MaxComboCount", 3);
        }

        private void ExtractThrowableData(Component throwableAction)
        {
            // Extract throwable-specific data
            _extractedData.MinThrowForce = GetFieldValueFloat(throwableAction, "m_MinVelocity", 10f);
            _extractedData.MaxThrowForce = GetFieldValueFloat(throwableAction, "m_MaxVelocity", 30f);
            _extractedData.ChargeTime = GetFieldValueFloat(throwableAction, "m_MaxChargeLength", 1f);
        }

        private void ApplyToEcsWeapon()
        {
            if (_targetEcsWeapon == null || _extractedData == null)
            {
                Debug.LogWarning("[OpsiveExtractor] Missing target weapon or extracted data");
                return;
            }

            var authoring = _targetEcsWeapon.GetComponent<WeaponAuthoring>();
            if (authoring == null)
            {
                // Add component if missing
                authoring = _targetEcsWeapon.AddComponent<WeaponAuthoring>();
                Debug.Log($"[OpsiveExtractor] Added WeaponAuthoring to {_targetEcsWeapon.name}");
            }

            Undo.RecordObject(authoring, "Apply OPSIVE Weapon Data");

            // Apply extracted values
            authoring.Type = _extractedData.WeaponType;

            switch (_extractedData.WeaponType)
            {
                case WeaponType.Shootable:
                    authoring.ClipSize = _extractedData.ClipSize;
                    authoring.StartingAmmo = _extractedData.ClipSize;
                    authoring.ReserveAmmo = _extractedData.ClipSize * 3;
                    authoring.FireRate = _extractedData.FireRate;
                    authoring.Damage = _extractedData.Damage;
                    authoring.Range = _extractedData.Range;
                    authoring.SpreadAngle = _extractedData.Spread;
                    authoring.RecoilAmount = _extractedData.RecoilAmount;
                    authoring.RecoilRecovery = _extractedData.RecoilRecovery;
                    authoring.ReloadTime = _extractedData.ReloadTime;
                    authoring.IsAutomatic = _extractedData.IsAutomatic;
                    authoring.UseHitscan = _extractedData.UseHitscan;
                    break;

                case WeaponType.Melee:
                    authoring.MeleeDamage = _extractedData.MeleeDamage;
                    authoring.MeleeRange = _extractedData.MeleeRange;
                    authoring.AttackSpeed = _extractedData.AttackSpeed;
                    authoring.ComboCount = _extractedData.ComboCount;
                    break;

                case WeaponType.Throwable:
                    authoring.MinThrowForce = _extractedData.MinThrowForce;
                    authoring.MaxThrowForce = _extractedData.MaxThrowForce;
                    authoring.ChargeTime = _extractedData.ChargeTime;
                    break;
            }

            EditorUtility.SetDirty(authoring);
            Debug.Log($"[OpsiveExtractor] Applied data to {_targetEcsWeapon.name}");
        }

        private void DrawExtractedValues()
        {
            if (_extractedData == null) return;

            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Source", _extractedData.SourceName);
            EditorGUILayout.LabelField("Weapon Type", _extractedData.WeaponType.ToString());

            EditorGUILayout.Space(5);

            switch (_extractedData.WeaponType)
            {
                case WeaponType.Shootable:
                    EditorGUILayout.LabelField("Clip Size", _extractedData.ClipSize.ToString());
                    EditorGUILayout.LabelField("Fire Rate", $"{_extractedData.FireRate:F1} shots/sec");
                    EditorGUILayout.LabelField("Damage", $"{_extractedData.Damage:F1}");
                    EditorGUILayout.LabelField("Range", $"{_extractedData.Range:F1}m");
                    EditorGUILayout.LabelField("Spread", $"{_extractedData.Spread:F2}°");
                    EditorGUILayout.LabelField("Reload Time", $"{_extractedData.ReloadTime:F1}s");
                    EditorGUILayout.LabelField("Automatic", _extractedData.IsAutomatic.ToString());
                    EditorGUILayout.LabelField("Hitscan", _extractedData.UseHitscan.ToString());
                    break;

                case WeaponType.Melee:
                    EditorGUILayout.LabelField("Damage", $"{_extractedData.MeleeDamage:F1}");
                    EditorGUILayout.LabelField("Range", $"{_extractedData.MeleeRange:F1}m");
                    EditorGUILayout.LabelField("Attack Speed", $"{_extractedData.AttackSpeed:F1}/sec");
                    EditorGUILayout.LabelField("Combo Count", _extractedData.ComboCount.ToString());
                    break;

                case WeaponType.Throwable:
                    EditorGUILayout.LabelField("Min Force", $"{_extractedData.MinThrowForce:F1}");
                    EditorGUILayout.LabelField("Max Force", $"{_extractedData.MaxThrowForce:F1}");
                    EditorGUILayout.LabelField("Charge Time", $"{_extractedData.ChargeTime:F1}s");
                    break;
            }

            EditorGUI.indentLevel--;
        }

        private void ScanForOpsiveWeapons()
        {
            _foundWeapons.Clear();
            var guids = AssetDatabase.FindAssets("t:GameObject");
            int found = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (go != null)
                {
                    var shootable = FindComponentByTypeName(go, "ShootableAction");
                    var melee = FindComponentByTypeName(go, "MeleeAction");
                    var throwable = FindComponentByTypeName(go, "ThrowableAction");

                    if (shootable != null || melee != null || throwable != null)
                    {
                        string actionType = shootable != null ? "Shootable" :
                                           melee != null ? "Melee" : "Throwable";
                        Debug.Log($"[OpsiveExtractor] Found {actionType}: {path}");
                        _foundWeapons.Add(go);
                        found++;
                    }
                }
            }

            Debug.Log($"[OpsiveExtractor] Scan complete. Found {found} OPSIVE weapon prefabs.");
        }

        // ============================================
        // REFLECTION HELPERS
        // ============================================

        private Component FindComponentByTypeName(GameObject go, string typeName)
        {
            foreach (var component in go.GetComponents<Component>())
            {
                if (component == null) continue;

                var type = component.GetType();
                if (type.Name == typeName || type.Name.EndsWith(typeName))
                {
                    return component;
                }

                // Check base types
                var baseType = type.BaseType;
                while (baseType != null)
                {
                    if (baseType.Name == typeName || baseType.Name.EndsWith(typeName))
                    {
                        return component;
                    }
                    baseType = baseType.BaseType;
                }
            }
            return null;
        }

        private object GetFieldValue(object obj, string fieldName)
        {
            if (obj == null) return null;

            var type = obj.GetType();
            var field = type.GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            return field?.GetValue(obj);
        }

        private object GetPropertyValue(object obj, string propName)
        {
            if (obj == null) return null;

            var type = obj.GetType();
            var prop = type.GetProperty(propName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            return prop?.GetValue(obj);
        }

        private float GetFieldValueFloat(object obj, string fieldName, float defaultValue)
        {
            var value = GetFieldValue(obj, fieldName);
            if (value is float f) return f;
            if (value is double d) return (float)d;
            if (value is int i) return i;
            return defaultValue;
        }

        private int GetFieldValueInt(object obj, string fieldName, int defaultValue)
        {
            var value = GetFieldValue(obj, fieldName);
            if (value is int i) return i;
            if (value is float f) return (int)f;
            return defaultValue;
        }

        private int GetPropertyValueInt(object obj, string propName, int defaultValue)
        {
            var value = GetPropertyValue(obj, propName);
            if (value is int i) return i;
            if (value is float f) return (int)f;
            return defaultValue;
        }

        private bool GetFieldValueBool(object obj, string fieldName, bool defaultValue)
        {
            var value = GetFieldValue(obj, fieldName);
            if (value is bool b) return b;
            return defaultValue;
        }

        // ============================================
        // DATA STRUCTURES
        // ============================================

        private class ExtractedWeaponData
        {
            public string SourceName;
            public WeaponType WeaponType;

            // Shootable
            public int ClipSize = 30;
            public float FireRate = 10f;
            public float Damage = 20f;
            public float Range = 100f;
            public float Spread = 2f;
            public float RecoilAmount = 5f;
            public float RecoilRecovery = 10f;
            public float ReloadTime = 2f;
            public bool IsAutomatic = true;
            public bool UseHitscan = true;
            public int FireCount = 1;

            // Melee
            public float MeleeDamage = 50f;
            public float MeleeRange = 2f;
            public float AttackSpeed = 2f;
            public int ComboCount = 3;

            // Throwable
            public float MinThrowForce = 10f;
            public float MaxThrowForce = 30f;
            public float ChargeTime = 1f;
        }
    }
}
