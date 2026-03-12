using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Ship.Airlocks
{
    /// <summary>
    /// Direction of airlock transition.
    /// </summary>
    public enum AirlockDirection : byte
    {
        /// <summary>Transition from exterior (EVA) to interior (ship).</summary>
        EnterShip = 0,
        
        /// <summary>Transition from interior (ship) to exterior (EVA).</summary>
        ExitShip = 1
    }

    /// <summary>
    /// Current state of the airlock cycle.
    /// </summary>
    public enum AirlockState : byte
    {
        /// <summary>Airlock is idle, ready for use.</summary>
        Idle = 0,
        
        /// <summary>Airlock is cycling to allow entry into ship interior.</summary>
        CyclingToInterior = 1,
        
        /// <summary>Airlock is cycling to allow exit to exterior.</summary>
        CyclingToExterior = 2
    }

    /// <summary>
    /// Side of door in airlock (interior/pressurized or exterior/vacuum).
    /// </summary>
    public enum DoorSide : byte
    {
        /// <summary>Door facing ship interior (pressurized side).</summary>
        Interior = 0,
        
        /// <summary>Door facing exterior (vacuum side).</summary>
        Exterior = 1
    }

    /// <summary>
    /// Main airlock component. Placed on airlock entities.
    /// Server-authoritative state machine for airlock cycling.
    /// </summary>
    /// <remarks>
    /// The airlock manages transitions between pressurized and vacuum environments.
    /// Only one player can use an airlock at a time (CurrentUser != Entity.Null during cycle).
    /// Safety invariant: Interior and exterior doors cannot both be open during cycling.
    /// </remarks>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct Airlock : IComponentData
    {
        /// <summary>Reference to parent ship entity.</summary>
        [GhostField]
        public Entity ShipEntity;

        /// <summary>Teleport target position inside ship (pressurized zone).</summary>
        public float3 InteriorSpawn;

        /// <summary>Teleport target position outside ship (vacuum zone).</summary>
        public float3 ExteriorSpawn;

        /// <summary>Forward direction to face after spawning inside.</summary>
        public float3 InteriorForward;

        /// <summary>Forward direction to face after spawning outside.</summary>
        public float3 ExteriorForward;

        /// <summary>Current state of the airlock cycle.</summary>
        [GhostField]
        public AirlockState State;

        /// <summary>Total time for a complete cycle in seconds.</summary>
        public float CycleTime;

        /// <summary>Current progress through the cycle (0 to CycleTime).</summary>
        [GhostField(Quantization = 100)]
        public float CycleProgress;

        /// <summary>Entity of the player currently using this airlock. Entity.Null if not in use.</summary>
        [GhostField]
        public Entity CurrentUser;

        /// <summary>Stable ID for cross-world entity lookup (assigned in authoring).</summary>
        [GhostField]
        public int StableId;

        /// <summary>
        /// Creates a default airlock configuration.
        /// </summary>
        public static Airlock Default => new()
        {
            InteriorSpawn = float3.zero,
            ExteriorSpawn = float3.zero,
            InteriorForward = new float3(0, 0, 1),
            ExteriorForward = new float3(0, 0, -1),
            State = AirlockState.Idle,
            CycleTime = 3f, // 3 second cycle
            CycleProgress = 0f,
            CurrentUser = Entity.Null,
            StableId = 0
        };
    }

    /// <summary>
    /// Component on individual airlock door entities.
    /// Optional - used if doors need separate visual/physics handling.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct AirlockDoor : IComponentData
    {
        /// <summary>Which side of the airlock this door is on.</summary>
        [GhostField]
        public DoorSide DoorSide;

        /// <summary>Current open/closed state of the door.</summary>
        [GhostField]
        public bool IsOpen;

        /// <summary>If true, door cannot be opened (during cycle, damage, etc.).</summary>
        [GhostField]
        public bool IsLocked;

        /// <summary>Reference to the parent airlock entity.</summary>
        public Entity AirlockEntity;
    }

    /// <summary>
    /// Interactable component for airlock entities.
    /// Defines interaction range and prompt text.
    /// </summary>
    public struct AirlockInteractable : IComponentData
    {
        /// <summary>Maximum distance from which player can interact with airlock.</summary>
        public float Range;

        /// <summary>Prompt shown when player in EVA can enter ship.</summary>
        public FixedString64Bytes PromptEnter;

        /// <summary>Prompt shown when player in ship can exit to EVA.</summary>
        public FixedString64Bytes PromptExit;

        /// <summary>Prompt shown when airlock is currently in use.</summary>
        public FixedString64Bytes PromptBusy;

        /// <summary>Prompt shown when airlock is locked (damage, no power, etc.).</summary>
        public FixedString64Bytes PromptLocked;

        /// <summary>
        /// Creates default interactable configuration.
        /// </summary>
        public static AirlockInteractable Default => new()
        {
            Range = 2.5f,
            PromptEnter = "Press E: Enter Ship",
            PromptExit = "Press E: Exit Ship",
            PromptBusy = "Airlock Busy",
            PromptLocked = "Airlock Locked"
        };
    }

    /// <summary>
    /// Buffer element for airlock use requests.
    /// Player appends request; server consumes and validates.
    /// </summary>
    /// <remarks>
    /// Uses request-buffer pattern for client prediction and server authority.
    /// ClientTick is used for ordering and anti-spam protection.
    /// Server validates: distance/range, airlock availability, player state.
    /// </remarks>
    [InternalBufferCapacity(2)]
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct AirlockUseRequest : IBufferElementData
    {
        /// <summary>Stable ID of the airlock being requested (for cross-world lookup).</summary>
        [GhostField]
        public int AirlockStableId;

        /// <summary>Direction of requested transition.</summary>
        [GhostField]
        public AirlockDirection Direction;

        /// <summary>Client tick when request was made (for ordering/anti-spam).</summary>
        [GhostField]
        public uint ClientTick;
    }

    /// <summary>
    /// Component tracking pending airlock transition on a player.
    /// Added to player during cycle, removed on completion or abort.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct AirlockTransitionPending : IComponentData
    {
        /// <summary>The airlock entity the player is transitioning through.</summary>
        [GhostField]
        public Entity AirlockEntity;

        /// <summary>Direction of transition.</summary>
        [GhostField]
        public AirlockDirection Direction;
    }

    /// <summary>
    /// Client-side prompt state for airlock UI.
    /// Updated by AirlockPromptSystem, consumed by UI.
    /// </summary>
    public struct AirlockPromptState : IComponentData
    {
        /// <summary>Best airlock entity for interaction. Entity.Null if none valid.</summary>
        public Entity TargetAirlock;

        /// <summary>Current prompt text to display.</summary>
        public FixedString64Bytes PromptText;

        /// <summary>Whether the prompt should be visible.</summary>
        public bool IsPromptVisible;

        /// <summary>Whether interaction is currently allowed.</summary>
        public bool CanInteract;

        /// <summary>Distance to the target airlock.</summary>
        public float Distance;
    }

    /// <summary>
    /// Tag component indicating an airlock is temporarily locked.
    /// Used for damage, power failure, or other blocking conditions.
    /// </summary>
    public struct AirlockLocked : IComponentData
    {
        /// <summary>Reason for lock (for UI display).</summary>
        public FixedString64Bytes LockReason;
    }

    /// <summary>
    /// Client-side debounce state for interaction input.
    /// Prevents multiple requests per frame.
    /// </summary>
    public struct AirlockInteractDebounce : IComponentData
    {
        /// <summary>Last tick when an interaction was requested.</summary>
        public uint LastRequestTick;

        /// <summary>Minimum ticks between requests.</summary>
        public uint DebounceTickCount;
    }

    /// <summary>
    /// Singleton for airlock system configuration.
    /// </summary>
    public struct AirlockSystemConfig : IComponentData
    {
        /// <summary>Default cycle time for airlocks without custom config.</summary>
        public float DefaultCycleTime;

        /// <summary>Maximum interaction range.</summary>
        public float MaxInteractionRange;

        /// <summary>Rate limit: minimum ticks between requests per player.</summary>
        public uint RequestRateLimitTicks;
    }
}
