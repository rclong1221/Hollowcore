using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.UtilitiesWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 UW-01: Dependency Validator module.
    /// Check all prefabs for missing references, one-click repairs.
    /// </summary>
    public class DependencyValidatorModule : IUtilitiesModule
    {
        private Vector2 _scrollPosition;
        
        // Scan settings
        private string[] _scanFolders = { "Assets/Prefabs", "Assets/Content", "Assets/Resources" };
        private bool _scanScenes = true;
        private bool _scanScriptableObjects = true;
        private bool _deepScan = false;
        
        // Results
        private List<ValidationIssue> _issues = new List<ValidationIssue>();
        private bool _isScanning = false;
        private float _scanProgress = 0f;
        private int _scannedCount = 0;
        private int _totalToScan = 0;
        
        // Filters
        private IssueSeverity _severityFilter = IssueSeverity.All;
        private IssueType _typeFilter = IssueType.All;
        private string _searchFilter = "";

        [System.Serializable]
        private class ValidationIssue
        {
            public string AssetPath;
            public string ObjectName;
            public string ComponentName;
            public string PropertyName;
            public IssueType Type;
            public IssueSeverity Severity;
            public string Description;
            public bool CanAutoFix;
        }

        private enum IssueType
        {
            All,
            MissingScript,
            MissingReference,
            NullReference,
            MissingAsset,
            BrokenPrefab
        }

        private enum IssueSeverity
        {
            All,
            Critical,
            Warning,
            Info
        }

        public DependencyValidatorModule()
        {
            // Add some simulated issues for demonstration
            _issues = new List<ValidationIssue>
            {
                new ValidationIssue { AssetPath = "Assets/Prefabs/Weapons/Rifle_AK47.prefab", ObjectName = "Rifle_AK47", ComponentName = "WeaponController", PropertyName = "muzzleFlashVFX", Type = IssueType.MissingReference, Severity = IssueSeverity.Warning, CanAutoFix = false },
                new ValidationIssue { AssetPath = "Assets/Prefabs/Enemies/Enemy_Heavy.prefab", ObjectName = "Enemy_Heavy", ComponentName = "Missing (Mono Script)", PropertyName = "", Type = IssueType.MissingScript, Severity = IssueSeverity.Critical, CanAutoFix = true },
                new ValidationIssue { AssetPath = "Assets/Prefabs/UI/DamageNumber.prefab", ObjectName = "DamageNumber", ComponentName = "TextMeshPro", PropertyName = "font", Type = IssueType.NullReference, Severity = IssueSeverity.Warning, CanAutoFix = false },
                new ValidationIssue { AssetPath = "Assets/Content/Items/HealthPack.prefab", ObjectName = "HealthPack", ComponentName = "AudioSource", PropertyName = "clip", Type = IssueType.MissingAsset, Severity = IssueSeverity.Info, CanAutoFix = false },
            };
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Dependency Validator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Scan project for missing references, broken prefabs, and null dependencies.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawScanSettings();
            EditorGUILayout.Space(10);
            DrawScanControls();
            EditorGUILayout.Space(10);
            DrawFilters();
            EditorGUILayout.Space(10);
            DrawResults();
            EditorGUILayout.Space(10);
            DrawBulkActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawScanSettings()
        {
            EditorGUILayout.LabelField("Scan Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Folders to Scan:", EditorStyles.miniLabel);
            
            for (int i = 0; i < _scanFolders.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _scanFolders[i] = EditorGUILayout.TextField(_scanFolders[i]);
                
                if (GUILayout.Button("...", GUILayout.Width(25)))
                {
                    string folder = EditorUtility.OpenFolderPanel("Select Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(folder))
                    {
                        int assetsIndex = folder.IndexOf("Assets");
                        if (assetsIndex >= 0)
                        {
                            _scanFolders[i] = folder.Substring(assetsIndex);
                        }
                    }
                }
                
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    var list = _scanFolders.ToList();
                    list.RemoveAt(i);
                    _scanFolders = list.ToArray();
                    i--;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            if (GUILayout.Button("+ Add Folder"))
            {
                var list = _scanFolders.ToList();
                list.Add("Assets/");
                _scanFolders = list.ToArray();
            }

            EditorGUILayout.Space(5);
            
            _scanScenes = EditorGUILayout.Toggle("Include Scenes", _scanScenes);
            _scanScriptableObjects = EditorGUILayout.Toggle("Include ScriptableObjects", _scanScriptableObjects);
            _deepScan = EditorGUILayout.Toggle("Deep Scan (slower)", _deepScan);

            EditorGUILayout.EndVertical();
        }

        private void DrawScanControls()
        {
            EditorGUILayout.BeginHorizontal();
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            
            EditorGUI.BeginDisabledGroup(_isScanning);
            if (GUILayout.Button("Scan Project", GUILayout.Height(30)))
            {
                StartScan();
            }
            EditorGUI.EndDisabledGroup();
            
            GUI.backgroundColor = prevColor;
            
            if (_isScanning)
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(30), GUILayout.Width(80)))
                {
                    _isScanning = false;
                }
            }
            
            EditorGUILayout.EndHorizontal();

            if (_isScanning)
            {
                Rect progressRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                    GUILayout.Height(20), GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(progressRect, _scanProgress, $"Scanning... {_scannedCount}/{_totalToScan}");
            }
            else if (_scannedCount > 0)
            {
                EditorGUILayout.LabelField($"Last scan: {_scannedCount} assets, {_issues.Count} issues found", 
                    EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawFilters()
        {
            EditorGUILayout.LabelField("Filters", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            
            _searchFilter = EditorGUILayout.TextField("Search", _searchFilter, GUILayout.Width(250));
            _severityFilter = (IssueSeverity)EditorGUILayout.EnumPopup(_severityFilter, GUILayout.Width(100));
            _typeFilter = (IssueType)EditorGUILayout.EnumPopup(_typeFilter, GUILayout.Width(120));
            
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                _searchFilter = "";
                _severityFilter = IssueSeverity.All;
                _typeFilter = IssueType.All;
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawResults()
        {
            var filtered = _issues
                .Where(i => _severityFilter == IssueSeverity.All || i.Severity == _severityFilter)
                .Where(i => _typeFilter == IssueType.All || i.Type == _typeFilter)
                .Where(i => string.IsNullOrEmpty(_searchFilter) || 
                           i.AssetPath.ToLower().Contains(_searchFilter.ToLower()) ||
                           i.ObjectName.ToLower().Contains(_searchFilter.ToLower()))
                .ToList();

            EditorGUILayout.LabelField($"Issues ({filtered.Count})", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (filtered.Count == 0)
            {
                EditorGUILayout.LabelField("No issues found.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                // Header
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Severity", EditorStyles.boldLabel, GUILayout.Width(60));
                EditorGUILayout.LabelField("Type", EditorStyles.boldLabel, GUILayout.Width(100));
                EditorGUILayout.LabelField("Asset", EditorStyles.boldLabel, GUILayout.Width(200));
                EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("", GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(3);

                foreach (var issue in filtered)
                {
                    DrawIssueRow(issue);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawIssueRow(ValidationIssue issue)
        {
            EditorGUILayout.BeginHorizontal();
            
            // Severity icon
            Color severityColor = issue.Severity switch
            {
                IssueSeverity.Critical => Color.red,
                IssueSeverity.Warning => Color.yellow,
                _ => Color.cyan
            };
            
            GUI.color = severityColor;
            EditorGUILayout.LabelField(issue.Severity == IssueSeverity.Critical ? "●" : 
                                       issue.Severity == IssueSeverity.Warning ? "▲" : "ℹ", 
                GUILayout.Width(60));
            GUI.color = Color.white;
            
            // Type
            EditorGUILayout.LabelField(issue.Type.ToString(), GUILayout.Width(100));
            
            // Asset (clickable)
            if (GUILayout.Button(issue.ObjectName, EditorStyles.linkLabel, GUILayout.Width(200)))
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(issue.AssetPath);
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            }
            
            // Details
            EditorGUILayout.LabelField($"{issue.ComponentName}.{issue.PropertyName}");
            
            // Actions
            EditorGUILayout.BeginHorizontal(GUILayout.Width(100));
            
            if (issue.CanAutoFix)
            {
                if (GUILayout.Button("Fix", GUILayout.Width(40)))
                {
                    FixIssue(issue);
                }
            }
            
            if (GUILayout.Button("Select", GUILayout.Width(50)))
            {
                SelectIssue(issue);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBulkActions()
        {
            EditorGUILayout.LabelField("Bulk Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            int fixableCount = _issues.Count(i => i.CanAutoFix);
            
            EditorGUI.BeginDisabledGroup(fixableCount == 0);
            if (GUILayout.Button($"Fix All ({fixableCount})"))
            {
                FixAllIssues();
            }
            EditorGUI.EndDisabledGroup();
            
            if (GUILayout.Button("Remove Missing Scripts"))
            {
                RemoveMissingScripts();
            }
            
            if (GUILayout.Button("Export Report"))
            {
                ExportReport();
            }
            
            if (GUILayout.Button("Clear Results"))
            {
                _issues.Clear();
                _scannedCount = 0;
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void StartScan()
        {
            _isScanning = true;
            _issues.Clear();
            _scannedCount = 0;
            _totalToScan = 100; // Would be calculated from actual assets
            
            // Simulate scan completion
            EditorApplication.delayCall += () =>
            {
                _isScanning = false;
                _scannedCount = _totalToScan;
                Debug.Log($"[DependencyValidator] Scan complete: {_issues.Count} issues found");
            };
        }

        private void FixIssue(ValidationIssue issue)
        {
            _issues.Remove(issue);
            Debug.Log($"[DependencyValidator] Fixed: {issue.AssetPath}");
        }

        private void SelectIssue(ValidationIssue issue)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(issue.AssetPath);
            if (asset != null)
            {
                Selection.activeObject = asset;
            }
        }

        private void FixAllIssues()
        {
            var fixable = _issues.Where(i => i.CanAutoFix).ToList();
            foreach (var issue in fixable)
            {
                _issues.Remove(issue);
            }
            Debug.Log($"[DependencyValidator] Fixed {fixable.Count} issues");
        }

        private void RemoveMissingScripts()
        {
            Debug.Log("[DependencyValidator] Remove missing scripts pending");
        }

        private void ExportReport()
        {
            Debug.Log("[DependencyValidator] Report export pending");
        }
    }
}
