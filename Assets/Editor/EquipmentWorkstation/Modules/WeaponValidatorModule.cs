using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using DIG.Weapons.Authoring;
using DIG.Weapons.Animation;
using DIG.Items.Authoring;

namespace DIG.Editor.EquipmentWorkstation.Modules
{
    /// <summary>
    /// Weapon Prefab Validator module for Equipment Workstation.
    /// Validates weapon prefabs for correct component setup.
    /// </summary>
    public class WeaponValidatorModule : IEquipmentModule
    {
        private GameObject _selectedPrefab;
        private Vector2 _scrollPosition;
        private List<ValidationResult> _results = new List<ValidationResult>();
        private bool _hasValidated = false;

        private enum ResultType { Pass, Warning, Error }

        private struct ValidationResult
        {
            public string Category;
            public string Message;
            public ResultType Type;
            public System.Action FixAction;

            public ValidationResult(string category, string message, ResultType type, System.Action fixAction = null)
            {
                Category = category;
                Message = message;
                Type = type;
                FixAction = fixAction;
            }
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Weapon Prefab Validator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select a weapon prefab to validate its component setup, VFX configuration, and animation event readiness.", MessageType.Info);
            EditorGUILayout.Space(10);

            // Prefab selection
            EditorGUI.BeginChangeCheck();
            _selectedPrefab = (GameObject)EditorGUILayout.ObjectField("Weapon Prefab", _selectedPrefab, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                _hasValidated = false;
            }

            // Auto-select from project selection
            if (Selection.activeGameObject != null && _selectedPrefab != Selection.activeGameObject)
            {
                if (GUILayout.Button("Use Selected: " + Selection.activeGameObject.name, EditorStyles.miniButton))
                {
                    _selectedPrefab = Selection.activeGameObject;
                    _hasValidated = false;
                }
            }

            EditorGUILayout.Space(5);

            // Validate button
            EditorGUI.BeginDisabledGroup(_selectedPrefab == null);
            if (GUILayout.Button("Validate Prefab", GUILayout.Height(30)))
            {
                ValidatePrefab();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(10);

            // Results
            if (_hasValidated)
            {
                DrawResults();
            }
        }

        private void ValidatePrefab()
        {
            _results.Clear();

            if (_selectedPrefab == null)
            {
                _results.Add(new ValidationResult("General", "No prefab selected", ResultType.Error));
                _hasValidated = true;
                return;
            }

            // Run all validation checks
            ValidateWeaponAuthoring();
            ValidateVFXComponent();
            ValidateAnimationRelay();
            ValidateMagazineController();
            ValidateTransforms();

            _hasValidated = true;

            // Log summary
            int errors = _results.Count(r => r.Type == ResultType.Error);
            int warnings = _results.Count(r => r.Type == ResultType.Warning);
            int passes = _results.Count(r => r.Type == ResultType.Pass);

            if (errors > 0)
                Debug.LogError($"[WeaponValidator] {_selectedPrefab.name}: {errors} errors, {warnings} warnings");
            else if (warnings > 0)
                Debug.LogWarning($"[WeaponValidator] {_selectedPrefab.name}: {warnings} warnings, {passes} passed");
            else
                Debug.Log($"[WeaponValidator] {_selectedPrefab.name}: All {passes} checks passed!");
        }

        private void ValidateWeaponAuthoring()
        {
            var weaponAuthoring = _selectedPrefab.GetComponent<WeaponAuthoring>();

            if (weaponAuthoring == null)
            {
                _results.Add(new ValidationResult("Core", "Missing WeaponAuthoring component", ResultType.Error,
                    () => _selectedPrefab.AddComponent<WeaponAuthoring>()));
                return;
            }

            _results.Add(new ValidationResult("Core", "WeaponAuthoring present", ResultType.Pass));

            // Check values
            if (weaponAuthoring.ClipSize <= 0)
            {
                _results.Add(new ValidationResult("Core", $"ClipSize is {weaponAuthoring.ClipSize} (should be > 0)", ResultType.Error));
            }
            else
            {
                _results.Add(new ValidationResult("Core", $"ClipSize: {weaponAuthoring.ClipSize}", ResultType.Pass));
            }

            if (weaponAuthoring.ReloadTime <= 0)
            {
                _results.Add(new ValidationResult("Core", $"ReloadTime is {weaponAuthoring.ReloadTime}s (should be > 0)", ResultType.Warning));
            }
            else
            {
                _results.Add(new ValidationResult("Core", $"ReloadTime: {weaponAuthoring.ReloadTime}s", ResultType.Pass));
            }

            if (weaponAuthoring.FireRate <= 0)
            {
                _results.Add(new ValidationResult("Core", $"FireRate is {weaponAuthoring.FireRate} (should be > 0)", ResultType.Error));
            }
        }

