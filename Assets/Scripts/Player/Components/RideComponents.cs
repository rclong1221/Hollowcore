using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;

namespace Player.Components
{
    /// <summary>
    /// Tracks player's riding state. Replicated via GhostFields.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct RideState : IComponentData
    {
        /// <summary>Is the player currently riding?</summary>
        [GhostField] public bool IsRiding;

        /// <summary>The mount entity being ridden</summary>
        [GhostField] public Entity MountEntity;

        /// <summary>Current ride phase (0=None, 1=Mount, 2=Ride, 3=Dismount)</summary>
        [GhostField] public int RidePhase;

        /// <summary>True if mounting/dismounting from left side</summary>
        [GhostField] public bool FromLeftSide;

        /// <summary>Mount animation progress (0-1)</summary>
        [GhostField] public float MountProgress;

        /// <summary>Dismount requested flag</summary>
        [GhostField] public bool DismountRequested;

        public static RideState Default => new RideState
        {
            IsRiding = false,
            MountEntity = Entity.Null,
            RidePhase = RidePhaseConstants.None,
            FromLeftSide = false,
            MountProgress = 0f,
            DismountRequested = false
        };
    }

    /// <summary>
    /// Component on rideable entities (mounts, vehicles).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct RideableState : IComponentData
    {
        /// <summary>Does this mount currently have a rider?</summary>
        [GhostField] public bool HasRider;

        /// <summary>The entity of the current rider</summary>
        [GhostField] public Entity RiderEntity;

        /// <summary>Can this mount currently be ridden?</summary>
        public bool CanBeRidden;

        /// <summary>Offset for mounting from left side</summary>
        public float3 MountOffsetLeft;

        /// <summary>Offset for mounting from right side</summary>
        public float3 MountOffsetRight;

        /// <summary>Final seat position offset</summary>
        public float3 SeatOffset;

        /// <summary>Interaction radius for mount detection</summary>
        public float InteractionRadius;

        public static RideableState Default => new RideableState
        {
            HasRider = false,
            RiderEntity = Entity.Null,
            CanBeRidden = true,
            MountOffsetLeft = new float3(-1f, 0f, 0f),
            MountOffsetRight = new float3(1f, 0f, 0f),
            SeatOffset = new float3(0f, 1.5f, 0f),
            InteractionRadius = 2f
        };
    }

    /// <summary>
    /// Movement input forwarded from rider to mount.
    /// Set by RideControlSystem when a player is riding.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct MountMovementInput : IComponentData
    {
        /// <summary>Horizontal movement input (-1 to 1) for turning/strafing</summary>
        [GhostField] public float HorizontalInput;
        
        /// <summary>Forward movement input (-1 to 1)</summary>
        [GhostField] public float ForwardInput;
        
        /// <summary>Sprint/run input (0 = walk, 1 = run)</summary>
        [GhostField] public byte SprintInput;
        
        /// <summary>Look direction in world space</summary>
        [GhostField] public float3 LookDirection;
    }
    
    /// <summary>
    /// Configuration for mount movement.
    /// </summary>
    public struct MountMovementConfig : IComponentData
    {
        /// <summary>Walk/trot speed</summary>
        public float WalkSpeed;
        
        /// <summary>Run/gallop speed (when sprinting)</summary>
        public float RunSpeed;
        
        /// <summary>Turn/rotation speed in degrees per second</summary>
        public float TurnSpeed;
        
        public static MountMovementConfig Default => new MountMovementConfig
        {
            WalkSpeed = 5f,
            RunSpeed = 12f,
            TurnSpeed = 120f
        };
    }

    /// <summary>
    /// Config for ride detection (non-networked).
    /// </summary>
    public struct RideConfig : IComponentData
    {
        public float DetectionRange;
        
        public static RideConfig Default => new RideConfig
        {
            DetectionRange = 3f
        };
    }

    /// <summary>
    /// Transient component indicating a nearby rideable.
    /// Added/removed by RideMountDetectionSystem.
    /// </summary>
    public struct NearbyRideable : IComponentData
    {
        public Entity RideableEntity;
        public bool MountFromLeft;
    }

    /// <summary>
    /// Ride phase constants.
    /// </summary>
    public static class RidePhaseConstants
    {
        public const int None = 0;
        public const int Mounting = 1;
        public const int Riding = 2;
        public const int Dismounting = 3;
        public const int DismountComplete = 4; // Brief state to let animator exit Ride layer
    }
}

