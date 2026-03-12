using UnityEngine;
using UnityEditor;
using DIG.Items.Definitions;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Items.Editor.Wizards
{
    /// <summary>
    /// EPIC 14.6 Phase 5 - Configuration Validator
    /// Performs health checks on the equipment system configuration.
    /// </summary>
    public class ConfigurationValidator : EditorWindow
    {
        private List<ValidationResult> _results = new List<ValidationResult>();
        private Vector2 _scrollPos;
        private bool _showWarnings = true;

        [MenuItem("DIG/Wizards/5. Validate: System Health")]
        public static void ShowWindow()
        {
            var window = GetWindow<ConfigurationValidator>("Validator");
            window.minSize = new Vector2(600, 400);
            window.RunChecks();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawReport();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("System Health Report", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            _showWarnings = GUILayout.Toggle(_showWarnings, "Show Warnings", EditorStyles.toolbarButton);
            if (GUILayout.Button("Run Checks", EditorStyles.toolbarButton))
            {
                RunChecks();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawReport()
        {
            if (_results.Count == 0)
            {
                EditorGUILayout.HelpBox("No issues found! System is healthy.", MessageType.Info);
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            bool criticalFound = false;

            foreach (var result in _results)
            {
                if (result.Type == ResultType.Warning && !_showWarnings) continue;
                if (result.Type == ResultType.Error) criticalFound = true;

                DrawResultItem(result);
            }

            if (!criticalFound && _results.Any(r => r.Type == ResultType.Error))
            {
                EditorGUILayout.HelpBox("Good job! No errors found.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawResultItem(ValidationResult result)
        {
            MessageType msgType = result.Type == ResultType.Error ? MessageType.Error : MessageType.Warning;
            
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            // Icon & Message
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(new GUIContent(result.Message, EditorGUIUtility.IconContent(result.Type == ResultType.Error ? "console.erroricon" : "console.warnicon").image), EditorStyles.boldLabel);
            if (result.Context != null)
            {
                if (GUILayout.Button($"Object: {result.Context.name}", EditorStyles.miniButton))
                {
                    EditorGUIUtility.PingObject(result.Context);
                    Selection.activeObject = result.Context;
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void RunChecks()
        {
            _results.Clear();

            CheckSlots();
            CheckCategories();
            // Future: Check Weapons, Player

            // Sort: Errors first
            _results.Sort((a, b) => a.Type == b.Type ? 0 : (a.Type == ResultType.Error ? -1 : 1));
        }

        private void CheckSlots()
        {
            var guids = AssetDatabase.FindAssets("t:EquipmentSlotDefinition");
            var slots = new List<EquipmentSlotDefinition>();
            var ids = new HashSet<string>();
            var indices = new HashSet<int>();

            foreach (var guid in guids)
            {
                var slot = AssetDatabase.LoadAssetAtPath<EquipmentSlotDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (slot == null) continue;
                slots.Add(slot);

                // 1. Duplicate ID
                if (ids.Contains(slot.SlotID))
                {
                    AddError($"Duplicate Slot ID detected: '{slot.SlotID}'", slot);
                }
                else
                {
                    ids.Add(slot.SlotID);
                }

                // 2. Duplicate Index (Warning)
                if (indices.Contains(slot.SlotIndex))
                {
                    AddWarning($"Duplicate Slot Index: {slot.SlotIndex}. This may cause ECS buffer overlap.", slot);
                }
                else
                {
                    indices.Add(slot.SlotIndex);
                }
                
                // 3. Circular Suppression
                // (Simplified check for self-suppression for now)
                foreach(var rule in slot.SuppressionRules)
                {
                    if (rule.WatchSlotID == slot.SlotID)
                    {
                        AddError("Slot suppresses itself (Circular logic).", slot);
                    }
                }
            }
        }

        private void CheckCategories()
        {
            var guids = AssetDatabase.FindAssets("t:WeaponCategoryDefinition");
            foreach (var guid in guids)
            {
                var cat = AssetDatabase.LoadAssetAtPath<WeaponCategoryDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (cat == null) continue;

                // 1. Self-Inheritance
                if (cat.ParentCategory == cat)
                {
                    AddError($"Category '{cat.CategoryID}' inherits from itself.", cat);
                }
                
                // 2. Missing ID
                if (string.IsNullOrEmpty(cat.CategoryID))
                {
                    AddError($"Category asset has no CategoryID.", cat);
                }
            }
        }

        private void AddError(string msg, UnityEngine.Object context)
        {
            _results.Add(new ValidationResult { Type = ResultType.Error, Message = msg, Context = context });
        }

        private void AddWarning(string msg, UnityEngine.Object context)
        {
            _results.Add(new ValidationResult { Type = ResultType.Warning, Message = msg, Context = context });
        }

        private class ValidationResult
        {
            public ResultType Type;
            public string Message;
            public UnityEngine.Object Context;
        }

        private enum ResultType
        {
            Error,
            Warning
        }
    }
}
