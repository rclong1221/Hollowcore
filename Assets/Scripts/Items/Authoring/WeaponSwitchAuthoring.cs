using Unity.Entities;
using UnityEngine;
using DIG.Items.Systems;

namespace DIG.Items
{
    /// <summary>
    /// Authoring component for adding weapon switch capability to player prefabs.
    /// Add this to enable weapon switching via number keys and scroll wheel.
    /// </summary>
    [AddComponentMenu("DIG/Items/Weapon Switch Authoring")]
    public class WeaponSwitchAuthoring : MonoBehaviour
    {
        [Header("Switch Settings")]
        [Tooltip("Minimum time between weapon switches")]
        public float SwitchCooldown = 0.25f;

        [Tooltip("Wrap around when cycling (last → first)")]
        public bool WrapAround = true;

        [Tooltip("Auto-equip default weapon on spawn")]
        public bool AutoEquipDefault = true;

        public class Baker : Baker<WeaponSwitchAuthoring>
        {
            public override void Bake(WeaponSwitchAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Add item switch settings
                AddComponent(entity, new ItemSwitchSettings
                {
                    SwitchCooldown = authoring.SwitchCooldown,
                    WrapAround = authoring.WrapAround,
                    AutoEquipDefault = authoring.AutoEquipDefault
                });

                // Add input component for weapon switching
                AddComponent(entity, new WeaponSwitchInput());

                // Add switch request component
                AddComponent(entity, new ItemSwitchRequest());

                // Add active set tracking
                AddComponent(entity, new ActiveItemSet());

                // Add last equipped tracking
                AddComponent(entity, new LastEquippedItem());

                // Add equip request component (required by ItemSetSwitchSystem)
                AddComponent(entity, new EquipRequest());
                
                // Add off-hand use request component (for shield blocking etc)
                AddComponent(entity, new OffHandUseRequest());

                AddComponent(entity, new ActiveSlotIndex { Value = -1 });

                // Add components required by ItemEquipSystem for visual equipping
                AddComponent(entity, new HasEquipment());
                
                // Add equipped item buffer
                var equippedBuffer = AddBuffer<EquippedItemElement>(entity);
                equippedBuffer.Add(new EquippedItemElement { ItemEntity = Entity.Null, QuickSlot = 0 });
                equippedBuffer.Add(new EquippedItemElement { ItemEntity = Entity.Null, QuickSlot = 0 });
                
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
