using Unity.Entities;
using Unity.NetCode;

namespace DIG.Items
{
    /// <summary>
    /// Types of equipment slots.
    /// </summary>
    public enum EquipmentSlotType : byte
    {
        None = 0,
        Primary = 1,      // Main weapon
        Secondary = 2,    // Sidearm/backup
        Melee = 3,        // Melee weapon
        Tool = 4,         // Drill, welder, etc
        Throwable = 5,    // Grenades, throwable items
        Consumable = 6    // Food, medicine
    }

    /// <summary>
    /// Configuration for a character's equipment capacity.
    /// </summary>
    public struct EquipmentSettings : IComponentData
    {
        /// <summary>
        /// Number of primary weapon slots.
        /// </summary>
        public int PrimarySlots;

        /// <summary>
        /// Number of secondary weapon slots.
        /// </summary>
        public int SecondarySlots;

        /// <summary>
        /// Number of tool slots.
        /// </summary>
        public int ToolSlots;

        /// <summary>
        /// Number of consumable slots.
        /// </summary>
        public int ConsumableSlots;

        /// <summary>
        /// Default player equipment settings.
        /// </summary>
        public static EquipmentSettings Default => new()
        {
            PrimarySlots = 2,
            SecondarySlots = 1,
            ToolSlots = 2,
            ConsumableSlots = 4
        };
    }

    /// <summary>
    /// Tag component indicating this entity has equipment slots.
    /// </summary>
    public struct HasEquipment : IComponentData { }

    /// <summary>
    /// Component for items that can be picked up from the world.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ItemPickup : IComponentData
    {
        /// <summary>
        /// Type ID of the item this pickup contains.
        /// </summary>
        [GhostField]
        public int ItemTypeId;

        /// <summary>
        /// Quantity for stackable items.
        /// </summary>
        [GhostField]
        public int Quantity;

        /// <summary>
        /// Radius for auto-pickup detection.
        /// </summary>
        public float PickupRadius;

        /// <summary>
        /// If true, requires interaction key. If false, auto-pickup.
        /// </summary>
        public bool RequiresInteraction;
    }

    /// <summary>
    /// Event component for pickup processing.
    /// </summary>
    public struct PickupEvent : IComponentData
    {
        /// <summary>
        /// The pickup entity being collected.
        /// </summary>
        public Entity PickupEntity;

        /// <summary>
        /// The player collecting the pickup.
        /// </summary>
        public Entity PlayerEntity;

        /// <summary>
        /// Whether this event needs processing.
        /// </summary>
        public bool Pending;
    }

    /// <summary>
    /// First-person/third-person visual configuration for items.
    /// </summary>
    public struct PerspectiveItem : IComponentData
    {
        /// <summary>
        /// Entity for first-person viewmodel (local player only).
        /// </summary>
        public Entity FirstPersonVisual;

        /// <summary>
        /// Entity for third-person worldmodel (seen by others).
        /// </summary>
        public Entity ThirdPersonVisual;

        /// <summary>
        /// True if currently showing first-person view.
        /// </summary>
        public bool IsFirstPerson;
    }
}
