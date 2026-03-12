using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Targeting.Core;

namespace DIG.Targeting.Authoring
{
    /// <summary>
    /// Authoring component for the Targeting Module child entity.
    /// 
    /// SETUP INSTRUCTIONS:
    /// 1. Create a child GameObject under the Player prefab named "TargetingModule"
    /// 2. Add this TargetingModuleAuthoring component to that child
    /// 3. The baker will create a separate entity with all advanced targeting components
    /// 4. The parent Player's TargetingModuleLink will reference this entity at runtime
    /// 
    /// This architecture keeps the Player archetype under the 16KB chunk size limit
    /// while still providing full targeting functionality.
    /// </summary>
    [DisallowMultipleComponent]
    public class TargetingModuleAuthoring : MonoBehaviour
    {
        [Header("Multi-Lock Settings")]
        [Tooltip("Maximum number of targets that can be locked simultaneously")]
        public int MaxMultiLockTargets = 6;
        
        [Header("Over-the-Shoulder Settings")]
        [Tooltip("Shoulder offset multiplier (positive = right shoulder, negative = left)")]
        public float DefaultShoulderSide = 1f;
        [Tooltip("FOV multiplier when aiming down sights (lower = more zoom)")]
        public float AimingZoom = 0.7f;
        
        class Baker : Baker<TargetingModuleAuthoring>
        {
            public override void Bake(TargetingModuleAuthoring authoring)
            {
                // Use None because this is a pure data entity - no transform needed
                var entity = GetEntity(TransformUsageFlags.None);
                
                // Tag this as a targeting module
                AddComponent<TargetingModuleTag>(entity);
                
                // Back-reference to owner will be set at runtime by TargetingModuleLinkSystem
                AddComponent(entity, new TargetingModuleOwner { Owner = Entity.Null });
                
                // ═══════════════════════════════════════════════════════════════════
                // EPIC 15.16: ADVANCED TARGETING COMPONENTS
                // ═══════════════════════════════════════════════════════════════════
                
                // Aim Assist State (Sticky Aim + Magnetism)
                AddComponent(entity, new AimAssistState
                {
                    StickyTarget = Entity.Null,
                    CurrentStickyStrength = 0f,
                    MagnetismPull = float2.zero,
                    InStickyZone = false
                });
                
                // Part Targeting State (for body part targeting like heads, limbs)
                AddComponent(entity, new PartTargetingState
                {
                    CurrentPartIndex = 0,
                    PartOffset = float3.zero,
                    CurrentDamageMultiplier = 1f
                });
                
                // Predictive Aim State (lead indicator calculation)
                AddComponent(entity, new PredictiveAimState
                {
                    TargetVelocity = float3.zero,
                    PreviousTargetPosition = float3.zero,
                    PredictedAimPoint = float3.zero,
                    TimeToIntercept = 0f,
                    IsValid = false
                });
                
                // Multi-Lock State (for missile salvos, etc.)
                AddComponent(entity, new MultiLockState
                {
                    LockedCount = 0,
                    IsAccumulating = false,
                    ReadyToFire = false,
                    MaxTargets = authoring.MaxMultiLockTargets
                });
                
                // Buffer for multiple locked targets
                AddBuffer<LockedTargetElement>(entity);
                
                // Over-the-Shoulder State (shoulder swap, ADS zoom)
                AddComponent(entity, new OverTheShoulderState
                {
                    CurrentShoulderSide = authoring.DefaultShoulderSide,
                    DesiredShoulderSide = authoring.DefaultShoulderSide,
                    IsAiming = false,
                    CurrentZoom = 1f,
                    DesiredZoom = authoring.AimingZoom
                });
            }
        }
    }
}
