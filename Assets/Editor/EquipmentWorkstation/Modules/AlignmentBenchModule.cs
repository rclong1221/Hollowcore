using UnityEngine;
using UnityEditor;
using DIG.Items.Authoring;

namespace DIG.Editor.EquipmentWorkstation.Modules
{
    public class AlignmentBenchModule : IEquipmentModule
    {
        // User-assigned prefabs
        private GameObject _characterPrefab;
        private GameObject _weaponPrefab;
        
        // Session instances
        private GameObject _benchInstance;      // Root container
        private GameObject _characterInstance;  // Spawned character
        private GameObject _weaponInstance;     // Spawned weapon
        private Animator _characterAnimator;    // Character's animator
        private Transform _handBone;            // Right hand bone
        
        // Left hand IK ghost
        private GameObject _ghostHandLeft;
        private bool _showLeftHand = true;
        
        private bool _isSessionActive = false;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Alignment Bench", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Align weapons to the character's actual hand pose.", MessageType.Info);

            GUILayout.Space(10);

            // Character Prefab (NEW)
            _characterPrefab = (GameObject)EditorGUILayout.ObjectField("Character Prefab", _characterPrefab, typeof(GameObject), false);
            
            // Weapon Prefab
            _weaponPrefab = (GameObject)EditorGUILayout.ObjectField("Weapon Prefab", _weaponPrefab, typeof(GameObject), false);

            // Validation
            if (_characterPrefab == null || _weaponPrefab == null)
            {
                EditorGUILayout.HelpBox("Assign both Character and Weapon prefabs to begin.", MessageType.Warning);
                return;
            }

            // Check if character has an Animator
            var testAnimator = _characterPrefab.GetComponent<Animator>();
            if (testAnimator == null)
            {
                EditorGUILayout.HelpBox("Character prefab must have an Animator component.", MessageType.Error);
                return;
            }

            GUILayout.Space(10);

