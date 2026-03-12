using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Player.IK;

namespace DIG.Player.View
{
    /// <summary>
    /// Look At IK functionality for PlayerIKBridge.
    /// Handles head/body tracking toward camera aim point.
    /// </summary>
    public partial class PlayerIKBridge
    {
        public bool EnableLogging = false;

        /// <summary>
        /// Applies LookAt IK to have the head/body track a target.
        /// Called on base layer.
        /// </summary>
        private void LookAtTarget()
        {
            if (!_entityManager.HasComponent<LookAtIKState>(_entity)) return;
            
            var lookState = _entityManager.GetComponentData<LookAtIKState>(_entity);
            
            // Also get AimDirection to compare
            float3 aimPoint = float3.zero;
            if (_entityManager.HasComponent<AimDirection>(_entity))
            {
                var aimDir = _entityManager.GetComponentData<AimDirection>(_entity);
                aimPoint = aimDir.AimPoint;
            }
            
            // Debug logging every 300 frames (~5 seconds)
            if (EnableLogging && Time.frameCount % 300 == 0)
            {
                UnityEngine.Debug.Log($"[LookAtIK] Bridge: Entity={_entity.Index}:{_entity.Version} HasTarget={lookState.HasTarget} Weight={lookState.CurrentWeight:F2} Target={lookState.LookTarget} Smoothed={lookState.SmoothedTarget} AimPoint={aimPoint}");
            }
            
            if (lookState.HasTarget && lookState.CurrentWeight > 0.01f)
            {
                _animator.SetLookAtPosition(lookState.LookTarget);

                // ECS calculates simplified weights, but here we can read detailed settings if we wanted
                // For now, assume TargetWeight applies to all for simplicity, or hardcode distribution
                float w = lookState.CurrentWeight;
                _animator.SetLookAtWeight(w, w * 0.3f, w, 0.5f, 0.5f);
            }
        }
    }
}
