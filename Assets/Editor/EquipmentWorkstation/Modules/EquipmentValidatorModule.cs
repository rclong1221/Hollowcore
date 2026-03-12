using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using DIG.Items.Definitions;

namespace DIG.Editor.EquipmentWorkstation
{
    public class EquipmentValidatorModule : IEquipmentModule
    {
        private List<ValResult> _results = new List<ValResult>();
        private Vector2 _scrollPos;
        private bool _hasRun = false;

        private class ValResult
        {
            public MessageType Type;
            public string Message;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("System Validator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Scans the Equipment System for configuration errors, duplicate IDs, and missing references.", MessageType.Info);

            if (GUILayout.Button("Validate Configuration", GUILayout.Height(30)))
            {
                RunValidation();
            }

            if (_hasRun)
            {
                EditorGUILayout.Space();
                DrawResults();
            }
        }

        private void DrawResults()
        {
            if (_results.Count == 0)
            {
                EditorGUILayout.HelpBox("No issues found.", MessageType.Info);
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, EditorStyles.helpBox);
            
            foreach (var res in _results)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(EditorGUIUtility.IconContent(res.Type == MessageType.Error ? "console.erroricon" : "console.warnicon"), GUILayout.Width(20));
                EditorGUILayout.SelectableLabel(res.Message, EditorStyles.wordWrappedLabel, GUILayout.Height(40));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndScrollView();
        }

        private void RunValidation()
        {
            _results.Clear();
            _hasRun = true;

            // 1. Validate WeaponConfigs
            string[] guids = AssetDatabase.FindAssets("t:WeaponConfig");
            HashSet<int> seenIDs = new HashSet<int>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<DIG.Items.Definitions.WeaponConfig>(path);
                
                if (config == null) continue;

                // Check Identity
                if (string.IsNullOrEmpty(config.WeaponName))
                    _results.Add(new ValResult { Type = MessageType.Error, Message = $"Config '{config.name}' has no WeaponName." });
                
                if (config.ItemID <= 0)
                    _results.Add(new ValResult { Type = MessageType.Error, Message = $"Config '{config.name}' has invalid ItemID {config.ItemID}." });
                else
                {
                    if (seenIDs.Contains(config.ItemID))
                        _results.Add(new ValResult { Type = MessageType.Error, Message = $"Duplicate ItemID {config.ItemID} found in '{config.name}'." });
                    else
                        seenIDs.Add(config.ItemID);
                }

                // Check Combos
                if (config.ComboChain != null)
                {
                    for (int i = 0; i < config.ComboChain.Count; i++)
                    {
                        var step = config.ComboChain[i];
                        if (step.Duration <= 0f)
                            _results.Add(new ValResult { Type = MessageType.Error, Message = $"Config '{config.name}' Combo Step {i} has Duration <= 0. This will cause infinite speed." });
                        
                        if (step.InputWindowEnd <= step.InputWindowStart)
                            _results.Add(new ValResult { Type = MessageType.Warning, Message = $"Config '{config.name}' Combo Step {i} has invalid Input Window (End <= Start)." });
                    }
                }
            }
            
            if (_results.Count == 0)
            {
                Debug.Log("[EquipmentValidator] Validation Passed. No issues found.");
            }
        }
    }
}
