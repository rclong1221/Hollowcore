using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Ship.Stations
{
    /// <summary>
    /// Type of ship station.
    /// </summary>
    public enum StationType : byte
    {
        /// <summary>Ship helm/pilot seat - controls movement and navigation.</summary>
        Helm = 0,

        /// <summary>Drill control station - operates mining equipment.</summary>
        DrillControl = 1,

        /// <summary>Weapon station - controls ship weapons.</summary>
        WeaponStation = 2,

        /// <summary>Systems panel - power, life support, repairs.</summary>
        SystemsPanel = 3,

        /// <summary>Engineering station - advanced ship systems.</summary>
        Engineering = 4,

        /// <summary>Communications station - comms and scanning.</summary>
        Communications = 5
    }

    /// <summary>
    /// Action type for station use requests.
    /// </summary>
    public enum StationUseAction : byte
    {
        /// <summary>Request to enter/operate the station.</summary>
        Enter = 0,

        /// <summary>Request to exit/leave the station.</summary>
        Exit = 1
    }

    /// <summary>
    /// Main component for operable ship stations.
    /// Attached to station entities (helm, drill controls, weapon turrets, etc.)
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct OperableStation : IComponentData
    {
        /// <summary>Type of station (determines behavior and input mapping).</summary>
        [GhostField] public StationType Type;

        /// <summary>Local-space position (relative to station) where the player snaps to when operating.</summary>
        public float3 InteractionPoint;

        /// <summary>Local-space forward direction the player faces while operating.</summary>
        public float3 InteractionForward;
        
        /// <summary>Reference to the parent ship entity this station belongs to.</summary>
        [GhostField] public Entity ShipEntity;

        /// <summary>Maximum distance from which player can interact.</summary>
        public float Range;

        /// <summary>Entity of the player currently operating this station (Entity.Null if unoccupied).</summary>
        [GhostField] public Entity CurrentOperator;

        /// <summary>Optional camera target entity for piloting view.</summary>
        public Entity CameraTarget;

        /// <summary>Stable ID for cross-world entity lookup.</summary>
        [GhostField] public int StableId;

        /// <summary>Default station settings.</summary>
        public static OperableStation Default => new()
        {
            Type = StationType.Helm,
            InteractionPoint = float3.zero,
            InteractionForward = new float3(0, 0, 1),
            Range = 2f,
            CurrentOperator = Entity.Null,
            CameraTarget = Entity.Null,
            StableId = 0
        };
    }

    /// <summary>
    /// Component on player entity indicating they are currently operating a station.
    /// Added when player enters station, removed when they exit.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct OperatingStation : IComponentData
    {
        /// <summary>Entity of the station being operated.</summary>
        [GhostField] public Entity StationEntity;

        /// <summary>Cached type of the station.</summary>
        [GhostField] public StationType StationType;

        /// <summary>True if actively operating (convenience for queries).</summary>
        [GhostField] public bool IsOperating;
    }

    /// <summary>
    /// Buffer element for client requests to enter/exit stations.
    /// Server consumes and validates these requests.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    [InternalBufferCapacity(2)]
    public struct StationUseRequest : IBufferElementData
    {
        /// <summary>Stable ID of target station (for cross-world entity lookup).</summary>
        [GhostField] public int StationStableId;

        /// <summary>Action to perform (Enter or Exit).</summary>
        [GhostField] public StationUseAction Action;

        /// <summary>Client tick for ordering and anti-spam.</summary>
        [GhostField] public uint ClientTick;
    }

    /// <summary>
    /// Input state for station control.
    /// Written by player input when operating, read by station behavior systems.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct StationInput : IComponentData
    {
        /// <summary>Movement input (WASD/stick) - used by Helm for thrust/yaw.</summary>
        [GhostField(Quantization = 100)] public float2 Move;

        /// <summary>Look/aim input - used by WeaponStation for targeting.</summary>
        [GhostField(Quantization = 100)] public float2 Look;

        /// <summary>Primary action (fire/drill/activate).</summary>
        [GhostField] public byte Primary;

        /// <summary>Secondary action (alt-fire/special).</summary>
        [GhostField] public byte Secondary;

        /// <summary>Modifier key (shift/boost).</summary>
        [GhostField] public byte Modifier;

        /// <summary>Menu/cancel key.</summary>
        [GhostField] public byte Cancel;
    }

    /// <summary>
    /// Interactable configuration for station prompts.
    /// </summary>
    public struct StationInteractable : IComponentData
    {
        /// <summary>Prompt shown when player can enter.</summary>
        public FixedString64Bytes PromptEnter;

        /// <summary>Prompt shown when player can exit.</summary>
        public FixedString64Bytes PromptExit;

        /// <summary>Prompt shown when station is occupied by another player.</summary>
        public FixedString64Bytes PromptOccupied;

        /// <summary>Prompt shown when station is disabled/locked.</summary>
        public FixedString64Bytes PromptDisabled;
    }

    /// <summary>
    /// Client-side prompt state for station UI.
    /// </summary>
    public struct StationPromptState : IComponentData
    {
        /// <summary>Currently targeted station entity.</summary>
        public Entity TargetStation;

        /// <summary>Prompt text to display.</summary>
        public FixedString64Bytes PromptText;

        /// <summary>Whether prompt should be visible.</summary>
        public bool IsPromptVisible;

        /// <summary>Whether interaction is currently possible.</summary>
        public bool CanInteract;

        /// <summary>Distance to the station.</summary>
        public float Distance;
    }

    /// <summary>
    /// Tag component for disabled/locked stations.
    /// </summary>
    public struct StationDisabled : IComponentData
    {
        /// <summary>Reason for being disabled.</summary>
        public FixedString64Bytes DisableReason;
    }

    /// <summary>
    /// Client-side debounce for station interaction input.
    /// </summary>
    public struct StationInteractDebounce : IComponentData
    {
        /// <summary>Last tick when a request was sent.</summary>
        public uint LastRequestTick;

        /// <summary>Minimum ticks between requests.</summary>
        public uint DebounceTickCount;
    }

    /// <summary>
    /// Camera override for station operation.
    /// Added to player when they enter a station with a custom camera.
    /// </summary>
    public struct StationCameraOverride : IComponentData
    {
        /// <summary>Camera target entity to use while operating.</summary>
        public Entity CameraTargetEntity;

        /// <summary>Original camera settings to restore on exit.</summary>
        public float OriginalDistance;
        public float OriginalPitch;
        public float OriginalYaw;
    }

    /// <summary>
    /// Tag for station camera target entities.
    /// </summary>
    public struct StationCameraTarget : IComponentData
    {
        /// <summary>Position offset from station.</summary>
        public float3 PositionOffset;

        /// <summary>Look-at offset from station.</summary>
        public float3 LookAtOffset;

        /// <summary>Field of view for this camera.</summary>
        public float FOV;
    }
}
