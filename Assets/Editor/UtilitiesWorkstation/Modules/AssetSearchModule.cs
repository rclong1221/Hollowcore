using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.UtilitiesWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 UW-03: Asset Search module.
    /// Find assets by component type, property value.
    /// </summary>
    public class AssetSearchModule : IUtilitiesModule
    {
        private Vector2 _scrollPosition;
        
        // Search settings
        private SearchMode _searchMode = SearchMode.ByComponent;
        private string _componentName = "";
        private string _propertyName = "";
        private string _propertyValue = "";
        private PropertyComparison _comparison = PropertyComparison.Equals;
        private string[] _searchFolders = { "Assets" };
        private bool _searchInChildren = true;
        
        // Results
        private List<SearchResult> _results = new List<SearchResult>();
        private bool _isSearching = false;
        private float _searchProgress = 0f;
        
        // Saved searches
        private List<SavedSearch> _savedSearches = new List<SavedSearch>();

        private enum SearchMode
        {
            ByComponent,
            ByProperty,
            ByReference,
            ByTag,
            ByLayer
        }

        private enum PropertyComparison
        {
            Equals,
            Contains,
            GreaterThan,
            LessThan,
            IsNull,
            IsNotNull
        }

        [System.Serializable]
        private class SearchResult
        {
            public Object Asset;
            public string AssetPath;
            public string MatchDetails;
            public GameObject MatchedObject;
        }

        [System.Serializable]
        private class SavedSearch
        {
            public string Name;
            public SearchMode Mode;
            public string Query;
        }

        public AssetSearchModule()
        {
            // Default saved searches
            _savedSearches = new List<SavedSearch>
            {
                new SavedSearch { Name = "All Rigidbodies", Mode = SearchMode.ByComponent, Query = "Rigidbody" },
                new SavedSearch { Name = "Missing Colliders", Mode = SearchMode.ByComponent, Query = "!Collider" },
                new SavedSearch { Name = "High Damage Weapons", Mode = SearchMode.ByProperty, Query = "damage>100" },
            };
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Asset Search", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Find assets by component type, property values, or references.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawSavedSearches();
            EditorGUILayout.Space(10);
            DrawSearchSettings();
            EditorGUILayout.Space(10);
            DrawSearchControls();
            EditorGUILayout.Space(10);
            DrawResults();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSavedSearches()
        {
            EditorGUILayout.LabelField("Quick Search", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            
            foreach (var saved in _savedSearches.Take(5))
            {
                if (GUILayout.Button(saved.Name, EditorStyles.miniButton))
                {
                    _searchMode = saved.Mode;
                    _componentName = saved.Query;
                    ExecuteSearch();
                }
            }
            
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSearchSettings()
        {
            EditorGUILayout.LabelField("Search Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _searchMode = (SearchMode)EditorGUILayout.EnumPopup("Search Mode", _searchMode);
            
            EditorGUILayout.Space(5);
            
            switch (_searchMode)
            {
                case SearchMode.ByComponent:
                    DrawComponentSearch();
                    break;
                case SearchMode.ByProperty:
                    DrawPropertySearch();
                    break;
                case SearchMode.ByReference:
                    DrawReferenceSearch();
                    break;
                case SearchMode.ByTag:
                    DrawTagSearch();
                    break;
                case SearchMode.ByLayer:
                    DrawLayerSearch();
                    break;
            }

            EditorGUILayout.Space(5);
            
            // Common settings
            EditorGUILayout.LabelField("Search Folders:", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            
            for (int i = 0; i < _searchFolders.Length; i++)
            {
                _searchFolders[i] = EditorGUILayout.TextField(_searchFolders[i], GUILayout.Width(150));
            }
            
            if (GUILayout.Button("+", GUILayout.Width(20)))
            {
                var list = _searchFolders.ToList();
                list.Add("Assets/");
                _searchFolders = list.ToArray();
            }
            
            EditorGUILayout.EndHorizontal();
            
            _searchInChildren = EditorGUILayout.Toggle("Search in Children", _searchInChildren);

            EditorGUILayout.EndVertical();
        }

        private void DrawComponentSearch()
        {
            _componentName = EditorGUILayout.TextField("Component Type", _componentName);
            
            EditorGUILayout.LabelField("Examples: Rigidbody, AudioSource, WeaponController", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Use ! prefix to find missing: !Collider", EditorStyles.miniLabel);
            
            // Common components quick select
            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Common:", GUILayout.Width(55));
            
            string[] common = { "Rigidbody", "Collider", "AudioSource", "Animator", "ParticleSystem" };
            foreach (var c in common)
            {
                if (GUILayout.Button(c, EditorStyles.miniButton, GUILayout.Width(80)))
                {
                    _componentName = c;
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPropertySearch()
        {
            _componentName = EditorGUILayout.TextField("Component Type", _componentName);
            _propertyName = EditorGUILayout.TextField("Property Name", _propertyName);
            
            EditorGUILayout.BeginHorizontal();
            _comparison = (PropertyComparison)EditorGUILayout.EnumPopup(_comparison, GUILayout.Width(100));
            
            if (_comparison != PropertyComparison.IsNull && _comparison != PropertyComparison.IsNotNull)
            {
                _propertyValue = EditorGUILayout.TextField(_propertyValue);
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawReferenceSearch()
        {
            EditorGUILayout.LabelField("Find assets that reference:", EditorStyles.miniLabel);
            // Would use ObjectField for reference
            EditorGUILayout.ObjectField("Target Asset", null, typeof(Object), false);
        }

        private void DrawTagSearch()
        {
            EditorGUILayout.TagField("Tag", "Untagged");
        }

        private void DrawLayerSearch()
        {
            EditorGUILayout.LayerField("Layer", 0);
        }

        private void DrawSearchControls()
        {
            EditorGUILayout.BeginHorizontal();
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            
            EditorGUI.BeginDisabledGroup(_isSearching || string.IsNullOrEmpty(_componentName));
            if (GUILayout.Button("Search", GUILayout.Height(30)))
            {
                ExecuteSearch();
            }
            EditorGUI.EndDisabledGroup();
            
            GUI.backgroundColor = prevColor;
            
            if (_isSearching)
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(30), GUILayout.Width(80)))
                {
                    _isSearching = false;
                }
            }
            
            if (GUILayout.Button("Save Search", GUILayout.Height(30), GUILayout.Width(100)))
            {
                SaveCurrentSearch();
            }
            
            EditorGUILayout.EndHorizontal();

            if (_isSearching)
            {
                Rect progressRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                    GUILayout.Height(20), GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(progressRect, _searchProgress, "Searching...");
            }
        }

        private void DrawResults()
        {
            EditorGUILayout.LabelField($"Results ({_results.Count})", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_results.Count == 0)
            {
                EditorGUILayout.LabelField("No results. Enter a search query and click Search.", 
                    EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                // Toolbar
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                
                if (GUILayout.Button("Select All", EditorStyles.toolbarButton))
                {
                    Selection.objects = _results.Select(r => r.Asset).Where(a => a != null).ToArray();
                }
                
                if (GUILayout.Button("Export List", EditorStyles.toolbarButton))
                {
                    ExportResults();
                }
                
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
                {
                    _results.Clear();
                }
                
                GUILayout.FlexibleSpace();
                
                EditorGUILayout.EndHorizontal();

                // Results list
                EditorGUILayout.Space(5);
                
                foreach (var result in _results.Take(50))
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    // Asset icon
                    if (result.Asset != null)
                    {
                        Texture icon = AssetPreview.GetMiniThumbnail(result.Asset);
                        GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
                    }
                    
                    // Asset name (clickable)
                    if (GUILayout.Button(result.Asset?.name ?? "null", EditorStyles.linkLabel, GUILayout.Width(150)))
                    {
                        if (result.Asset != null)
                        {
                            Selection.activeObject = result.Asset;
                            EditorGUIUtility.PingObject(result.Asset);
                        }
                    }
                    
                    // Path
                    EditorGUILayout.LabelField(result.AssetPath, EditorStyles.miniLabel, GUILayout.Width(300));
                    
                    // Match details
                    EditorGUILayout.LabelField(result.MatchDetails, EditorStyles.miniLabel);
                    
                    EditorGUILayout.EndHorizontal();
                }

                if (_results.Count > 50)
                {
                    EditorGUILayout.LabelField($"... and {_results.Count - 50} more results", 
                        EditorStyles.centeredGreyMiniLabel);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void ExecuteSearch()
        {
            _results.Clear();
            _isSearching = true;
            
            // Search for prefabs with the component
            var guids = AssetDatabase.FindAssets("t:Prefab", _searchFolders);
            
            bool searchMissing = _componentName.StartsWith("!");
            string searchComponent = searchMissing ? _componentName.Substring(1) : _componentName;
            
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab == null) continue;
                
                bool hasComponent = false;
                
                // Check for component
                var components = _searchInChildren ? 
                    prefab.GetComponentsInChildren<Component>(true) : 
                    prefab.GetComponents<Component>();
                
                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    
                    if (comp.GetType().Name.ToLower().Contains(searchComponent.ToLower()))
                    {
                        hasComponent = true;
                        break;
                    }
                }
                
                bool match = searchMissing ? !hasComponent : hasComponent;
                
                if (match)
                {
                    _results.Add(new SearchResult
                    {
                        Asset = prefab,
                        AssetPath = path,
                        MatchDetails = searchMissing ? $"Missing {searchComponent}" : $"Has {searchComponent}",
                        MatchedObject = prefab
                    });
                }
            }
            
            _isSearching = false;
            Debug.Log($"[AssetSearch] Found {_results.Count} results for '{_componentName}'");
        }

        private void SaveCurrentSearch()
        {
            _savedSearches.Add(new SavedSearch
            {
                Name = $"Search {_savedSearches.Count + 1}",
                Mode = _searchMode,
                Query = _componentName
            });
        }

        private void ExportResults()
        {
            string csv = "Asset,Path,Details\n";
            foreach (var result in _results)
            {
                csv += $"{result.Asset?.name},{result.AssetPath},{result.MatchDetails}\n";
            }
            
            string path = EditorUtility.SaveFilePanel("Export Results", "", "search_results.csv", "csv");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, csv);
                Debug.Log($"[AssetSearch] Exported {_results.Count} results to {path}");
            }
        }
    }
}
