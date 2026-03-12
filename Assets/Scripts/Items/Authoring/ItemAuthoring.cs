using Unity.Entities;
using UnityEngine;

namespace DIG.Items.Authoring
{
    /// <summary>
    /// Authoring component for item prefabs.
    /// Configures item type, category, and animation timings.
    /// </summary>
    public class ItemAuthoring : MonoBehaviour
    {
        [Header("Item Identity")]
        [Tooltip("Unique ID for this item type")]
        public int ItemTypeId;

        [Tooltip("Display name")]
        public string DisplayName = "Item";

        [Tooltip("Item category")]
        public ItemCategory Category = ItemCategory.None;

        [Header("Equip Settings")]
        [Tooltip("Time to complete equip animation")]
        public float EquipDuration = 0.5f;

        [Tooltip("Time to complete unequip animation")]
        public float UnequipDuration = 0.3f;

        [Header("Stacking")]
        [Tooltip("Can this item stack?")]
        public bool IsStackable = false;

        [Tooltip("Maximum stack size")]
        public int MaxStack = 1;

        [Header("Identity")]
        [Tooltip("Default QuickSlot (1-9) for this item.")]
        [Range(1, 9)]
        public int DefaultQuickSlot = 1;

        public class Baker : Baker<ItemAuthoring>
        {
            public override void Bake(ItemAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Add item definition
                AddComponent(entity, new ItemDefinition
                {
                    ItemTypeId = authoring.ItemTypeId,
                    DisplayName = authoring.DisplayName,
                    Category = authoring.Category,
                    EquipDuration = authoring.EquipDuration,
                    UnequipDuration = authoring.UnequipDuration,
                    IsStackable = authoring.IsStackable,
                    MaxStack = authoring.MaxStack,
                    DefaultQuickSlot = authoring.DefaultQuickSlot
                });

                // Add character item (runtime state)
                AddComponent(entity, new CharacterItem
                {
                    ItemTypeId = authoring.ItemTypeId,
                    SlotId = -1,
                    OwnerEntity = Entity.Null,
                    State = ItemState.Unequipped,
                    StateTime = 0f
                });
            }
        }
    }
}
