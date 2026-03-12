using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Player.Authoring;
using Player.Components;

namespace DIG.Editor.CharacterWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 CW-02: Hitbox Copy module.
    /// Copy hitbox rig between characters, template system.
    /// </summary>
    public class HitboxCopyModule : ICharacterModule
    {
        private GameObject _sourceCharacter;
        private GameObject _targetCharacter;
        private Vector2 _scrollPosition;
        private List<HitboxTemplate> _templates = new List<HitboxTemplate>();
        private string _newTemplateName = "NewTemplate";
        private int _selectedTemplateIndex = -1;
        
        private bool _copyMultipliers = true;
        private bool _copyRegions = true;
        private bool _copyShapes = true;
        private bool _matchByName = true; // vs match by hierarchy position
        
        private List<HitboxMapping> _mappings = new List<HitboxMapping>();
        private bool _hasMappings = false;
        
        [System.Serializable]
        private class HitboxTemplate
        {
            public string Name;
            public string FilePath;
            public List<HitboxData> Hitboxes = new List<HitboxData>();
        }
        
        [System.Serializable]
        private class HitboxData
        {
            public string BonePath;
            public string BoneName;
            public HitboxRegion Region;
            public float DamageMultiplier;
            public Vector3 Center;
            public float Radius;
            public float Height;
            public int Direction; // CapsuleCollider direction
        }
        
        private class HitboxMapping
        {
            public HitboxAuthoring Source;
            public Transform TargetBone;
            public string SourcePath;
            public string TargetPath;
            public bool Matched;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Hitbox Copy & Templates", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Copy hitbox configurations between characters or save/load templates.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawCharacterCopy();
            EditorGUILayout.Space(15);
            DrawTemplateSystem();

            EditorGUILayout.EndScrollView();
        }

        private void DrawCharacterCopy()
        {
            EditorGUILayout.LabelField("Character-to-Character Copy", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _sourceCharacter = (GameObject)EditorGUILayout.ObjectField(
                "Source Character", _sourceCharacter, typeof(GameObject), true);
            _targetCharacter = (GameObject)EditorGUILayout.ObjectField(
                "Target Character", _targetCharacter, typeof(GameObject), true);

            EditorGUILayout.Space(5);
            
            EditorGUILayout.LabelField("Copy Settings", EditorStyles.miniLabel);
            _copyMultipliers = EditorGUILayout.Toggle("Copy Damage Multipliers", _copyMultipliers);
            _copyRegions = EditorGUILayout.Toggle("Copy Region Types", _copyRegions);
            _copyShapes = EditorGUILayout.Toggle("Copy Collider Shapes", _copyShapes);
            
            EditorGUILayout.Space(5);
            _matchByName = EditorGUILayout.Toggle("Match Bones by Name", _matchByName);
            EditorGUILayout.HelpBox(
                _matchByName 
                    ? "Bones matched by name (e.g., 'Spine' to 'Spine')" 
                    : "Bones matched by hierarchy position",
                MessageType.None);

            EditorGUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(_sourceCharacter == null || _targetCharacter == null);
            
            if (GUILayout.Button("Analyze Mapping", GUILayout.Height(25)))
            {
                AnalyzeMapping();
            }

            if (_hasMappings)
            {
                DrawMappingPreview();
                
                EditorGUILayout.Space(5);
                if (GUILayout.Button("Apply Copy", GUILayout.Height(30)))
                {
                    ApplyCopy();
                }
            }
            
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
        }

        private void DrawMappingPreview()
        {
            EditorGUILayout.LabelField("Bone Mappings", EditorStyles.miniLabel);
            
            int matched = _mappings.Count(m => m.Matched);
            int unmatched = _mappings.Count - matched;
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Matched: {matched}", GUILayout.Width(100));
            EditorGUILayout.LabelField($"Unmatched: {unmatched}", 
                unmatched > 0 ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            // Show first few mappings
            int displayCount = Mathf.Min(5, _mappings.Count);
            for (int i = 0; i < displayCount; i++)
            {
                var m = _mappings[i];
                Color prevColor = GUI.color;
                GUI.color = m.Matched ? Color.green : Color.yellow;
                EditorGUILayout.LabelField($"  {m.SourcePath} → {(m.Matched ? m.TargetPath : "?")}");
                GUI.color = prevColor;
            }
            
            if (_mappings.Count > displayCount)
            {
                EditorGUILayout.LabelField($"  ... and {_mappings.Count - displayCount} more");
            }
        }

        private void AnalyzeMapping()
        {
            _mappings.Clear();
            _hasMappings = false;

            if (_sourceCharacter == null || _targetCharacter == null) return;

            var sourceHitboxes = _sourceCharacter.GetComponentsInChildren<HitboxAuthoring>(true);
            
            foreach (var source in sourceHitboxes)
            {
                var mapping = new HitboxMapping
                {
                    Source = source,
                    SourcePath = GetBonePath(source.transform, _sourceCharacter.transform)
                };

                // Try to find matching bone in target
                if (_matchByName)
                {
                    mapping.TargetBone = FindBoneByName(_targetCharacter.transform, source.transform.name);
                }
                else
                {
                    mapping.TargetBone = FindBoneByPath(_targetCharacter.transform, mapping.SourcePath);
                }

                mapping.Matched = mapping.TargetBone != null;
                if (mapping.Matched)
                {
                    mapping.TargetPath = GetBonePath(mapping.TargetBone, _targetCharacter.transform);
                }

                _mappings.Add(mapping);
            }

            _hasMappings = true;
            Debug.Log($"[HitboxCopy] Analyzed {_mappings.Count} hitboxes, {_mappings.Count(m => m.Matched)} matched.");
        }

        private void ApplyCopy()
        {
            if (!_hasMappings) return;

            Undo.SetCurrentGroupName("Copy Hitboxes");
            int undoGroup = Undo.GetCurrentGroup();

            int copied = 0;
            foreach (var mapping in _mappings.Where(m => m.Matched))
            {
                Undo.RecordObject(mapping.TargetBone.gameObject, "Copy Hitbox");

                // Get or add HitboxAuthoring
                var targetAuth = mapping.TargetBone.GetComponent<HitboxAuthoring>();
                if (targetAuth == null)
                {
                    targetAuth = Undo.AddComponent<HitboxAuthoring>(mapping.TargetBone.gameObject);
                }

                // Copy settings
                if (_copyMultipliers)
                {
                    targetAuth.DamageMultiplier = mapping.Source.DamageMultiplier;
                }
                if (_copyRegions)
                {
                    targetAuth.Region = mapping.Source.Region;
                }

                // Copy collider shape
                if (_copyShapes)
                {
                    CopyCollider(mapping.Source.gameObject, mapping.TargetBone.gameObject);
                }

                EditorUtility.SetDirty(mapping.TargetBone.gameObject);
                copied++;
            }

            Undo.CollapseUndoOperations(undoGroup);
            
            Debug.Log($"[HitboxCopy] Copied {copied} hitboxes to {_targetCharacter.name}");
        }

        private void CopyCollider(GameObject source, GameObject target)
        {
            // Check for CapsuleCollider
            var sourceCapsule = source.GetComponent<CapsuleCollider>();
            if (sourceCapsule != null)
            {
                var targetCapsule = target.GetComponent<CapsuleCollider>();
                if (targetCapsule == null)
                {
                    targetCapsule = Undo.AddComponent<CapsuleCollider>(target);
                }
                targetCapsule.center = sourceCapsule.center;
                targetCapsule.radius = sourceCapsule.radius;
                targetCapsule.height = sourceCapsule.height;
                targetCapsule.direction = sourceCapsule.direction;
                targetCapsule.isTrigger = true;
                return;
            }

            // Check for SphereCollider
            var sourceSphere = source.GetComponent<SphereCollider>();
            if (sourceSphere != null)
            {
                var targetSphere = target.GetComponent<SphereCollider>();
                if (targetSphere == null)
                {
                    targetSphere = Undo.AddComponent<SphereCollider>(target);
                }
                targetSphere.center = sourceSphere.center;
                targetSphere.radius = sourceSphere.radius;
                targetSphere.isTrigger = true;
                return;
            }

            // Check for BoxCollider
            var sourceBox = source.GetComponent<BoxCollider>();
            if (sourceBox != null)
            {
                var targetBox = target.GetComponent<BoxCollider>();
                if (targetBox == null)
                {
                    targetBox = Undo.AddComponent<BoxCollider>(target);
                }
                targetBox.center = sourceBox.center;
                targetBox.size = sourceBox.size;
                targetBox.isTrigger = true;
            }
        }

        private void DrawTemplateSystem()
        {
            EditorGUILayout.LabelField("Template System", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Template list
            RefreshTemplates();
            
            EditorGUILayout.LabelField($"Saved Templates ({_templates.Count})", EditorStyles.miniLabel);
            
            for (int i = 0; i < _templates.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                bool selected = i == _selectedTemplateIndex;
                if (GUILayout.Toggle(selected, _templates[i].Name, EditorStyles.radioButton))
                {
                    _selectedTemplateIndex = i;
                }
                
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"{_templates[i].Hitboxes.Count} hitboxes", 
                    EditorStyles.miniLabel, GUILayout.Width(80));
                
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    DeleteTemplate(i);
                }
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(10);

            // Save new template
            EditorGUILayout.LabelField("Save Template", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            _newTemplateName = EditorGUILayout.TextField(_newTemplateName);
            
            EditorGUI.BeginDisabledGroup(_sourceCharacter == null);
            if (GUILayout.Button("Save", GUILayout.Width(60)))
            {
                SaveTemplate(_sourceCharacter, _newTemplateName);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Apply template
            EditorGUI.BeginDisabledGroup(_selectedTemplateIndex < 0 || _targetCharacter == null);
            if (GUILayout.Button("Apply Selected Template", GUILayout.Height(25)))
            {
                ApplyTemplate(_templates[_selectedTemplateIndex], _targetCharacter);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
        }

        private void RefreshTemplates()
        {
            string templatePath = "Assets/Editor/CharacterWorkstation/Templates";
            if (!Directory.Exists(templatePath))
            {
                Directory.CreateDirectory(templatePath);
            }

            var files = Directory.GetFiles(templatePath, "*.json");
            
            // Only reload if count changed
            if (files.Length != _templates.Count)
            {
                _templates.Clear();
                foreach (var file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var template = JsonUtility.FromJson<HitboxTemplate>(json);
                        template.FilePath = file;
                        _templates.Add(template);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Failed to load template {file}: {e.Message}");
                    }
                }
            }
        }

        private void SaveTemplate(GameObject character, string templateName)
        {
            var template = new HitboxTemplate { Name = templateName };
            
            var hitboxes = character.GetComponentsInChildren<HitboxAuthoring>(true);
            foreach (var hb in hitboxes)
            {
                var data = new HitboxData
                {
                    BonePath = GetBonePath(hb.transform, character.transform),
                    BoneName = hb.transform.name,
                    Region = hb.Region,
                    DamageMultiplier = hb.DamageMultiplier
                };

                // Save collider data
                var capsule = hb.GetComponent<CapsuleCollider>();
                if (capsule != null)
                {
                    data.Center = capsule.center;
                    data.Radius = capsule.radius;
                    data.Height = capsule.height;
                    data.Direction = capsule.direction;
                }
                else
                {
                    var sphere = hb.GetComponent<SphereCollider>();
                    if (sphere != null)
                    {
                        data.Center = sphere.center;
                        data.Radius = sphere.radius;
                        data.Height = 0; // Sphere indicator
                    }
                }

                template.Hitboxes.Add(data);
            }

            string templatePath = "Assets/Editor/CharacterWorkstation/Templates";
            if (!Directory.Exists(templatePath))
            {
                Directory.CreateDirectory(templatePath);
            }

            string filePath = Path.Combine(templatePath, $"{templateName}.json");
            string json = JsonUtility.ToJson(template, true);
            File.WriteAllText(filePath, json);
            
            AssetDatabase.Refresh();
            Debug.Log($"[HitboxCopy] Saved template '{templateName}' with {template.Hitboxes.Count} hitboxes.");
            
            _templates.Clear(); // Force refresh
        }

        private void DeleteTemplate(int index)
        {
            if (index < 0 || index >= _templates.Count) return;
            
            var template = _templates[index];
            if (File.Exists(template.FilePath))
            {
                File.Delete(template.FilePath);
                AssetDatabase.Refresh();
            }
            
            _templates.RemoveAt(index);
            if (_selectedTemplateIndex >= _templates.Count)
            {
                _selectedTemplateIndex = _templates.Count - 1;
            }
        }

        private void ApplyTemplate(HitboxTemplate template, GameObject target)
        {
            Undo.SetCurrentGroupName("Apply Hitbox Template");
            int undoGroup = Undo.GetCurrentGroup();

            int applied = 0;
            foreach (var data in template.Hitboxes)
            {
                // Find matching bone
                Transform bone = _matchByName 
                    ? FindBoneByName(target.transform, data.BoneName)
                    : FindBoneByPath(target.transform, data.BonePath);

                if (bone == null) continue;

                Undo.RecordObject(bone.gameObject, "Apply Hitbox");

                // Add HitboxAuthoring
                var auth = bone.GetComponent<HitboxAuthoring>();
                if (auth == null)
                {
                    auth = Undo.AddComponent<HitboxAuthoring>(bone.gameObject);
                }

                auth.Region = data.Region;
                auth.DamageMultiplier = data.DamageMultiplier;

                // Add collider
                if (data.Height > 0)
                {
                    var capsule = bone.GetComponent<CapsuleCollider>();
                    if (capsule == null)
                    {
                        capsule = Undo.AddComponent<CapsuleCollider>(bone.gameObject);
                    }
                    capsule.center = data.Center;
                    capsule.radius = data.Radius;
                    capsule.height = data.Height;
                    capsule.direction = data.Direction;
                    capsule.isTrigger = true;
                }
                else if (data.Radius > 0)
                {
                    var sphere = bone.GetComponent<SphereCollider>();
                    if (sphere == null)
                    {
                        sphere = Undo.AddComponent<SphereCollider>(bone.gameObject);
                    }
                    sphere.center = data.Center;
                    sphere.radius = data.Radius;
                    sphere.isTrigger = true;
                }

                EditorUtility.SetDirty(bone.gameObject);
                applied++;
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"[HitboxCopy] Applied template to {applied}/{template.Hitboxes.Count} bones.");
        }

        private string GetBonePath(Transform bone, Transform root)
        {
            List<string> parts = new List<string>();
            Transform current = bone;
            
            while (current != null && current != root)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }
            
            return string.Join("/", parts);
        }

        private Transform FindBoneByName(Transform root, string name)
        {
            if (root.name == name) return root;
            
            foreach (Transform child in root)
            {
                var found = FindBoneByName(child, name);
                if (found != null) return found;
            }
            
            return null;
        }

        private Transform FindBoneByPath(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path)) return root;
            
            string[] parts = path.Split('/');
            Transform current = root;
            
            foreach (string part in parts)
            {
                current = current.Find(part);
                if (current == null) return null;
            }
            
            return current;
        }
    }
}
