#pragma warning disable CS0162 // Unreachable code detected - intentional debug toggle
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Items.Systems
{
    /// <summary>
    /// Processes item switch requests for equipment slots.
    ///
    /// Handles:
    /// - SwitchToQuickSlot (number keys 1-9 for main hand)
    /// - OffHandQuickSlot (modifier+number keys for off hand)
    ///
    /// NOTE: Legacy types (CycleNext, CyclePrevious, SwitchToSet, SwitchToLast, Holster)
    /// have been removed. All weapon switching now goes through the data-driven slot system (EPIC14.5).
    ///
    /// This system processes ItemSwitchRequest and issues EquipRequest
    /// to the ItemEquipSystem for actual equip/unequip logic.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(ItemEquipSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ItemSetSwitchSystem : ISystem
    {
        // Toggle debug logging for this system. Set to true to enable logs.
        private const bool DebugEnabled = false;
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;

            // Process switch requests
            foreach (var (switchRequest, activeSet, lastEquipped, itemSets, settings, equipRequest, entity) in
                     SystemAPI.Query<
                         RefRW<ItemSwitchRequest>,
                         RefRW<ActiveItemSet>,
                         RefRW<LastEquippedItem>,
                         DynamicBuffer<ItemSetEntry>,
                         RefRO<ItemSwitchSettings>,
                         RefRW<EquipRequest>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                ref var request = ref switchRequest.ValueRW;
                ref var active = ref activeSet.ValueRW;
                ref var last = ref lastEquipped.ValueRW;
                var config = settings.ValueRO;

                if (!request.Pending)
                    continue;

                // Check cooldown
                float timeSinceSwitch = currentTime - active.LastSwitchTime;
                if (timeSinceSwitch < config.SwitchCooldown)
                {
                    // Still on cooldown, keep request pending
                    continue;
                }

                Entity targetItem = Entity.Null;
                int targetIndex = -1;
                int targetQuickSlot = 0; // Track the QuickSlot for visual display
                FixedString32Bytes targetSetName = active.CurrentSetName;

                switch (request.SwitchType)
                {
                    case ItemSwitchType.SwitchToQuickSlot:
                        if (DebugEnabled)
                            UnityEngine.Debug.Log($"[DIG.Weapons] [ItemSetSwitchSystem] Processing MAIN HAND QuickSlot request: {request.QuickSlotNumber}");
                        targetItem = FindItemByQuickSlot(itemSets, request.QuickSlotNumber,
                            out targetSetName, out targetIndex);
                        targetQuickSlot = request.QuickSlotNumber;
                        if (targetItem == Entity.Null)
                        {
                            // Debug dump buffer contents including entity indices
                            var slots = new FixedString512Bytes();
                            for (int i = 0; i < itemSets.Length; i++)
                            {
                                slots.Append(itemSets[i].QuickSlot);
                                slots.Append('(');
                                slots.Append(itemSets[i].ItemEntity.Index);
                                slots.Append(')');
                                if (i < itemSets.Length - 1) slots.Append(',');
                            }
                            if (DebugEnabled)
                                UnityEngine.Debug.LogWarning($"[DIG.Weapons] [ItemSetSwitchSystem] No item found for QuickSlot {request.QuickSlotNumber} in buffer of {itemSets.Length} items. Buffer QuickSlots(Entity): [{slots}]");
                        }
                        break;
                    
                    case ItemSwitchType.OffHandQuickSlot:
                        if (DebugEnabled)
                            UnityEngine.Debug.Log($"[DIG.Weapons] [ItemSetSwitchSystem] Processing OFF-HAND QuickSlot request: {request.QuickSlotNumber}");
                        targetItem = FindItemByQuickSlot(itemSets, request.QuickSlotNumber,
                            out targetSetName, out targetIndex);
                        targetQuickSlot = request.QuickSlotNumber;
                        
                        if (targetItem != Entity.Null)
                        {
                            // Issue equip request for OFF-HAND (SlotId = 1)
                            equipRequest.ValueRW = new EquipRequest
                            {
                                ItemEntity = targetItem,
                                SlotId = 1, // OFF-HAND slot
                                Pending = true,
                                QuickSlot = targetQuickSlot
                            };
                            if (DebugEnabled)
                                UnityEngine.Debug.Log($"[DIG.Weapons] [ItemSetSwitchSystem] Issuing OFF-HAND EquipRequest for Entity {targetItem.Index}, QuickSlot {targetQuickSlot}");
                            active.LastSwitchTime = currentTime;
                        }
                        else
                        {
                            if (DebugEnabled)
                                UnityEngine.Debug.LogWarning($"[DIG.Weapons] [ItemSetSwitchSystem] No item found for OFF-HAND QuickSlot {request.QuickSlotNumber}");
                        }
                        // Clear request and continue (don't fall through to main hand logic)
                        request.Pending = false;
                        request.SwitchType = ItemSwitchType.None;
                        continue;
                    
                    default:
                        // Unknown switch type, clear and skip
                        request.Pending = false;
                        request.SwitchType = ItemSwitchType.None;
                        continue;
                }

                // If we found a valid target and it's different from current
                if (targetItem != Entity.Null && targetItem != active.CurrentItemEntity)
                {
                    // Save current as "last" before switching
                    last.LastItemEntity = active.CurrentItemEntity;
                    last.LastSetName = active.CurrentSetName;
                    last.LastIndex = active.CurrentIndex;

                    // Issue equip request with QuickSlot
                    equipRequest.ValueRW = new EquipRequest
                    {
                        ItemEntity = targetItem,
                        SlotId = 0, // Primary slot
                        Pending = true,
                        QuickSlot = targetQuickSlot
                    };
                    if (DebugEnabled)
                        UnityEngine.Debug.Log($"[DIG.Weapons] [ItemSetSwitchSystem] Issuing EquipRequest for Entity {targetItem.Index}, QuickSlot {targetQuickSlot}");

                    // Update active set tracking
                    active.CurrentItemEntity = targetItem;
                    active.CurrentSetName = targetSetName;
                    active.CurrentIndex = targetIndex;
                    active.LastSwitchTime = currentTime;
                }

                // Clear the request
                request.Pending = false;
                request.SwitchType = ItemSwitchType.None;
            }
        }

        private Entity FindItemByQuickSlot(DynamicBuffer<ItemSetEntry> itemSets,
            int quickSlot, out FixedString32Bytes setName, out int index)
        {
            setName = default;
            index = -1;

            for (int i = 0; i < itemSets.Length; i++)
            {
                if (itemSets[i].QuickSlot == quickSlot)
                {
                    setName = itemSets[i].SetName;
                    index = itemSets[i].Order;
                    return itemSets[i].ItemEntity;
                }
            }

            return Entity.Null;
        }
    }
}
