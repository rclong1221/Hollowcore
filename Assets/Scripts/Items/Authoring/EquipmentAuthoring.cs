using Unity.Entities;
using UnityEngine;

namespace DIG.Items.Authoring
{
    /// <summary>
    /// Authoring component for characters with equipment slots.
    /// Configures slot counts and types.
    /// </summary>
    public class EquipmentAuthoring : MonoBehaviour
    {
        [Header("Slot Configuration")]
        [Tooltip("Number of primary weapon slots")]
        public int PrimarySlots = 2;

        [Tooltip("Number of secondary weapon slots")]
        public int SecondarySlots = 1;

        [Tooltip("Number of tool slots")]
        public int ToolSlots = 2;

        [Tooltip("Number of consumable slots")]
        public int ConsumableSlots = 4;

        public class Baker : Baker<EquipmentAuthoring>
        {
            public override void Bake(EquipmentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Add equipment settings
                AddComponent(entity, new EquipmentSettings
                {
                    PrimarySlots = authoring.PrimarySlots,
                    SecondarySlots = authoring.SecondarySlots,
                    ToolSlots = authoring.ToolSlots,
                    ConsumableSlots = authoring.ConsumableSlots
                });

                // Add tag
                AddComponent(entity, new HasEquipment());

                // Add active slot tracker
                AddComponent(entity, new ActiveSlotIndex
                {
                    Value = -1
                });

                // Add equip request (initially empty)
                AddComponent(entity, new EquipRequest
                {
                    ItemEntity = Entity.Null,
                    SlotId = -1,
                    Pending = false
                });
                
                // Add equipped item buffer
                var equippedBuffer = AddBuffer<EquippedItemElement>(entity);
                
                // Calculate total slots from authoring configuration
                int totalSlots = authoring.PrimarySlots + authoring.SecondarySlots + authoring.ToolSlots + authoring.ConsumableSlots;
                
                // Default to 2 slots if config is zero/missing (Safety fallback for legacy)
                if (totalSlots < 2) totalSlots = 2;

                // Initialize empty slots
                for (int i = 0; i < totalSlots; i++)
                {
                    equippedBuffer.Add(new EquippedItemElement { ItemEntity = Entity.Null, QuickSlot = 0 });
                }

                Debug.Log($"[EquipmentAuthoring] Initialized {totalSlots} equipment slots for entity {entity.Index}.");

                // Add equip animation state (Restored)
                AddComponent(entity, new EquipAnimationState
                {
                    EquipDuration = 0f,
                    UnequipDuration = 0f,
                    CurrentTime = 0f,
                    IsEquipping = false,
                    IsUnequipping = false,
                    TargetItem = Entity.Null
                });
            }
        }
    }
}
