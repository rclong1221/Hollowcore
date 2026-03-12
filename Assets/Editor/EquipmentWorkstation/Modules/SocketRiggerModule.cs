using UnityEngine;
using UnityEditor;
using DIG.Items.Authoring;
using DIG.Editor.Utils;

using DIG.Editor.EquipmentWorkstation; // For IEquipmentModule

namespace DIG.Editor.EquipmentWorkstation.Modules
{
    public class SocketRiggerModule : IEquipmentModule
    {
        private GameObject _targetCharacter;
        private Animator _animator;
        private string _statusMessage;
        
        public void OnGUI()
        {
            EditorGUILayout.LabelField("Socket Rigger", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Use this tool to add standard 'Sockets' to your character prefabs. This enables the Universal Asset Pipeline.", MessageType.Info);

            GUILayout.Space(10);
            
            _targetCharacter = (GameObject)EditorGUILayout.ObjectField("Character Prefab", _targetCharacter, typeof(GameObject), true);

            if (_targetCharacter != null)
            {
                if (_animator == null || _animator.gameObject != _targetCharacter)
                    _animator = _targetCharacter.GetComponent<Animator>();

                if (_animator == null)
                {
                    EditorGUILayout.HelpBox("Selected object has no Animator! Cannot auto-detect bones.", MessageType.Error);
                }
                else if (!_animator.isHuman)
                {
                    EditorGUILayout.HelpBox("Animator is not Humanoid! Cannot auto-detect bones.", MessageType.Error);
                }
                else
                {
                    DrawRiggingControls();
                }
            }
            
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox(_statusMessage, MessageType.None);
            }
        }

        private void DrawRiggingControls()
        {
            GUILayout.Space(10);
            if (GUILayout.Button("Auto-Generate Sockets", GUILayout.Height(30)))
            {
                GenerateSockets();
            }
            
            GUILayout.Space(5);
            EditorGUILayout.LabelField("Detected Configuration:", EditorStyles.boldLabel);
            
            DrawSocketStatus(SocketAuthoring.SocketType.MainHand, HumanBodyBones.RightHand);
            DrawSocketStatus(SocketAuthoring.SocketType.OffHand, HumanBodyBones.LeftHand);
            DrawSocketStatus(SocketAuthoring.SocketType.Back, HumanBodyBones.Chest); // Consistent with generation
            DrawSocketStatus(SocketAuthoring.SocketType.Hips, HumanBodyBones.Hips);
        }

        private void DrawSocketStatus(SocketAuthoring.SocketType type, HumanBodyBones bone)
        {
            Transform boneTransform = _animator.GetBoneTransform(bone);
            // Fallback for Back socket if Chest is not mapped (some Generic/Mixamo rigs)
            if (boneTransform == null && type == SocketAuthoring.SocketType.Back)
            {
                boneTransform = _animator.GetBoneTransform(HumanBodyBones.Spine);
            }

            if (boneTransform == null)
            {
                EditorGUILayout.LabelField($"{type}: Bone Missing ({bone})", EditorStyles.miniLabel);
                return;
            }

            var existingSocket = FindSocket(boneTransform, type);
            var style = existingSocket != null ? EditorStyles.label : EditorStyles.boldLabel;
            var text = existingSocket != null ? $"{type}: [OK] Found on {boneTransform.name}" : $"{type}: [MISSING]";
            var color = existingSocket != null ? Color.green : Color.red;

            GUI.color = color;
            EditorGUILayout.LabelField(text, style);
            GUI.color = Color.white;
        }

        private void GenerateSockets()
        {
            if (_animator == null) return;

            bool isAsset = EditorUtility.IsPersistent(_targetCharacter);
            
            if (isAsset)
            {
                string path = AssetDatabase.GetAssetPath(_targetCharacter);
                using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
                {
                    GameObject root = scope.prefabContentsRoot;
                    Animator rootAnimator = root.GetComponent<Animator>();
                    if (rootAnimator == null)
                    {
                         EditorGUILayout.HelpBox("Prefab root has no Animator!", MessageType.Error);
                         return;
                    }
                    
                    int count = 0;
                    count += EnsureSocket(rootAnimator, SocketAuthoring.SocketType.MainHand, HumanBodyBones.RightHand);
                    count += EnsureSocket(rootAnimator, SocketAuthoring.SocketType.OffHand, HumanBodyBones.LeftHand);
                    count += EnsureSocket(rootAnimator, SocketAuthoring.SocketType.Back, HumanBodyBones.Chest); 
                    count += EnsureSocket(rootAnimator, SocketAuthoring.SocketType.Hips, HumanBodyBones.Hips);
                    
                    _statusMessage = $"Rigging Complete. Verified {count} sockets on Prefab.";
                }
            }
            else
            {
                // Scene Instance
                int count = 0;
                count += EnsureSocket(_animator, SocketAuthoring.SocketType.MainHand, HumanBodyBones.RightHand);
                count += EnsureSocket(_animator, SocketAuthoring.SocketType.OffHand, HumanBodyBones.LeftHand);
                count += EnsureSocket(_animator, SocketAuthoring.SocketType.Back, HumanBodyBones.Chest);
                count += EnsureSocket(_animator, SocketAuthoring.SocketType.Hips, HumanBodyBones.Hips);
                
                _statusMessage = $"Rigging Complete. Verified {count} sockets on Instance.";
                EditorUtility.SetDirty(_targetCharacter);
            }
        }

        private int EnsureSocket(Animator anim, SocketAuthoring.SocketType type, HumanBodyBones bone)
        {
            Transform boneTransform = anim.GetBoneTransform(bone);
            if (boneTransform == null) return 0;

            var existing = FindSocket(boneTransform, type);
            if (existing != null) return 0;

            GameObject socketObj = new GameObject($"Socket_{type}");
            socketObj.transform.SetParent(boneTransform, false);
            socketObj.transform.localPosition = Vector3.zero;
            socketObj.transform.localRotation = Quaternion.identity;

            var auth = socketObj.AddComponent<SocketAuthoring>();
            auth.Type = type;

            if (!EditorUtility.IsPersistent(anim.gameObject))
                Undo.RegisterCreatedObjectUndo(socketObj, $"Create {type} Socket");
            
            return 1;
        }

        private SocketAuthoring FindSocket(Transform parent, SocketAuthoring.SocketType type)
        {
            // Search *direct* children first for cleanliness, but could search deeper
            foreach (Transform child in parent)
            {
                var auth = child.GetComponent<SocketAuthoring>();
                if (auth != null && auth.Type == type) return auth;
            }
            return null;
        }
    }
}
