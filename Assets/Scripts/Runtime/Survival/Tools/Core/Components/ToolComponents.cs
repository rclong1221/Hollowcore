using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Survival.Tools
{
    /// <summary>
    /// Enum defining all tool types in the game.
    /// </summary>
    public enum ToolType : byte
    {
        None = 0,
        Welder = 1,
        Drill = 2,
        Sprayer = 3,
        Flashlight = 4,
        Geiger = 5
    }

    /// <summary>
    /// Core tool component identifying a tool entity.
    /// Every tool entity must have this component.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct Tool : IComponentData
    {
        /// <summary>
        /// The type of tool this entity represents.
        /// </summary>
        [GhostField]
        public ToolType ToolType;

        /// <summary>
        /// Display name for UI purposes.
        /// </summary>
        public FixedString32Bytes DisplayName;
    }

    /// <summary>
    /// Tracks durability/ammo for tools that consume resources.
    /// Placed on tool entities, predicted for smooth client experience.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct ToolDurability : IComponentData
    {
        /// <summary>
        /// Current durability/ammo level (0 to Max).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float Current;

        /// <summary>
        /// Maximum durability/ammo capacity.
        /// </summary>
        public float Max;

        /// <summary>
        /// Rate at which durability depletes per second while tool is in use.
        /// </summary>
        public float DegradeRatePerSecond;

        /// <summary>
        /// True when Current <= 0. Tool cannot be used when depleted.
        /// </summary>
        [GhostField]
        public bool IsDepleted;

        /// <summary>
        /// Returns current durability as a percentage (0-1).
        /// </summary>
        public readonly float Percent => Max > 0 ? Current / Max : 0;

        /// <summary>
        /// Creates a default fully-charged tool durability.
        /// </summary>
        public static ToolDurability Default(float max = 100f, float degradeRate = 1f) => new()
        {
            Current = max,
            Max = max,
            DegradeRatePerSecond = degradeRate,
            IsDepleted = false
        };
    }

    /// <summary>
    /// Tracks the current usage state of a tool.
    /// Updated by input and raycast systems, consumed by tool-specific usage systems.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct ToolUsageState : IComponentData
    {
        /// <summary>
        /// True while the use input is held down.
        /// </summary>
        [GhostField]
        public bool IsInUse;

        /// <summary>
        /// Accumulated time the tool has been in use (for charged actions).
        /// Resets when IsInUse becomes false.
        /// </summary>
        public float UseTimer;

        /// <summary>
        /// World position where the tool's raycast hit.
        /// Updated by ToolRaycastSystem each frame.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float3 TargetPoint;

        /// <summary>
        /// The entity being targeted by the tool (if any).
        /// Entity.Null if raycast hit world geometry or nothing.
        /// </summary>
        [GhostField]
        public Entity TargetEntity;

        /// <summary>
        /// Normal of the surface hit by the raycast.
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float3 TargetNormal;

        /// <summary>
        /// True if the raycast hit something within range.
        /// </summary>
        [GhostField]
        public bool HasTarget;
    }

    /// <summary>
    /// Tracks which tool is currently equipped by a player.
    /// Placed on player entities, predicted for smooth switching.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct ActiveTool : IComponentData
    {
        /// <summary>
        /// Reference to the currently equipped tool entity.
        /// Entity.Null means empty hands (no tool equipped).
        /// </summary>
        [GhostField]
        public Entity ToolEntity;

        /// <summary>
        /// The hotbar slot index of the currently equipped tool (0-4).
        /// -1 means no tool selected.
        /// </summary>
        [GhostField]
        public int SlotIndex;

        /// <summary>
        /// Default state with no tool equipped.
        /// </summary>
        public static ActiveTool Empty => new()
        {
            ToolEntity = Entity.Null,
            SlotIndex = -1
        };
    }

    /// <summary>
    /// Buffer element tracking tools owned by a player.
    /// Each element represents a tool in a specific hotbar slot.
    /// </summary>
    [InternalBufferCapacity(5)] // Default 5 tool slots
    public struct ToolOwnership : IBufferElementData
    {
        /// <summary>
        /// Reference to the owned tool entity.
        /// </summary>
        public Entity ToolEntity;

        /// <summary>
        /// The hotbar slot this tool occupies (0-4).
        /// </summary>
        public int SlotIndex;
    }

    /// <summary>
    /// Tag component linking a tool entity back to its owner.
    /// Added to tool entities when spawned for a player.
    /// </summary>
    public struct ToolOwner : IComponentData
    {
        /// <summary>
        /// The player entity that owns this tool.
        /// </summary>
        public Entity OwnerEntity;
    }
}
