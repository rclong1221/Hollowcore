using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.EquipmentWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 EW-05: Bulk Operations module.
    /// Multi-select prefabs, batch stat adjustments, find/replace values.
    /// </summary>
    public class BulkOpsModule : IEquipmentModule
    {
        private Vector2 _scrollPosition;
        private List<GameObject> _selectedPrefabs = new List<GameObject>();
        
        // Operation type
        private enum OperationType { AdjustStats, FindReplace, AddComponent, RemoveComponent }
        private OperationType _operation = OperationType.AdjustStats;
        
        // Stat adjustment
        private enum AdjustMode { Flat, Percentage, Set }
        private AdjustMode _adjustMode = AdjustMode.Percentage;
        private string _targetProperty = "Damage";
        private float _adjustValue = 10f;
        
        // Find/Replace
        private string _findValue = "";
        private string _replaceValue = "";
        private bool _useRegex = false;
        
        // Component ops
        private string _componentTypeName = "";
        
        // Preview
        private List<OperationPreview> _previewResults = new List<OperationPreview>();
        private bool _hasPreview = false;

        private struct OperationPreview
        {
            public string PrefabName;
            public string OldValue;
            public string NewValue;
            public bool WillChange;
        }

        private readonly string[] _adjustableProperties = new[]
        {
            "Damage", "FireRate", "ClipSize", "ReloadTime", "Range", "BaseSpread"
        };

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Bulk Operations", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Perform batch operations on multiple weapon prefabs. " +
                "Always preview changes before applying.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawPrefabSelection();
            EditorGUILayout.Space(10);
            DrawOperationSelection();
            EditorGUILayout.Space(10);
            DrawOperationConfig();
            EditorGUILayout.Space(10);
            DrawPreview();
            EditorGUILayout.Space(10);
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPrefabSelection()
        {
            EditorGUILayout.LabelField("Target Prefabs", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add from Selection"))
            {
                AddFromSelection();
            }
            if (GUILayout.Button("Scan Folder..."))
            {
                ScanFolder();
            }
            if (GUILayout.Button("Clear All"))
            {
                _selectedPrefabs.Clear();
                _hasPreview = false;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Selected: {_selectedPrefabs.Count} prefabs");

            // List selected prefabs
            if (_selectedPrefabs.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MaxHeight(120));
                for (int i = 0; i < _selectedPrefabs.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    EditorGUILayout.ObjectField(_selectedPrefabs[i], typeof(GameObject), false);
                    
                    if (GUILayout.Button("×", GUILayout.Width(20)))
                    {
                        _selectedPrefabs.RemoveAt(i);
                        _hasPreview = false;
                        break;
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawOperationSelection()
        {
            EditorGUILayout.LabelField("Operation Type", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = _operation == OperationType.AdjustStats ? Color.cyan : Color.white;
            if (GUILayout.Button("Adjust Stats", GUILayout.Height(30)))
            {
                _operation = OperationType.AdjustStats;
                _hasPreview = false;
            }
            
            GUI.backgroundColor = _operation == OperationType.FindReplace ? Color.cyan : Color.white;
            if (GUILayout.Button("Find/Replace", GUILayout.Height(30)))
            {
                _operation = OperationType.FindReplace;
                _hasPreview = false;
            }
            
            GUI.backgroundColor = _operation == OperationType.AddComponent ? Color.cyan : Color.white;
            if (GUILayout.Button("Add Component", GUILayout.Height(30)))
            {
                _operation = OperationType.AddComponent;
                _hasPreview = false;
            }
            
            GUI.backgroundColor = _operation == OperationType.RemoveComponent ? Color.cyan : Color.white;
            if (GUILayout.Button("Remove Component", GUILayout.Height(30)))
            {
                _operation = OperationType.RemoveComponent;
                _hasPreview = false;
            }
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawOperationConfig()
        {
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            switch (_operation)
            {
                case OperationType.AdjustStats:
                    DrawAdjustStatsConfig();
                    break;
                case OperationType.FindReplace:
                    DrawFindReplaceConfig();
                    break;
                case OperationType.AddComponent:
                case OperationType.RemoveComponent:
                    DrawComponentConfig();
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAdjustStatsConfig()
        {
            int propIndex = System.Array.IndexOf(_adjustableProperties, _targetProperty);
            propIndex = EditorGUILayout.Popup("Property", propIndex >= 0 ? propIndex : 0, _adjustableProperties);
            _targetProperty = _adjustableProperties[propIndex];

            _adjustMode = (AdjustMode)EditorGUILayout.EnumPopup("Mode", _adjustMode);

            switch (_adjustMode)
            {
                case AdjustMode.Flat:
                    _adjustValue = EditorGUILayout.FloatField("Add/Subtract", _adjustValue);
                    EditorGUILayout.HelpBox($"Will add {_adjustValue:+0;-0} to {_targetProperty}", MessageType.Info);
                    break;
                case AdjustMode.Percentage:
                    _adjustValue = EditorGUILayout.Slider("Percentage", _adjustValue, -100f, 200f);
                    EditorGUILayout.HelpBox($"Will multiply {_targetProperty} by {1 + _adjustValue / 100:F2}x ({_adjustValue:+0;-0}%)", MessageType.Info);
                    break;
                case AdjustMode.Set:
                    _adjustValue = EditorGUILayout.FloatField("New Value", _adjustValue);
                    EditorGUILayout.HelpBox($"Will set all {_targetProperty} to {_adjustValue}", MessageType.Info);
                    break;
            }
        }

        private void DrawFindReplaceConfig()
        {
            int propIndex = System.Array.IndexOf(_adjustableProperties, _targetProperty);
            propIndex = EditorGUILayout.Popup("Property", propIndex >= 0 ? propIndex : 0, _adjustableProperties);
            _targetProperty = _adjustableProperties[propIndex];

            _findValue = EditorGUILayout.TextField("Find Value", _findValue);
            _replaceValue = EditorGUILayout.TextField("Replace With", _replaceValue);
            _useRegex = EditorGUILayout.Toggle("Use Regex", _useRegex);
        }

        private void DrawComponentConfig()
        {
            _componentTypeName = EditorGUILayout.TextField("Component Type", _componentTypeName);
            
            if (!string.IsNullOrEmpty(_componentTypeName))
            {
                var type = System.Type.GetType(_componentTypeName);
                if (type == null)
                {
                    type = System.AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => a.GetTypes())
                        .FirstOrDefault(t => t.Name == _componentTypeName);
                }

                if (type != null)
                {
                    EditorGUILayout.HelpBox($"Found: {type.FullName}", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("Component type not found. Try full name with namespace.", MessageType.Warning);
                }
            }
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (!_hasPreview)
            {
                EditorGUI.BeginDisabledGroup(_selectedPrefabs.Count == 0);
                if (GUILayout.Button("Generate Preview", GUILayout.Height(30)))
                {
                    GeneratePreview();
                }
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                int changesCount = _previewResults.Count(p => p.WillChange);
                EditorGUILayout.LabelField($"Changes: {changesCount} / {_previewResults.Count}");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MaxHeight(150));
                foreach (var preview in _previewResults)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    GUI.color = preview.WillChange ? Color.yellow : Color.gray;
                    GUILayout.Label(preview.WillChange ? "✓" : "-", GUILayout.Width(15));
                    GUI.color = Color.white;
                    
                    GUILayout.Label(preview.PrefabName, GUILayout.Width(150));
                    GUILayout.Label(preview.OldValue, GUILayout.Width(80));
                    GUILayout.Label("→", GUILayout.Width(20));
                    
                    GUI.color = preview.WillChange ? Color.green : Color.gray;
                    GUILayout.Label(preview.NewValue, GUILayout.Width(80));
                    GUI.color = Color.white;
                    
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();

                if (GUILayout.Button("Refresh Preview"))
                {
                    GeneratePreview();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginDisabledGroup(!_hasPreview || _previewResults.Count(p => p.WillChange) == 0);

            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Apply Changes", GUILayout.Height(35)))
            {
                if (EditorUtility.DisplayDialog(
                    "Confirm Bulk Operation",
                    $"Apply changes to {_previewResults.Count(p => p.WillChange)} prefabs?\n\nThis cannot be undone.",
                    "Apply",
                    "Cancel"))
                {
                    ApplyChanges();
                }
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Cancel", GUILayout.Height(35)))
            {
                _hasPreview = false;
                _previewResults.Clear();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
        }

        private void AddFromSelection()
        {
            foreach (var obj in Selection.gameObjects)
            {
                if (PrefabUtility.IsPartOfPrefabAsset(obj) || PrefabUtility.GetPrefabAssetType(obj) != PrefabAssetType.NotAPrefab)
                {
                    var root = PrefabUtility.GetNearestPrefabInstanceRoot(obj);
                    var prefabAsset = root != null 
                        ? PrefabUtility.GetCorrespondingObjectFromSource(root) 
                        : obj;
                    
                    if (prefabAsset != null && !_selectedPrefabs.Contains(prefabAsset))
                    {
                        _selectedPrefabs.Add(prefabAsset);
                    }
                }
            }
            _hasPreview = false;
        }

        private void ScanFolder()
        {
            string folder = EditorUtility.OpenFolderPanel("Select Folder to Scan", "Assets", "");
            if (string.IsNullOrEmpty(folder)) return;

            // Convert to relative path
            if (folder.StartsWith(Application.dataPath))
            {
                folder = "Assets" + folder.Substring(Application.dataPath.Length);
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab != null && prefab.GetComponent<DIG.Weapons.Authoring.WeaponAuthoring>() != null)
                {
                    if (!_selectedPrefabs.Contains(prefab))
                    {
                        _selectedPrefabs.Add(prefab);
                    }
                }
            }

            _hasPreview = false;
            Debug.Log($"[BulkOps] Scanned folder, found {_selectedPrefabs.Count} weapon prefabs");
        }

        private void GeneratePreview()
        {
            _previewResults.Clear();

            foreach (var prefab in _selectedPrefabs)
            {
                var authoring = prefab.GetComponent<DIG.Weapons.Authoring.WeaponAuthoring>();
                if (authoring == null) continue;

                var preview = new OperationPreview
                {
                    PrefabName = prefab.name
                };

                float currentValue = GetPropertyValue(authoring, _targetProperty);
                float newValue = CalculateNewValue(currentValue);

                preview.OldValue = currentValue.ToString("F1");
                preview.NewValue = newValue.ToString("F1");
                preview.WillChange = !Mathf.Approximately(currentValue, newValue);

                _previewResults.Add(preview);
            }

            _hasPreview = true;
        }

        private float GetPropertyValue(DIG.Weapons.Authoring.WeaponAuthoring authoring, string propName)
        {
            switch (propName)
            {
                case "Damage": return authoring.Damage;
                case "FireRate": return authoring.FireRate;
                case "ClipSize": return authoring.ClipSize;
                case "ReloadTime": return authoring.ReloadTime;
                case "Range": return authoring.Range;
                case "SpreadAngle": return authoring.SpreadAngle;
                default: return 0;
            }
        }

        private void SetPropertyValue(DIG.Weapons.Authoring.WeaponAuthoring authoring, string propName, float value)
        {
            switch (propName)
            {
                case "Damage": authoring.Damage = value; break;
                case "FireRate": authoring.FireRate = value; break;
                case "ClipSize": authoring.ClipSize = (int)value; break;
                case "ReloadTime": authoring.ReloadTime = value; break;
                case "Range": authoring.Range = value; break;
                case "SpreadAngle": authoring.SpreadAngle = value; break;
            }
        }

        private float CalculateNewValue(float currentValue)
        {
            switch (_adjustMode)
            {
                case AdjustMode.Flat:
                    return currentValue + _adjustValue;
                case AdjustMode.Percentage:
                    return currentValue * (1 + _adjustValue / 100f);
                case AdjustMode.Set:
                    return _adjustValue;
                default:
                    return currentValue;
            }
        }

        private void ApplyChanges()
        {
            int successCount = 0;

            for (int i = 0; i < _selectedPrefabs.Count; i++)
            {
                var prefab = _selectedPrefabs[i];
                if (i >= _previewResults.Count || !_previewResults[i].WillChange) continue;

                // Modify prefab
                string path = AssetDatabase.GetAssetPath(prefab);
                var prefabRoot = PrefabUtility.LoadPrefabContents(path);
                
                var authoring = prefabRoot.GetComponent<DIG.Weapons.Authoring.WeaponAuthoring>();
                if (authoring != null)
                {
                    float currentValue = GetPropertyValue(authoring, _targetProperty);
                    float newValue = CalculateNewValue(currentValue);
                    SetPropertyValue(authoring, _targetProperty, newValue);
                    
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                    successCount++;
                }
                
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.Refresh();
            _hasPreview = false;
            _previewResults.Clear();

            Debug.Log($"[BulkOps] Applied changes to {successCount} prefabs");
        }
    }
}
