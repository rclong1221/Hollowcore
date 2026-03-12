using UnityEngine;
using DIG.Items.Interfaces;

namespace DIG.Items.Bridges
{
    /// <summary>
    /// IAnimatorBridge implementation for Unity's Mecanim state machine system.
    /// Uses standard animator parameter naming conventions.
    /// </summary>
    public class MecanimAnimatorBridge : MonoBehaviour, IAnimatorBridge
    {
        [Header("Animator")]
        [SerializeField] private Animator _animator;
        
        [Header("Parameter Names")]
        [SerializeField] private string _slot0ItemIdParam = "Slot0ItemID";
        [SerializeField] private string _slot0ItemStateParam = "Slot0ItemStateIndex";
        [SerializeField] private string _slot0ItemChangeParam = "Slot0ItemStateIndexChange";
        [SerializeField] private string _slot0SubstateParam = "Slot0ItemSubstateIndex";
        [SerializeField] private string _slot1ItemIdParam = "Slot1ItemID";
        [SerializeField] private string _slot1ItemStateParam = "Slot1ItemStateIndex";
        [SerializeField] private string _slot1ItemChangeParam = "Slot1ItemStateIndexChange";
        [SerializeField] private string _movementSetParam = "MovementSetID";
        [SerializeField] private string _aimingParam = "Aiming";
        
        [Header("Debug")]
        [SerializeField] private bool _debugLogging = false;
        
        // Cached hashes
        private int _hashSlot0ItemId;
        private int _hashSlot0ItemState;
        private int _hashSlot0ItemChange;
        private int _hashSlot0Substate;
        private int _hashSlot1ItemId;
        private int _hashSlot1ItemState;
        private int _hashSlot1ItemChange;
        private int _hashMovementSet;
        private int _hashAiming;
        
        // State tracking
        private int[] _currentItemIds = new int[2];
        private int[] _currentStates = new int[2];
        private bool _isBlocking;
        private bool _isAiming;
        
        public Animator Animator => _animator;
        
        private void Awake()
        {
            if (_animator == null)
                _animator = GetComponent<Animator>();
                
            if (_animator != null)
                Initialize(_animator);
        }
        
        public void Initialize(Animator animator)
        {
            _animator = animator;
            
            // Cache parameter hashes
            _hashSlot0ItemId = Animator.StringToHash(_slot0ItemIdParam);
            _hashSlot0ItemState = Animator.StringToHash(_slot0ItemStateParam);
            _hashSlot0ItemChange = Animator.StringToHash(_slot0ItemChangeParam);
            _hashSlot0Substate = Animator.StringToHash(_slot0SubstateParam);
            _hashSlot1ItemId = Animator.StringToHash(_slot1ItemIdParam);
            _hashSlot1ItemState = Animator.StringToHash(_slot1ItemStateParam);
            _hashSlot1ItemChange = Animator.StringToHash(_slot1ItemChangeParam);
            _hashMovementSet = Animator.StringToHash(_movementSetParam);
            _hashAiming = Animator.StringToHash(_aimingParam);
            
            if (_debugLogging)
                Debug.Log($"[MecanimAnimatorBridge] Initialized with Animator: {animator.name}");
        }
        
        public void SetEquippedItem(int slotIndex, int itemId)
        {
            if (_animator == null) return;
            
            _currentItemIds[slotIndex] = itemId;
            
            int hash = slotIndex == 0 ? _hashSlot0ItemId : _hashSlot1ItemId;
            _animator.SetInteger(hash, itemId);
            
            if (_debugLogging)
                Debug.Log($"[OpsiveAnimatorBridge] SetEquippedItem Slot{slotIndex} ItemID={itemId}");
        }
        
        public void TriggerAction(int slotIndex, int stateIndex)
        {
            if (_animator == null) return;
            
            _currentStates[slotIndex] = stateIndex;
            
            int stateHash = slotIndex == 0 ? _hashSlot0ItemState : _hashSlot1ItemState;
            int changeHash = slotIndex == 0 ? _hashSlot0ItemChange : _hashSlot1ItemChange;
            
            _animator.SetInteger(stateHash, stateIndex);
            _animator.SetTrigger(changeHash);
            
            if (_debugLogging)
                Debug.Log($"[OpsiveAnimatorBridge] TriggerAction Slot{slotIndex} State={stateIndex}");
        }
        
        public void SetMovementSet(int movementSetId)
        {
            if (_animator == null) return;
            
            _animator.SetInteger(_hashMovementSet, movementSetId);
            
            if (_debugLogging)
                Debug.Log($"[OpsiveAnimatorBridge] SetMovementSet ID={movementSetId}");
        }
        
        public int GetCurrentState(int slotIndex)
        {
            return _currentStates[slotIndex];
        }
        
        public bool IsAnimationComplete(int slotIndex)
        {
            if (_animator == null) return true;
            
            // Check if the current state has finished by looking at normalized time
            var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.normalizedTime >= 1.0f;
        }
        
        public void CancelAction(int slotIndex)
        {
            // Reset to idle state
            TriggerAction(slotIndex, 0);
        }
        
        public void SetAimActive(bool isAiming)
        {
            if (_animator == null) return;
            
            _isAiming = isAiming;
            _animator.SetBool(_hashAiming, isAiming);
            
            if (_debugLogging)
                Debug.Log($"[OpsiveAnimatorBridge] SetAimActive={isAiming}");
        }
        
        public void SetBlocking(bool isBlocking)
        {
            _isBlocking = isBlocking;
            
            // Block state is typically handled via Slot1ItemStateIndex = 3
            if (isBlocking)
                TriggerAction(1, 3); // 3 = Use/Block state
            else
                TriggerAction(1, 0); // 0 = Idle
        }
        
        public void SetLayerWeight(string layerName, float weight)
        {
            if (_animator == null) return;
            
            int layerIndex = _animator.GetLayerIndex(layerName);
            if (layerIndex >= 0)
            {
                _animator.SetLayerWeight(layerIndex, weight);
                
                if (_debugLogging)
                    Debug.Log($"[OpsiveAnimatorBridge] SetLayerWeight {layerName}={weight}");
            }
        }
        
        public void OnUpdate()
        {
            // Override point for per-frame updates
            // Currently handled by specific weapon type logic
        }
        
        /// <summary>
        /// Set substate index for combo attacks.
        /// </summary>
        public void SetSubstate(int slotIndex, int substateIndex)
        {
            if (_animator == null) return;
            
            if (slotIndex == 0)
                _animator.SetInteger(_hashSlot0Substate, substateIndex);
                
            if (_debugLogging)
                Debug.Log($"[OpsiveAnimatorBridge] SetSubstate Slot{slotIndex} Substate={substateIndex}");
        }
    }
}
