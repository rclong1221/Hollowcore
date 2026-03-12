using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Player.IK
{
    // --- Foot IK ---
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct FootIKSettings : IComponentData
    {
        public float FootRayLength;
        public float FootOffset;
        public float BodyHeightAdjustment;
        public float FootIKWeight;
        public float BodyIKWeight;
        public float BlendSpeed;
        public float3 LeftFootPosOffset; // Calibration
        public float3 RightFootPosOffset;
    }

    // Client-side only - foot IK is a local visual effect
    // Each client calculates their own based on their ground raycasts
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct FootIKState : IComponentData
    {
        public float3 LeftFootTarget;      // No GhostField - client-side only
        public float3 RightFootTarget;     // No GhostField - client-side only
        public quaternion LeftFootRotation;
        public quaternion RightFootRotation;
        public float LeftFootWeight;
        public float RightFootWeight;
        public float BodyOffset;
    }

    // --- Look At IK ---
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct LookAtIKSettings : IComponentData
    {
        public LookAtMode Mode;
        
        // Angles
        public float MaxHeadAngle;
        public float MaxSpineAngle;
        public float MaxTotalAngle;
        
        // Weights
        public float HeadWeight;
        public float BodyWeight;
        public float EyesWeight;
        public float ClampWeight;
        
        // Behavior
        public float BlendSpeed;
        public float AimLagAmount;
        public float SpeedReductionStart;
        public float SpeedReductionEnd;
        public float MaxAimDistance;
    }

    public enum LookAtMode : byte
    {
        MouseAim,
        NearestEnemy,
        InterestPoint,
        Manual,
        Disabled
    }

    // Client-side only - no GhostFields since head IK is a local visual effect
    // Each client calculates their own based on their camera
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct LookAtIKState : IComponentData
    {
        public float3 LookTarget;      // No GhostField - client-side only
        public float3 SmoothedTarget;
        public float CurrentWeight;
        public float TargetWeight;
        public bool HasTarget;
    }

    // AimDirection is client-side only - each client calculates from their own camera
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct AimDirection : IComponentData
    {
        public float3 AimPoint;        // No GhostField - client-side only
        public float2 AimAngles;       // No GhostField - client-side only
    }

    // --- Hand IK (EPIC 13.17.2 + EPIC 14.18) ---
    // Extended for weapon aiming and recoil support

    /// <summary>
    /// IK goal enumeration for all limbs.
    /// </summary>
    public enum IKGoal : byte
    {
        LeftHand = 0,
        LeftElbow = 1,
        RightHand = 2,
        RightElbow = 3,
        LeftFoot = 4,
        LeftKnee = 5,
        RightFoot = 6,
        RightKnee = 7,
        Last = 8
    }

    /// <summary>
    /// Settings for hand IK behavior during aiming and weapon use.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct HandIKSettings : IComponentData
    {
        // Basic settings
        public float HandWeight;               // 0-1, determines how much IK is applied
        public float HandAdjustmentSpeed;      // Speed to blend hand IK in/out (default: 10)
        public float3 HandPositionOffset;      // Local offset to add to hand positions
        
        // Upper arm settings
        public float UpperArmWeight;           // 0-1, how much upper arms rotate toward aim
        public float UpperArmAdjustmentSpeed;  // Speed to blend upper arm IK (default: 10)
        
        // Interaction/reach settings (from original)
        public float InterpolationSpeed;       // Speed for interpolating to targets
        public float MaxReachDistance;         // Max reach before weight reduction
        public float BlendSpeed;               // Weight blend speed
        public float DefaultWeight;            // Default IK weight when targeting (default: 1)
        
        // Spring settings for recoil
        public float SpringStiffness;          // Default: 0.2
        public float SpringDamping;            // Default: 0.25
    }

    /// <summary>
    /// Runtime state for hand IK including weapon aiming and recoil.
    /// Updated by HandIKSystem and PlayerIKBridge.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct HandIKState : IComponentData
    {
        // Left hand position/rotation
        public float3 LeftHandTarget;
        public quaternion LeftHandRotation;
        public float LeftHandPositionWeight;
        public float LeftHandRotationWeight;
        public float LeftHandWeight;          // Combined IK weight (blended)
        public float LeftHandTargetWeight;    // Target weight to blend toward
        public bool HasLeftTarget;

        // Right hand position/rotation
        public float3 RightHandTarget;
        public quaternion RightHandRotation;
        public float RightHandPositionWeight;
        public float RightHandRotationWeight;
        public float RightHandWeight;         // Combined IK weight (blended)
        public float RightHandTargetWeight;   // Target weight to blend toward
        public bool HasRightTarget;
        
        // Upper arm state
        public float UpperArmWeight;           // Current interpolated weight
        public float3 DominantHandPosition;    // Calculated from upper arm rotation
        public float3 NonDominantHandPosition;
        public float3 NonDominantHandOffset;   // Offset from dominant to non-dominant
        
        // Spring state for recoil (left hand)
        public float3 LeftPositionSpringValue;
        public float3 LeftPositionSpringVelocity;
        public float3 LeftRotationSpringValue;
        public float3 LeftRotationSpringVelocity;
        
        // Spring state for recoil (right hand)
        public float3 RightPositionSpringValue;
        public float3 RightPositionSpringVelocity;
        public float3 RightRotationSpringValue;
        public float3 RightRotationSpringVelocity;
        
        // Dominant hand tracking
        public bool IsRightHandDominant;       // True if right hand holds weapon
        public bool IsAiming;                  // True if currently aiming
        public bool IsUsingItem;               // True if using item (attacking)
        
        // Blend tracking
        public float LeftBlendTimer;
        public float RightBlendTimer;
    }

    /// <summary>
    /// IK target override for ability-driven IK (climbing, vaulting, etc).
    /// </summary>
    public struct IKTargetOverride : IBufferElementData
    {
        public IKGoal Goal;
        public float3 Position;
        public quaternion Rotation;
        public float StartTime;
        public float Duration;
        public bool Active;
    }

    // --- Bridge ---
    /// <summary>
    /// Tag to identify entities that need Player IK bridge linking.
    /// When present, PlayerIKBridgeLinkSystem will find and link the PlayerIKBridge MonoBehaviour.
    /// </summary>
    public struct PlayerIKBridgeLink : IComponentData
    {
        // Marker component - no data needed
    }
}
