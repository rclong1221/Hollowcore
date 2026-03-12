using UnityEngine;
using System.Collections.Generic;
using DIG.Items.Interfaces;
using DIG.Items.Definitions;

namespace DIG.Items.Bridges
{
    /// <summary>
    /// IViewModeHandler implementation for third-person camera mode.
    /// Renders all equipment slots on the full character body.
    /// </summary>
    public class ThirdPersonViewHandler : MonoBehaviour, IViewModeHandler
    {
        [Header("Character")]
        [SerializeField] private Transform _characterRoot;
        [SerializeField] private Animator _animator;
        
        [Header("Attach Points")]
        [SerializeField] private Transform _rightHandAttach;
        [SerializeField] private Transform _leftHandAttach;
        [SerializeField] private Transform _backAttach;
        
        [Header("Slot Configuration")]
        [SerializeField] private List<EquipmentSlotDefinition> _slotDefinitions;
        
        [Header("Debug")]
        [SerializeField] private bool _debugLogging = false;
        
        // Active equipment instances
        private Dictionary<string, GameObject> _activeEquipment = new Dictionary<string, GameObject>();
        
        public ViewMode CurrentMode => ViewMode.ThirdPerson;
        
        private void Awake()
        {
            if (_characterRoot == null)
                _characterRoot = transform;
                
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
                
            AutoFindAttachPoints();
        }
        
        public void Initialize(Transform characterRoot)
        {
            _characterRoot = characterRoot;
            _animator = characterRoot.GetComponentInChildren<Animator>();
            AutoFindAttachPoints();
        }
        
        private void AutoFindAttachPoints()
        {
            if (_animator == null) return;
            
            // Try to find standard attach points from Animator's avatar
            if (_rightHandAttach == null)
                _rightHandAttach = FindBone(HumanBodyBones.RightHand);
                
            if (_leftHandAttach == null)
                _leftHandAttach = FindBone(HumanBodyBones.LeftHand);
                
            if (_backAttach == null)
                _backAttach = FindBone(HumanBodyBones.Spine);
        }
        
        private Transform FindBone(HumanBodyBones bone)
        {
            if (_animator != null && _animator.isHuman)
                return _animator.GetBoneTransform(bone);
            return null;
        }
        
        public void OnViewModeChanged(ViewMode newMode)
        {
            // Third-person supports all modes, no action needed
            if (_debugLogging)
                Debug.Log($"[ThirdPersonViewHandler] View mode changed to {newMode}");
        }
        
        public GameObject RenderEquipment(string slotId, GameObject itemPrefab)
        {
            if (itemPrefab == null) return null;
            
            // Hide existing equipment in this slot
            HideEquipment(slotId);
            
            // Get attach point for slot
            Transform attachPoint = GetAttachPoint(slotId);
            if (attachPoint == null)
            {
                if (_debugLogging)
                    Debug.LogWarning($"[ThirdPersonViewHandler] No attach point for slot {slotId}");
                return null;
            }
            
            // Instantiate and attach
            GameObject instance = Instantiate(itemPrefab, attachPoint);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            
            _activeEquipment[slotId] = instance;
            
            if (_debugLogging)
                Debug.Log($"[ThirdPersonViewHandler] Rendered {itemPrefab.name} in slot {slotId}");
            
            return instance;
        }
        
        public void HideEquipment(string slotId)
        {
            if (_activeEquipment.TryGetValue(slotId, out var existing))
            {
                if (existing != null)
                    Destroy(existing);
                    
                _activeEquipment.Remove(slotId);
                
                if (_debugLogging)
                    Debug.Log($"[ThirdPersonViewHandler] Hidden equipment in slot {slotId}");
            }
        }
        
        public Transform GetAttachPoint(string slotId)
        {
            // Check slot definitions first
            if (_slotDefinitions != null)
            {
                foreach (var slotDef in _slotDefinitions)
                {
                    if (slotDef != null && slotDef.SlotID == slotId)
                    {
                        // Try to get bone from slot definition
                        Transform bone = FindBone(slotDef.AttachmentBone);
                        if (bone != null)
                            return bone;
                            
                        // Try fallback path
                        if (!string.IsNullOrEmpty(slotDef.FallbackAttachPath))
                        {
                            Transform fallback = _characterRoot.Find(slotDef.FallbackAttachPath);
                            if (fallback != null)
                                return fallback;
                        }
                    }
                }
            }
            
            // Default mappings
            switch (slotId)
            {
                case "MainHand":
                    return _rightHandAttach;
                case "OffHand":
                    return _leftHandAttach;
                case "Back":
                    return _backAttach;
                default:
                    return _rightHandAttach;
            }
        }
        
        public bool SupportsSlot(string slotId)
        {
            // Third-person mode supports all slots
            return true;
        }
        
        /// <summary>
        /// Get the currently equipped GameObject for a slot.
        /// </summary>
        public GameObject GetEquippedInstance(string slotId)
        {
            _activeEquipment.TryGetValue(slotId, out var instance);
            return instance;
        }
    }
}
