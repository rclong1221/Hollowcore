using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using DIG.Items.Definitions;
using DIG.Items.Authoring;
using System.IO;
using System.Collections.Generic;
using DIG.Editor.Utils;

namespace DIG.Items.Editor.Wizards
{
    /// <summary>
    /// EPIC 14.6 Phase 2 - Weapon Creation Wizard
    /// Streamlines the creation of new weapons, prefabs, and animator states.
    /// </summary>
    public class WeaponCreationWizard : EditorWindow
    {
        // Wizard State
        private int _currentStep = 0;
        private const int TOTAL_STEPS = 4;
        private Vector2 _scrollPos;
        private List<string> _validationMessages = new List<string>();

        // Step 1: Identity
        private string _weaponName = "NewWeapon";
        private string _displayName = "New Weapon";
        private WeaponCategoryDefinition _category;
        private bool _createNewCategory = false;
        private string _newCategoryId = "NewCategory";
        private GripType _newCategoryGrip = GripType.OneHanded; 
        private Texture2D _icon;

        // Step 2: Model & Components
        private GameObject _modelPrefab;
        private int _itemTypeId = 0; // Auto-generated usually
        private bool _isStackable = false;
        private int _maxStack = 1;
        private int _animatorItemId = 0;
        private int _movementSetId = 0;
        private bool _isTwoHanded = false;

        // Step 3: Animation Setup
        private AnimationClip _equipClip;
        private AnimationClip _idleClip;
        private List<AnimationClip> _attackClips = new List<AnimationClip>();
        private int _comboCount = 1;
        private float _useDuration = 0.5f;
        
        // Step 4: Animator Integration
        private AnimatorController _targetController;
        private bool _createAnimatorStates = true;

        [MenuItem("DIG/Wizards/2. Create: New Weapon")]
        public static void ShowWindow()
        {
            var window = GetWindow<WeaponCreationWizard>("Weapon Creator");
            window.minSize = new Vector2(500, 600);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            DrawHeader();
            DrawProgressBar();
            EditorGUILayout.Space(20);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_currentStep)
            {
                case 0: DrawStep1_Identity(); break;
                case 1: DrawStep2_ModelComponents(); break;
                case 2: DrawStep3_AnimationSetup(); break;
                case 3: DrawStep4_ReviewCreate(); break;
            }

