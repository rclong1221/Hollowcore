using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Player.Components;

namespace Player.Authoring
{
    /// <summary>
    /// Authoring component for FreeClimb system.
    /// Adds FreeClimbSettings, FreeClimbState, and FreeClimbLocalState to the player entity.
    /// Attach this to the player prefab to enable climbing.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Player/FreeClimb Settings Authoring")]
    public class FreeClimbSettingsAuthoring : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Base climbing speed in m/s")]
        public float ClimbSpeed = 2.0f;

        [Tooltip("Sprint multiplier while climbing")]
        public float SprintSpeedMultiplier = 1.5f;

        [Tooltip("Rotation speed when turning on surface")]
        public float RotationSpeed = 10.0f;

        [Header("Positioning")]
        [Tooltip("Distance from surface to player center")]
        public float SurfaceOffset = 0.4f;

        [Tooltip("Offset from grip to hand targets (usually Y = shoulder height)")]
        public Vector3 HandTargetOffset = new Vector3(0, 1.6f, 0);

        [Header("Cooldowns")]
        [Tooltip("Cooldown after dismount before remounting")]
        public float ReMountCooldown = 0.5f;

        [Tooltip("Speed of mount/dismount transition animation")]
        public float MountTransitionSpeed = 2.5f;

        [Header("Detection")]
        [Tooltip("Radius to detect climbable surfaces")]
        public float DetectionRadius = 0.8f;

        [Tooltip("Distance to detect climbable surfaces")]
        public float DetectionDistance = 1.0f;

        [Tooltip("Distance to cast for surface verification")]
        public float SurfaceCheckDistance = 1.0f;

        [Tooltip("Layers that are climbable")]
        public LayerMask ClimbableLayers = ~0;

        [Tooltip("Auto-climb when falling onto climbable surface")]
        public bool AutoClimbLedge = true;

        [Header("Collision")]
        [Tooltip("Multiplier for climb collider height")]
        public float ClimbColliderHeightMultiplier = 1.0f;

        [Tooltip("Multiplier for climb collider radius")]
        public float ClimbColliderRadiusMultiplier = 0.8f;

        [Tooltip("Layers to check for obstacles")]
        public LayerMask ObstacleLayers = ~0;

        [Tooltip("Minimum angle from up for valid surface (degrees)")]
        public float MinSurfaceAngle = 45f;

        [Tooltip("Maximum angle from up for valid surface (degrees)")]
        public float MaxSurfaceAngle = 135f;

        [Header("Stamina")]
        [Tooltip("Stamina cost per second while climbing")]
        public float StaminaCost = 5f;

        [Tooltip("Delay before stamina recovers after climbing")]
        public float ClimbStaminaRecoveryDelay = 1f;

        [Header("Ledge Detection")]
        [Tooltip("Height above grip to check for ledge")]
        public float LedgeCheckHeight = 0.5f;

        [Tooltip("Depth to check for ledge surface")]
        public float LedgeCheckDepth = 0.5f;

        [Tooltip("Minimum ledge thickness")]
        public float LedgeMinThickness = 0.1f;

        [Tooltip("Layers for ledge top detection")]
        public LayerMask LedgeTopLayers = ~0;

        [Tooltip("Height to check for ceiling obstruction")]
        public float CeilingCheckHeight = 2.0f;

        [Header("Dismount")]
        [Tooltip("Cooldown after dismount")]
        public float DismountCooldown = 0.3f;

        [Tooltip("Vertical velocity threshold for fast fall dismount")]
        public float FastFallThreshold = -5f;

        [Tooltip("Distance to check for ground")]
        public float GroundCheckDistance = 0.5f;

        [Tooltip("Push force when dismounting")]
        public float DismountPushForce = 3f;

        [Tooltip("Upward force when dismounting")]
        public float DismountUpForce = 2f;

        [Header("Wall Jump")]
        [Tooltip("Input threshold to trigger wall jump")]
        public float WallJumpInputThreshold = 0.5f;

        [Tooltip("Maximum wall jump distance")]
        public float WallJumpMaxDistance = 3f;

        [Tooltip("Minimum wall jump distance")]
        public float WallJumpMinDistance = 0.5f;

        [Tooltip("Wall jump transition speed")]
        public float WallJumpSpeed = 5f;

        [Tooltip("Depth to check for target surface")]
        public float WallJumpDepth = 0.3f;

        [Header("Object Gravity")]
        [Tooltip("How strongly pulled toward surface (0-1)")]
        public float AdhesionStrength = 1.0f;

        [Tooltip("Sphere cast radius for multi-directional detection")]
        public float SurfaceDetectionRadius = 0.5f;

        [Tooltip("Distance at which adhesion starts weakening")]
        public float AdhesionFalloffDistance = 1.0f;

        [Tooltip("Speed of interpolation between surfaces")]
        public float SurfaceTransitionSpeed = 10.0f;

        [Tooltip("Max angle change per frame for smooth transitions")]
        public float MaxAdhesionAngle = 10.0f;

