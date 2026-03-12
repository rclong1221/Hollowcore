using Unity.Entities;
using UnityEngine;
using DIG.Player.IK;

namespace DIG.Player.View
{
    /// <summary>
    /// Event handling functionality for PlayerIKBridge.
    /// Handles death, respawn, equipment changes, and aim state changes.
    /// </summary>
    public partial class PlayerIKBridge
    {
        /// <summary>
        /// Called when the character dies. Disables all IK immediately.
        /// </summary>
        public void OnDeath()
        {
            _ikEnabled = false;
            
            // Reset all IK weights
            _footIKWeight[0] = 0f;
            _footIKWeight[1] = 0f;
            _handPositionIKWeight[0] = 0f;
            _handPositionIKWeight[1] = 0f;
            _handRotationIKWeight[0] = 0f;
            _handRotationIKWeight[1] = 0f;
            _upperArmWeight = 0f;
            _hipsOffset = 0f;
        }
        
        /// <summary>
        /// Called when the character respawns. Re-enables IK and recalibrates.
        /// </summary>
        public void OnRespawn()
        {
            _ikEnabled = true;
            
            // Force recalibration of foot IK
            _footIKCalibrated = false;
            
            // Clear IK target interpolations
            for (int i = 0; i < IKGoalCount; i++)
            {
                _ikTargetActive[i] = false;
                _ikInterpolationStart[i] = -1f;
                _ikInterpolationPosition[i] = Vector3.zero;
                _ikInterpolationRotation[i] = Quaternion.identity;
            }
        }
        
        /// <summary>
        /// Called when equipment changes. Updates dominant hand and recalculates offsets.
        /// </summary>
        /// <param name="isRightHandDominant">True if right hand holds the weapon</param>
        public void OnEquipmentChanged(bool isRightHandDominant)
        {
            _isRightHandDominant = isRightHandDominant;
            SetDominantHand(isRightHandDominant);
            
            // Store dominant hand position for interpolation
            if (_rightHand != null && _leftHand != null)
            {
                _dominantHandPosition = _isRightHandDominant ? _rightHand.position : _leftHand.position;
                _nonDominantHandPosition = _isRightHandDominant ? _leftHand.position : _rightHand.position;
            }
            
            // Enable second hand pass for two-handed weapons if needed
            // This is set based on the weapon type from the HandIKState
            if (_entityManager != null && _entityManager.Exists(_entity) && _entityManager.HasComponent<HandIKState>(_entity))
            {
                var handState = _entityManager.GetComponentData<HandIKState>(_entity);
                _requireSecondHandPositioning = handState.IsAiming || handState.IsUsingItem;
            }
        }
        
        /// <summary>
        /// Called when aiming starts/stops. Updates hand IK weights smoothly.
        /// </summary>
        /// <param name="isAiming">True if currently aiming</param>
        public void OnAimStateChanged(bool isAiming)
        {
            if (!_entityManager.Exists(_entity) || !_entityManager.HasComponent<HandIKState>(_entity)) return;
            
            var handState = _entityManager.GetComponentData<HandIKState>(_entity);
            handState.IsAiming = isAiming;
            _entityManager.SetComponentData(_entity, handState);
            
            _requireSecondHandPositioning = isAiming;
        }
        
        /// <summary>
        /// Called when item use starts/stops (non-weapon items like health potions).
        /// </summary>
        /// <param name="isUsing">True if using an item</param>
        public void OnItemUseStateChanged(bool isUsing)
        {
            if (!_entityManager.Exists(_entity) || !_entityManager.HasComponent<HandIKState>(_entity)) return;
            
            var handState = _entityManager.GetComponentData<HandIKState>(_entity);
            handState.IsUsingItem = isUsing;
            _entityManager.SetComponentData(_entity, handState);
        }
    }
}
