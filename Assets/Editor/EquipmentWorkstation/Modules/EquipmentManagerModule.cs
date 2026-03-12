using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DIG.Editor.EquipmentWorkstation
{
    public class EquipmentManagerModule : IEquipmentModule
    {
        private Vector2 _scrollPos;
        private List<string> _slots = new List<string> { "Right Hand (Slot 0)", "Left Hand (Slot 1)", "Back (Slot 2)" };
        private int _selectedSlotIndex = -1;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Equipment Slots", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Manage Equipment Slots and their definitions.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();

            // Left Pane: List
            DrawSlotList();

            EditorGUILayout.Space();

            // Right Pane: Inspector
            DrawSlotInspector();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSlotList()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Width(200), GUILayout.ExpandHeight(true));
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            for (int i = 0; i < _slots.Count; i++)
            {
                if (GUILayout.Button(_slots[i], i == _selectedSlotIndex ? EditorStyles.helpBox : EditorStyles.label))
                {
                    _selectedSlotIndex = i;
                }
            }

            EditorGUILayout.EndScrollView();
            
            if (GUILayout.Button("+ New Slot"))
            {
                _slots.Add($"New Slot {_slots.Count}");
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSlotInspector()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.ExpandHeight(true));
            
            if (_selectedSlotIndex >= 0 && _selectedSlotIndex < _slots.Count)
            {
                EditorGUILayout.LabelField("Slot Settings", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                string name = _slots[_selectedSlotIndex];
                _slots[_selectedSlotIndex] = EditorGUILayout.TextField("Name", name);
                
                EditorGUILayout.IntField("Slot ID", _selectedSlotIndex);
                EditorGUILayout.Toggle("Allow Multiple Items", false);
                
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Suppression Rules", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Rules for hiding other slots when this one is active.", MessageType.None);
                // Placeholder list
                GUILayout.Label("- Suppress 'Left Hand' when 'Two-Handed' is active");
                
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Delete Slot", GUILayout.Width(100)))
                {
                    _slots.RemoveAt(_selectedSlotIndex);
                    _selectedSlotIndex = -1;
                }
            }
            else
            {
                EditorGUILayout.LabelField("Select a slot to edit.", EditorStyles.centeredGreyMiniLabel);
            }
            
            EditorGUILayout.EndVertical();
        }
    }
}
