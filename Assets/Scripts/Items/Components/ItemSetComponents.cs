using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Items
{
    /// <summary>
    /// Represents a group of items that can be equipped together.
    /// Similar to OPSIVE's ItemSet concept - allows grouping weapons
    /// that share a slot or represent a loadout.
    ///
    /// Example: ItemSetGroup "Primary" might contain Rifle, Shotgun, SMG
    /// Player can cycle through these with scroll wheel or number keys.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    [InternalBufferCapacity(8)]
    public struct ItemSetEntry : IBufferElementData
    {
        /// <summary>
        /// Name of this item set (e.g., "Primary", "Secondary", "Melee").
        /// </summary>
        [GhostField]
        public FixedString32Bytes SetName;

        /// <summary>
        /// The item entity in this set.
        /// </summary>
        [GhostField]
        public Entity ItemEntity;

        /// <summary>
        /// Order within the set for cycling (0 = first).
        /// </summary>
        [GhostField]
        public int Order;

        /// <summary>
        /// Quick slot number (1-9, 0 = no quick slot).
        /// </summary>
        [GhostField]
        public int QuickSlot;

        /// <summary>
        /// Whether this item is the default for its set.
        /// </summary>
        [GhostField]
        public bool IsDefault;
    }

    /// <summary>
    /// Tracks the currently active item set and index within that set.
    /// Added to player entities for weapon switching.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ActiveItemSet : IComponentData
    {
        /// <summary>
        /// Name of the currently active set.
        /// </summary>
        [GhostField]
        public FixedString32Bytes CurrentSetName;

        /// <summary>
        /// Index within the current set (for cycling).
        /// </summary>
        [GhostField]
        public int CurrentIndex;

        /// <summary>
        /// Entity of the currently equipped item.
        /// </summary>
        [GhostField]
        public Entity CurrentItemEntity;

        /// <summary>
        /// Time when last switch occurred (for cooldown).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float LastSwitchTime;
    }

    /// <summary>
    /// Request to switch items. Processed by ItemSetSwitchSystem.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ItemSwitchRequest : IComponentData
    {
        /// <summary>
        /// Type of switch request.
        /// </summary>
        [GhostField]
        public ItemSwitchType SwitchType;

        /// <summary>
        /// Quick slot number (for SwitchToQuickSlot and OffHandQuickSlot).
        /// </summary>
        [GhostField]
        public int QuickSlotNumber;

        /// <summary>
        /// Whether this request is pending.
        /// </summary>
        [GhostField]
        public bool Pending;
    }

    /// <summary>
    /// Types of item switch requests.
    /// NOTE: Legacy types (CycleNext, CyclePrevious, SwitchToSet, SwitchToLast, Holster) have been removed.
    /// All weapon switching now goes through the data-driven slot system (EPIC14.5).
    /// </summary>
    public enum ItemSwitchType : byte
    {
        None = 0,
        SwitchToQuickSlot,  // Number key 1-9 (main hand, slot 0)
        OffHandQuickSlot    // Modifier+Number key 1-9 (off hand, slot 1)
    }

    /// <summary>
    /// Configuration for item switching behavior.
    /// Added to player prefabs.
    /// </summary>
    public struct ItemSwitchSettings : IComponentData
    {
        /// <summary>
        /// Minimum time between switches (prevents spam).
        /// </summary>
        public float SwitchCooldown;

        /// <summary>
        /// Whether cycling wraps around (last → first).
        /// </summary>
        public bool WrapAround;

        /// <summary>
        /// Whether to auto-equip default item on spawn.
        /// </summary>
        public bool AutoEquipDefault;

        /// <summary>
        /// Default values.
        /// </summary>
        public static ItemSwitchSettings Default => new ItemSwitchSettings
        {
            SwitchCooldown = 0.25f,
            WrapAround = true,
            AutoEquipDefault = true
        };
    }

    /// <summary>
    /// Tracks the last equipped item for "switch to last" functionality.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct LastEquippedItem : IComponentData
    {
        [GhostField]
        public Entity LastItemEntity;

        [GhostField]
        public FixedString32Bytes LastSetName;

        [GhostField]
        public int LastIndex;
    }
}
