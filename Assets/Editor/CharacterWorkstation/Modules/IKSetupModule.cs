using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using DIG.Player.IK;

namespace DIG.Editor.CharacterWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 CW-06: IK Setup module.
    /// Weapon IK targets, look-at constraints, foot placement.
    /// </summary>
    public class IKSetupModule : ICharacterModule
    {
        private GameObject _selectedCharacter;
        private Animator _animator;
        private Vector2 _scrollPosition;
        
        // IK Settings
        private bool _enableFootIK = true;
        private bool _enableLookAtIK = true;
        private bool _enableHandIK = true;
        
        // Foot IK
        private float _footRayLength = 0.6f;
        private float _footOffset = 0.05f;
        private float _bodyHeightAdjust = 0.3f;
        private float _footIKWeight = 1f;
        private float _footBlendSpeed = 10f;
        
        // Look At IK
        private LookAtMode _lookAtMode = LookAtMode.MouseAim;
        private float _maxHeadAngle = 70f;
        private float _maxSpineAngle = 30f;
        private float _headWeight = 1f;
        private float _bodyWeight = 0.3f;
        private float _lookAtBlendSpeed = 10f;
        
        // Hand IK
        private float _handWeight = 1f;
        private float _handAdjustSpeed = 10f;
        private Vector3 _handPositionOffset = Vector3.zero;
        private float _upperArmWeight = 1f;
        private float _springStiffness = 0.2f;
        private float _springDamping = 0.25f;
        
        // Weapon IK Points
        private Transform _leftHandIKTarget;
        private Transform _rightHandIKTarget;
        private Transform _aimTarget;
        
        // Preview
        private bool _isPreviewing = false;
        private Transform _previewTarget;

        // Presets
        private enum IKPreset { None, FPS_Rifle, FPS_Pistol, ThirdPerson, Melee, Bow }
        private IKPreset _selectedPreset = IKPreset.None;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("IK Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure Inverse Kinematics for character animations. " +
                "Foot placement, look-at, and weapon IK.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawCharacterSelection();
            EditorGUILayout.Space(10);
            DrawPresetSelection();
            EditorGUILayout.Space(10);
            DrawFootIKSection();
            EditorGUILayout.Space(10);
            DrawLookAtIKSection();
            EditorGUILayout.Space(10);
            DrawHandIKSection();
            EditorGUILayout.Space(10);
            DrawWeaponIKPoints();
            EditorGUILayout.Space(10);
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawCharacterSelection()
        {
            EditorGUILayout.LabelField("Character", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            _selectedCharacter = (GameObject)EditorGUILayout.ObjectField(
                "Character", _selectedCharacter, typeof(GameObject), true);
            
            if (EditorGUI.EndChangeCheck() && _selectedCharacter != null)
            {
                _animator = _selectedCharacter.GetComponent<Animator>();
                LoadFromCharacter();
            }

            if (Selection.activeGameObject != null && _selectedCharacter != Selection.activeGameObject)
            {
                if (GUILayout.Button($"Use Selected: {Selection.activeGameObject.name}", EditorStyles.miniButton))
                {
                    _selectedCharacter = Selection.activeGameObject;
                    _animator = _selectedCharacter.GetComponent<Animator>();
                    LoadFromCharacter();
                }
            }

            if (_animator != null)
            {
                EditorGUILayout.LabelField($"Animator: {(_animator.isHuman ? "Humanoid" : "Generic")}", 
                    EditorStyles.miniLabel);
            }
            else if (_selectedCharacter != null)
            {
                EditorGUILayout.HelpBox("No Animator found on character.", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPresetSelection()
        {
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            _selectedPreset = (IKPreset)EditorGUILayout.EnumPopup("Preset", _selectedPreset);
            
            EditorGUI.BeginDisabledGroup(_selectedPreset == IKPreset.None);
            if (GUILayout.Button("Apply", GUILayout.Width(60)))
            {
                ApplyPreset(_selectedPreset);
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();

            // Quick preset buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("FPS")) ApplyPreset(IKPreset.FPS_Rifle);
            if (GUILayout.Button("TPS")) ApplyPreset(IKPreset.ThirdPerson);
            if (GUILayout.Button("Melee")) ApplyPreset(IKPreset.Melee);
            if (GUILayout.Button("Bow")) ApplyPreset(IKPreset.Bow);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawFootIKSection()
        {
            _enableFootIK = EditorGUILayout.BeginToggleGroup("Foot IK", _enableFootIK);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _footRayLength = EditorGUILayout.Slider("Ray Length", _footRayLength, 0.1f, 1.5f);
            _footOffset = EditorGUILayout.Slider("Foot Offset", _footOffset, 0f, 0.2f);
            _bodyHeightAdjust = EditorGUILayout.Slider("Body Height Adjust", _bodyHeightAdjust, 0f, 0.5f);
            _footIKWeight = EditorGUILayout.Slider("IK Weight", _footIKWeight, 0f, 1f);
            _footBlendSpeed = EditorGUILayout.Slider("Blend Speed", _footBlendSpeed, 1f, 30f);

            EditorGUILayout.HelpBox(
                "Foot IK adjusts foot positions to match ground surfaces. " +
                "Requires terrain or uneven ground to see effect.",
                MessageType.None);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndToggleGroup();
        }

        private void DrawLookAtIKSection()
        {
            _enableLookAtIK = EditorGUILayout.BeginToggleGroup("Look At IK", _enableLookAtIK);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _lookAtMode = (LookAtMode)EditorGUILayout.EnumPopup("Mode", _lookAtMode);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Angle Limits", EditorStyles.miniLabel);
            _maxHeadAngle = EditorGUILayout.Slider("Max Head Angle", _maxHeadAngle, 0f, 90f);
            _maxSpineAngle = EditorGUILayout.Slider("Max Spine Angle", _maxSpineAngle, 0f, 60f);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Weights", EditorStyles.miniLabel);
            _headWeight = EditorGUILayout.Slider("Head Weight", _headWeight, 0f, 1f);
            _bodyWeight = EditorGUILayout.Slider("Body Weight", _bodyWeight, 0f, 1f);
            _lookAtBlendSpeed = EditorGUILayout.Slider("Blend Speed", _lookAtBlendSpeed, 1f, 30f);

            // Preview target
            EditorGUILayout.Space(5);
            _previewTarget = (Transform)EditorGUILayout.ObjectField("Preview Target", 
                _previewTarget, typeof(Transform), true);
            
            EditorGUI.BeginDisabledGroup(_selectedCharacter == null || _previewTarget == null);
            _isPreviewing = EditorGUILayout.Toggle("Preview Look At", _isPreviewing);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndToggleGroup();
        }

        private void DrawHandIKSection()
        {
            _enableHandIK = EditorGUILayout.BeginToggleGroup("Hand IK (Weapons/Aiming)", _enableHandIK);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Hand Placement", EditorStyles.miniLabel);
            _handWeight = EditorGUILayout.Slider("Hand Weight", _handWeight, 0f, 1f);
            _handAdjustSpeed = EditorGUILayout.Slider("Adjust Speed", _handAdjustSpeed, 1f, 30f);
            _handPositionOffset = EditorGUILayout.Vector3Field("Position Offset", _handPositionOffset);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Upper Arm", EditorStyles.miniLabel);
            _upperArmWeight = EditorGUILayout.Slider("Upper Arm Weight", _upperArmWeight, 0f, 1f);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Recoil Spring", EditorStyles.miniLabel);
            _springStiffness = EditorGUILayout.Slider("Stiffness", _springStiffness, 0.05f, 1f);
            _springDamping = EditorGUILayout.Slider("Damping", _springDamping, 0.1f, 1f);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndToggleGroup();
        }

        private void DrawWeaponIKPoints()
        {
            EditorGUILayout.LabelField("Weapon IK Targets", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _leftHandIKTarget = (Transform)EditorGUILayout.ObjectField(
                "Left Hand Target", _leftHandIKTarget, typeof(Transform), true);
            _rightHandIKTarget = (Transform)EditorGUILayout.ObjectField(
                "Right Hand Target", _rightHandIKTarget, typeof(Transform), true);
            _aimTarget = (Transform)EditorGUILayout.ObjectField(
                "Aim Target", _aimTarget, typeof(Transform), true);

            EditorGUILayout.Space(5);

            EditorGUI.BeginDisabledGroup(_selectedCharacter == null);
            
            if (GUILayout.Button("Auto-Find IK Targets"))
            {
                AutoFindIKTargets();
            }
            
            if (GUILayout.Button("Create IK Target Hierarchy"))
            {
                CreateIKTargetHierarchy();
            }
            
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.HelpBox(
                "IK targets are typically child transforms of weapons. " +
                "Create them on weapon prefabs for proper hand placement.",
                MessageType.None);

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginDisabledGroup(_selectedCharacter == null);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Apply to Character", GUILayout.Height(30)))
            {
                ApplyToCharacter();
            }
            
            if (GUILayout.Button("Load from Character", GUILayout.Height(30)))
            {
                LoadFromCharacter();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Add IK Authoring"))
            {
                AddIKAuthoring();
            }
            
            if (GUILayout.Button("Test in Scene"))
            {
                TestInScene();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
        }

        private void ApplyPreset(IKPreset preset)
        {
            switch (preset)
            {
                case IKPreset.FPS_Rifle:
                    _enableFootIK = false; // Usually not needed in FPS
                    _enableLookAtIK = true;
                    _enableHandIK = true;
                    _lookAtMode = LookAtMode.MouseAim;
                    _maxHeadAngle = 30f;
                    _maxSpineAngle = 15f;
                    _headWeight = 0.5f;
                    _bodyWeight = 0.2f;
                    _handWeight = 1f;
                    _upperArmWeight = 0.8f;
                    _springStiffness = 0.15f;
                    _springDamping = 0.2f;
                    break;

                case IKPreset.FPS_Pistol:
                    _enableFootIK = false;
                    _enableLookAtIK = true;
                    _enableHandIK = true;
                    _lookAtMode = LookAtMode.MouseAim;
                    _maxHeadAngle = 45f;
                    _maxSpineAngle = 20f;
                    _headWeight = 0.6f;
                    _bodyWeight = 0.1f;
                    _handWeight = 1f;
                    _upperArmWeight = 1f;
                    _springStiffness = 0.25f;
                    _springDamping = 0.3f;
                    break;

                case IKPreset.ThirdPerson:
                    _enableFootIK = true;
                    _enableLookAtIK = true;
                    _enableHandIK = true;
                    _footRayLength = 0.6f;
                    _footOffset = 0.05f;
                    _bodyHeightAdjust = 0.3f;
                    _footIKWeight = 1f;
                    _lookAtMode = LookAtMode.MouseAim;
                    _maxHeadAngle = 70f;
                    _maxSpineAngle = 30f;
                    _headWeight = 1f;
                    _bodyWeight = 0.3f;
                    _handWeight = 1f;
                    _upperArmWeight = 1f;
                    break;

                case IKPreset.Melee:
                    _enableFootIK = true;
                    _enableLookAtIK = true;
                    _enableHandIK = false; // Weapon animations handle hands
                    _footRayLength = 0.5f;
                    _footOffset = 0.05f;
                    _bodyHeightAdjust = 0.2f;
                    _footIKWeight = 0.8f;
                    _lookAtMode = LookAtMode.NearestEnemy;
                    _maxHeadAngle = 80f;
                    _maxSpineAngle = 20f;
                    _headWeight = 1f;
                    _bodyWeight = 0.2f;
                    break;

                case IKPreset.Bow:
                    _enableFootIK = true;
                    _enableLookAtIK = true;
                    _enableHandIK = true;
                    _footRayLength = 0.5f;
                    _lookAtMode = LookAtMode.MouseAim;
                    _maxHeadAngle = 60f;
                    _maxSpineAngle = 40f;
                    _headWeight = 0.8f;
                    _bodyWeight = 0.5f;
                    _handWeight = 0.7f;
                    _upperArmWeight = 1f;
                    _springStiffness = 0.1f;
                    _springDamping = 0.15f;
                    break;
            }

            Debug.Log($"[IKSetup] Applied preset: {preset}");
        }

        private void LoadFromCharacter()
        {
            if (_selectedCharacter == null) return;

            var ikAuth = _selectedCharacter.GetComponent<DIG.Player.Authoring.IK.IKAuthoring>();
            if (ikAuth == null)
            {
                Debug.Log("[IKSetup] No IKAuthoring found, using defaults.");
                return;
            }

            // Foot IK
            _footRayLength = ikAuth.FootRayLength;
            _footOffset = ikAuth.FootOffset;
            _bodyHeightAdjust = ikAuth.BodyHeightAdjustment;
            _footIKWeight = ikAuth.FootIKWeight;
            _footBlendSpeed = ikAuth.FootBlendSpeed;

            // Look At IK
            _lookAtMode = ikAuth.LookAtMode;
            _maxHeadAngle = ikAuth.MaxHeadAngle;
            _maxSpineAngle = ikAuth.MaxSpineAngle;
            _headWeight = ikAuth.HeadWeight;
            _bodyWeight = ikAuth.BodyWeight;
            _lookAtBlendSpeed = ikAuth.LookAtBlendSpeed;

            // Hand IK
            _handWeight = ikAuth.HandWeight;
            _handAdjustSpeed = ikAuth.HandAdjustmentSpeed;
            _handPositionOffset = ikAuth.HandPositionOffset;
            _upperArmWeight = ikAuth.UpperArmWeight;
            _springStiffness = ikAuth.SpringStiffness;
            _springDamping = ikAuth.SpringDamping;

            Debug.Log($"[IKSetup] Loaded settings from {_selectedCharacter.name}");
        }

        private void ApplyToCharacter()
        {
            if (_selectedCharacter == null) return;

            Undo.RecordObject(_selectedCharacter, "Apply IK Settings");

            var ikAuth = _selectedCharacter.GetComponent<DIG.Player.Authoring.IK.IKAuthoring>();
            if (ikAuth == null)
            {
                ikAuth = Undo.AddComponent<DIG.Player.Authoring.IK.IKAuthoring>(_selectedCharacter);
            }

            // Foot IK
            ikAuth.FootRayLength = _footRayLength;
            ikAuth.FootOffset = _footOffset;
            ikAuth.BodyHeightAdjustment = _bodyHeightAdjust;
            ikAuth.FootIKWeight = _enableFootIK ? _footIKWeight : 0f;
            ikAuth.FootBlendSpeed = _footBlendSpeed;

            // Look At IK
            ikAuth.LookAtMode = _enableLookAtIK ? _lookAtMode : LookAtMode.Disabled;
            ikAuth.MaxHeadAngle = _maxHeadAngle;
            ikAuth.MaxSpineAngle = _maxSpineAngle;
            ikAuth.HeadWeight = _headWeight;
            ikAuth.BodyWeight = _bodyWeight;
            ikAuth.LookAtBlendSpeed = _lookAtBlendSpeed;

            // Hand IK
            ikAuth.HandWeight = _enableHandIK ? _handWeight : 0f;
            ikAuth.HandAdjustmentSpeed = _handAdjustSpeed;
            ikAuth.HandPositionOffset = _handPositionOffset;
            ikAuth.UpperArmWeight = _upperArmWeight;
            ikAuth.SpringStiffness = _springStiffness;
            ikAuth.SpringDamping = _springDamping;

            EditorUtility.SetDirty(_selectedCharacter);
            Debug.Log($"[IKSetup] Applied IK settings to {_selectedCharacter.name}");
        }

        private void AddIKAuthoring()
        {
            if (_selectedCharacter == null) return;

            if (_selectedCharacter.GetComponent<DIG.Player.Authoring.IK.IKAuthoring>() != null)
            {
                Debug.Log("[IKSetup] IKAuthoring already exists.");
                return;
            }

            Undo.AddComponent<DIG.Player.Authoring.IK.IKAuthoring>(_selectedCharacter);
            Debug.Log($"[IKSetup] Added IKAuthoring to {_selectedCharacter.name}");
        }

        private void AutoFindIKTargets()
        {
            if (_selectedCharacter == null) return;

            // Look for common IK target names
            string[] leftHandNames = { "LeftHandIK", "IK_LeftHand", "L_Hand_IK", "LeftGrip" };
            string[] rightHandNames = { "RightHandIK", "IK_RightHand", "R_Hand_IK", "RightGrip" };
            string[] aimNames = { "AimTarget", "AimPoint", "LookTarget" };

            foreach (var name in leftHandNames)
            {
                var found = FindChildByName(_selectedCharacter.transform, name);
                if (found != null) { _leftHandIKTarget = found; break; }
            }

            foreach (var name in rightHandNames)
            {
                var found = FindChildByName(_selectedCharacter.transform, name);
                if (found != null) { _rightHandIKTarget = found; break; }
            }

            foreach (var name in aimNames)
            {
                var found = FindChildByName(_selectedCharacter.transform, name);
                if (found != null) { _aimTarget = found; break; }
            }

            Debug.Log($"[IKSetup] Auto-find complete. L:{_leftHandIKTarget?.name ?? "None"} R:{_rightHandIKTarget?.name ?? "None"}");
        }

        private void CreateIKTargetHierarchy()
        {
            if (_selectedCharacter == null) return;

            Undo.SetCurrentGroupName("Create IK Targets");
            int undoGroup = Undo.GetCurrentGroup();

            // Create IK targets container
            var container = new GameObject("IK_Targets");
            Undo.RegisterCreatedObjectUndo(container, "Create IK Container");
            container.transform.SetParent(_selectedCharacter.transform);
            container.transform.localPosition = Vector3.zero;

            // Create left hand target
            var leftHand = new GameObject("LeftHandIK");
            Undo.RegisterCreatedObjectUndo(leftHand, "Create Left Hand IK");
            leftHand.transform.SetParent(container.transform);
            leftHand.transform.localPosition = new Vector3(-0.3f, 1.2f, 0.5f);
            _leftHandIKTarget = leftHand.transform;

            // Create right hand target
            var rightHand = new GameObject("RightHandIK");
            Undo.RegisterCreatedObjectUndo(rightHand, "Create Right Hand IK");
            rightHand.transform.SetParent(container.transform);
            rightHand.transform.localPosition = new Vector3(0.3f, 1.2f, 0.5f);
            _rightHandIKTarget = rightHand.transform;

            // Create aim target
            var aim = new GameObject("AimTarget");
            Undo.RegisterCreatedObjectUndo(aim, "Create Aim Target");
            aim.transform.SetParent(container.transform);
            aim.transform.localPosition = new Vector3(0f, 1.5f, 10f);
            _aimTarget = aim.transform;

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[IKSetup] Created IK target hierarchy.");
        }

        private void TestInScene()
        {
            if (_selectedCharacter == null || !Application.isPlaying)
            {
                Debug.Log("[IKSetup] Enter Play mode to test IK.");
                return;
            }

            // Would trigger IK preview in play mode
            Debug.Log("[IKSetup] IK test mode active.");
        }

        private Transform FindChildByName(Transform parent, string name)
        {
            if (parent.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                return parent;

            foreach (Transform child in parent)
            {
                var found = FindChildByName(child, name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
