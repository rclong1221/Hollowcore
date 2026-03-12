using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Ship.LocalSpace
{
    /// <summary>
    /// Identifies an entity as a ship root.
    /// All ship-related components attach to this entity.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ShipRoot : IComponentData
    {
        /// <summary>Stable ID for debugging and ownership tracking.</summary>
        [GhostField] public int ShipId;

        /// <summary>Display name for debugging.</summary>
        public Unity.Collections.FixedString32Bytes ShipName;
    }

    /// <summary>
    /// Ship velocity state for physics and prediction.
    /// Updated by ship movement systems.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ShipKinematics : IComponentData
    {
        /// <summary>World-space linear velocity.</summary>
        [GhostField(Quantization = 100)] public float3 LinearVelocity;

        /// <summary>World-space angular velocity (radians/sec).</summary>
        [GhostField(Quantization = 100)] public float3 AngularVelocity;

        /// <summary>Whether the ship is currently moving.</summary>
        [GhostField] public bool IsMoving;

        /// <summary>Maximum linear speed for clamping.</summary>
        public float MaxLinearSpeed;

        /// <summary>Maximum angular speed for clamping.</summary>
        public float MaxAngularSpeed;

        /// <summary>Default kinematics (stationary).</summary>
        public static ShipKinematics Default => new()
        {
            LinearVelocity = float3.zero,
            AngularVelocity = float3.zero,
            IsMoving = false,
            MaxLinearSpeed = 50f,
            MaxAngularSpeed = 2f
        };
    }

    /// <summary>
    /// Tracks the ship's previous transform for delta calculation.
    /// Used to apply inertial correction to occupants.
    /// </summary>
    public struct ShipPreviousTransform : IComponentData
    {
        /// <summary>Position at end of previous tick.</summary>
        public float3 PreviousPosition;

        /// <summary>Rotation at end of previous tick.</summary>
        public quaternion PreviousRotation;

        /// <summary>Whether this is the first frame (no valid previous).</summary>
        public bool IsFirstFrame;
    }

    /// <summary>
    /// Component on entities that are inside a ship and move with it.
    /// Stores the entity's position/rotation relative to the ship.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct InShipLocalSpace : IComponentData
    {
        /// <summary>Entity of the ship this entity is attached to.</summary>
        [GhostField] public Entity ShipEntity;

        /// <summary>Position in ship-local coordinates.</summary>
        [GhostField(Quantization = 100)] public float3 LocalPosition;

        /// <summary>Rotation in ship-local coordinates.</summary>
        [GhostField(Quantization = 1000)] public quaternion LocalRotation;

        /// <summary>Whether local space tracking is active.</summary>
        [GhostField] public bool IsAttached;

        /// <summary>Creates an attached local space with identity rotation.</summary>
        public static InShipLocalSpace Create(Entity shipEntity, float3 localPosition, quaternion localRotation)
        {
            return new InShipLocalSpace
            {
                ShipEntity = shipEntity,
                LocalPosition = localPosition,
                LocalRotation = math.lengthsq(localRotation.value) > 0.001f ? math.normalize(localRotation) : quaternion.identity,
                IsAttached = true
            };
        }
    }

    /// <summary>
    /// Tag component for entities that should receive inertial correction
    /// from ship movement but don't use full local-space tracking.
    /// </summary>
    public struct ShipInertialCorrectionTarget : IComponentData
    {
        /// <summary>Entity of the ship providing inertial reference.</summary>
        public Entity ShipEntity;
    }

    /// <summary>
    /// Buffer of all occupants currently inside a ship.
    /// Maintained by enter/exit systems for efficient iteration.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct ShipOccupant : IBufferElementData
    {
        /// <summary>Entity of the occupant.</summary>
        public Entity OccupantEntity;

        /// <summary>Whether this occupant is the pilot.</summary>
        public bool IsPilot;
    }

    /// <summary>
    /// Smoothing state for client-side interpolation of mispredictions.
    /// </summary>
    public struct LocalSpaceSmoothing : IComponentData
    {
        /// <summary>Interpolation progress (0 = correction start, 1 = fully corrected).</summary>
        public float SmoothingProgress;

        /// <summary>Position error being smoothed out.</summary>
        public float3 PositionError;

        /// <summary>Rotation error being smoothed out.</summary>
        public quaternion RotationError;

        /// <summary>Duration of smoothing in seconds.</summary>
        public float SmoothingDuration;

        /// <summary>Default smoothing state.</summary>
        public static LocalSpaceSmoothing Default => new()
        {
            SmoothingProgress = 1f,
            PositionError = float3.zero,
            RotationError = quaternion.identity,
            SmoothingDuration = 0.1f
        };
    }

    /// <summary>
    /// Request to attach an entity to a ship's local space.
    /// </summary>
    public struct AttachToShipRequest : IComponentData
    {
        /// <summary>Target ship entity.</summary>
        public Entity ShipEntity;

        /// <summary>Initial local position (optional, Entity.Null = compute from current world pos).</summary>
        public float3 InitialLocalPosition;

        /// <summary>Initial local rotation (optional).</summary>
        public quaternion InitialLocalRotation;

        /// <summary>Use current world transform to compute local space.</summary>
        public bool ComputeFromWorldTransform;
    }

    /// <summary>
    /// Request to detach an entity from ship local space.
    /// </summary>
    public struct DetachFromShipRequest : IComponentData
    {
        /// <summary>Whether to preserve current world position (true) or snap to local pos (false).</summary>
        public bool PreserveWorldPosition;
    }
}
