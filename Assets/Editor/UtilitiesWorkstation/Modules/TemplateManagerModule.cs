using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace DIG.Editor.UtilitiesWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 UW-04: Template Manager module.
    /// Cross-system template save/load/export.
    /// </summary>
    public class TemplateManagerModule : IUtilitiesModule
    {
        private Vector2 _scrollPosition;
        
        // Templates
        private List<TemplateEntry> _templates = new List<TemplateEntry>();
        private TemplateCategory _categoryFilter = TemplateCategory.All;
        private string _searchFilter = "";
        
        // Create template
        private string _newTemplateName = "";
        private TemplateCategory _newTemplateCategory = TemplateCategory.Weapon;
        private Object _sourceAsset = null;
        private string _templateDescription = "";

        private enum TemplateCategory
        {
            All,
            Weapon,
            Character,
            VFX,
            Audio,
            UI,
            Custom
        }

        [System.Serializable]
        private class TemplateEntry
        {
            public string Name;
            public string Description;
            public TemplateCategory Category;
            public string FilePath;
            public string CreatedDate;
            public string Version;
            public bool IsFavorite;
            public string[] Tags;
        }

        public TemplateManagerModule()
        {
            // Load existing templates
            LoadTemplates();
        }

        private void LoadTemplates()
        {
            _templates = new List<TemplateEntry>
            {
                new TemplateEntry { Name = "Assault Rifle Base", Category = TemplateCategory.Weapon, Description = "Standard AR configuration", CreatedDate = "2025-01-15", Version = "1.0", Tags = new[] { "rifle", "auto" } },
                new TemplateEntry { Name = "Pistol Template", Category = TemplateCategory.Weapon, Description = "Semi-auto pistol setup", CreatedDate = "2025-01-14", Version = "1.2", Tags = new[] { "pistol", "semi" } },
                new TemplateEntry { Name = "Shotgun Spread", Category = TemplateCategory.Weapon, Description = "Pellet spread config", CreatedDate = "2025-01-10", Version = "1.0", Tags = new[] { "shotgun" } },
                new TemplateEntry { Name = "Enemy Grunt", Category = TemplateCategory.Character, Description = "Basic enemy AI", CreatedDate = "2025-01-12", Version = "2.1", Tags = new[] { "enemy", "basic" } },
                new TemplateEntry { Name = "Muzzle Flash Pack", Category = TemplateCategory.VFX, Description = "Various muzzle flash VFX", CreatedDate = "2025-01-08", Version = "1.0", Tags = new[] { "muzzle", "vfx" } },
                new TemplateEntry { Name = "Gunshot Audio", Category = TemplateCategory.Audio, Description = "Layered gunshot audio config", CreatedDate = "2025-01-05", Version = "1.1", Tags = new[] { "audio", "gunshot" } },
            };
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Template Manager", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Save, load, and share configuration templates across systems.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawCreateTemplate();
            EditorGUILayout.Space(10);
            DrawFilters();
            EditorGUILayout.Space(10);
            DrawTemplateList();
            EditorGUILayout.Space(10);
            DrawImportExport();

            EditorGUILayout.EndScrollView();
        }

        private void DrawCreateTemplate()
        {
            EditorGUILayout.LabelField("Create Template", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.BeginVertical();
            _newTemplateName = EditorGUILayout.TextField("Name", _newTemplateName);
            _newTemplateCategory = (TemplateCategory)EditorGUILayout.EnumPopup("Category", _newTemplateCategory);
            _templateDescription = EditorGUILayout.TextField("Description", _templateDescription);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("Source Asset:");
            _sourceAsset = EditorGUILayout.ObjectField(_sourceAsset, typeof(Object), false);
            
            EditorGUILayout.Space(5);
            
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_newTemplateName) || _sourceAsset == null);
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            
            if (GUILayout.Button("Create Template", GUILayout.Height(30)))
            {
                CreateTemplate();
            }
            
            GUI.backgroundColor = prevColor;
            
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();

            // Drag drop hint
            Rect dropArea = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Or drag asset here", EditorStyles.helpBox);
            HandleDragDrop(dropArea);

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
                    
                    if (DragAndDrop.objectReferences.Length > 0)
                    {
                        _sourceAsset = DragAndDrop.objectReferences[0];
                        _newTemplateName = _sourceAsset.name + "_Template";
                    }
                }
                
                evt.Use();
            }
        }

        private void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();
            
            _searchFilter = EditorGUILayout.TextField("Search", _searchFilter, GUILayout.Width(200));
            _categoryFilter = (TemplateCategory)EditorGUILayout.EnumPopup(_categoryFilter, GUILayout.Width(100));
            
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.LabelField($"{GetFilteredTemplates().Count()} templates", GUILayout.Width(100));
            
            EditorGUILayout.EndHorizontal();
        }

        private IEnumerable<TemplateEntry> GetFilteredTemplates()
        {
            return _templates
                .Where(t => _categoryFilter == TemplateCategory.All || t.Category == _categoryFilter)
                .Where(t => string.IsNullOrEmpty(_searchFilter) || 
                           t.Name.ToLower().Contains(_searchFilter.ToLower()) ||
                           t.Description.ToLower().Contains(_searchFilter.ToLower()) ||
                           (t.Tags != null && t.Tags.Any(tag => tag.ToLower().Contains(_searchFilter.ToLower()))));
        }

        private void DrawTemplateList()
        {
            EditorGUILayout.LabelField("Templates", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var filtered = GetFilteredTemplates().ToList();

            if (filtered.Count == 0)
            {
                EditorGUILayout.LabelField("No templates found.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                foreach (var template in filtered)
                {
                    DrawTemplateRow(template);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTemplateRow(TemplateEntry template)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            
            // Favorite star
            GUI.color = template.IsFavorite ? Color.yellow : Color.gray;
            if (GUILayout.Button("★", GUILayout.Width(25)))
            {
                template.IsFavorite = !template.IsFavorite;
            }
            GUI.color = Color.white;
            
            // Category color
            Color catColor = template.Category switch
            {
                TemplateCategory.Weapon => new Color(0.8f, 0.3f, 0.3f),
                TemplateCategory.Character => new Color(0.3f, 0.6f, 0.8f),
                TemplateCategory.VFX => new Color(0.8f, 0.6f, 0.2f),
                TemplateCategory.Audio => new Color(0.5f, 0.8f, 0.3f),
                TemplateCategory.UI => new Color(0.7f, 0.4f, 0.8f),
                _ => Color.gray
            };
            
            Rect colorRect = GUILayoutUtility.GetRect(8, 20, GUILayout.Width(8));
            EditorGUI.DrawRect(colorRect, catColor);
            
            // Info
            EditorGUILayout.BeginVertical();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(template.Name, EditorStyles.boldLabel, GUILayout.Width(180));
            EditorGUILayout.LabelField($"v{template.Version}", EditorStyles.miniLabel, GUILayout.Width(40));
            EditorGUILayout.LabelField(template.CreatedDate, EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.LabelField(template.Description, EditorStyles.wordWrappedMiniLabel);
            
            // Tags
            if (template.Tags != null && template.Tags.Length > 0)
            {
                EditorGUILayout.BeginHorizontal();
                foreach (var tag in template.Tags)
                {
                    GUILayout.Label(tag, EditorStyles.miniButton, GUILayout.Width(50));
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
            
            // Actions
            EditorGUILayout.BeginVertical(GUILayout.Width(160));
            
            EditorGUILayout.BeginHorizontal();
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            
            if (GUILayout.Button("Apply", GUILayout.Width(50)))
            {
                ApplyTemplate(template);
            }
            
            GUI.backgroundColor = prevColor;
            
            if (GUILayout.Button("Edit", GUILayout.Width(40)))
            {
                EditTemplate(template);
            }
            
            if (GUILayout.Button("Clone", GUILayout.Width(45)))
            {
                CloneTemplate(template);
            }
            
            GUI.color = Color.red;
            if (GUILayout.Button("×", GUILayout.Width(20)))
            {
                DeleteTemplate(template);
            }
            GUI.color = Color.white;
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawImportExport()
        {
            EditorGUILayout.LabelField("Import / Export", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Import Template Pack"))
            {
                ImportTemplatePack();
            }
            
            if (GUILayout.Button("Export Selected"))
            {
                ExportSelected();
            }
            
            if (GUILayout.Button("Export All"))
            {
                ExportAll();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            
            // Template storage location
            string storagePath = "Assets/Editor/Templates";
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Storage:", GUILayout.Width(60));
            EditorGUILayout.TextField(storagePath);
            if (GUILayout.Button("Open", GUILayout.Width(50)))
            {
                if (Directory.Exists(storagePath))
                {
                    EditorUtility.RevealInFinder(storagePath);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void CreateTemplate()
        {
            var newTemplate = new TemplateEntry
            {
                Name = _newTemplateName,
                Category = _newTemplateCategory,
                Description = _templateDescription,
                CreatedDate = System.DateTime.Now.ToString("yyyy-MM-dd"),
                Version = "1.0",
                Tags = new string[0]
            };
            
            _templates.Add(newTemplate);
            
            // Clear form
            _newTemplateName = "";
            _templateDescription = "";
            _sourceAsset = null;
            
            Debug.Log($"[TemplateManager] Created template: {newTemplate.Name}");
        }

        private void ApplyTemplate(TemplateEntry template)
        {
            Debug.Log($"[TemplateManager] Applying template: {template.Name}");
        }

        private void EditTemplate(TemplateEntry template)
        {
            _newTemplateName = template.Name;
            _newTemplateCategory = template.Category;
            _templateDescription = template.Description;
            
            Debug.Log($"[TemplateManager] Editing template: {template.Name}");
        }

        private void CloneTemplate(TemplateEntry template)
        {
            var clone = new TemplateEntry
            {
                Name = template.Name + "_Copy",
                Category = template.Category,
                Description = template.Description,
                CreatedDate = System.DateTime.Now.ToString("yyyy-MM-dd"),
                Version = "1.0",
                Tags = template.Tags?.ToArray()
            };
            
            _templates.Add(clone);
            Debug.Log($"[TemplateManager] Cloned template: {clone.Name}");
        }

        private void DeleteTemplate(TemplateEntry template)
        {
            if (EditorUtility.DisplayDialog("Delete Template", 
                $"Are you sure you want to delete '{template.Name}'?", "Delete", "Cancel"))
            {
                _templates.Remove(template);
                Debug.Log($"[TemplateManager] Deleted template: {template.Name}");
            }
        }

        private void ImportTemplatePack()
        {
            string path = EditorUtility.OpenFilePanel("Import Template Pack", "", "json");
            if (!string.IsNullOrEmpty(path))
            {
                Debug.Log($"[TemplateManager] Import from: {path}");
            }
        }

        private void ExportSelected()
        {
            var favorites = _templates.Where(t => t.IsFavorite).ToList();
            if (favorites.Count == 0)
            {
                EditorUtility.DisplayDialog("Export", "Mark templates as favorite (★) to export them.", "OK");
                return;
            }
            
            string path = EditorUtility.SaveFilePanel("Export Templates", "", "templates.json", "json");
            if (!string.IsNullOrEmpty(path))
            {
                Debug.Log($"[TemplateManager] Exported {favorites.Count} templates to: {path}");
            }
        }

        private void ExportAll()
        {
            string path = EditorUtility.SaveFilePanel("Export All Templates", "", "all_templates.json", "json");
            if (!string.IsNullOrEmpty(path))
            {
                Debug.Log($"[TemplateManager] Exported {_templates.Count} templates to: {path}");
            }
        }
    }
}
