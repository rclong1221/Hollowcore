using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Configurable settings for the FreeClimb system.
    /// Attach this to the player prefab.
    /// </summary>
    public struct FreeClimbSettings : IComponentData
    {
        // Movement
        public float ClimbSpeed;
        public float SprintSpeedMultiplier;
        public float RotationSpeed;
        public float SurfaceOffset;
        public float3 HandTargetOffset;
        public float ReMountCooldown;
        public float MountTransitionSpeed;
        public bool AutoClimbLedge;
        
        // Raycast settings
        public float DetectionRadius;
        public float DetectionDistance;
        public float SurfaceCheckDistance;
        public uint ClimbableLayers;
        
        // Collision settings
        public float ClimbColliderHeightMultiplier;
        public float ClimbColliderRadiusMultiplier;
        public uint ObstacleLayers;
        public float MinSurfaceAngle;
        public float MaxSurfaceAngle;
        
        // Stamina
        public float StaminaCost;
        public float ClimbStaminaRecoveryDelay;
        
        // Ledge detection
        public float LedgeCheckHeight;
        public float LedgeCheckDepth;
        public float LedgeMinThickness;
        public uint LedgeTopLayers;
        public float CeilingCheckHeight;
        
        // Dismount
        public float DismountCooldown;
        public float FastFallThreshold;
        public float GroundCheckDistance;
        public float DismountPushForce;
        public float DismountUpForce;
        
        // Wall Jump
        public float WallJumpInputThreshold;
        public float WallJumpMaxDistance;
        public float WallJumpMinDistance;
        public float WallJumpSpeed;
        public float WallJumpDepth;
        
        // EPIC 14.26: Object Gravity / Surface Adhesion
        public float AdhesionStrength;          // How strongly pulled toward surface (0-1)
        public float SurfaceDetectionRadius;   // Sphere cast radius for multi-directional detection
        public float AdhesionFalloffDistance;  // Distance at which adhesion starts weakening
        public float SurfaceTransitionSpeed;   // Speed of interpolation between surfaces
        public float MaxAdhesionAngle;          // Max angle change per frame for smooth transitions
        
        // Refinement (EPIC 14.27)
        public int AdhesionHysteresisFrames;   // Frames to maintain adhesion after raycast miss
        public float ReverseProbeDistance;     // Distance for thin-wall checking
        public float TransitionTimeout;        // Max seconds to stay in IsTransitioning
        
        public static FreeClimbSettings Default => new FreeClimbSettings
        {
            ClimbSpeed = 2.0f,
            SprintSpeedMultiplier = 1.5f,
            RotationSpeed = 10.0f,
            SurfaceOffset = 0.4f,
            HandTargetOffset = new float3(0, 1.6f, 0),
            ReMountCooldown = 0.5f,
            MountTransitionSpeed = 2.5f,
            AutoClimbLedge = true,
            DetectionRadius = 0.8f,
            DetectionDistance = 1.0f,
            SurfaceCheckDistance = 1.0f,
            ClimbableLayers = 0xFFFFFFFF,
            ClimbColliderHeightMultiplier = 1.0f,
            ClimbColliderRadiusMultiplier = 0.8f,
            ObstacleLayers = 0xFFFFFFFF,
            MinSurfaceAngle = 30f,
            MaxSurfaceAngle = 150f,
            StaminaCost = 5f,
            ClimbStaminaRecoveryDelay = 1f,
            LedgeCheckHeight = 0.5f,
            LedgeCheckDepth = 0.5f,
            LedgeMinThickness = 0.1f,
            LedgeTopLayers = 0xFFFFFFFF,
            CeilingCheckHeight = 2.0f,
            DismountCooldown = 0.3f,
            FastFallThreshold = -5f,
            GroundCheckDistance = 0.5f,
            DismountPushForce = 3f,
            DismountUpForce = 2f,
            WallJumpInputThreshold = 0.5f,
            WallJumpMaxDistance = 3f,
            WallJumpMinDistance = 0.5f,
            WallJumpSpeed = 5f,
            WallJumpDepth = 0.3f,
            // EPIC 14.26 defaults
            AdhesionStrength = 1.0f,
            SurfaceDetectionRadius = 0.6f,
            AdhesionFalloffDistance = 0.8f,
            SurfaceTransitionSpeed = 8.0f,
            MaxAdhesionAngle = 45f,
            // EPIC 14.27 defaults
            AdhesionHysteresisFrames = 5,
            ReverseProbeDistance = 0.3f,
            TransitionTimeout = 2.0f
        };
    }

    /// <summary>
    /// Tag component for detection system to feed candidates to the mount system.
    /// </summary>
    public struct FreeClimbCandidate : IComponentData
    {
        public Entity SurfaceEntity;
        public float3 GripWorldPosition;
        public float3 GripWorldNormal;
        public float Distance;
        public float SurfaceAngle;
    }

    /// <summary>
    /// Main state component for FreeClimb system.
    /// Replicated via NetCode to ensure server-client synchronization.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct FreeClimbState : IComponentData
    {
        [GhostField] public bool IsClimbing;
        [GhostField] public bool IsClimbingUp;
        [GhostField] public bool IsFreeHanging;
        [GhostField] public bool IsHangTransitioning;  // EPIC 14.24: Blocks input during hang entry animation
        [GhostField] public bool IsWallJumping;
        [GhostField] public bool IsMantling;           // EPIC 15.3: Procedural Mantling (Vaulting from ground)
        [GhostField] public bool IsTransitioning;
        
        // Surface Data
        [GhostField] public Entity SurfaceEntity;
        [GhostField] public float3 GripWorldPosition;
        [GhostField] public float3 GripWorldNormal;
        
        // Local space data
        [GhostField] public float3 GripLocalPosition;
        [GhostField] public float3 GripLocalNormal;
        
        // Transition / Animation Data
        [GhostField] public float TransitionProgress;
        [GhostField] public float3 TransitionStartPos;
        [GhostField] public quaternion TransitionStartRot;
        [GhostField] public float3 TransitionTargetPos;
        [GhostField] public quaternion TransitionTargetRot;
        
        // Timestamps (Network Time)
        [GhostField] public double MountTime;
        [GhostField] public double LastDismountTime;
        [GhostField] public double TransitionStartTime;
        
        // Corner / Movement Tech
        [GhostField] public double LastCornerTime;
        [GhostField] public float3 LastMoveDirection;
        
        // Reference to last surface
        [GhostField] public Entity LastClimbedSurface;
        
        // Collider state
        [GhostField] public bool ColliderAdjusted;
        [GhostField] public float OriginalRadius;
        [GhostField] public float OriginalHeight;
        
        // Free hang state
        [GhostField] public double FreeHangEntryRequestTime;
        [GhostField] public double FreeHangStartTime;
        [GhostField] public double FreeHangExitRequestTime;
        [GhostField] public double HangTransitionStartTime;  // EPIC 14.24: For timeout safety
        
        // Wall jump state
        [GhostField] public float WallJumpProgress;
        [GhostField] public float3 WallJumpStartPos;
        [GhostField] public quaternion WallJumpStartRot;
        [GhostField] public float3 WallJumpTargetPos;
        [GhostField] public quaternion WallJumpTargetRot;
        [GhostField] public float3 WallJumpTargetGrip;
        [GhostField] public float3 WallJumpTargetNormal;
        [GhostField] public Entity WallJumpTargetSurface;
        
        // Vault state
        [GhostField] public bool NeedsCrouchAfterVault;
        
        // ======== EPIC 14.26: Object Gravity / Surface Adhesion ========
        
        // Surface Gravity - replaces discrete grip tracking with continuous adhesion
        [GhostField] public float3 SurfaceGravityDirection;  // Direction of pull toward surface (inverted normal)
        [GhostField] public float3 SurfaceContactPoint;      // Where character touches surface (replaces GripWorldPosition for new logic)
        [GhostField] public float3 SurfaceNormal;            // Current surface normal (always valid while adhered)
        
        // Adhesion State
        [GhostField] public float AdhesionStrength;          // Current adhesion strength (0-1, 0=falling)
        [GhostField] public float SurfaceDistance;           // Current distance to surface
        [GhostField] public bool IsAdhered;                  // Currently attached to a surface via object gravity
        [GhostField] public bool SurfaceNeedsRevalidation;   // Flag for voxel destruction - force surface check
        
        // Edge Detection (automatic hang/vault triggers)
        [GhostField] public bool AtLedgeTop;                 // Detected top edge (no wall above)
        [GhostField] public bool AtLedgeBottom;              // Detected bottom edge (no wall below)
        [GhostField] public float3 LedgeDirection;           // Direction along the ledge edge
        
        [GhostField] public bool IsSurfaceTransitioning;     // Smoothly moving between surfaces
        [GhostField] public float3 TransitionFromNormal;     // Normal we're transitioning from
        [GhostField] public float3 TransitionToNormal;       // Normal we're transitioning to
        [GhostField] public float SurfaceTransitionProgress; // 0-1 blend between normals
        
        // Refinement (EPIC 14.27)
        [GhostField] public float3 SmoothedNormal;           // Slerped normal for rotation
        [GhostField] public int StickyFramesRemaining;      // For adhesion hysteresis
        [GhostField] public bool IsSticky;                   // Currently in hysteresis mode
    }

    /// <summary>
    /// Local-only state for prediction and input handling.
    /// NOT replicated. Used to maintain cooldowns and smoothing across rollbacks.
    /// </summary>
    public struct FreeClimbLocalState : IComponentData
    {
        public double LastCornerTime;
        public float3 StickMoveDirection;
        public double LastJumpTime;
        public float3 SmoothedNormal;
    }

    /// <summary>
    /// Event component added when player starts climbing.
    /// Consumed by audio/VFX systems.
    /// </summary>
    public struct ClimbStartEvent : IComponentData
    {
        public int MaterialId;
        public float3 Position;
    }

    /// <summary>
    /// Surface material ID for audio/VFX selection.
    /// </summary>
    public struct SurfaceMaterialId : IComponentData
    {
        public int Id;
    }

    /// <summary>
    /// Static event queue for climb animation events.
    /// Thread-safe for MonoBehaviour → ECS bridge.
    /// </summary>
    public struct FreeClimbAnimationEvents : IComponentData
    {
        public FreeClimbEventFlags CompletedEvents;
        public int EventFrame;

        public static FreeClimbAnimationEvents Default => new FreeClimbAnimationEvents
        {
            CompletedEvents = FreeClimbEventFlags.None,
            EventFrame = 0
        };

        // --- Static API for AnimationEventRelay ---
        
        private static EventType _pendingEvent;
        private static readonly object _lock = new object();

        public enum EventType
        {
            None,
            StartInPosition,
            Complete,
            TurnComplete,
            HangStartInPosition,
            HangComplete
        }

        public static void QueueEvent(EventType type)
        {
            lock (_lock) { _pendingEvent = type; }
        }

        public static EventType ConsumeEvent()
        {
            lock (_lock)
            {
                var evt = _pendingEvent;
                _pendingEvent = EventType.None;
                return evt;
            }
        }
    }

    /// <summary>
    /// Flags for climb animation completion events (Internal ECS usage).
    /// </summary>
    [System.Flags]
    public enum FreeClimbEventFlags
    {
        None = 0,
        MountComplete = 1 << 0,
        VaultComplete = 1 << 1,
        DismountComplete = 1 << 2,
        CornerComplete = 1 << 3,
        WallJumpComplete = 1 << 4
    }
}
