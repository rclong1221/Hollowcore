using UnityEngine;
using Unity.Entities;
using DIG.Swimming;

namespace DIG.Swimming.Authoring
{
    /// <summary>
    /// Adds swimming capabilities to an entity (e.g., Player).
    /// Add this to the player prefab.
    /// </summary>
    [AddComponentMenu("DIG/Swimming/Swimming Authoring")]
    public class SwimmingAuthoring : MonoBehaviour
    {
        [Header("State")]
        [Tooltip("Player height for calculating submersion ratio")]
        public float PlayerHeight = 1.8f;
        
        [Tooltip("Ratio of height submerged to enter swim mode (0-1)")]
        [Range(0f, 1f)]
        public float SwimEntryThreshold = 0.6f;
        
        [Tooltip("Ratio of height submerged to exit swim mode (must be less than entry)")]
        [Range(0f, 1f)]
        public float SwimExitThreshold = 0.3f;
        
        [Header("Movement")]
        [Tooltip("Base swim speed (m/s)")]
        public float SwimSpeed = 2f;
        
        [Tooltip("Sprint multiplier")]
        public float SprintMultiplier = 1.5f;
        
        [Tooltip("Vertical speed (ascend/descend)")]
        public float VerticalSpeed = 1.5f;
        
        [Tooltip("Drag coefficient (higher = more resistance)")]
        public float DragCoefficient = 3f;
        
        [Tooltip("Buoyancy force multiplier")]
        public float BuoyancyForce = 2f;
        
        [Header("Breath")]
        [Tooltip("Max breath in seconds")]
        public float MaxBreath = 30f;

        [Tooltip("Breath recovery rate per second")]
        public float BreathRecoveryRate = 10f;

        [Tooltip("Drowning damage per second")]
        public float DrowningDamage = 10f;

        [Header("Physics Adjustments")]
        [Tooltip("Collider height when underwater (smaller prevents wall clipping)")]
        public float UnderwaterColliderHeight = 1.0f;

        [Tooltip("Collider radius when underwater")]
        public float UnderwaterColliderRadius = 0.4f;

        [Header("Surface Positioning")]
        [Tooltip("Speed to anchor at water surface when idle")]
        public float SurfaceAnchorSpeed = 2.0f;

        [Tooltip("Offset from water surface for idle position (negative = below surface)")]
        public float SurfaceAnchorOffset = -0.3f;

        [Tooltip("Distance from surface to trigger anchoring")]
        public float SurfaceThreshold = 0.5f;

        class Baker : Baker<SwimmingAuthoring>
        {
            public override void Bake(SwimmingAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                // Tag
                AddComponent<CanSwim>(entity);
                
                // Components
                AddComponent(entity, new SwimmingState
                {
                    WaterSurfaceY = float.MinValue,
                    SubmersionDepth = 0f,
                    IsSwimming = false,
                    IsSubmerged = false,
                    WaterZoneEntity = Entity.Null,
                    SwimEntryThreshold = authoring.SwimEntryThreshold,
                    SwimExitThreshold = authoring.SwimExitThreshold,
                    PlayerHeight = authoring.PlayerHeight
                });
                
                AddComponent(entity, new SwimmingMovementSettings
                {
                    SwimSpeed = authoring.SwimSpeed,
                    SprintMultiplier = authoring.SprintMultiplier,
                    VerticalSpeed = authoring.VerticalSpeed,
                    DragCoefficient = authoring.DragCoefficient,
                    Acceleration = 4f,
                    Deceleration = 5f,
                    BuoyancyForce = authoring.BuoyancyForce
                });
                
                AddComponent(entity, new BreathState
                {
                    CurrentBreath = authoring.MaxBreath,
                    MaxBreath = authoring.MaxBreath,
                    IsHoldingBreath = false,
                    DrowningDamageTimer = 0f,
                    DrowningDamagePerTick = authoring.DrowningDamage,
                    DrowningDamageInterval = 1f,
                    BreathRecoveryRate = authoring.BreathRecoveryRate
                });

                // Controller state for tracking swim enter/exit
                AddComponent(entity, SwimmingControllerState.Default);

                // Event callbacks for other systems to react to state changes
                AddComponent(entity, SwimmingEvents.Default);

                // Physics settings for collider adjustment and surface positioning
                AddComponent(entity, new SwimmingPhysicsSettings
                {
                    UnderwaterColliderHeight = authoring.UnderwaterColliderHeight,
                    UnderwaterColliderRadius = authoring.UnderwaterColliderRadius,
                    SurfaceAnchorSpeed = authoring.SurfaceAnchorSpeed,
                    SurfaceAnchorOffset = authoring.SurfaceAnchorOffset,
                    SurfaceThreshold = authoring.SurfaceThreshold
                });
            }
        }
    }
}
