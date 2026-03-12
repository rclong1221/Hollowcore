using System;
using Unity.Entities;
using DIG.Items.Definitions;

namespace DIG.Items
{
    // AnimationWeaponType enum removed - use WeaponCategoryDefinition instead
    
    
    /// <summary>
    /// Data structure returned by IEquipmentProvider containing equipped item information.
    /// This is a pure data struct that can be populated from any inventory system.
    /// </summary>
    public struct ItemInfo
    {
        /// <summary>
        /// The ECS entity of the equipped item (may be Entity.Null for non-ECS systems).
        /// </summary>
        public Entity ItemEntity;
        
        /// <summary>
        /// The AnimatorItemID used by the animation system (e.g., 2=Pistol, 26=Shield, 61=Magic).
        /// </summary>
        public int AnimatorItemID;
        
        /// <summary>
        /// Category ID string (e.g., "Gun", "Melee", "Shield", "Bow", "Magic").
        /// Replaces the deprecated AnimationWeaponType enum.
        /// </summary>
        public string CategoryID;
        
        /// <summary>
        /// The weapon category definition for this item (data-driven replacement for AnimationWeaponType).
        /// </summary>
        public WeaponCategoryDefinition WeaponCategory;
        
        /// <summary>
        /// Movement set ID for the animator (0=Gun, 1=Melee, 2=Bow, etc.).
        /// </summary>
        public int MovementSetID;
        
        /// <summary>
        /// Display name for UI purposes.
        /// </summary>
        public string DisplayName;
        
        /// <summary>
        /// Returns true if this represents an empty slot.
        /// </summary>
        public bool IsEmpty => AnimatorItemID == 0;
        
        /// <summary>
        /// Creates an empty ItemInfo representing no equipped item.
        /// </summary>
        public static ItemInfo Empty => new ItemInfo
        {
            ItemEntity = Entity.Null,
            AnimatorItemID = 0,
            CategoryID = "",
            WeaponCategory = null,
            MovementSetID = 0,
            DisplayName = string.Empty
        };
    }
    
    /// <summary>
    /// Event args for equipment change notifications.
    /// </summary>
    public class EquipmentChangedEventArgs : EventArgs
    {
        public int SlotIndex { get; }
        public ItemInfo OldItem { get; }
        public ItemInfo NewItem { get; }
        
        public EquipmentChangedEventArgs(int slotIndex, ItemInfo oldItem, ItemInfo newItem)
        {
            SlotIndex = slotIndex;
            OldItem = oldItem;
            NewItem = newItem;
        }
    }
    
    /// <summary>
    /// Interface for equipment providers. Abstracts the equipment system so it can be
    /// replaced with Asset Store solutions (Inventory Pro, uMMORPG, etc.) without
    /// changing the animation bridge.
    /// 
    /// Slot indices:
    ///   0 = Main hand (Slot0ItemID)
    ///   1 = Off hand (Slot1ItemID)
    ///   2+ = Additional slots (future)
    /// </summary>
    public interface IEquipmentProvider
    {
        /// <summary>
        /// Get the number of equipment slots available.
        /// </summary>
        int SlotCount { get; }
        
        /// <summary>
        /// Get information about the item equipped in a specific slot.
        /// </summary>
        /// <param name="slotIndex">0 = main hand, 1 = off hand</param>
        /// <returns>ItemInfo with item data, or ItemInfo.Empty if slot is empty</returns>
        ItemInfo GetEquippedItem(int slotIndex);
        
        /// <summary>
        /// Check if a slot has an item equipped.
        /// </summary>
        bool IsSlotOccupied(int slotIndex);
        
        /// <summary>
        /// Equip an item to a slot. Implementation-specific behavior.
        /// </summary>
        void EquipItem(int slotIndex, Entity itemEntity);
        
        /// <summary>
        /// Unequip the item from a slot.
        /// </summary>
        void UnequipItem(int slotIndex);
        
        /// <summary>
        /// Event fired when equipment changes in any slot.
        /// </summary>
        event EventHandler<EquipmentChangedEventArgs> OnEquipmentChanged;

        /// <summary>
        /// Check if a slot is suppressed (e.g. by a two-handed weapon in another slot).
        /// Suppressed items are equipped but should not be visible or usable.
        /// </summary>
        bool IsSlotSuppressed(int slotIndex);
        
        /// <summary>
        /// Convenience property for main hand item.
        /// </summary>
        ItemInfo MainHandItem { get; }
        
        /// <summary>
        /// Convenience property for off hand item.
        /// </summary>
        ItemInfo OffHandItem { get; }
    }
}
