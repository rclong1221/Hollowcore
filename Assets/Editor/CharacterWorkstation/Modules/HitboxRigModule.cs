using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.CharacterWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 CW-01: Hitbox Rig module.
    /// Auto-generate hitboxes from skeleton, region painter, damage multiplier presets.
    /// </summary>
    public class HitboxRigModule : ICharacterModule
    {
        private GameObject _selectedCharacter;
        private Vector2 _scrollPosition;
        
        // Bone mapping
        private List<BoneHitbox> _boneHitboxes = new List<BoneHitbox>();
        private bool _hasAnalyzed = false;
        
        // Presets
        private enum DamagePreset { Shooter, SoulsLike, Arcade, Custom }
        private DamagePreset _preset = DamagePreset.Shooter;
        
        // Hitbox settings
        private float _headMultiplier = 2.0f;
        private float _torsoMultiplier = 1.0f;
        private float _armsMultiplier = 0.75f;
        private float _legsMultiplier = 0.8f;
        
        private struct BoneHitbox
        {
            public Transform Bone;
            public string BoneName;
            public HitboxRegion Region;
            public float Radius;
            public float Height;
            public bool Enabled;
        }

        private enum HitboxRegion { Head, Torso, LeftArm, RightArm, LeftLeg, RightLeg }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Hitbox Rig Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Auto-generate hitboxes from character skeleton. " +
                "Assign damage regions and multipliers for precise hit detection.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawCharacterSelection();
            EditorGUILayout.Space(10);
            DrawPresetSelection();
            EditorGUILayout.Space(10);
            DrawBoneList();
            EditorGUILayout.Space(10);
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawCharacterSelection()
        {
            EditorGUILayout.LabelField("Target Character", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            _selectedCharacter = (GameObject)EditorGUILayout.ObjectField(
                "Character Prefab", _selectedCharacter, typeof(GameObject), true);
            
            if (EditorGUI.EndChangeCheck())
            {
                _hasAnalyzed = false;
            }

            if (Selection.activeGameObject != null && _selectedCharacter != Selection.activeGameObject)
            {
                if (GUILayout.Button($"Use Selected: {Selection.activeGameObject.name}", EditorStyles.miniButton))
                {
                    _selectedCharacter = Selection.activeGameObject;
                    _hasAnalyzed = false;
                }
            }

            if (_selectedCharacter != null && !_hasAnalyzed)
            {
                if (GUILayout.Button("Analyze Skeleton", GUILayout.Height(25)))
                {
                    AnalyzeSkeleton();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPresetSelection()
        {
            EditorGUILayout.LabelField("Damage Multiplier Preset", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            _preset = (DamagePreset)EditorGUILayout.EnumPopup("Preset", _preset);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyPreset();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Shooter")) { _preset = DamagePreset.Shooter; ApplyPreset(); }
            if (GUILayout.Button("Souls-Like")) { _preset = DamagePreset.SoulsLike; ApplyPreset(); }
            if (GUILayout.Button("Arcade")) { _preset = DamagePreset.Arcade; ApplyPreset(); }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            
            _headMultiplier = EditorGUILayout.Slider("Head", _headMultiplier, 0.5f, 4f);
            _torsoMultiplier = EditorGUILayout.Slider("Torso", _torsoMultiplier, 0.5f, 2f);
            _armsMultiplier = EditorGUILayout.Slider("Arms", _armsMultiplier, 0.25f, 1.5f);
            _legsMultiplier = EditorGUILayout.Slider("Legs", _legsMultiplier, 0.25f, 1.5f);

            EditorGUILayout.EndVertical();
        }

        private void DrawBoneList()
        {
            if (!_hasAnalyzed || _boneHitboxes.Count == 0) return;

            EditorGUILayout.LabelField($"Bone Hitboxes ({_boneHitboxes.Count})", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MaxHeight(300));

            // Header
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(20));
            GUILayout.Label("Bone", EditorStyles.boldLabel, GUILayout.Width(150));
            GUILayout.Label("Region", EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.Label("Radius", EditorStyles.boldLabel, GUILayout.Width(60));
            GUILayout.Label("Height", EditorStyles.boldLabel, GUILayout.Width(60));
            GUILayout.Label("Multi", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < _boneHitboxes.Count; i++)
            {
                var hitbox = _boneHitboxes[i];
                
                EditorGUILayout.BeginHorizontal();
                
                hitbox.Enabled = EditorGUILayout.Toggle(hitbox.Enabled, GUILayout.Width(20));
                
                EditorGUI.BeginDisabledGroup(!hitbox.Enabled);
                
                GUILayout.Label(hitbox.BoneName, GUILayout.Width(150));
                hitbox.Region = (HitboxRegion)EditorGUILayout.EnumPopup(hitbox.Region, GUILayout.Width(80));
                hitbox.Radius = EditorGUILayout.FloatField(hitbox.Radius, GUILayout.Width(60));
                hitbox.Height = EditorGUILayout.FloatField(hitbox.Height, GUILayout.Width(60));
                
                float mult = GetMultiplierForRegion(hitbox.Region);
                GUI.color = mult > 1.5f ? Color.red : (mult < 1f ? Color.cyan : Color.white);
                GUILayout.Label($"{mult:F2}x", GUILayout.Width(50));
                GUI.color = Color.white;
                
                EditorGUI.EndDisabledGroup();
                
                EditorGUILayout.EndHorizontal();
                
                _boneHitboxes[i] = hitbox;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginDisabledGroup(!_hasAnalyzed || _selectedCharacter == null);

            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Generate Hitboxes", GUILayout.Height(30)))
            {
                GenerateHitboxes();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Clear Hitboxes", GUILayout.Height(30)))
            {
                ClearHitboxes();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All")) SetAllEnabled(true);
            if (GUILayout.Button("Deselect All")) SetAllEnabled(false);
            if (GUILayout.Button("Auto-Size")) AutoSizeHitboxes();
            EditorGUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
        }

        private void ApplyPreset()
        {
            switch (_preset)
            {
                case DamagePreset.Shooter:
                    _headMultiplier = 2.0f;
                    _torsoMultiplier = 1.0f;
                    _armsMultiplier = 0.75f;
                    _legsMultiplier = 0.8f;
                    break;
                case DamagePreset.SoulsLike:
                    _headMultiplier = 1.3f;
                    _torsoMultiplier = 1.0f;
                    _armsMultiplier = 1.0f;
                    _legsMultiplier = 0.9f;
                    break;
                case DamagePreset.Arcade:
                    _headMultiplier = 1.5f;
                    _torsoMultiplier = 1.0f;
                    _armsMultiplier = 1.0f;
                    _legsMultiplier = 1.0f;
                    break;
            }
        }

        private float GetMultiplierForRegion(HitboxRegion region)
        {
            switch (region)
            {
                case HitboxRegion.Head: return _headMultiplier;
                case HitboxRegion.Torso: return _torsoMultiplier;
                case HitboxRegion.LeftArm:
                case HitboxRegion.RightArm: return _armsMultiplier;
                case HitboxRegion.LeftLeg:
                case HitboxRegion.RightLeg: return _legsMultiplier;
                default: return 1.0f;
            }
        }

        private void AnalyzeSkeleton()
        {
            _boneHitboxes.Clear();

            if (_selectedCharacter == null) return;

            var animator = _selectedCharacter.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                // Use humanoid mapping
                AddHumanoidBone(animator, HumanBodyBones.Head, HitboxRegion.Head, 0.12f, 0.2f);
                AddHumanoidBone(animator, HumanBodyBones.Chest, HitboxRegion.Torso, 0.2f, 0.35f);
                AddHumanoidBone(animator, HumanBodyBones.Spine, HitboxRegion.Torso, 0.18f, 0.25f);
                AddHumanoidBone(animator, HumanBodyBones.Hips, HitboxRegion.Torso, 0.15f, 0.2f);
                AddHumanoidBone(animator, HumanBodyBones.LeftUpperArm, HitboxRegion.LeftArm, 0.06f, 0.25f);
                AddHumanoidBone(animator, HumanBodyBones.LeftLowerArm, HitboxRegion.LeftArm, 0.05f, 0.22f);
                AddHumanoidBone(animator, HumanBodyBones.RightUpperArm, HitboxRegion.RightArm, 0.06f, 0.25f);
                AddHumanoidBone(animator, HumanBodyBones.RightLowerArm, HitboxRegion.RightArm, 0.05f, 0.22f);
                AddHumanoidBone(animator, HumanBodyBones.LeftUpperLeg, HitboxRegion.LeftLeg, 0.08f, 0.4f);
                AddHumanoidBone(animator, HumanBodyBones.LeftLowerLeg, HitboxRegion.LeftLeg, 0.06f, 0.35f);
                AddHumanoidBone(animator, HumanBodyBones.RightUpperLeg, HitboxRegion.RightLeg, 0.08f, 0.4f);
                AddHumanoidBone(animator, HumanBodyBones.RightLowerLeg, HitboxRegion.RightLeg, 0.06f, 0.35f);
            }
            else
            {
                // Fallback: scan all transforms
                var transforms = _selectedCharacter.GetComponentsInChildren<Transform>();
                foreach (var t in transforms)
                {
                    var region = GuessRegionFromName(t.name);
                    if (region.HasValue)
                    {
                        _boneHitboxes.Add(new BoneHitbox
                        {
                            Bone = t,
                            BoneName = t.name,
                            Region = region.Value,
                            Radius = 0.1f,
                            Height = 0.2f,
                            Enabled = true
                        });
                    }
                }
            }

            _hasAnalyzed = true;
            Debug.Log($"[HitboxRig] Analyzed skeleton, found {_boneHitboxes.Count} bones");
        }

        private void AddHumanoidBone(Animator animator, HumanBodyBones bone, HitboxRegion region, float radius, float height)
        {
            var boneTransform = animator.GetBoneTransform(bone);
            if (boneTransform != null)
            {
                _boneHitboxes.Add(new BoneHitbox
                {
                    Bone = boneTransform,
                    BoneName = bone.ToString(),
                    Region = region,
                    Radius = radius,
                    Height = height,
                    Enabled = true
                });
            }
        }

        private HitboxRegion? GuessRegionFromName(string name)
        {
            name = name.ToLower();
            if (name.Contains("head") || name.Contains("skull")) return HitboxRegion.Head;
            if (name.Contains("spine") || name.Contains("chest") || name.Contains("torso")) return HitboxRegion.Torso;
            if (name.Contains("left") && (name.Contains("arm") || name.Contains("shoulder"))) return HitboxRegion.LeftArm;
            if (name.Contains("right") && (name.Contains("arm") || name.Contains("shoulder"))) return HitboxRegion.RightArm;
            if (name.Contains("left") && (name.Contains("leg") || name.Contains("thigh"))) return HitboxRegion.LeftLeg;
            if (name.Contains("right") && (name.Contains("leg") || name.Contains("thigh"))) return HitboxRegion.RightLeg;
            return null;
        }

        private void GenerateHitboxes()
        {
            if (_selectedCharacter == null) return;

            Undo.RegisterFullObjectHierarchyUndo(_selectedCharacter, "Generate Hitboxes");

            foreach (var hitbox in _boneHitboxes.Where(h => h.Enabled))
            {
                // Create hitbox child object
                var hitboxObj = new GameObject($"Hitbox_{hitbox.BoneName}");
                hitboxObj.transform.SetParent(hitbox.Bone);
                hitboxObj.transform.localPosition = Vector3.zero;
                hitboxObj.transform.localRotation = Quaternion.identity;
                hitboxObj.layer = LayerMask.NameToLayer("Hitbox");

                // Add capsule collider
                var capsule = hitboxObj.AddComponent<CapsuleCollider>();
                capsule.radius = hitbox.Radius;
                capsule.height = hitbox.Height;
                capsule.isTrigger = true;

                // Add Hitbox component if it exists
                var hitboxType = System.Type.GetType("Player.Components.Hitbox, Assembly-CSharp");
                if (hitboxType != null)
                {
                    hitboxObj.AddComponent(hitboxType);
                }

                Undo.RegisterCreatedObjectUndo(hitboxObj, "Create Hitbox");
            }

            Debug.Log($"[HitboxRig] Generated {_boneHitboxes.Count(h => h.Enabled)} hitboxes");
        }

        private void ClearHitboxes()
        {
            if (_selectedCharacter == null) return;

            var hitboxes = _selectedCharacter.GetComponentsInChildren<Transform>()
                .Where(t => t.name.StartsWith("Hitbox_"))
                .ToList();

            foreach (var h in hitboxes)
            {
                Undo.DestroyObjectImmediate(h.gameObject);
            }

            Debug.Log($"[HitboxRig] Cleared {hitboxes.Count} hitboxes");
        }

        private void SetAllEnabled(bool enabled)
        {
            for (int i = 0; i < _boneHitboxes.Count; i++)
            {
                var h = _boneHitboxes[i];
                h.Enabled = enabled;
                _boneHitboxes[i] = h;
            }
        }

        private void AutoSizeHitboxes()
        {
            // TODO: Use mesh bounds to auto-calculate hitbox sizes
            Debug.Log("[HitboxRig] Auto-size not yet implemented");
        }
    }
}
