using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.AnimationWorkstation
{
    public class AnimationFixerModule : IWorkstationModule
    {
        private AnimatorController _controller;
        private int _selectedRuleIndex = 0;
        private string[] _ruleNames;
        private List<FixRule> _rules;
        
        // Status
        private bool _hasChecked = false;
        private string _statusMessage = "";
        private MessageType _statusType = MessageType.None;

        public AnimationFixerModule()
        {
            InitializeRules();
        }

        private void InitializeRules()
        {
            _rules = new List<FixRule>
            {
                new FixRule
                {
                    Name = "Bow Transitions (ID 4)",
                    Description = "Ensures 'AnyState -> Bow' transitions exist in the Upperbody Layer.\nRequired for correctly entering the Bow state from any other state.",
                    LayerName = "Upperbody Layer",
                    TargetSubMachine = "Bow",
                    ItemID = 4,
                    TransitionDuration = 0.2f
                },
                new FixRule
                {
                    Name = "Knife Transitions (ID 23)",
                    Description = "Ensures 'AnyState -> Knife' transitions exist in the Upperbody Layer.\nRequired for rapid melee switching.",
                    LayerName = "Upperbody Layer",
                    TargetSubMachine = "Knife",
                    ItemID = 23,
                    TransitionDuration = 0.1f
                },
                new FixRule
                {
                    Name = "Swimming Pack (IDs 301-304)",
                    Description = "Adds 'AnyState -> Swim/Dive' transitions in the Base Layer.\nEssential for the Swimming ability pack.",
                    LayerName = "Base Layer", // Usually Base Layer for abilities
                    TargetSubMachine = "", 
                    IsSwimPack = true
                }
            };

            _ruleNames = _rules.Select(r => r.Name).ToArray();
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Transition Fixer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select a standardization rule to check and repair Animator transitions.", MessageType.Info);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _controller = (AnimatorController)EditorGUILayout.ObjectField("Target Controller", _controller, typeof(AnimatorController), false);

            if (_controller == null)
            {
                 EditorGUILayout.Space();
                 if (GUILayout.Button("Auto-Find ClimbingDemo.controller", GUILayout.Height(30)))
                 {
                     string[] guids = AssetDatabase.FindAssets("ClimbingDemo t:AnimatorController");
                     if (guids.Length > 0)
                     {
                         string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                         _controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                         CheckStatus(_rules[_selectedRuleIndex]); // Auto-check
                     }
                 }
                 EditorGUILayout.EndVertical();
                 return;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // Rule Selection
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.BeginChangeCheck();
            _selectedRuleIndex = EditorGUILayout.Popup("Rule Set", _selectedRuleIndex, _ruleNames);
            var rule = _rules[_selectedRuleIndex];
            if (EditorGUI.EndChangeCheck())
            {
                _hasChecked = false;
                CheckStatus(rule);
            }

            EditorGUILayout.LabelField("Description:", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(rule.Description, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // Status Area
            if (_hasChecked)
            {
                EditorGUILayout.HelpBox(_statusMessage, _statusType);
            }
            else
            {
                if (GUILayout.Button("Check Status"))
                {
                    CheckStatus(rule);
                }
            }

            EditorGUILayout.Space();

            // Action
            EditorGUI.BeginDisabledGroup(_statusType == MessageType.Info && _hasChecked); // Disable if all good
            if (GUILayout.Button("Apply Fixes", GUILayout.Height(40)))
            {
                ExecuteRule(rule);
                CheckStatus(rule); // Re-check after fix
            }
            EditorGUI.EndDisabledGroup();
        }

        private void CheckStatus(FixRule rule)
        {
            _hasChecked = true;
            
            var layer = FindLayer(_controller, rule.LayerName);
            if (layer == null)
            {
                _statusMessage = $"Layer '{rule.LayerName}' not found in controller.";
                _statusType = MessageType.Error;
                return;
            }

            // Simple heuristic check
            // Count AnyState transitions matching specific criteria
            int matchCount = 0;
            if (rule.IsSwimPack)
            {
                 // Swim check logic (simplified)
                 matchCount = layer.stateMachine.anyStateTransitions.Count(t => t.destinationState != null && (t.destinationState.name.Contains("Swim") || t.destinationState.name.Contains("Dive")));
                 if (matchCount > 0)
                 {
                     _statusMessage = $"Create: Found {matchCount} existing Swim transitions. Looks OK.";
                     _statusType = MessageType.Info;
                 }
                 else
                 {
                     _statusMessage = "Missing Swim Pack transitions.";
                     _statusType = MessageType.Warning;
                 }
            }
            else
            {
                // Weapon check
                 matchCount = layer.stateMachine.anyStateTransitions.Count(t => 
                    t.conditions.Any(c => c.parameter == "Slot0ItemID" && (int)c.threshold == rule.ItemID));

                 if (matchCount >= 2) // Expecting at least Equip/Idle/etc
                 {
                     _statusMessage = $"Status: Found {matchCount} transitions for ItemID {rule.ItemID}. System appears healthy.";
                     _statusType = MessageType.Info; // Good
                 }
                 else if (matchCount > 0)
                 {
                     _statusMessage = $"Status: Found partial setup ({matchCount} transitions). Might need repair.";
                     _statusType = MessageType.Warning;
                 }
                 else
                 {
                     _statusMessage = $"Status: No transitions found for ItemID {rule.ItemID}. Needs setup.";
                     _statusType = MessageType.Warning; // Using checkmark icon for 'Needs Fix' is confusing, standard Warning/Error is better
                 }
            }
        }

        private void ExecuteRule(FixRule rule)
        {
            if (rule.IsSwimPack)
            {
                FixSwimPack(rule);
            }
            else
            {
                FixWeaponTransitions(rule);
            }
        }

        private void FixWeaponTransitions(FixRule rule)
        {
            var layer = FindLayer(_controller, rule.LayerName);
            if (layer == null) { Debug.LogError($"Layer '{rule.LayerName}' not found!"); return; }

            var sm = FindSubStateMachine(layer.stateMachine, rule.TargetSubMachine);
            if (sm == null)
            {
                // Try finding it recursively or warn
                Debug.LogWarning($"Sub-State Machine '{rule.TargetSubMachine}' not found. Trying to find states in root...");
                sm = layer.stateMachine; // Fallback to root if sub-machine not found logic is strict
            }

            int addedCount = 0;
            
            // Standard Pattern: AnyState -> [SubState]
            if (EnsureTransition(layer.stateMachine, sm, "Equip", rule.ItemID, 0, rule.TransitionDuration)) addedCount++;
            if (EnsureTransition(layer.stateMachine, sm, "Idle", rule.ItemID, 1, rule.TransitionDuration)) addedCount++;
            if (EnsureTransition(layer.stateMachine, sm, "Aim", rule.ItemID, 1, rule.TransitionDuration)) addedCount++;
             
             Debug.Log($"[Fixer] Added {addedCount} new transitions for {rule.Name}.");
        }
        
        // Returns true if added
        private bool EnsureTransition(AnimatorStateMachine rootSM, AnimatorStateMachine targetSM, string stateName, int itemID, int stateIndex, float duration)
        {
             var state = FindState(targetSM, stateName);
             if (state == null) return false;
             
             // Check if exists
             foreach(var t in rootSM.anyStateTransitions)
             {
                 if (t.destinationState == state) return false; // Already exists
             }
             
             var trans = rootSM.AddAnyStateTransition(state);
             trans.duration = duration;
             trans.hasExitTime = false;
             trans.AddCondition(AnimatorConditionMode.If, 0, "Slot0ItemStateIndexChange");
             trans.AddCondition(AnimatorConditionMode.Equals, itemID, "Slot0ItemID");
             if (stateIndex >= 0)
                trans.AddCondition(AnimatorConditionMode.Equals, stateIndex, "Slot0ItemStateIndex");
                
             return true;
        }

        private void FixSwimPack(FixRule rule)
        {
             Debug.Log("[Fixer] Swim Pack logic placeholder.");
             // Real implementation would look for Swim states and add transitions
        }

        // --- Helpers ---

        private AnimatorControllerLayer FindLayer(AnimatorController controller, string name)
        {
            return controller.layers.FirstOrDefault(l => l.name == name);
        }

        private AnimatorStateMachine FindSubStateMachine(AnimatorStateMachine parent, string name)
        {
            foreach (var sm in parent.stateMachines)
            {
                if (sm.stateMachine.name == name) return sm.stateMachine;
            }
            return null;
        }
        
        private AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            // Shallow check
            foreach(var s in sm.states)
            {
                if (s.state.name == name) return s.state;
            }
            // Recursive check
            foreach(var childSM in sm.stateMachines)
            {
                var s = FindState(childSM.stateMachine, name);
                if (s != null) return s;
            }
            return null;
        }

        public class FixRule
        {
            public string Name;
            public string Description;
            public string LayerName;
            public string TargetSubMachine;
            public int ItemID;
            public float TransitionDuration;
            public bool IsSwimPack;
        }
    }
}