            EditorGUILayout.EndScrollView();
            DrawNavigationButtons();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Weapon Creation Wizard", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Create a new weapon with prefab, config, and animations.", EditorStyles.helpBox);
            EditorGUILayout.Space(10);
        }

        private void DrawProgressBar()
        {
            EditorGUILayout.BeginHorizontal();
            string[] stepNames = { "Identity", "Model", "Animation", "Create" };
            for (int i = 0; i < TOTAL_STEPS; i++)
            {
                var style = i == _currentStep ? EditorStyles.toolbarButton : EditorStyles.miniButtonMid;
                var color = i < _currentStep ? Color.green : (i == _currentStep ? Color.cyan : Color.gray);
                GUI.backgroundColor = color;
                GUILayout.Button(stepNames[i], style, GUILayout.ExpandWidth(true));
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        #region Step 1: Identity

        private void DrawStep1_Identity()
        {
            EditorGUILayout.LabelField("Step 1: Weapon Identity", EditorStyles.largeLabel);
            EditorGUILayout.Space(10);

            _weaponName = EditorGUILayout.TextField("Internal Name", _weaponName);
            _displayName = EditorGUILayout.TextField("Display Name", _displayName);
            _icon = (Texture2D)EditorGUILayout.ObjectField("Icon", _icon, typeof(Texture2D), false);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Category", EditorStyles.boldLabel);
            
            _createNewCategory = EditorGUILayout.Toggle("Create New Category?", _createNewCategory);

            if (_createNewCategory)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Will create a new category asset automatically.", MessageType.Info);
                _newCategoryId = EditorGUILayout.TextField("New Category ID", _newCategoryId);
                _newCategoryGrip = (GripType)EditorGUILayout.EnumPopup("Grip Type", _newCategoryGrip);
                
                // If we are creating a new category, null out the existing one
                _category = null;
                EditorGUI.indentLevel--;
            }
            else
            {
                _category = (WeaponCategoryDefinition)EditorGUILayout.ObjectField("Select Category", _category, typeof(WeaponCategoryDefinition), false);
                if (_category == null)
                {
                    EditorGUILayout.HelpBox("Please select a category or choose 'Create New'.", MessageType.Warning);
                }
            }
        }

        #endregion

        #region Step 2: Model & Components

        private void DrawStep2_ModelComponents()
        {
            EditorGUILayout.LabelField("Step 2: Model & Components", EditorStyles.largeLabel);
            EditorGUILayout.Space(10);

            _modelPrefab = (GameObject)EditorGUILayout.ObjectField("Weapon Model (FBX/Prefab)", _modelPrefab, typeof(GameObject), false);
            if (_modelPrefab == null)
            {
                EditorGUILayout.HelpBox("Assign a 3D model or prefab for the weapon visuals.", MessageType.Warning);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Item Configuration", EditorStyles.boldLabel);
            
            _itemTypeId = EditorGUILayout.IntField("Item Type ID", _itemTypeId);
            if (GUILayout.Button("Find Next Available ID"))
            {
                _itemTypeId = FindNextItemTypeID();
            }

            _isStackable = EditorGUILayout.Toggle("Is Stackable", _isStackable);
            if (_isStackable)
            {
                _maxStack = EditorGUILayout.IntField("Max Stack", _maxStack);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Animation Config", EditorStyles.boldLabel);
            
            _animatorItemId = EditorGUILayout.IntField("Animator Item ID", _animatorItemId);
            _movementSetId = EditorGUILayout.IntField("Movement Set ID", _movementSetId);
            _isTwoHanded = EditorGUILayout.Toggle("Is Two Handed", _isTwoHanded);

            // Auto-fill from category if selected
            if (_category != null && _movementSetId == 0)
            {
                _movementSetId = _category.GetMovementSetID();
                _isTwoHanded = _category.GripType == GripType.TwoHanded;
            }
        }

        #endregion

        #region Step 3: Animation Setup

        private void DrawStep3_AnimationSetup()
        {
            EditorGUILayout.LabelField("Step 3: Animation Clips", EditorStyles.largeLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox("Assign AnimationClips to be used in the Animator Controller.", MessageType.Info);

            _equipClip = (AnimationClip)EditorGUILayout.ObjectField("Equip Clip", _equipClip, typeof(AnimationClip), false);
            _idleClip = (AnimationClip)EditorGUILayout.ObjectField("Idle Clip", _idleClip, typeof(AnimationClip), false);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Attacks", EditorStyles.boldLabel);
            _comboCount = EditorGUILayout.IntSlider("Combo Count", _comboCount, 1, 5);
            
            // Resize list if combo count changed
            while (_attackClips.Count < _comboCount) _attackClips.Add(null);
            while (_attackClips.Count > _comboCount) _attackClips.RemoveAt(_attackClips.Count - 1);

            for (int i = 0; i < _comboCount; i++)
            {
                _attackClips[i] = (AnimationClip)EditorGUILayout.ObjectField($"Attack {i + 1}", _attackClips[i], typeof(AnimationClip), false);
            }

            EditorGUILayout.Space(10);
            _useDuration = EditorGUILayout.FloatField("Use Duration", _useDuration);
        }

        #endregion

        #region Step 4: Review & Create

        private void DrawStep4_ReviewCreate()
        {
            EditorGUILayout.LabelField("Step 4: Review & Create", EditorStyles.largeLabel);
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Animator Controller Integration", EditorStyles.boldLabel);
            _targetController = (AnimatorController)EditorGUILayout.ObjectField("Target Controller", _targetController, typeof(AnimatorController), false);
            _createAnimatorStates = EditorGUILayout.Toggle("Create Animator States", _createAnimatorStates);

            if (_targetController == null && _createAnimatorStates)
            {
                // Try to find one in project
                if (GUILayout.Button("Find Player Controller"))
                {
                    var guids = AssetDatabase.FindAssets("t:AnimatorController Player");
                    if (guids.Length > 0)
                        _targetController = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }
            
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Weapon: {_weaponName}");
            EditorGUILayout.LabelField($"Category: {(_createNewCategory ? _newCategoryId : (_category != null ? _category.CategoryID : "None"))}");
            EditorGUILayout.LabelField($"Prefab Path: Assets/Content/Weapons/Prefabs/{(_createNewCategory ? _newCategoryId : (_category != null ? _category.CategoryID : "Uncategorized"))}/{_weaponName}.prefab");

            EditorGUILayout.Space(20);
        }

        #endregion

        #region Navigation & Logic

        private void DrawNavigationButtons()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();

            if (_currentStep > 0)
            {
                if (GUILayout.Button("Back", GUILayout.Width(100)))
                    _currentStep--;
            }
            else
            {
                GUILayout.Space(104); // spacer
            }

            GUILayout.FlexibleSpace();

            if (_currentStep < TOTAL_STEPS - 1)
            {
                if (GUILayout.Button("Next", GUILayout.Width(100)))
                    _currentStep++;
            }
            else
            {
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Create Weapon", GUILayout.Width(150)))
                {
                    ExecuteCreation();
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);
        }

        private int FindNextItemTypeID()
        {
            // Simple heuristic to find a free ID - in a real project could scan all ItemAuthoring components
            return Random.Range(100, 999);
        }

        /// <summary>
        /// Main execution - Creates Assets, Prefabs, and Updates Animator
        /// </summary>
        private void ExecuteCreation()
        {
            if (string.IsNullOrEmpty(_weaponName))
            {
                EditorUtility.DisplayDialog("Error", "Weapon Name cannot be empty.", "OK");
                return;
            }
            
            if (_modelPrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a model prefab.", "OK");
                return;
            }

            // 1. Create Category if needed
            if (_createNewCategory)
            {
                CreateNewCategory();
            }

            if (_category == null)
            {
                EditorUtility.DisplayDialog("Error", "No category available.", "OK");
                return;
            }

            // 2. Create Prefab
            GameObject newPrefab = CreateWeaponPrefab();
            if (newPrefab == null) return;

            // 3. Update Animator
            if (_createAnimatorStates && _targetController != null)
            {
                UpdateAnimatorController();
            }

            // 4. Finish
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(newPrefab);
            EditorUtility.DisplayDialog("Success", $"Weapon '{_weaponName}' created successfully!", "OK");
            Close();
        }

        private void CreateNewCategory()
        {
            string folderPath = "Assets/Content/Equipment/Definitions/Categories";
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var newCat = ScriptableObject.CreateInstance<WeaponCategoryDefinition>();
            newCat.CategoryID = _newCategoryId;
            newCat.DisplayName = _newCategoryId; // Default display name
            newCat.GripType = _newCategoryGrip;
            
            // Assume 1st level category for now
            string path = $"{folderPath}/{_newCategoryId}.asset";
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            
            AssetDatabase.CreateAsset(newCat, path);
            _category = newCat;
            Debug.Log($"[WeaponWizard] Created new category at {path}");
        }

        private GameObject CreateWeaponPrefab()
        {
            string categoryName = _category.CategoryID;
            string folderPath = $"Assets/Content/Weapons/Prefabs/{categoryName}";
            
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            // 1. Instantiate the model
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(_modelPrefab);
            
            // 2. Add ItemAuthoring
            var itemAuth = instance.AddComponent<ItemAuthoring>();
            itemAuth.ItemTypeId = _itemTypeId;
            itemAuth.DisplayName = _displayName;
            itemAuth.Category = ItemCategory.Weapon; // Default mapping
            // itemAuth.DefaultQuickSlot = ... (optional)

            // 3. Add ItemAnimationConfigAuthoring
            var animAuth = instance.AddComponent<ItemAnimationConfigAuthoring>();
            animAuth.Category = _category;
            animAuth.AnimatorItemID = _animatorItemId;
            animAuth.MovementSetID = _movementSetId;
            animAuth.ComboCount = _comboCount;
            animAuth.UseDuration = _useDuration;
            animAuth.IsTwoHanded = _isTwoHanded;

            // 4. Save as Prefab
            string prefabPath = $"{folderPath}/{_weaponName}.prefab";
            prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            
            // Cleanup
            DestroyImmediate(instance);

            Debug.Log($"[WeaponWizard] Created weapon prefab at {prefabPath}");
            return prefabAsset;
        }

        private void UpdateAnimatorController()
        {
            string subStateName = _category.AnimatorSubstateMachine;
            if (string.IsNullOrEmpty(subStateName)) subStateName = _category.CategoryID;

            // 1. Get/Create Sub State Machine
            var root = _targetController.layers[0].stateMachine; // Assume base layer for now
            var weaponMachine = AnimatorControllerGenerator.GetOrCreateSubStateMachine(root, subStateName);

            // 2. Create States
            if (_equipClip) AnimatorControllerGenerator.AddStateWithClip(weaponMachine, $"{_weaponName}_Equip", _equipClip);
            if (_idleClip) AnimatorControllerGenerator.AddStateWithClip(weaponMachine, $"{_weaponName}_Idle", _idleClip);

            for (int i = 0; i < _attackClips.Count; i++)
            {
                if (_attackClips[i])
                    AnimatorControllerGenerator.AddStateWithClip(weaponMachine, $"{_weaponName}_Attack{i+1}", _attackClips[i]);
            }
            
            // 3. Ensure Parameters
            AnimatorControllerGenerator.EnsureParameter(_targetController, "Slot0ItemID", AnimatorControllerParameterType.Int);
            AnimatorControllerGenerator.EnsureParameter(_targetController, "Slot0ItemStateIndex", AnimatorControllerParameterType.Int);

            Debug.Log($"[WeaponWizard] Updated Animator Controller '{_targetController.name}' with states for {_weaponName}");
        }

        #endregion
    }
}