            if (!_isSessionActive)
            {
                if (GUILayout.Button("Start Alignment Session", GUILayout.Height(30)))
                {
                    StartSession();
                }
            }
            else
            {
                if (_benchInstance == null)
                {
                    // Safety check: User might have deleted it manually
                    _isSessionActive = false;
                    return;
                }

                EditorGUILayout.HelpBox(
                    "SESSION ACTIVE:\n" +
                    "1. Select the weapon and adjust Position/Rotation to fit the hand.\n" +
                    "2. Optionally adjust 'Ghost_LeftHand_IK' for left hand grip.", 
                    MessageType.Info);

                _showLeftHand = EditorGUILayout.Toggle("Show Left Hand IK Target", _showLeftHand);
                if (_ghostHandLeft != null) _ghostHandLeft.SetActive(_showLeftHand);
                
                GUILayout.Space(10);
                
                if (GUILayout.Button("SAVE TO PREFAB", GUILayout.Height(40)))
                {
                    SaveSession();
                }

                GUILayout.Space(5);

                if (GUILayout.Button("Cancel"))
                {
                    CancelSession();
                }
            }
        }

        private void StartSession()
        {
            // 0. Cleanup orphans from previous crashes
            CleanupOrphans();

            // 1. Create Bench Root
            _benchInstance = new GameObject("Alignment_Bench (TEMP)");
            _benchInstance.transform.position = Vector3.zero;

            // 2. Spawn Character
            _characterInstance = (GameObject)PrefabUtility.InstantiatePrefab(_characterPrefab, _benchInstance.transform);
            _characterInstance.transform.localPosition = Vector3.zero;
            _characterInstance.transform.localRotation = Quaternion.identity;
            _characterInstance.name = "Character_Preview";

            // 3. Get Animator (for future use with animation clips)
            _characterAnimator = _characterInstance.GetComponent<Animator>();
            if (_characterAnimator == null)
            {
                Debug.LogError("[AlignmentBench] Character has no Animator!");
                CancelSession();
                return;
            }

            // 4. Find Socket_MainHand via SocketAuthoring component (matches runtime behavior)
            Transform weaponParent = null;
            var sockets = _characterInstance.GetComponentsInChildren<SocketAuthoring>(true);
            foreach (var socket in sockets)
            {
                if (socket.Type == SocketAuthoring.SocketType.MainHand)
                {
                    weaponParent = socket.transform;
                    break;
                }
            }

            // Fallback to hand bone if no socket found
            if (weaponParent == null)
            {
                if (_characterAnimator.isHuman)
                {
                    weaponParent = _characterAnimator.GetBoneTransform(HumanBodyBones.RightHand);
                }
                else
                {
                    weaponParent = FindChildRecursive(_characterInstance.transform, "RightHand") 
                                ?? FindChildRecursive(_characterInstance.transform, "Hand_R")
                                ?? FindChildRecursive(_characterInstance.transform, "mixamorig:RightHand");
                }
            }

            if (weaponParent == null)
            {
                Debug.LogError("[AlignmentBench] Could not find MainHand socket or hand bone on character!");
                CancelSession();
                return;
            }
            
            _handBone = weaponParent; // Store for reference

            // 5. Instantiate Weapon as child of socket (matches runtime parenting)
            _weaponInstance = (GameObject)PrefabUtility.InstantiatePrefab(_weaponPrefab, weaponParent);
            _weaponInstance.transform.localPosition = Vector3.zero;
            _weaponInstance.transform.localRotation = Quaternion.identity;

            // 6. Apply existing grip data if present
            var existingAuth = _weaponInstance.GetComponent<ItemGripAuthoring>();
            if (existingAuth != null)
            {
                existingAuth.ApplyGrip(_weaponInstance.transform);
                _showLeftHand = (existingAuth.LeftHandIKOverride != null);
            }
            else
            {
                _weaponInstance.AddComponent<ItemGripAuthoring>();
                _showLeftHand = false;
            }

            // 7. Create Ghost Left Hand (IK Target - Child of Weapon)
            _ghostHandLeft = CreateGhostHand("Ghost_LeftHand_IK", new Color(0, 1, 0, 0.5f));
            _ghostHandLeft.transform.SetParent(_weaponInstance.transform, false);
            
            // Initial position
            if (existingAuth != null && existingAuth.LeftHandIKOverride != null)
            {
                Transform childTarget = FindChildRecursive(_weaponInstance.transform, existingAuth.LeftHandIKOverride.name);
                if (childTarget != null)
                {
                    _ghostHandLeft.transform.position = childTarget.position;
                    _ghostHandLeft.transform.rotation = childTarget.rotation;
                }
                else
                {
                    _ghostHandLeft.transform.localPosition = new Vector3(0, 0, 0.3f);
                }
            }
            else
            {
                _ghostHandLeft.transform.localPosition = new Vector3(0, 0, 0.3f);
            }
            
            _ghostHandLeft.SetActive(_showLeftHand);

            // 8. Select weapon (but don't FrameSelected to avoid pink material errors)
            Selection.activeGameObject = _weaponInstance;
            // Note: Removed SceneView.lastActiveSceneView.FrameSelected() to prevent material errors
            
            _isSessionActive = true;
            
            Debug.Log($"[AlignmentBench] Session started. Weapon parented to: {weaponParent.name}");
        }

        private void CleanupOrphans()
        {
            var existing = GameObject.Find("Alignment_Bench (TEMP)");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
                Debug.Log("[AlignmentBench] Cleaned up orphaned session.");
            }
        }

        private GameObject CreateGhostHand(string name, Color? color = null)
        {
            var hand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hand.name = name;
            hand.transform.localScale = new Vector3(0.08f, 0.1f, 0.03f);
            
            // Add visual hint (finger)
            var finger = GameObject.CreatePrimitive(PrimitiveType.Cube);
            finger.name = "Index_Finger_Hint";
            finger.transform.SetParent(hand.transform, false);
            finger.transform.localPosition = new Vector3(0, 0.1f, 0.05f); 
            finger.transform.localScale = new Vector3(0.2f, 1.5f, 0.2f);
            
            // Cleanup Colliders
            Object.DestroyImmediate(hand.GetComponent<Collider>());
            Object.DestroyImmediate(finger.GetComponent<Collider>());
            
            // Use default primitive material - no custom shader needed for editor preview
            // The default material is sufficient for alignment visualization
            // Tint via vertex color or just leave as default white/gray

            return hand;
        }
        
        private Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var result = FindChildRecursive(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private void SaveSession()
        {
            GUI.FocusControl(null);
            
            if (_weaponInstance == null) return;

            // 1. Get Grip Offset (Weapon relative to Hand Bone)
            Vector3 gripPos = _weaponInstance.transform.localPosition;
            Quaternion gripRot = _weaponInstance.transform.localRotation;
            
            // 2. Get Left Hand IK Offset (Ghost relative to Weapon)
            Vector3 leftIkPos = Vector3.zero;
            Quaternion leftIkRot = Quaternion.identity;
            if (_ghostHandLeft != null)
            {
                leftIkPos = _ghostHandLeft.transform.localPosition;
                leftIkRot = _ghostHandLeft.transform.localRotation;
            }

            // 3. Write to Prefab
            string prefabPath = AssetDatabase.GetAssetPath(_weaponPrefab);
            string leftHandTargetName = null;
            
            using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                GameObject prefabRoot = scope.prefabContentsRoot;
                var auth = prefabRoot.GetComponent<ItemGripAuthoring>();
                if (auth == null) auth = prefabRoot.AddComponent<ItemGripAuthoring>();

                // A. Save Grip
                auth.GripPositionOffset = gripPos;
                auth.GripRotationOffset = gripRot;
                
                // B. Save Left Hand IK (if enabled)
                if (_showLeftHand)
                {
                    string targetName = "LeftHandIK_Target";
                    
                    Transform targetTransform = null;
                    if (auth.LeftHandIKOverride != null)
                    {
                        if (auth.LeftHandIKOverride.root == prefabRoot.transform)
                        {
                            targetTransform = auth.LeftHandIKOverride;
                            targetName = targetTransform.name;
                        }
                    }
                    
                    if (targetTransform == null)
                    {
                        targetTransform = FindChildRecursive(prefabRoot.transform, targetName);
                        if (targetTransform == null)
                        {
                            var newObj = new GameObject(targetName);
                            newObj.transform.SetParent(prefabRoot.transform, false);
                            targetTransform = newObj.transform;
                        }
                    }
                    
                    targetTransform.localPosition = leftIkPos;
                    targetTransform.localRotation = leftIkRot;
                    
                    auth.LeftHandIKOverride = targetTransform;
                    EditorUtility.SetDirty(targetTransform);
                    leftHandTargetName = targetTransform.name;
                }
                
                EditorUtility.SetDirty(auth);
            }
            
            AssetDatabase.SaveAssets();

            Debug.Log($"[AlignmentBench] Saved {_weaponPrefab.name}.\n Grip: Pos={gripPos} Rot={gripRot.eulerAngles}" + 
                (_showLeftHand ? $"\n Left Hand IK: {leftIkPos} (Target: {leftHandTargetName})" : "\n Left Hand IK: Unchanged"));

            CancelSession();
        }

        public void CancelSession()
        {
            if (_benchInstance != null)
            {
                Object.DestroyImmediate(_benchInstance);
            }
            _characterInstance = null;
            _weaponInstance = null;
            _characterAnimator = null;
            _handBone = null;
            _ghostHandLeft = null;
            _isSessionActive = false;
        }
    }
}
