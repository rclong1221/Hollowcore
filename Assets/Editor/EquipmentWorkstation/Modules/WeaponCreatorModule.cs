using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using DIG.Items;
using DIG.Items.Authoring;
using DIG.Weapons.Authoring;
using DIG.Items.Definitions;
using DIG.Editor.Utils;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace DIG.Editor.EquipmentWorkstation
{
    public class WeaponCreatorModule : IEquipmentModule
    {
        // Identity
        private string _weaponName = "NewWeapon";
        private string _displayName = "New Weapon";
        private int _itemTypeId = 100;
        
        // Category
        private WeaponCategoryDefinition _category;
        
        // Visuals
        private GameObject _modelPrefab;
        private AnimationClip _idleClip;
        private AnimationClip _equipClip;
        
        // Weapon Config
        private WeaponType _weaponType = WeaponType.Shootable;
        private int _fireRate = 600;
        private float _damage = 25f;
        private int _clipSize = 30;
        
        // Settings
        private AnimatorController _targetController;

        private Vector2 _scrollPos;
        
        // Cached ID data - only refreshed manually via button
        private static HashSet<int> _cachedItemIds = null;
        private static bool _cacheValid = false;

        public void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            EditorGUILayout.LabelField("Weapon Creator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Fully automated weapon pipeline: Creates ECS Prefabs, configures Authoring components, and updates the Animator Controller.", MessageType.Info);

            EditorGUILayout.Space();

            // --- 1. Identity & ID ---
            EditorGUILayout.LabelField("1. Identity", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            _weaponName = EditorGUILayout.TextField("Internal Name", _weaponName);
            _displayName = EditorGUILayout.TextField("Display Name", _displayName);
            
            EditorGUILayout.BeginHorizontal();
            _itemTypeId = EditorGUILayout.IntField("Item ID", _itemTypeId);
            if (GUILayout.Button("Auto-Gen", GUILayout.Width(80)))
            {
                RefreshIdCache(); // Manual refresh when generating
                _itemTypeId = GenerateUniqueItemID();
            }
            if (GUILayout.Button("Check IDs", GUILayout.Width(80)))
            {
                RefreshIdCache(); // Manual refresh button
            }
            EditorGUILayout.EndHorizontal();

            // Only show collision warning if cache has been populated
            if (_cacheValid && IsIdTaken(_itemTypeId))
            {
                EditorGUILayout.HelpBox($"ID {_itemTypeId} is already in use!", MessageType.Error);
            }
            else if (!_cacheValid)
            {
                EditorGUILayout.HelpBox("Click 'Check IDs' to verify ID availability.", MessageType.Info);
            }

            _category = (WeaponCategoryDefinition)EditorGUILayout.ObjectField("Category", _category, typeof(WeaponCategoryDefinition), false);
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // --- 2. Configuration ---
            EditorGUILayout.LabelField("2. Components", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            _weaponType = (WeaponType)EditorGUILayout.EnumPopup("Weapon Type", _weaponType);
            
            EditorGUI.indentLevel++;
            if (_weaponType == WeaponType.Shootable)
            {
                _fireRate = EditorGUILayout.IntField("Fire Rate (RPM)", _fireRate);
                _clipSize = EditorGUILayout.IntField("Clip Size", _clipSize);
            }
            else if (_weaponType == WeaponType.Melee)
            {
                // Simple placeholder fields for melee
                 EditorGUILayout.LabelField("Melee specific settings will go here.");
            }
            _damage = EditorGUILayout.FloatField("Damage", _damage);
            EditorGUI.indentLevel--;
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // --- 3. Assets ---
            EditorGUILayout.LabelField("3. Assets", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            _modelPrefab = (GameObject)EditorGUILayout.ObjectField("Model Prefab", _modelPrefab, typeof(GameObject), false);
            _idleClip = (AnimationClip)EditorGUILayout.ObjectField("Idle Clip", _idleClip, typeof(AnimationClip), false);
            _equipClip = (AnimationClip)EditorGUILayout.ObjectField("Equip Clip", _equipClip, typeof(AnimationClip), false);
            _targetController = (AnimatorController)EditorGUILayout.ObjectField("Target Controller", _targetController, typeof(AnimatorController), false);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // --- Actions ---
            bool isValid = !string.IsNullOrEmpty(_weaponName) && _modelPrefab != null && (!_cacheValid || !IsIdTaken(_itemTypeId));
            
            EditorGUI.BeginDisabledGroup(!isValid);
            if (GUILayout.Button("Create Weapon", GUILayout.Height(40)))
            {
                CreateWeapon();
            }
            EditorGUI.EndDisabledGroup();

            if (!isValid)
            {
                 if (_cacheValid && IsIdTaken(_itemTypeId)) EditorGUILayout.HelpBox("Fix Duplicate ID to proceed.", MessageType.Error);
                 else EditorGUILayout.HelpBox("Name and Model Prefab are required.", MessageType.Warning);
            }

            EditorGUILayout.EndScrollView();
        }

        private void RefreshIdCache()
        {
            _cachedItemIds = new HashSet<int>();

            // Only scan prefabs in Content/Weapons folder to reduce scope significantly
            string[] searchFolders = new[] { "Assets/Content/Weapons", "Assets/DIG/Items" };
            var validFolders = searchFolders.Where(f => AssetDatabase.IsValidFolder(f)).ToArray();

            if (validFolders.Length == 0)
            {
                Debug.LogWarning("[WeaponCreator] No weapon folders found. Expected: Assets/Content/Weapons or Assets/DIG/Items");
                _cacheValid = true;
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", validFolders);
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                var auth = go.GetComponent<ItemAuthoring>();
                if (auth != null && auth.ItemTypeId > 0)
                {
                    _cachedItemIds.Add(auth.ItemTypeId);
                }
            }

            _cacheValid = true;
            Debug.Log($"[WeaponCreator] ID cache refreshed. Found {_cachedItemIds.Count} existing IDs.");
        }
        
        private int GenerateUniqueItemID()
        {
            if (_cachedItemIds == null || _cachedItemIds.Count == 0)
                return 100;

            // Find first available ID starting from 100
            int candidate = 100;
            while (_cachedItemIds.Contains(candidate) && candidate < 10000)
            {
                candidate++;
            }
            return candidate;
        }

        private bool IsIdTaken(int id)
        {
            return _cachedItemIds != null && _cachedItemIds.Contains(id);
        }
        
        private void InvalidateIdCache()
        {
            _cachedItemIds = null;
            _cacheValid = false;
        }

        private void CreateWeapon()
        {
            string categoryName = _category != null ? _category.CategoryID : "Uncategorized";
            string folderPath = $"Assets/Content/Weapons/Prefabs/{categoryName}";
            
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            // 1. Instantiate
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(_modelPrefab);
            instance.name = _weaponName;
            
            // 2. Add ItemAuthoring
            var itemAuth = instance.AddComponent<ItemAuthoring>();
            itemAuth.ItemTypeId = _itemTypeId;
            itemAuth.DisplayName = _displayName;
            itemAuth.Category = ItemCategory.Weapon; // Assumed mapping
            itemAuth.IsStackable = false;

            // 3. Add WeaponAuthoring (THE CORE LOGIC)
            var weaponAuth = instance.AddComponent<WeaponAuthoring>();
            weaponAuth.Type = _weaponType;
            weaponAuth.AnimatorItemID = _itemTypeId; // Use ItemID as AnimatorID for simplicity
            weaponAuth.Damage = _damage;
            
            if (_weaponType == WeaponType.Shootable)
            {
                weaponAuth.FireRate = _fireRate;
                weaponAuth.ClipSize = _clipSize;
                weaponAuth.StartingAmmo = _clipSize;
            }

            // 4. Save Prefab
            string path = $"{folderPath}/{_weaponName}.prefab";
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            
            // 5. Animator Update
            if (_targetController != null && (_idleClip != null || _equipClip != null))
            {
                UpdateAnimator(path);
            }

            Debug.Log($"[Creator] Successfully created weapon '{_weaponName}' (ID: {_itemTypeId}) at {path}");
            InvalidateIdCache(); // Force refresh so the new ID is recognized
            AssetDatabase.Refresh();
        }



        private void UpdateAnimator(string prefabPath)
        {
             if (_targetController == null) return;

             string subStateName = _category != null ? _category.AnimatorSubstateMachine : "Uncategorized";
             if (string.IsNullOrEmpty(subStateName)) subStateName = _category != null ? _category.CategoryID : "Uncategorized";

             // 1. Get/Create Sub State Machine
             // Using the now shared utility
             var root = _targetController.layers[0].stateMachine; 
             var weaponMachine = AnimatorControllerGenerator.GetOrCreateSubStateMachine(root, subStateName);

             // 2. Create States
             if (_equipClip) AnimatorControllerGenerator.AddStateWithClip(weaponMachine, $"{_weaponName}_Equip", _equipClip);
             if (_idleClip) AnimatorControllerGenerator.AddStateWithClip(weaponMachine, $"{_weaponName}_Idle", _idleClip);

             Debug.Log($"[Creator] Updated Animator Controller '{_targetController.name}' with states for {_weaponName} in SubStateMachine '{subStateName}'");
        }
        
        // Helper to match ItemCategory enum if needed, or use Int casting
        // Assuming ItemCategory is available in namespace
    }
}
