using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.SystemsWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 SW-02: Entity Pool module.
    /// Generic entity pooling management, warmup config.
    /// </summary>
    public class EntityPoolModule : ISystemsModule
    {
        private Vector2 _scrollPosition;
        
        // Pool categories
        private List<EntityPoolCategory> _categories = new List<EntityPoolCategory>();
        
        // Global settings
        private int _globalPoolLimit = 1000;
        private bool _autoExpand = true;
        private int _autoExpandIncrement = 10;
        private float _shrinkThreshold = 0.3f;
        private float _shrinkDelay = 30f;

        [System.Serializable]
        private class EntityPoolCategory
        {
            public string Name;
            public Color DisplayColor = Color.gray;
            public List<EntityPoolEntry> Entries = new List<EntityPoolEntry>();
            public bool IsExpanded = true;
        }

        [System.Serializable]
        private class EntityPoolEntry
        {
            public string Name;
            public GameObject Prefab;
            public int InitialSize = 10;
            public int MaxSize = 50;
            public int CurrentActive = 0;
            public int CurrentPooled = 0;
            public float LastAccessTime = 0;
        }

        public EntityPoolModule()
        {
            InitializeDefaultCategories();
        }

        private void InitializeDefaultCategories()
        {
            _categories = new List<EntityPoolCategory>
            {
                new EntityPoolCategory 
                { 
                    Name = "Combat", 
                    DisplayColor = new Color(0.8f, 0.3f, 0.3f),
                    Entries = new List<EntityPoolEntry>
                    {
                        new EntityPoolEntry { Name = "DamageNumber", InitialSize = 30, MaxSize = 100 },
                        new EntityPoolEntry { Name = "HitMarker", InitialSize = 20, MaxSize = 50 },
                        new EntityPoolEntry { Name = "FloatingText", InitialSize = 20, MaxSize = 50 },
                    }
                },
                new EntityPoolCategory 
                { 
                    Name = "Characters", 
                    DisplayColor = new Color(0.3f, 0.6f, 0.8f),
                    Entries = new List<EntityPoolEntry>
                    {
                        new EntityPoolEntry { Name = "Enemy_Basic", InitialSize = 10, MaxSize = 30 },
                        new EntityPoolEntry { Name = "Enemy_Heavy", InitialSize = 5, MaxSize = 15 },
                        new EntityPoolEntry { Name = "NPC_Civilian", InitialSize = 10, MaxSize = 25 },
                    }
                },
                new EntityPoolCategory 
                { 
                    Name = "Items", 
                    DisplayColor = new Color(0.8f, 0.7f, 0.3f),
                    Entries = new List<EntityPoolEntry>
                    {
                        new EntityPoolEntry { Name = "Pickup_Ammo", InitialSize = 20, MaxSize = 50 },
                        new EntityPoolEntry { Name = "Pickup_Health", InitialSize = 15, MaxSize = 30 },
                        new EntityPoolEntry { Name = "Loot_Crate", InitialSize = 10, MaxSize = 20 },
                    }
                },
            };
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Entity Pool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Manage generic entity object pools by category with auto-expansion and shrinking.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawGlobalSettings();
            EditorGUILayout.Space(10);
            DrawPoolOverview();
            EditorGUILayout.Space(10);
            DrawCategories();
            EditorGUILayout.Space(10);
            DrawWarmupSettings();
            EditorGUILayout.Space(10);
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawGlobalSettings()
        {
            EditorGUILayout.LabelField("Global Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _globalPoolLimit = EditorGUILayout.IntField("Global Pool Limit", _globalPoolLimit);
            
            EditorGUILayout.Space(5);
            
            _autoExpand = EditorGUILayout.Toggle("Auto-Expand Pools", _autoExpand);
            if (_autoExpand)
            {
                EditorGUI.indentLevel++;
                _autoExpandIncrement = EditorGUILayout.IntSlider("Expand Increment", _autoExpandIncrement, 1, 50);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
            
            _shrinkThreshold = EditorGUILayout.Slider("Shrink Threshold", _shrinkThreshold, 0.1f, 0.5f);
            EditorGUILayout.LabelField($"Shrink when usage < {_shrinkThreshold * 100:F0}%", EditorStyles.miniLabel);
            
            _shrinkDelay = EditorGUILayout.Slider("Shrink Delay (s)", _shrinkDelay, 5f, 120f);

            EditorGUILayout.EndVertical();
        }

        private void DrawPoolOverview()
        {
            EditorGUILayout.LabelField("Pool Overview", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            int totalEntries = _categories.Sum(c => c.Entries.Count);
            int totalInitial = _categories.Sum(c => c.Entries.Sum(e => e.InitialSize));
            int totalMax = _categories.Sum(c => c.Entries.Sum(e => e.MaxSize));

            EditorGUILayout.BeginHorizontal();
            
            // Category stats
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField($"Categories: {_categories.Count}");
            EditorGUILayout.LabelField($"Total Entries: {totalEntries}");
            EditorGUILayout.EndVertical();
            
            // Size stats
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField($"Initial Objects: {totalInitial}");
            EditorGUILayout.LabelField($"Max Objects: {totalMax}");
            EditorGUILayout.EndVertical();
            
            // Limit check
            EditorGUILayout.BeginVertical();
            bool overLimit = totalMax > _globalPoolLimit;
            GUI.color = overLimit ? Color.red : Color.green;
            EditorGUILayout.LabelField($"Limit: {totalMax}/{_globalPoolLimit}");
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();

            // Category breakdown bar
            Rect barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(20), GUILayout.ExpandWidth(true));
            DrawCategoryBar(barRect);

            EditorGUILayout.EndVertical();
        }

        private void DrawCategoryBar(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

            int totalMax = _categories.Sum(c => c.Entries.Sum(e => e.MaxSize));
            if (totalMax == 0) return;

            float x = rect.x;
            foreach (var category in _categories)
            {
                int categoryMax = category.Entries.Sum(e => e.MaxSize);
                float width = (categoryMax / (float)totalMax) * rect.width;
                
                EditorGUI.DrawRect(new Rect(x, rect.y, width - 1, rect.height), category.DisplayColor);
                
                if (width > 40)
                {
                    GUI.Label(new Rect(x + 2, rect.y + 2, width - 4, rect.height - 4), 
                        category.Name, EditorStyles.miniLabel);
                }
                
                x += width;
            }
        }

        private void DrawCategories()
        {
            EditorGUILayout.LabelField("Pool Categories", EditorStyles.boldLabel);

            for (int i = 0; i < _categories.Count; i++)
            {
                var category = _categories[i];
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Category header
                EditorGUILayout.BeginHorizontal();
                
                // Color indicator
                Rect colorRect = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16));
                EditorGUI.DrawRect(colorRect, category.DisplayColor);
                
                category.IsExpanded = EditorGUILayout.Foldout(category.IsExpanded, category.Name, true);
                
                GUILayout.FlexibleSpace();
                
                EditorGUILayout.LabelField($"{category.Entries.Count} entries", 
                    EditorStyles.miniLabel, GUILayout.Width(60));
                
                if (GUILayout.Button("+", GUILayout.Width(20)))
                {
                    category.Entries.Add(new EntityPoolEntry 
                    { 
                        Name = $"New_{category.Name}_{category.Entries.Count}" 
                    });
                }
                
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _categories.RemoveAt(i);
                    i--;
                    continue;
                }
                
                EditorGUILayout.EndHorizontal();
                
                if (category.IsExpanded)
                {
                    EditorGUI.indentLevel++;
                    
                    // Category settings
                    EditorGUILayout.BeginHorizontal();
                    category.Name = EditorGUILayout.TextField("Name", category.Name);
                    category.DisplayColor = EditorGUILayout.ColorField(category.DisplayColor, GUILayout.Width(50));
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.Space(5);
                    
                    // Entries
                    for (int j = 0; j < category.Entries.Count; j++)
                    {
                        var entry = category.Entries[j];
                        
                        EditorGUILayout.BeginHorizontal();
                        
                        entry.Prefab = (GameObject)EditorGUILayout.ObjectField(
                            entry.Prefab, typeof(GameObject), false, GUILayout.Width(120));
                        
                        if (entry.Prefab != null && string.IsNullOrEmpty(entry.Name))
                        {
                            entry.Name = entry.Prefab.name;
                        }
                        
                        EditorGUILayout.LabelField("Init:", GUILayout.Width(25));
                        entry.InitialSize = EditorGUILayout.IntField(entry.InitialSize, GUILayout.Width(40));
                        
                        EditorGUILayout.LabelField("Max:", GUILayout.Width(30));
                        entry.MaxSize = EditorGUILayout.IntField(entry.MaxSize, GUILayout.Width(40));
                        
                        entry.MaxSize = Mathf.Max(entry.InitialSize, entry.MaxSize);
                        
                        if (GUILayout.Button("×", GUILayout.Width(20)))
                        {
                            category.Entries.RemoveAt(j);
                            j--;
                        }
                        
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Category"))
            {
                _categories.Add(new EntityPoolCategory
                {
                    Name = $"Category_{_categories.Count}",
                    DisplayColor = Random.ColorHSV(0, 1, 0.5f, 1, 0.5f, 1)
                });
            }
        }

        private void DrawWarmupSettings()
        {
            EditorGUILayout.LabelField("Warmup Configuration", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Warmup All"))
            {
                WarmupAll();
            }
            
            if (GUILayout.Button("Warmup Category..."))
            {
                ShowCategoryWarmupMenu();
            }
            
            EditorGUILayout.EndHorizontal();

            int totalInitial = _categories.Sum(c => c.Entries.Sum(e => e.InitialSize));
            EditorGUILayout.LabelField($"Objects to instantiate: {totalInitial}");

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            
            if (GUILayout.Button("Save Configuration", GUILayout.Height(30)))
            {
                SaveConfiguration();
            }
            
            GUI.backgroundColor = prevColor;

            if (GUILayout.Button("Generate Pool Manager", GUILayout.Height(30)))
            {
                GeneratePoolManager();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Export JSON"))
            {
                ExportJSON();
            }
            
            if (GUILayout.Button("Import JSON"))
            {
                ImportJSON();
            }
            
            if (GUILayout.Button("Reset Defaults"))
            {
                InitializeDefaultCategories();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void WarmupAll()
        {
            Debug.Log("[EntityPool] Warmup all pools pending (requires Play Mode)");
        }

        private void ShowCategoryWarmupMenu()
        {
            GenericMenu menu = new GenericMenu();
            foreach (var category in _categories)
            {
                menu.AddItem(new GUIContent(category.Name), false, () => 
                {
                    Debug.Log($"[EntityPool] Warmup category: {category.Name}");
                });
            }
            menu.ShowAsContext();
        }

        private void SaveConfiguration()
        {
            Debug.Log($"[EntityPool] Saved {_categories.Count} categories");
        }

        private void GeneratePoolManager()
        {
            Debug.Log("[EntityPool] Pool manager generation pending");
        }

        private void ExportJSON()
        {
            Debug.Log("[EntityPool] Export pending");
        }

        private void ImportJSON()
        {
            Debug.Log("[EntityPool] Import pending");
        }
    }
}
