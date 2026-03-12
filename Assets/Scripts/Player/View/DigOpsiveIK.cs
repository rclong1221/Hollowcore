using UnityEngine;

namespace DIG.Player.View
{
    /// <summary>
    /// A lightweight replacement for Opsive's CharacterIK that drives IK weights purely from 
    /// Animator curves (e.g., "LeftHandIKWeight", "RightHandIKWeight") and assigned targets.
    /// This allows us to use Opsive's animation assets without depending on the heavy 
    /// UltimateCharacterLocomotion component.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class DigOpsiveIK : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Layer index for Upper Body in the Animator (usually 4 in Opsive Demo)")]
        [SerializeField] private int _upperBodyLayerIndex = 4;
        
        [Tooltip("Weight multiplier for IK")]
        [Range(0f, 1f)]
        [SerializeField] private float _globalWeight = 1.0f;

        [Header("Debug")]
        [SerializeField] private bool _debugLogging = false;
        
        // IK Targets (set by external bridges like WeaponEquipVisualBridge)
        public Transform LeftHandIKTarget { get; set; }
        public Transform RightHandIKTarget { get; set; }
        public Transform LeftHandIKHint { get; set; } // Elbow
        public Transform RightHandIKHint { get; set; } // Elbow

        // Curve IDs (Cached for performance)
        private int _hashLeftHandWeight;
        private int _hashRightHandWeight;
        
        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _hashLeftHandWeight = Animator.StringToHash("LeftHandIKWeight");
            _hashRightHandWeight = Animator.StringToHash("RightHandIKWeight");
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null) return;

            // We only care about the layer that controls upper body IK (usually 4)
            // Opsive Logic: OnAnimatorIK runs for every layer. We should only apply it once or on the specific layer.
            // Usually, IK application logic in Unity happens on the base layer (0) or the layer with "IK Pass" enabled.
            // Ensure "IK Pass" is enabled on your Animator Controller Layer!
            
            // If we want to strictly follow Opsive's pattern, they apply logic when layer matches.
            // But realistically, we just need to apply it once per frame. 
            // Checking layerIndex == _upperBodyLayerIndex ensures we only do it when that layer is processed.
            // NOTE: If _upperBodyLayerIndex is NOT set to "IK Pass" in the controller, this callback won't happen for that layer.
            // Safest bet: Run on Base Layer (0) if we are sure we want global override, 
            // OR run on specific layer if we want it tied to that state machine.
            // Let's stick to the configured layer index.
            
            // Ensure we run at least once per frame, preferably on the base layer which always runs.
            if (layerIndex != 0 && layerIndex != _upperBodyLayerIndex) return;

            // Debug Logic
            if (_debugLogging)
            {
                float wL = _animator.GetFloat(_hashLeftHandWeight);
                if (wL > 0) Debug.Log($"[DigOpsiveIK] Layer {layerIndex}: LeftHandWeight={wL}, Target={LeftHandIKTarget?.name}");
            }

            ApplyHandIK(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, LeftHandIKTarget, LeftHandIKHint, _hashLeftHandWeight);
            ApplyHandIK(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, RightHandIKTarget, RightHandIKHint, _hashRightHandWeight);
        }

        private void ApplyHandIK(AvatarIKGoal goal, AvatarIKHint hint, Transform target, Transform hintTarget, int weightHash)
        {
            if (target == null)
            {
                _animator.SetIKPositionWeight(goal, 0f);
                _animator.SetIKRotationWeight(goal, 0f);
                return;
            }

            // Get weight from Animator Curve
            float curveWeight = _animator.GetFloat(weightHash);
            
            // Allow debugging to trace missing curves
            if (_debugLogging && curveWeight > 0)
            {
                // verify curve existence
            }

            float finalWeight = curveWeight * _globalWeight;

            if (finalWeight > 0.001f)
            {
                _animator.SetIKPositionWeight(goal, finalWeight);
                _animator.SetIKRotationWeight(goal, finalWeight);
                _animator.SetIKPosition(goal, target.position);
                _animator.SetIKRotation(goal, target.rotation);

                if (hintTarget != null)
                {
                    _animator.SetIKHintPositionWeight(hint, finalWeight);
                    _animator.SetIKHintPosition(hint, hintTarget.position);
                }
            }
            else
            {
                _animator.SetIKPositionWeight(goal, 0f);
                _animator.SetIKRotationWeight(goal, 0f);
            }
        }
    }
}
