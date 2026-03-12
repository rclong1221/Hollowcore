using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using DIG.Items.Authoring;
using DIG.Items.Definitions;
using System.Collections.Generic;
using System.Linq;
using DIG.Editor.Utils;

namespace DIG.Items.Editor.Wizards
{
    /// <summary>
    /// EPIC 14.6 Phase 4 - Animation Integration Assistant
    /// Scans project for weapons and validates/fixes their integration in the Animator Controller.
    /// </summary>
    public class AnimationIntegrationAssistant : EditorWindow
    {
        private AnimatorController _targetController;
        private List<WeaponStatus> _weaponStatuses = new List<WeaponStatus>();
        private Vector2 _scrollPos;
        private WeaponStatus _selectedWeapon;

        // UI State
        private bool _showOnlyIssues = false;

        [MenuItem("DIG/Wizards/4. Integrate: Animation Assistant")]
        public static void ShowWindow()
        {
            var window = GetWindow<AnimationIntegrationAssistant>("Anim Assistant");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            // Don't auto-scan on enable - let user trigger refresh manually
            // This prevents expensive project-wide scans when window opens
        }

        private void OnGUI()
        {
            DrawHeader();

            EditorGUILayout.BeginHorizontal();
            
            // Left Panel: List
            DrawWeaponList();
            
            // Divider
            GUILayout.Box("", GUILayout.Width(1), GUILayout.ExpandHeight(true));
            
            // Right Panel: Inspector/Fixer
            DrawWeaponDetails();
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.toolbar);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Animation Integration Assistant", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh Scan", EditorStyles.toolbarButton))
            {
                ScanProject();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            _targetController = (AnimatorController)EditorGUILayout.ObjectField("Target Controller", _targetController, typeof(AnimatorController), false);
            if (GUILayout.Button("Find", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                var guids = AssetDatabase.FindAssets("t:AnimatorController Player");
                if (guids.Length > 0)
                    _targetController = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            _showOnlyIssues = EditorGUILayout.ToggleLeft("Show Only Issues", _showOnlyIssues, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawWeaponList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(350));
            
            if (_targetController == null)
            {
                EditorGUILayout.HelpBox("Please select an Animator Controller to scan.", MessageType.Error);
                EditorGUILayout.EndVertical();
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Group by Category
            var grouped = _weaponStatuses
                .Where(w => !_showOnlyIssues || !w.IsFullyValid)
                .GroupBy(w => w.CategoryName)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                EditorGUILayout.LabelField(group.Key, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                
                foreach (var weapon in group)
                {
                    DrawWeaponItem(weapon);
                }
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            if (!_weaponStatuses.Any())
            {
                EditorGUILayout.HelpBox("Click 'Refresh Scan' to search for weapons.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawWeaponItem(WeaponStatus weapon)
        {
            EditorGUILayout.BeginHorizontal();
            
            // Icon
            var icon = weapon.IsFullyValid ? "✓" : "⚠";
            var color = weapon.IsFullyValid ? Color.green : Color.red;
            var prevColor = GUI.contentColor;
            GUI.contentColor = color;
            GUILayout.Label(icon, GUILayout.Width(20));
            GUI.contentColor = prevColor;

            // Name Button
            var style = new GUIStyle(EditorStyles.label);
            if (_selectedWeapon == weapon) style.fontStyle = FontStyle.Bold;
            
            if (GUILayout.Button($"{weapon.WeaponName} (ID: {weapon.AnimatorItemID})", style))
            {
                _selectedWeapon = weapon;
                EditorGUIUtility.PingObject(weapon.Prefab);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawWeaponDetails()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            
            if (_selectedWeapon == null)
            {
                EditorGUILayout.LabelField("Select a weapon to view details.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                DrawSelectedWeaponReview();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedWeaponReview()
        {
            EditorGUILayout.LabelField(_selectedWeapon.WeaponName, EditorStyles.largeLabel);
            EditorGUILayout.Space(10);

            // Status Overview
            if (_selectedWeapon.IsFullyValid)
            {
                EditorGUILayout.HelpBox("All checks passed. This weapon is correctly set up in the Animator.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Issues detected in Animator configuration.", MessageType.Warning);
            }

            EditorGUILayout.Space(10);
            
            // State Checklist
            EditorGUILayout.LabelField("State Checklist", EditorStyles.boldLabel);

            DrawStateCheck("Equip", _selectedWeapon.HasEquipState, ref _selectedWeapon.EquipClip);
            DrawStateCheck("Idle", _selectedWeapon.HasIdleState, ref _selectedWeapon.IdleClip);

            for (int i = 0; i < _selectedWeapon.ComboCount; i++)
            {
                if (i >= _selectedWeapon.AttackClips.Count) _selectedWeapon.AttackClips.Add(null);
                
                var clip = _selectedWeapon.AttackClips[i]; // local var needed for ref
                DrawStateCheck($"Attack {i + 1}", _selectedWeapon.HasAttackStates[i], ref clip);
                _selectedWeapon.AttackClips[i] = clip; // assign back
            }

            EditorGUILayout.Space(20);

            // Actions
            if (!_selectedWeapon.IsFullyValid)
            {
                if (GUILayout.Button("Auto-Fix / Generate States", GUILayout.Height(30)))
                {
                    FixSelectedWeapon();
                }
            }
        }

        private void DrawStateCheck(string stateSuffix, bool exists, ref AnimationClip clip)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            // Icon
            var icon = exists ? "✓" : "✗";
            var color = exists ? Color.green : Color.red;
            var prevColor = GUI.contentColor;
            GUI.contentColor = color;
            GUILayout.Label(icon, GUILayout.Width(20));
            GUI.contentColor = prevColor;

            // Label
            GUILayout.Label($"{_selectedWeapon.WeaponName}_{stateSuffix}", GUILayout.Width(200));

            // Clip Field
            clip = (AnimationClip)EditorGUILayout.ObjectField(clip, typeof(AnimationClip), false);

            EditorGUILayout.EndHorizontal();
        }

        private void ScanProject()
        {
            _weaponStatuses.Clear();
            _selectedWeapon = null;

            // Only scan weapon-specific folders instead of entire project
            string[] searchFolders = new[] {
                "Assets/Content/Weapons",
                "Assets/Prefabs/Weapons",
                "Assets/DIG/Weapons",
                "Assets/DIG/Items"
            };

            var validFolders = new List<string>();
            foreach (var folder in searchFolders)
            {
                if (AssetDatabase.IsValidFolder(folder))
                    validFolders.Add(folder);
            }

            if (validFolders.Count == 0)
            {
                Debug.LogWarning("[AnimAssistant] No weapon folders found. Expected: Assets/Content/Weapons or Assets/Prefabs/Weapons");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Prefab", validFolders.ToArray());
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var auth = prefab.GetComponent<ItemAnimationConfigAuthoring>();
                if (auth != null)
                {
                    var status = AnalyzeWeapon(prefab, auth);
                    _weaponStatuses.Add(status);
                }
            }

            // Sort
            _weaponStatuses.Sort((a, b) => a.WeaponName.CompareTo(b.WeaponName));
            Debug.Log($"[AnimAssistant] Scan complete. Found {_weaponStatuses.Count} weapons.");
        }

        private WeaponStatus AnalyzeWeapon(GameObject prefab, ItemAnimationConfigAuthoring auth)
        {
            var status = new WeaponStatus
            {
                Prefab = prefab,
                WeaponName = prefab.name,
                AnimatorItemID = auth.AnimatorItemID,
                CategoryName = auth.Category != null ? auth.Category.CategoryID : "Uncategorized",
                ComboCount = auth.ComboCount,
            };

            // Initialize Lists
            for(int i=0; i<status.ComboCount; i++) 
            {
                status.HasAttackStates.Add(false);
                status.AttackClips.Add(null);
            }

            if (_targetController != null)
            {
                // Check Animator
                string subStateName = auth.Category != null ? auth.Category.AnimatorSubstateMachine : auth.Category.CategoryID;
                if (string.IsNullOrEmpty(subStateName)) subStateName = status.CategoryName;

                var root = _targetController.layers[0].stateMachine; // Assume base layer
                var subMachine = FindSubStateMachine(root, subStateName);

                if (subMachine != null)
                {
                    status.HasEquipState = HasState(subMachine, $"{status.WeaponName}_Equip");
                    status.HasIdleState = HasState(subMachine, $"{status.WeaponName}_Idle");

                    for (int i = 0; i < status.ComboCount; i++)
                    {
                        status.HasAttackStates[i] = HasState(subMachine, $"{status.WeaponName}_Attack{i+1}");
                    }
                }
            }

            return status;
        }

        private void FixSelectedWeapon()
        {
            if (_targetController == null || _selectedWeapon == null) return;
            
            var auth = _selectedWeapon.Prefab.GetComponent<ItemAnimationConfigAuthoring>();
            string subStateName = auth.Category != null ? auth.Category.AnimatorSubstateMachine : auth.Category.CategoryID;
            if (string.IsNullOrEmpty(subStateName)) subStateName = _selectedWeapon.CategoryName;

            // 1. Get/Create Sub State Machine
            var root = _targetController.layers[0].stateMachine;
            var subMachine = AnimatorControllerGenerator.GetOrCreateSubStateMachine(root, subStateName);

            // 2. Create States
            if (_selectedWeapon.EquipClip != null)
                AnimatorControllerGenerator.AddStateWithClip(subMachine, $"{_selectedWeapon.WeaponName}_Equip", _selectedWeapon.EquipClip);
            
            if (_selectedWeapon.IdleClip != null)
                AnimatorControllerGenerator.AddStateWithClip(subMachine, $"{_selectedWeapon.WeaponName}_Idle", _selectedWeapon.IdleClip);

            for (int i = 0; i < _selectedWeapon.ComboCount; i++)
            {
                if (i < _selectedWeapon.AttackClips.Count && _selectedWeapon.AttackClips[i] != null)
                {
                    AnimatorControllerGenerator.AddStateWithClip(subMachine, $"{_selectedWeapon.WeaponName}_Attack{i + 1}", _selectedWeapon.AttackClips[i]);
                }
            }
            
            // 3. Ensure Parameters (Just in case)
            AnimatorControllerGenerator.EnsureParameter(_targetController, "Slot0ItemID", AnimatorControllerParameterType.Int);
            AnimatorControllerGenerator.EnsureParameter(_targetController, "Slot0ItemStateIndex", AnimatorControllerParameterType.Int);
            
            AssetDatabase.SaveAssets();
            ScanProject(); // Rescan
            
            // Reselect the weapon (Scan clears selection)
            _selectedWeapon = _weaponStatuses.FirstOrDefault(w => w.WeaponName == auth.gameObject.name);
        }

        // Helpers
        private AnimatorStateMachine FindSubStateMachine(AnimatorStateMachine root, string name)
        {
            // Simple recursive search (or just top level for now based on Wizard assumption)
             foreach (var childStateMachine in root.stateMachines)
            {
                if (childStateMachine.stateMachine.name == name)
                    return childStateMachine.stateMachine;
            }
            return null; // Not found
        }

        private bool HasState(AnimatorStateMachine machine, string stateName)
        {
            return machine.states.Any(s => s.state.name == stateName);
        }

        // Data Class
        private class WeaponStatus
        {
            public GameObject Prefab;
            public string WeaponName;
            public int AnimatorItemID;
            public string CategoryName;
            public int ComboCount;
            
            public bool HasEquipState;
            public bool HasIdleState;
            public List<bool> HasAttackStates = new List<bool>();

            // Stored Clips for "Fix" action
            public AnimationClip EquipClip;
            public AnimationClip IdleClip;
            public List<AnimationClip> AttackClips = new List<AnimationClip>();

            public bool IsFullyValid => HasEquipState && HasIdleState && HasAttackStates.All(x => x);
        }
    }
}
