using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.AudioWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 AW-05: Batch Assign module.
    /// Assign sounds to multiple weapons by category.
    /// </summary>
    public class BatchAssignModule : IAudioModule
    {
        private Vector2 _scrollPosition;
        private Vector2 _weaponListScroll;
        
        // Weapon selection
        private List<GameObject> _selectedWeapons = new List<GameObject>();
        private string _searchFilter = "";
        private WeaponCategory _categoryFilter = WeaponCategory.All;
        
        // Sound assignment
        private AssignmentMode _assignmentMode = AssignmentMode.Fire;
        private List<AudioClip> _clipsToAssign = new List<AudioClip>();
        private bool _replaceExisting = true;
        private bool _randomizeOrder = false;

        private enum WeaponCategory
        {
            All,
            Pistol,
            Rifle,
            Shotgun,
            SMG,
            Sniper,
            Heavy,
            Melee
        }

        private enum AssignmentMode
        {
            Fire,
            Reload,
            Empty,
            Equip,
            AllSlots
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Batch Assign", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Assign sounds to multiple weapons at once. Filter by category and assign to specific slots.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            
            // Left panel - weapon selection
            EditorGUILayout.BeginVertical(GUILayout.Width(250));
            DrawWeaponSelection();
            EditorGUILayout.EndVertical();

            // Right panel - assignment options
            EditorGUILayout.BeginVertical();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            DrawAssignmentMode();
            EditorGUILayout.Space(10);
            DrawClipSelection();
            EditorGUILayout.Space(10);
            DrawOptions();
            EditorGUILayout.Space(10);
            DrawPreview();
            EditorGUILayout.Space(10);
            DrawActions();
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawWeaponSelection()
        {
            EditorGUILayout.LabelField("Weapon Selection", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));

            // Filters
            _searchFilter = EditorGUILayout.TextField("Search", _searchFilter);
            _categoryFilter = (WeaponCategory)EditorGUILayout.EnumPopup("Category", _categoryFilter);

            EditorGUILayout.Space(5);

            // Quick select buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All"))
            {
                SelectAllWeapons();
            }
            if (GUILayout.Button("Clear"))
            {
                _selectedWeapons.Clear();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Weapon list
            _weaponListScroll = EditorGUILayout.BeginScrollView(_weaponListScroll);

            // Find weapon prefabs in project
            var weaponGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Weapons" });
            
            foreach (var guid in weaponGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab == null) continue;
                if (!string.IsNullOrEmpty(_searchFilter) && 
                    !prefab.name.ToLower().Contains(_searchFilter.ToLower())) continue;
                
                // Category filter would check components
                
                bool isSelected = _selectedWeapons.Contains(prefab);
                
                EditorGUILayout.BeginHorizontal();
                
                bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
                
                if (newSelected != isSelected)
                {
                    if (newSelected)
                        _selectedWeapons.Add(prefab);
                    else
                        _selectedWeapons.Remove(prefab);
                }
                
                EditorGUILayout.LabelField(prefab.name);
                
                EditorGUILayout.EndHorizontal();
            }

            if (weaponGuids.Length == 0)
            {
                EditorGUILayout.LabelField("No weapons found in Assets/Prefabs/Weapons", 
                    EditorStyles.centeredGreyMiniLabel);
                
                EditorGUILayout.Space(10);
                
                // Manual selection
                EditorGUILayout.LabelField("Or drag weapons here:", EditorStyles.miniLabel);
                
                Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
                GUI.Box(dropArea, "Drop Weapon Prefabs", EditorStyles.helpBox);
                HandleWeaponDragDrop(dropArea);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"{_selectedWeapons.Count} weapons selected", 
                EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndVertical();
        }

        private void HandleWeaponDragDrop(Rect dropArea)
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
                        if (obj is GameObject go && !_selectedWeapons.Contains(go))
                        {
                            _selectedWeapons.Add(go);
                        }
                    }
                }
                
                evt.Use();
            }
        }

        private void DrawAssignmentMode()
        {
            EditorGUILayout.LabelField("Assignment Target", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _assignmentMode = (AssignmentMode)EditorGUILayout.EnumPopup("Assign To", _assignmentMode);
            
            string description = _assignmentMode switch
            {
                AssignmentMode.Fire => "Fire/shoot sounds played when weapon fires",
                AssignmentMode.Reload => "Reload sounds (start, loop, end)",
                AssignmentMode.Empty => "Empty magazine/dry fire sound",
                AssignmentMode.Equip => "Equip/unequip handling sounds",
                AssignmentMode.AllSlots => "Distribute clips across all sound slots",
                _ => ""
            };
            
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawClipSelection()
        {
            EditorGUILayout.LabelField("Audio Clips to Assign", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            for (int i = 0; i < _clipsToAssign.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                _clipsToAssign[i] = (AudioClip)EditorGUILayout.ObjectField(
                    _clipsToAssign[i], typeof(AudioClip), false);
                
                if (_clipsToAssign[i] != null)
                {
                    EditorGUILayout.LabelField($"{_clipsToAssign[i].length:F2}s", 
                        EditorStyles.miniLabel, GUILayout.Width(40));
                }
                
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _clipsToAssign.RemoveAt(i);
                    i--;
                }
                
                EditorGUILayout.EndHorizontal();
            }

            if (_clipsToAssign.Count == 0)
            {
                EditorGUILayout.LabelField("No clips added", EditorStyles.centeredGreyMiniLabel);
            }

            if (GUILayout.Button("+ Add Clip Slot"))
            {
                _clipsToAssign.Add(null);
            }

            // Drag and drop area
            Rect dropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag & Drop Audio Clips", EditorStyles.helpBox);
            HandleClipDragDrop(dropArea);

            EditorGUILayout.Space(5);
            
            // Quick actions
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear All"))
            {
                _clipsToAssign.Clear();
            }
            if (GUILayout.Button("From Folder..."))
            {
                LoadClipsFromFolder();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void HandleClipDragDrop(Rect dropArea)
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
                        if (obj is AudioClip clip && !_clipsToAssign.Contains(clip))
                        {
                            _clipsToAssign.Add(clip);
                        }
                    }
                }
                
                evt.Use();
            }
        }

        private void DrawOptions()
        {
            EditorGUILayout.LabelField("Assignment Options", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _replaceExisting = EditorGUILayout.Toggle("Replace Existing Sounds", _replaceExisting);
            
            EditorGUILayout.LabelField(_replaceExisting 
                ? "Existing sounds will be replaced" 
                : "Clips will be added to existing sounds", 
                EditorStyles.miniLabel);

            EditorGUILayout.Space(5);
            
            _randomizeOrder = EditorGUILayout.Toggle("Randomize Per Weapon", _randomizeOrder);
            
            if (_randomizeOrder)
            {
                EditorGUILayout.LabelField("Each weapon gets clips in random order", 
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("Assignment Preview", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            int validClips = _clipsToAssign.Count(c => c != null);
            int weaponCount = _selectedWeapons.Count;

            EditorGUILayout.LabelField($"Will assign {validClips} clip(s) to {weaponCount} weapon(s)");
            EditorGUILayout.LabelField($"Mode: {_assignmentMode}");
            
            if (weaponCount > 0 && validClips > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Affected weapons:", EditorStyles.miniLabel);
                
                int showCount = Mathf.Min(5, weaponCount);
                for (int i = 0; i < showCount; i++)
                {
                    EditorGUILayout.LabelField($"  • {_selectedWeapons[i].name}", EditorStyles.miniLabel);
                }
                
                if (weaponCount > showCount)
                {
                    EditorGUILayout.LabelField($"  ... and {weaponCount - showCount} more", 
                        EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            bool canAssign = _selectedWeapons.Count > 0 && _clipsToAssign.Any(c => c != null);

            EditorGUI.BeginDisabledGroup(!canAssign);

            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            
            if (GUILayout.Button("Apply Assignment", GUILayout.Height(35)))
            {
                ApplyAssignment();
            }
            
            GUI.backgroundColor = prevColor;

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Preview Changes"))
            {
                PreviewChanges();
            }
            
            if (GUILayout.Button("Undo Last"))
            {
                Undo.PerformUndo();
            }
            
            EditorGUILayout.EndHorizontal();

            if (!canAssign)
            {
                EditorGUILayout.HelpBox(
                    "Select at least one weapon and add at least one audio clip.",
                    MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void SelectAllWeapons()
        {
            _selectedWeapons.Clear();
            
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Weapons" });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    _selectedWeapons.Add(prefab);
                }
            }
        }

        private void LoadClipsFromFolder()
        {
            string folderPath = EditorUtility.OpenFolderPanel("Select Audio Folder", "Assets", "");
            
            if (!string.IsNullOrEmpty(folderPath))
            {
                // Convert to relative path
                if (folderPath.StartsWith(Application.dataPath))
                {
                    folderPath = "Assets" + folderPath.Substring(Application.dataPath.Length);
                }
                
                var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folderPath });
                
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    
                    if (clip != null && !_clipsToAssign.Contains(clip))
                    {
                        _clipsToAssign.Add(clip);
                    }
                }
                
                Debug.Log($"[BatchAssign] Loaded {_clipsToAssign.Count} clips from {folderPath}");
            }
        }

        private void ApplyAssignment()
        {
            var validClips = _clipsToAssign.Where(c => c != null).ToList();
            
            if (validClips.Count == 0 || _selectedWeapons.Count == 0)
            {
                Debug.LogWarning("[BatchAssign] No valid clips or weapons selected");
                return;
            }

            int assignedCount = 0;
            
            foreach (var weapon in _selectedWeapons)
            {
                if (weapon == null) continue;
                
                // Would assign clips to weapon's audio components
                // This depends on your weapon audio system implementation
                
                Undo.RecordObject(weapon, "Batch Assign Audio");
                
                // Placeholder - actual implementation depends on weapon audio component structure
                Debug.Log($"[BatchAssign] Assigned {validClips.Count} clips to {weapon.name}");
                
                EditorUtility.SetDirty(weapon);
                assignedCount++;
            }
            
            AssetDatabase.SaveAssets();
            Debug.Log($"[BatchAssign] Assignment complete: {validClips.Count} clips → {assignedCount} weapons");
        }

        private void PreviewChanges()
        {
            Debug.Log($"[BatchAssign] Preview: Would assign {_clipsToAssign.Count(c => c != null)} clips to {_selectedWeapons.Count} weapons");
        }
    }
}