        private void ValidateVFXComponent()
        {
            var vfx = _selectedPrefab.GetComponent<ItemVFXAuthoring>();

            if (vfx == null)
            {
                _results.Add(new ValidationResult("VFX", "Missing ItemVFXAuthoring (no visual effects)", ResultType.Warning,
                    () => _selectedPrefab.AddComponent<ItemVFXAuthoring>()));
                return;
            }

            _results.Add(new ValidationResult("VFX", "ItemVFXAuthoring present", ResultType.Pass));

            // Check for Fire entry via SerializedObject
            var so = new SerializedObject(vfx);
            var effectsProp = so.FindProperty("_effects");

            bool hasFireEntry = false;
            if (effectsProp != null && effectsProp.isArray)
            {
                for (int i = 0; i < effectsProp.arraySize; i++)
                {
                    var entry = effectsProp.GetArrayElementAtIndex(i);
                    var idProp = entry.FindPropertyRelative("ID");
                    if (idProp != null && idProp.stringValue == "Fire")
                    {
                        hasFireEntry = true;
                        break;
                    }
                }
            }

            if (hasFireEntry)
            {
                _results.Add(new ValidationResult("VFX", "Has 'Fire' VFX entry", ResultType.Pass));
            }
            else
            {
                _results.Add(new ValidationResult("VFX", "No 'Fire' VFX entry (no muzzle flash)", ResultType.Warning));
            }
        }

        private void ValidateAnimationRelay()
        {
            var relay = _selectedPrefab.GetComponentInChildren<WeaponAnimationEventRelay>();

            if (relay != null)
            {
                _results.Add(new ValidationResult("Animation", "WeaponAnimationEventRelay found on weapon", ResultType.Pass));
            }
            else
            {
                _results.Add(new ValidationResult("Animation", "No WeaponAnimationEventRelay on weapon (check character)", ResultType.Warning));
            }
        }

        private void ValidateMagazineController()
        {
            var magController = _selectedPrefab.GetComponent<MagazineReloadController>();

            if (magController != null)
            {
                _results.Add(new ValidationResult("Reload", "MagazineReloadController present", ResultType.Pass));

                var so = new SerializedObject(magController);
                var magClipProp = so.FindProperty("_magazineClip");
                
                if (magClipProp != null && magClipProp.objectReferenceValue == null)
                {
                    _results.Add(new ValidationResult("Reload", "MagazineClip transform not assigned", ResultType.Warning));
                }
                else
                {
                    _results.Add(new ValidationResult("Reload", "MagazineClip assigned", ResultType.Pass));
                }
            }
            else
            {
                _results.Add(new ValidationResult("Reload", "No MagazineReloadController (simple reload)", ResultType.Warning));
            }
        }

        private void ValidateTransforms()
        {
            var muzzle = FindChildTransform(_selectedPrefab.transform, new[] { "Muzzle", "MuzzlePoint", "FirePoint", "Muzzle_Flash" });
            var ejectionPort = FindChildTransform(_selectedPrefab.transform, new[] { "EjectionPort", "ShellEject", "Ejection", "Shell_Eject" });

            if (muzzle != null)
            {
                _results.Add(new ValidationResult("Transforms", $"Muzzle found: {muzzle.name}", ResultType.Pass));
            }
            else
            {
                _results.Add(new ValidationResult("Transforms", "No muzzle point (VFX may spawn wrong)", ResultType.Warning));
            }

            if (ejectionPort != null)
            {
                _results.Add(new ValidationResult("Transforms", $"Ejection port found: {ejectionPort.name}", ResultType.Pass));
            }
            else
            {
                _results.Add(new ValidationResult("Transforms", "No ejection port found", ResultType.Warning));
            }
        }

        private Transform FindChildTransform(Transform parent, string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                var found = parent.Find(name);
                if (found != null) return found;

                foreach (Transform child in parent)
                {
                    if (child.name.ToLower().Contains(name.ToLower()))
                        return child;

                    var recursive = FindChildTransform(child, new[] { name });
                    if (recursive != null) return recursive;
                }
            }
            return null;
        }

        private void DrawResults()
        {
            EditorGUILayout.LabelField("Validation Results", EditorStyles.boldLabel);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.MaxHeight(300));

            string currentCategory = "";
            foreach (var result in _results)
            {
                if (result.Category != currentCategory)
                {
                    currentCategory = result.Category;
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField(currentCategory, EditorStyles.miniBoldLabel);
                }

                EditorGUILayout.BeginHorizontal();

                // Icon
                GUIContent icon;
                switch (result.Type)
                {
                    case ResultType.Pass:
                        icon = EditorGUIUtility.IconContent("TestPassed");
                        break;
                    case ResultType.Warning:
                        icon = EditorGUIUtility.IconContent("console.warnicon.sml");
                        break;
                    case ResultType.Error:
                        icon = EditorGUIUtility.IconContent("console.erroricon.sml");
                        break;
                    default:
                        icon = GUIContent.none;
                        break;
                }

                GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(18));
                EditorGUILayout.LabelField(result.Message);

                if (result.FixAction != null)
                {
                    if (GUILayout.Button("Fix", GUILayout.Width(40)))
                    {
                        result.FixAction();
                        ValidatePrefab();
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            int errors = _results.Count(r => r.Type == ResultType.Error);
            int warnings = _results.Count(r => r.Type == ResultType.Warning);
            int passes = _results.Count(r => r.Type == ResultType.Pass);

            EditorGUILayout.LabelField($"Summary: {passes} passed, {warnings} warnings, {errors} errors",
                errors > 0 ? EditorStyles.boldLabel : EditorStyles.label);
        }
    }
}