        class Baker : Baker<FreeClimbSettingsAuthoring>
        {
            public override void Bake(FreeClimbSettingsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Add settings component
                AddComponent(entity, new FreeClimbSettings
                {
                    ClimbSpeed = authoring.ClimbSpeed,
                    SprintSpeedMultiplier = authoring.SprintSpeedMultiplier,
                    RotationSpeed = authoring.RotationSpeed,
                    SurfaceOffset = authoring.SurfaceOffset,
                    HandTargetOffset = (float3)authoring.HandTargetOffset,
                    ReMountCooldown = authoring.ReMountCooldown,
                    MountTransitionSpeed = authoring.MountTransitionSpeed,
                    DetectionRadius = authoring.DetectionRadius,
                    DetectionDistance = authoring.DetectionDistance,
                    SurfaceCheckDistance = authoring.SurfaceCheckDistance,
                    ClimbableLayers = (uint)authoring.ClimbableLayers.value,
                    AutoClimbLedge = authoring.AutoClimbLedge,
                    ClimbColliderHeightMultiplier = authoring.ClimbColliderHeightMultiplier,
                    ClimbColliderRadiusMultiplier = authoring.ClimbColliderRadiusMultiplier,
                    ObstacleLayers = (uint)authoring.ObstacleLayers.value,
                    MinSurfaceAngle = authoring.MinSurfaceAngle,
                    MaxSurfaceAngle = authoring.MaxSurfaceAngle,
                    StaminaCost = authoring.StaminaCost,
                    ClimbStaminaRecoveryDelay = authoring.ClimbStaminaRecoveryDelay,
                    LedgeCheckHeight = authoring.LedgeCheckHeight,
                    LedgeCheckDepth = authoring.LedgeCheckDepth,
                    LedgeMinThickness = authoring.LedgeMinThickness,
                    LedgeTopLayers = (uint)authoring.LedgeTopLayers.value,
                    CeilingCheckHeight = authoring.CeilingCheckHeight,
                    DismountCooldown = authoring.DismountCooldown,
                    FastFallThreshold = authoring.FastFallThreshold,
                    GroundCheckDistance = authoring.GroundCheckDistance,
                    DismountPushForce = authoring.DismountPushForce,
                    DismountUpForce = authoring.DismountUpForce,
                    WallJumpInputThreshold = authoring.WallJumpInputThreshold,
                    WallJumpMaxDistance = authoring.WallJumpMaxDistance,
                    WallJumpMinDistance = authoring.WallJumpMinDistance,
                    WallJumpSpeed = authoring.WallJumpSpeed,
                    WallJumpDepth = authoring.WallJumpDepth,
                    // EPIC 14.26: Object Gravity
                    AdhesionStrength = authoring.AdhesionStrength,
                    SurfaceDetectionRadius = authoring.SurfaceDetectionRadius,
                    AdhesionFalloffDistance = authoring.AdhesionFalloffDistance,
                    SurfaceTransitionSpeed = authoring.SurfaceTransitionSpeed,
                    MaxAdhesionAngle = authoring.MaxAdhesionAngle
                });

                // Add replicated state component (NetCode synced)
                AddComponent(entity, new FreeClimbState
                {
                    IsClimbing = false,
                    IsClimbingUp = false,
                    IsFreeHanging = false,
                    IsWallJumping = false,
                    IsTransitioning = false,
                    SurfaceEntity = Entity.Null,
                    GripWorldPosition = float3.zero,
                    GripWorldNormal = new float3(0, 0, 1),
                    GripLocalPosition = float3.zero,
                    GripLocalNormal = float3.zero,
                    TransitionProgress = 0f,
                    TransitionStartPos = float3.zero,
                    TransitionStartRot = quaternion.identity,
                    TransitionTargetPos = float3.zero,
                    TransitionTargetRot = quaternion.identity,
                    MountTime = 0,
                    LastDismountTime = 0,
                    TransitionStartTime = 0,
                    LastCornerTime = 0,
                    LastMoveDirection = float3.zero,
                    LastClimbedSurface = Entity.Null,
                    ColliderAdjusted = false,
                    OriginalRadius = 0f,
                    OriginalHeight = 0f,
                    FreeHangEntryRequestTime = 0,
                    FreeHangStartTime = 0,
                    FreeHangExitRequestTime = 0,
                    WallJumpProgress = 0f,
                    WallJumpStartPos = float3.zero,
                    WallJumpStartRot = quaternion.identity,
                    WallJumpTargetPos = float3.zero,
                    WallJumpTargetRot = quaternion.identity,
                    WallJumpTargetGrip = float3.zero,
                    WallJumpTargetNormal = float3.zero,
                    WallJumpTargetSurface = Entity.Null,
                    NeedsCrouchAfterVault = false
                });

                // Add local-only state component (NOT replicated, survives rollback)
                AddComponent(entity, new FreeClimbLocalState
                {
                    LastCornerTime = 0,
                    StickMoveDirection = float3.zero,
                    LastJumpTime = 0
                });

                // Add animation events component
                AddComponent(entity, FreeClimbAnimationEvents.Default);
            }
        }
    }
}
