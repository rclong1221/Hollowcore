using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Items
{
    /// <summary>
    /// State of an item in the equip lifecycle.
    /// </summary>
    public enum ItemState : byte
    {
        Unequipped = 0,
        Equipping = 1,
        Equipped = 2,
        Unequipping = 3,
        Dropping = 4
    }

    /// <summary>
    /// Category of item for inventory organization.
    /// </summary>
    public enum ItemCategory : byte
    {
        None = 0,
        Weapon = 1,
        Tool = 2,
        Consumable = 3,
        Ammo = 4,
        Equipment = 5
    }

    /// <summary>
    /// Core item data attached to item entities.
    /// Tracks owner, slot, and current state.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct CharacterItem : IComponentData
    {
        /// <summary>
        /// Unique identifier for this item type (links to ItemDefinition).
        /// </summary>
        [GhostField]
        public int ItemTypeId;

        /// <summary>
        /// Slot this item is assigned to.
        /// </summary>
        [GhostField]
        public int SlotId;

        /// <summary>
        /// Entity that owns this item.
        /// </summary>
        [GhostField]
        public Entity OwnerEntity;

        /// <summary>
        /// Current state in the item lifecycle.
        /// </summary>
        [GhostField]
        public ItemState State;

        /// <summary>
        /// Time spent in current state.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float StateTime;
    }

    /// <summary>
    /// Static item metadata. Added to item prefabs.
    /// </summary>
    public struct ItemDefinition : IComponentData
    {
        /// <summary>
        /// Unique type ID for this item.
        /// </summary>
        public int ItemTypeId;

        /// <summary>
        /// Display name.
        /// </summary>
        public FixedString64Bytes DisplayName;

        /// <summary>
        /// Item category.
        /// </summary>
        public ItemCategory Category;

        /// <summary>
        /// Time to complete equip animation.
        /// </summary>
        public float EquipDuration;

        /// <summary>
        /// Time to complete unequip animation.
        /// </summary>
        public float UnequipDuration;

        /// <summary>
        /// Whether this item can be stacked.
        /// </summary>
        public bool IsStackable;

        /// <summary>
        /// Max stack size (1 for non-stackable).
        /// </summary>
        public int MaxStack;

        /// <summary>
        /// Default QuickSlot (1-9) for this item when added to inventory.
        /// </summary>
        public int DefaultQuickSlot;
    }



    /// <summary>
    /// Request to equip or unequip an item.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct EquipRequest : IComponentData
    {
        /// <summary>
        /// Item to equip (Entity.Null to just unequip current).
        /// </summary>
        [GhostField]
        public Entity ItemEntity;

        /// <summary>
        /// Target slot.
        /// </summary>
        [GhostField]
        public int SlotId;

        /// <summary>
        /// Whether this request is pending processing.
        /// </summary>
        [GhostField]
        public bool Pending;
        
        /// <summary>
        /// QuickSlot number (1-9) for this equip request.
        /// </summary>
        [GhostField]
        public int QuickSlot;
    }




    /// <summary>
    /// Request to use the off-hand item (e.g. Shield block, Torch swing).
    /// </summary>
    public struct OffHandUseRequest : IComponentData
    {
        public bool IsPressed; // True if the off-hand use input is currently held/pressed
    }

    /// <summary>
    /// Tracks animation timing during equip/unequip transitions.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct EquipAnimationState : IComponentData
    {
        /// <summary>
        /// Duration of current equip animation.
        /// </summary>
        public float EquipDuration;

        /// <summary>
        /// Duration of current unequip animation.
        /// </summary>
        public float UnequipDuration;

        /// <summary>
        /// Time elapsed in current animation.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float CurrentTime;

        /// <summary>
        /// True if currently equipping.
        /// </summary>
        [GhostField]
        public bool IsEquipping;

        /// <summary>
        /// True if currently unequipping.
        /// </summary>
        [GhostField]
        public bool IsUnequipping;

        /// <summary>
        /// Item being equipped/unequipped.
        /// </summary>
        [GhostField]
        public Entity TargetItem;
    }

    /// <summary>
    /// Tracks which slot is currently active (visible, usable).
    /// </summary>
    /// <summary>
    /// Tracks which slot is currently "Selected" (receiving input).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ActiveSlotIndex : IComponentData
    {
        [GhostField]
        public int Value;
    }

    /// <summary>
    /// Tracks items equipped in equipment slots.
    /// Index in buffer corresponds to Slot Index (0=Main, 1=Off, etc).
    /// </summary>
    [GhostComponent]
    public struct EquippedItemElement : IBufferElementData
    {
        [GhostField]
        public Entity ItemEntity;
        
        [GhostField]
        public int QuickSlot;
    }



    /// <summary>
    /// Configuration for weapon animation behavior.
    /// Replaces hardcoded values in WeaponEquipVisualBridge.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ItemAnimationConfig : IComponentData
    {
        [GhostField]
        public int AnimatorItemID;
        
        [GhostField]
        public int MovementSetID;
        
        /// <summary>
        /// Category ID string from WeaponCategoryDefinition (e.g., "Gun", "Melee", "Shield").
        /// Replaces the deprecated AnimationWeaponType enum.
        /// </summary>
        [GhostField]
        public FixedString32Bytes CategoryID;
        
        [GhostField]
        public int ComboCount;
        
        [GhostField]
        public float UseDuration;
        
        [GhostField]
        public bool LockMovementDuringUse;
        
        [GhostField]
        public bool CancelUseOnMove;
        
        [GhostField]
        public bool IsChanneled;
        
        [GhostField]
        public bool RequireAimToFire;

        /// <summary>
        /// If true, this weapon requires both hands (e.g. Rifle, Greatsword).
        /// Off-hand visuals will be suppressed when equipped.
        /// </summary>
        [GhostField]
        public bool IsTwoHanded;

        /// <summary>
        /// Default values for a basic gun.
        /// </summary>
        public static ItemAnimationConfig Default => new ItemAnimationConfig
        {
            AnimatorItemID = 0,
            MovementSetID = 0,
            CategoryID = "Gun",
            ComboCount = 0,
            UseDuration = 0f,
            LockMovementDuringUse = false,
            CancelUseOnMove = false,
            IsChanneled = false,
            RequireAimToFire = false,
            IsTwoHanded = false
        };
    }
}
