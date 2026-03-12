using UnityEngine;
using UnityEditor;
using DIG.Items.Definitions;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Items.Editor.Wizards
{
    /// <summary>
    /// Visual dashboard for managing EquipmentSlotDefinitions.
    /// EPIC 14.6 Phase 3
    /// </summary>
    public class SlotConfigurationDashboard : EditorWindow
    {
        private List<EquipmentSlotDefinition> _slots = new List<EquipmentSlotDefinition>();
        private EquipmentSlotDefinition _selectedSlot;
        private UnityEditor.Editor _cachedEditor;
        
        private Vector2 _listScrollPos;
        private Vector2 _inspectorScrollPos;
        
        private string _newSlotId = "";
        private bool _isCreating = false;

        [MenuItem("DIG/Wizards/3. Manage: Slot Dashboard")]
        public static void ShowWindow()
        {
            var window = GetWindow<SlotConfigurationDashboard>("Slot Dashboard");
            window.minSize = new Vector2(800, 500);
        }

        private void OnEnable()
        {
            // Don't auto-scan on enable - let user trigger refresh manually
        }

        private void OnGUI()
        {
            DrawHeader();
            
            EditorGUILayout.BeginHorizontal();
            
            // Left Panel: List
            DrawSlotList();
            
            // Divider
            GUILayout.Box("", GUILayout.Width(1), GUILayout.ExpandHeight(true));
            
            // Right Panel: Inspector
            DrawSlotInspector();
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Slot Configuration Dashboard", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            {
                RefreshSlotList();
            }
            if (GUILayout.Button("Create New Slot", EditorStyles.toolbarButton))
            {
                _isCreating = !_isCreating;
                _selectedSlot = null; // Deselect to focus on creation
            }
            EditorGUILayout.EndHorizontal();

            if (_isCreating)
            {
                DrawCreationPanel();
            }
        }

        private void DrawCreationPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Create New Slot", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            _newSlotId = EditorGUILayout.TextField("Slot ID (e.g. Helmet)", _newSlotId);
            if (GUILayout.Button("Create", GUILayout.Width(80)))
            {
                CreateNewSlot();
            }
            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                _isCreating = false;
                _newSlotId = "";
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        private void DrawSlotList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            
            EditorGUILayout.LabelField($"Available Slots ({_slots.Count})", EditorStyles.boldLabel);
            if (_slots.Count == 0)
            {
                EditorGUILayout.HelpBox("Click 'Refresh' to scan for slot definitions.", MessageType.Info);
            }
            EditorGUILayout.Space(5);

            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos);

            foreach (var slot in _slots)
            {
                if (slot == null) continue;

                DrawSlotCard(slot);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSlotCard(EquipmentSlotDefinition slot)
        {
            // Card Style
            var style = new GUIStyle(EditorStyles.helpBox);
            if (_selectedSlot == slot)
            {
                style.normal.background = Texture2D.grayTexture;
                style.normal.textColor = Color.white;
            }

            EditorGUILayout.BeginVertical(style);
            
            EditorGUILayout.BeginHorizontal();
            
            // Icon / Status
            Color statusColor = slot.RequiredModifier == ModifierKey.None ? Color.green : Color.cyan;
            var prevColor = GUI.color;
            GUI.color = statusColor;
            GUILayout.Box(GUIContent.none, GUILayout.Width(4), GUILayout.Height(30)); 
            GUI.color = prevColor;
            
            // Content
            if (GUILayout.Button(new GUIContent(slot.DisplayName, AssetDatabase.GetAssetPath(slot)), EditorStyles.label))
            {
                SelectSlot(slot);
            }
            
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.LabelField($"Index: {slot.SlotIndex}", EditorStyles.miniLabel, GUILayout.Width(50));
            
            EditorGUILayout.EndHorizontal();
            
            // Details Row
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(slot.RequiredModifier.ToString(), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (slot.SuppressionRules != null && slot.SuppressionRules.Count > 0)
            {
                EditorGUILayout.LabelField($"Rules: {slot.SuppressionRules.Count}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            
            // Small spacing
            EditorGUILayout.Space(2);
        }

        private void DrawSlotInspector()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            
            if (_selectedSlot != null)
            {
                // Safety check: verify the object still exists in the AssetDatabase/Memory
                if (_selectedSlot == null) 
                {
                    _selectedSlot = null;
                    return;
                }

                _inspectorScrollPos = EditorGUILayout.BeginScrollView(_inspectorScrollPos);
                
                EditorGUILayout.LabelField($"Editing: {_selectedSlot.SlotID}", EditorStyles.largeLabel);
                EditorGUILayout.Space(10);

                // Ensure target is valid before creating editor
                if (_cachedEditor == null || _cachedEditor.target != _selectedSlot)
                {
                    UnityEditor.Editor.CreateCachedEditor(_selectedSlot, null, ref _cachedEditor);
                }

                if (_cachedEditor != null && _cachedEditor.target != null)
                {
                    EditorGUI.BeginChangeCheck();
                    try 
                    {
                        _cachedEditor.OnInspectorGUI();
                    }
                    catch (System.Exception e)
                    {
                        // Fallback if inspector crashes (e.g. SerializedObject issue)
                        Debug.LogWarning($"[SlotDashboard] Inspector error: {e.Message}. Resetting selection.");
                        _selectedSlot = null;
                        _cachedEditor = null;
                        EditorGUILayout.EndScrollView();
                        return;
                    }

                    if (EditorGUI.EndChangeCheck() && _selectedSlot != null)
                    {
                        EditorUtility.SetDirty(_selectedSlot);
                    }
                }
                
                EditorGUILayout.Space(20);
                DrawSuppressionVisualizer();

                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.LabelField("Select a slot to edit or create a new one.", EditorStyles.centeredGreyMiniLabel);
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawSuppressionVisualizer()
        {
            if (_selectedSlot == null) return;
            
            EditorGUILayout.LabelField("Suppression Rules Visualization", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_selectedSlot.SuppressionRules == null || _selectedSlot.SuppressionRules.Count == 0)
            {
                EditorGUILayout.LabelField("No suppression rules defined.", EditorStyles.miniLabel);
            }
            else
            {
                foreach (var rule in _selectedSlot.SuppressionRules)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                    EditorGUILayout.LabelField($"IF {rule.WatchSlotID} IS {rule.Condition}", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField($"THEN {rule.Action}");
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.HelpBox($"This slot will be {rule.Action} when {rule.WatchSlotID} contains a {rule.Condition} item.", MessageType.Info);
                    EditorGUILayout.Space(5);
                }
            }
            
            EditorGUILayout.EndVertical();
        }

        private void SelectSlot(EquipmentSlotDefinition slot)
        {
            _selectedSlot = slot;
            _isCreating = false;
            // Focus in Project window too
            EditorGUIUtility.PingObject(slot); 
        }

        private void RefreshSlotList()
        {
            _slots.Clear();
            var guids = AssetDatabase.FindAssets("t:EquipmentSlotDefinition");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var slot = AssetDatabase.LoadAssetAtPath<EquipmentSlotDefinition>(path);
                if (slot != null)
                {
                    _slots.Add(slot);
                }
            }
            
            // Sort by index
            _slots.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
        }

        private void CreateNewSlot()
        {
            if (string.IsNullOrEmpty(_newSlotId))
            {
                EditorUtility.DisplayDialog("Error", "Slot ID cannot be empty.", "OK");
                return;
            }

            string folder = "Assets/Content/Equipment/Definitions/Slots";
            if (!System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            var newSlot = CreateInstance<EquipmentSlotDefinition>();
            newSlot.SlotID = _newSlotId;
            newSlot.DisplayName = _newSlotId;
            newSlot.SlotIndex = _slots.Count > 0 ? _slots.Max(s => s.SlotIndex) + 1 : 0; // Auto-increment index

            string path = $"{folder}/{_newSlotId}.asset";
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            
            AssetDatabase.CreateAsset(newSlot, path);
            AssetDatabase.SaveAssets();
            
            RefreshSlotList();
            SelectSlot(newSlot);
            _isCreating = false;
            _newSlotId = "";
            
            Debug.Log($"[SlotDashboard] Created new slot: {path}");
        }
    }
}
