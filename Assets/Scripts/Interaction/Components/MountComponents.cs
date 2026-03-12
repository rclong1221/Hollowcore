using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Interaction
{
    // ─────────────────────────────────────────────────────
    //  EPIC 16.1 Phase 4: Mount/Seat
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// Type of mount point determining movement and input behavior.
    /// </summary>
    public enum MountType : byte
    {
        /// <summary>Fixed position, free look (vehicle seat, chair).</summary>
        Seat = 0,
        /// <summary>Fixed position, aim controls mount (turret seat).</summary>
        Turret = 1,
        /// <summary>Up/down movement along local Y axis.</summary>
        Ladder = 2,
        /// <summary>Forward movement along local Z axis (auto or input).</summary>
        Zipline = 3,
        /// <summary>No control, free look (passenger seat).</summary>
        Passenger = 4
    }

    /// <summary>
    /// EPIC 16.1 Phase 4: Defines a mountable point on an entity.
    /// Placed on the MOUNT entity alongside Interactable(Instant).
    /// When the player interacts, they enter the mount via TriggerInteractionEffect.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct MountPoint : IComponentData
    {
        /// <summary>What kind of mount this is.</summary>
        public MountType Type;

        /// <summary>Local-space offset from mount entity origin to seat position.</summary>
        public float3 SeatOffset;

        /// <summary>Facing direction in local space.</summary>
        public quaternion SeatRotation;

        /// <summary>Where the player appears on dismount (local space from mount).</summary>
        public float3 DismountOffset;

        /// <summary>Who is currently mounted.</summary>
        [GhostField]
        public Entity OccupantEntity;

        /// <summary>Whether the seat is taken.</summary>
        [GhostField]
        public bool IsOccupied;

        /// <summary>Hide the player's avatar while mounted.</summary>
        public bool HidePlayerModel;

        /// <summary>When true, player input is forwarded to MountInput on this entity.</summary>
        public bool TransferInputToMount;

        /// <summary>Animator trigger hash for mount enter animation. 0 = no animation.</summary>
        public int MountAnimationHash;

        /// <summary>Animator trigger hash for mount exit animation. 0 = no animation.</summary>
        public int DismountAnimationHash;

        /// <summary>Duration of mount/dismount transition in seconds.</summary>
        public float MountTransitionDuration;

        /// <summary>Movement speed for Ladder type (units/sec).</summary>
        public float LadderSpeed;

        /// <summary>Movement speed for Zipline type (units/sec).</summary>
        public float ZiplineSpeed;

        /// <summary>Local Y minimum bound for Ladder movement.</summary>
        public float LadderMinY;

        /// <summary>Local Y maximum bound for Ladder movement.</summary>
        public float LadderMaxY;
    }

    /// <summary>
    /// EPIC 16.1 Phase 4: Tracks whether a player is currently mounted.
    /// Placed on the PLAYER entity (always present, IsMounted = false by default).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct MountState : IComponentData
    {
        /// <summary>The entity this player is mounted on.</summary>
        [GhostField]
        public Entity MountedOn;

        /// <summary>Whether the player is actively mounted.</summary>
        [GhostField]
        public bool IsMounted;

        /// <summary>Whether a mount/dismount transition is in progress.</summary>
        [GhostField]
        public bool IsTransitioning;

        /// <summary>Transition progress from 0 to 1.</summary>
        [GhostField(Quantization = 100)]
        public float TransitionProgress;

        /// <summary>Cached mount type for quick system checks.</summary>
        public MountType ActiveMountType;

        /// <summary>Cached BlockedAbilitiesMask before mounting, restored on dismount.</summary>
        public int PreviousBlockedAbilitiesMask;

        /// <summary>Player position before mounting (fallback for dismount positioning).</summary>
        public float3 PreMountPosition;

        /// <summary>Current ladder position offset along local Y (for Ladder type).</summary>
        [GhostField(Quantization = 100)]
        public float LadderOffset;
    }

    /// <summary>
    /// EPIC 16.1 Phase 4: Generic mount input forwarded from the rider's PlayerInput.
    /// Placed on the MOUNT entity. Game-specific systems (turret, vehicle) consume this.
    /// Written by MountInputRedirectSystem when MountPoint.TransferInputToMount is true.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct MountInput : IComponentData
    {
        /// <summary>Movement input (x = Horizontal, y = Vertical).</summary>
        [GhostField(Quantization = 100)]
        public float2 Move;

        /// <summary>Camera/aim input delta.</summary>
        [GhostField(Quantization = 100)]
        public float2 Look;

        /// <summary>Primary action (Fire/Use). 0 or 1.</summary>
        [GhostField]
        public byte Primary;

        /// <summary>Secondary action (AltUse). 0 or 1.</summary>
        [GhostField]
        public byte Secondary;

        /// <summary>Interact button. 0 or 1.</summary>
        [GhostField]
        public byte Interact;

        /// <summary>Jump button. 0 or 1.</summary>
        [GhostField]
        public byte Jump;
    }
}
