using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Player.Abilities
{
    // --- Quick Start ---
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct QuickStartAbility : IComponentData
    {
        [GhostField] public bool IsActive;
        public float Duration;
        public float AccelerationMultiplier;
        public float MinInputMagnitude;
        public float ElapsedTime;
    }

    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct QuickStartSettings : IComponentData
    {
        public float Duration;
        public float AccelerationMultiplier;
        public float MinInputMagnitude;
        public float VelocityThreshold;
    }

    // --- Quick Stop ---
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct QuickStopAbility : IComponentData
    {
        [GhostField] public bool IsActive;
        public float Duration;
        public float DecelerationMultiplier;
        public float MinVelocityToTrigger;
        [GhostField] public float3 StopDirection;
        public float ElapsedTime;
    }

    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct QuickStopSettings : IComponentData
    {
        public float Duration;
        public float DecelerationMultiplier;
        public float MinVelocityToTrigger;
    }

    // --- Quick Turn ---
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct QuickTurnAbility : IComponentData
    {
        [GhostField] public bool IsActive;
        public float TurnSpeed;
        public float Duration;
        [GhostField] public float3 TargetDirection;
        public float MomentumRetention;
        public float ElapsedTime;
    }

    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct QuickTurnSettings : IComponentData
    {
        public float TurnSpeed;
        public float Duration;
        public float MomentumRetention;
        public float DirectionThreshold; // e.g. -0.5 for >90 deg turn
    }

    // --- Speed Modifiers ---
    public struct SpeedModifier : IBufferElementData
    {
        public int SourceId;
        public float Multiplier;
        public float Duration; // -1 for permanent
        public float ElapsedTime;
    }

    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct SpeedModifierState : IComponentData
    {
        [GhostField] public float CombinedMultiplier;
        public float MinSpeed;
        public float MaxSpeed;
    }

    // --- Fall Ability ---
    // 13.14: Full feature parity with Opsive Fall ability
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct FallAbility : IComponentData
    {
        // --- Core State ---
        [GhostField] public bool IsFalling;
        [GhostField] public float FallStartHeight;
        [GhostField] public float FallDuration;

        // --- 13.14.6: State Index for Animation ---
        // 0 = falling, 1 = landed (used by animator state machine)
        [GhostField] public int StateIndex;

        // --- 13.14.3: Animation Event Tracking ---
        [GhostField] public bool Landed;
        [GhostField] public bool WaitingForAnimationEvent;
        public float AnimationEventTimeout;
        public float AnimationEventTimer;

        // --- 13.14.5: Teleport Handling ---
        [GhostField] public bool PendingImmediateTransformChange;
    }

    /// <summary>
    /// 13.14: Settings for the fall ability (baked from authoring).
    /// Mirrors Opsive Fall ability configuration.
    /// </summary>
    public struct FallSettings : IComponentData
    {
        // --- 13.14.1: Minimum Fall Height ---
        // Only trigger fall ability if character is this high above ground.
        // 0 = start fall at any height (like Opsive default 0.2f)
        public float MinFallHeight;

        // --- 13.14.2: Surface Impact Settings ---
        // Reference ID for surface impact effect lookup (replaces BlobAssetReference)
        public int LandSurfaceImpactId;
        // Minimum downward velocity required to trigger surface impact (-4f default)
        // Negative because falling = negative Y velocity
        public float MinSurfaceImpactVelocity;

        // --- 13.14.3: Animation Event Settings ---
        // Whether to wait for OnAnimatorFallComplete event before ending ability
        public bool WaitForLandEvent;
        // Timeout in seconds if animation event doesn't fire
        public float LandEventTimeout;

        // --- Thresholds for effects/damage ---
        public float MinFallHeightForAnimation;
        public float HardLandingHeight;
        public float MaxSafeFallHeight;

        // --- Physics layer mask for ground check ---
        // Stored as uint for Burst compatibility (converted from LayerMask in authoring)
        public uint SolidObjectLayerMask;

        public static FallSettings Default => new FallSettings
        {
            MinFallHeight = 0.2f,
            LandSurfaceImpactId = 0,
            MinSurfaceImpactVelocity = -4f,
            WaitForLandEvent = true,
            LandEventTimeout = 1.0f,
            MinFallHeightForAnimation = 1.5f,
            HardLandingHeight = 3.0f,
            MaxSafeFallHeight = 6.0f,
            SolidObjectLayerMask = 1 // Default layer only
        };
    }

    /// <summary>
    /// 13.14.2: Request to spawn a surface impact effect on landing.
    /// Created by FallDetectionSystem, consumed by SurfaceImpactSystem (presentation layer).
    /// </summary>
    public struct SurfaceImpactRequest : IComponentData, IEnableableComponent
    {
        // World position where impact occurred
        public float3 ContactPoint;
        // Surface normal at contact point
        public float3 ContactNormal;
        // Impact velocity (for intensity calculation)
        public float ImpactVelocity;
        // Surface material ID (from ground raycast)
        public int SurfaceMaterialId;
        // Surface impact preset ID (from FallSettings)
        public int SurfaceImpactId;
    }

    /// <summary>
    /// 13.14.3: Request to notify that fall animation completed.
    /// Set by animator bridge when OnAnimatorFallComplete event fires.
    /// </summary>
    public struct FallAnimationComplete : IComponentData, IEnableableComponent
    {
    }

    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct LandingEffect : IComponentData
    {
        public float RecoveryDuration;
        public float CameraShakeIntensity;
        [GhostField] public bool TriggerHardLanding;
        public float ElapsedRecovery;
    }

    // --- Idle Ability ---
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct IdleAbility : IComponentData
    {
        public float IdleTime;
        public float NextVariationTime;
        public float MinVariationInterval;
        public float MaxVariationInterval;
        [GhostField] public int CurrentVariation;
        public int VariationCount;
    }

    // --- Restrictions ---
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct RestrictPosition : IComponentData
    {
        public float3 Min;
        public float3 Max;
        public bool3 AxesEnabled;
    }

    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct RestrictRotation : IComponentData
    {
        public float MinYaw, MaxYaw;
        public float MinPitch, MaxPitch;
        public bool RestrictYaw;
        public bool RestrictPitch;
    }

    // --- Move Towards ---
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct MoveTowardsAbility : IComponentData
    {
        [GhostField] public float3 TargetPosition;
        public float StopDistance;
        public float MoveSpeed;
        public bool FaceTargetOnArrival;
        public bool UseNavMesh;
        [GhostField] public bool IsMoving;
    }
}
