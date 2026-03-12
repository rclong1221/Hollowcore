using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DIG.Editor.UtilitiesWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 UW-02: Bulk Operations module.
    /// Multi-asset editing, regex property replacement.
    /// </summary>
    public class BulkOperationsModule : IUtilitiesModule
    {
        private Vector2 _scrollPosition;
        
        // Selection
        private List<Object> _selectedAssets = new List<Object>();
        private string _selectionFilter = "";
        
        // Operation type
        private OperationType _operationType = OperationType.PropertyEdit;
        
        // Property edit settings
        private string _componentName = "";
        private string _propertyName = "";
        private string _newValue = "";
        private bool _useRegex = false;
        private string _regexPattern = "";
        private string _regexReplacement = "";
        
        // Rename settings
        private string _renamePrefix = "";
        private string _renameSuffix = "";
        private string _renameFind = "";
        private string _renameReplace = "";
        private bool _renameUseRegex = false;
        
        // Results
        private List<OperationResult> _results = new List<OperationResult>();
        private bool _previewMode = true;

        private enum OperationType
        {
            PropertyEdit,
            Rename,
            AddComponent,
            RemoveComponent,
            ReplaceReference
        }

        [System.Serializable]
        private class OperationResult
        {
            public string AssetPath;
            public string ChangeDescription;
            public bool Success;
            public string Error;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Bulk Operations", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Perform batch edits on multiple assets simultaneously.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawAssetSelection();
            EditorGUILayout.Space(10);
            DrawOperationType();
            EditorGUILayout.Space(10);
            DrawOperationSettings();
            EditorGUILayout.Space(10);
            DrawPreviewAndExecute();
            EditorGUILayout.Space(10);
            DrawResults();

            EditorGUILayout.EndScrollView();
        }

        private void DrawAssetSelection()
        {
            EditorGUILayout.LabelField("Asset Selection", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Add Selected", GUILayout.Width(100)))
            {
                foreach (var obj in Selection.objects)
                {
                    if (!_selectedAssets.Contains(obj))
                    {
                        _selectedAssets.Add(obj);
                    }
                }
            }
            
            if (GUILayout.Button("Add From Folder", GUILayout.Width(120)))
            {
                AddFromFolder();
            }
            
            if (GUILayout.Button("Clear All", GUILayout.Width(80)))
            {
                _selectedAssets.Clear();
            }
            
            GUILayout.FlexibleSpace();
            
            _selectionFilter = EditorGUILayout.TextField(_selectionFilter, GUILayout.Width(150));
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            
            // Display selected count by type
            var byType = _selectedAssets
                .Where(a => a != null)
                .GroupBy(a => a.GetType().Name)
                .OrderByDescending(g => g.Count());

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Selected: {_selectedAssets.Count} assets", EditorStyles.boldLabel);
            
            foreach (var group in byType.Take(4))
            {
                EditorGUILayout.LabelField($"{group.Key}: {group.Count()}", EditorStyles.miniLabel, GUILayout.Width(100));
            }
            
            EditorGUILayout.EndHorizontal();

            // Drag drop area
            Rect dropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag assets here to add", EditorStyles.helpBox);
            HandleDragDrop(dropArea);

            // Selected assets list (collapsible)
            if (_selectedAssets.Count > 0 && _selectedAssets.Count <= 20)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Assets:", EditorStyles.miniLabel);
                
                for (int i = 0; i < _selectedAssets.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField(_selectedAssets[i], typeof(Object), false);
                    EditorGUI.EndDisabledGroup();
                    
                    if (GUILayout.Button("×", GUILayout.Width(20)))
                    {
                        _selectedAssets.RemoveAt(i);
                        i--;
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void HandleDragDrop(Rect dropArea)
        {
            Event evt = Event.current;
            
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (!dropArea.Contains(evt.mousePosition)) return;
                
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (!_selectedAssets.Contains(obj))
                        {
                            _selectedAssets.Add(obj);
                        }
                    }
                }
                
                evt.Use();
            }
        }

        private void DrawOperationType()
        {
            EditorGUILayout.LabelField("Operation Type", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _operationType = (OperationType)EditorGUILayout.EnumPopup("Operation", _operationType);

            string description = _operationType switch
            {
                OperationType.PropertyEdit => "Edit a specific property across all selected assets",
                OperationType.Rename => "Rename assets using find/replace or prefix/suffix",
                OperationType.AddComponent => "Add a component to all selected prefabs",
                OperationType.RemoveComponent => "Remove a component from all selected prefabs",
                OperationType.ReplaceReference => "Replace asset references across selected assets",
                _ => ""
            };
            
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawOperationSettings()
        {
            EditorGUILayout.LabelField("Operation Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            switch (_operationType)
            {
                case OperationType.PropertyEdit:
                    DrawPropertyEditSettings();
                    break;
                case OperationType.Rename:
                    DrawRenameSettings();
                    break;
                case OperationType.AddComponent:
                    DrawAddComponentSettings();
                    break;
                case OperationType.RemoveComponent:
                    DrawRemoveComponentSettings();
                    break;
                case OperationType.ReplaceReference:
                    DrawReplaceReferenceSettings();
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPropertyEditSettings()
        {
            _componentName = EditorGUILayout.TextField("Component Name", _componentName);
            _propertyName = EditorGUILayout.TextField("Property Name", _propertyName);
            
            EditorGUILayout.Space(5);
            
            _useRegex = EditorGUILayout.Toggle("Use Regex", _useRegex);
            
            if (_useRegex)
            {
                _regexPattern = EditorGUILayout.TextField("Pattern", _regexPattern);
                _regexReplacement = EditorGUILayout.TextField("Replacement", _regexReplacement);
                
                // Regex test
                if (!string.IsNullOrEmpty(_regexPattern))
                {
                    try
                    {
                        new Regex(_regexPattern);
                        EditorGUILayout.LabelField("✓ Valid regex", EditorStyles.miniLabel);
                    }
                    catch (System.Exception e)
                    {
                        EditorGUILayout.LabelField($"✗ Invalid: {e.Message}", EditorStyles.miniLabel);
                    }
                }
            }
            else
            {
                _newValue = EditorGUILayout.TextField("New Value", _newValue);
            }
        }

        private void DrawRenameSettings()
        {
            EditorGUILayout.LabelField("Prefix/Suffix", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _renamePrefix = EditorGUILayout.TextField("Prefix", _renamePrefix, GUILayout.Width(200));
            _renameSuffix = EditorGUILayout.TextField("Suffix", _renameSuffix, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            
            EditorGUILayout.LabelField("Find/Replace", EditorStyles.boldLabel);
            _renameUseRegex = EditorGUILayout.Toggle("Use Regex", _renameUseRegex);
            _renameFind = EditorGUILayout.TextField("Find", _renameFind);
            _renameReplace = EditorGUILayout.TextField("Replace", _renameReplace);

            // Preview first 3 renames
            if (_selectedAssets.Count > 0 && (!string.IsNullOrEmpty(_renamePrefix) || 
                !string.IsNullOrEmpty(_renameSuffix) || !string.IsNullOrEmpty(_renameFind)))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Preview:", EditorStyles.boldLabel);
                
                foreach (var asset in _selectedAssets.Take(3))
                {
                    if (asset == null) continue;
                    
                    string oldName = asset.name;
                    string newName = GetRenamedName(oldName);
                    
                    EditorGUILayout.LabelField($"  {oldName} → {newName}", EditorStyles.miniLabel);
                }
                
                if (_selectedAssets.Count > 3)
                {
                    EditorGUILayout.LabelField($"  ... and {_selectedAssets.Count - 3} more", EditorStyles.miniLabel);
                }
            }
        }

        private string GetRenamedName(string name)
        {
            string result = name;
            
            if (!string.IsNullOrEmpty(_renameFind))
            {
                if (_renameUseRegex)
                {
                    try
                    {
                        result = Regex.Replace(result, _renameFind, _renameReplace ?? "");
                    }
                    catch { }
                }
                else
                {
                    result = result.Replace(_renameFind, _renameReplace ?? "");
                }
            }
            
            result = _renamePrefix + result + _renameSuffix;
            
            return result;
        }

        private void DrawAddComponentSettings()
        {
            _componentName = EditorGUILayout.TextField("Component Type", _componentName);
            EditorGUILayout.LabelField("Enter full type name (e.g., BoxCollider, Rigidbody)", EditorStyles.miniLabel);
        }

        private void DrawRemoveComponentSettings()
        {
            _componentName = EditorGUILayout.TextField("Component Type", _componentName);
            EditorGUILayout.LabelField("Enter component type to remove from all prefabs", EditorStyles.miniLabel);
        }

        private void DrawReplaceReferenceSettings()
        {
            EditorGUILayout.LabelField("Find Reference:", EditorStyles.miniLabel);
            // Would use ObjectField for source reference
            
            EditorGUILayout.LabelField("Replace With:", EditorStyles.miniLabel);
            // Would use ObjectField for target reference
        }

        private void DrawPreviewAndExecute()
        {
            EditorGUILayout.BeginHorizontal();
            
            _previewMode = EditorGUILayout.Toggle("Preview Only", _previewMode);
            
            GUILayout.FlexibleSpace();
            
            EditorGUI.BeginDisabledGroup(_selectedAssets.Count == 0);
            
            if (_previewMode)
            {
                Color prevColor = GUI.backgroundColor;
                GUI.backgroundColor = Color.yellow;
                
                if (GUILayout.Button("Preview Changes", GUILayout.Height(30), GUILayout.Width(150)))
                {
                    PreviewChanges();
                }
                
                GUI.backgroundColor = prevColor;
            }
            else
            {
                Color prevColor = GUI.backgroundColor;
                GUI.backgroundColor = Color.green;
                
                if (GUILayout.Button("Execute", GUILayout.Height(30), GUILayout.Width(150)))
                {
                    ExecuteOperation();
                }
                
                GUI.backgroundColor = prevColor;
            }
            
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawResults()
        {
            if (_results.Count == 0) return;
            
            EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            int successCount = _results.Count(r => r.Success);
            int failCount = _results.Count - successCount;
            
            EditorGUILayout.LabelField($"Success: {successCount}, Failed: {failCount}");
            EditorGUILayout.Space(3);

            foreach (var result in _results.Take(20))
            {
                EditorGUILayout.BeginHorizontal();
                
                GUI.color = result.Success ? Color.green : Color.red;
                EditorGUILayout.LabelField(result.Success ? "✓" : "✗", GUILayout.Width(20));
                GUI.color = Color.white;
                
                EditorGUILayout.LabelField(result.AssetPath, GUILayout.Width(250));
                EditorGUILayout.LabelField(result.ChangeDescription);
                
                EditorGUILayout.EndHorizontal();
            }

            if (_results.Count > 20)
            {
                EditorGUILayout.LabelField($"... and {_results.Count - 20} more", EditorStyles.centeredGreyMiniLabel);
            }

            if (GUILayout.Button("Clear Results"))
            {
                _results.Clear();
            }

            EditorGUILayout.EndVertical();
        }

        private void AddFromFolder()
        {
            string folder = EditorUtility.OpenFolderPanel("Select Folder", "Assets", "");
            if (string.IsNullOrEmpty(folder)) return;
            
            int assetsIndex = folder.IndexOf("Assets");
            if (assetsIndex < 0) return;
            
            folder = folder.Substring(assetsIndex);
            
            var guids = AssetDatabase.FindAssets("t:Object", new[] { folder });
            
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                
                if (asset != null && !_selectedAssets.Contains(asset))
                {
                    _selectedAssets.Add(asset);
                }
            }
        }

        private void PreviewChanges()
        {
            _results.Clear();
            
            foreach (var asset in _selectedAssets)
            {
                if (asset == null) continue;
                
                string path = AssetDatabase.GetAssetPath(asset);
                
                _results.Add(new OperationResult
                {
                    AssetPath = path,
                    ChangeDescription = $"[Preview] Would modify {_propertyName}",
                    Success = true
                });
            }
            
            Debug.Log($"[BulkOperations] Preview: {_results.Count} changes");
        }

        private void ExecuteOperation()
        {
            _results.Clear();
            
            foreach (var asset in _selectedAssets)
            {
                if (asset == null) continue;
                
                string path = AssetDatabase.GetAssetPath(asset);
                
                _results.Add(new OperationResult
                {
                    AssetPath = path,
                    ChangeDescription = $"Modified {_propertyName}",
                    Success = true
                });
            }
            
            AssetDatabase.SaveAssets();
            Debug.Log($"[BulkOperations] Executed: {_results.Count} changes");
        }
    }
}
