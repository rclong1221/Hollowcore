#pragma warning disable CS0162 // Unreachable code detected - intentional debug toggle
using DIG.Items;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Items.Systems
{
    /// <summary>
    /// Processes EquipRequest components to manage item equipping.
    /// Handles unequip-before-equip queue and animation timing.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(ItemStateSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ItemEquipSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var charItemLookup = SystemAPI.GetComponentLookup<CharacterItem>(false);
            var itemDefLookup = SystemAPI.GetComponentLookup<ItemDefinition>(true);

            // Removed diagnostic logs for Burst compatibility

            foreach (var (request, animState, equippedBuffer, itemSetBuffer, activeSlotIndex, entity) in
                     SystemAPI.Query<RefRW<EquipRequest>, RefRW<EquipAnimationState>, DynamicBuffer<EquippedItemElement>, DynamicBuffer<ItemSetEntry>, RefRW<ActiveSlotIndex>>()
                     .WithAll<HasEquipment>()
                     .WithEntityAccess())
            {
                if (!request.ValueRO.Pending)
                    continue;

                // Resolved orphan DebugEnabled reference

                ref var anim = ref animState.ValueRW;
                
                // Validate SlotId vs Buffer size
                if (request.ValueRO.SlotId < 0 || request.ValueRO.SlotId >= equippedBuffer.Length)
                {
                    // Invalid slot, cancel request
                    request.ValueRW.Pending = false;
                    continue;
                }

                // Resolve ItemEntity from QuickSlot if missing
                if (request.ValueRO.ItemEntity == Entity.Null && request.ValueRO.QuickSlot > 0)
                {
                    // Removed logs for Burst compatibility

                     for (int i = 0; i < itemSetBuffer.Length; i++)
                     {
                         if (itemSetBuffer[i].QuickSlot == request.ValueRO.QuickSlot)
                         {
                             request.ValueRW.ItemEntity = itemSetBuffer[i].ItemEntity;
                             break;
                         }
                     }
                    
                     // If still null after lookup, request failed
                     if (request.ValueRW.ItemEntity == Entity.Null)
                     {
                         request.ValueRW.Pending = false;
                         continue;
                     }
                }

                Entity currentItemEntity = equippedBuffer[request.ValueRO.SlotId].ItemEntity;

                // If currently equipping/unequipping, wait
                if (anim.IsEquipping || anim.IsUnequipping)
                {
                    anim.CurrentTime += deltaTime;
                    
                    if (anim.IsUnequipping)
                    {
                        // Check if unequip is complete
                        if (anim.CurrentTime >= anim.UnequipDuration)
                        {
                            anim.IsUnequipping = false;
                            anim.CurrentTime = 0f;

                            // Mark old item as unequipped
                            if (anim.TargetItem != Entity.Null && charItemLookup.HasComponent(anim.TargetItem))
                            {
                                var oldItem = charItemLookup.GetRefRW(anim.TargetItem);
                                oldItem.ValueRW.State = ItemState.Unequipped;
                                oldItem.ValueRW.StateTime = 0f;
                            }

                            // Now start equipping the new item
                            if (request.ValueRO.ItemEntity != Entity.Null)
                            {
                                anim.IsEquipping = true;
                                anim.TargetItem = request.ValueRO.ItemEntity;

                                if (itemDefLookup.HasComponent(request.ValueRO.ItemEntity))
                                {
                                    anim.EquipDuration = itemDefLookup[request.ValueRO.ItemEntity].EquipDuration;
                                }
                                else
                                {
                                    anim.EquipDuration = 0.5f; // Default
                                }

                                // Start equip on item
                                if (charItemLookup.HasComponent(request.ValueRO.ItemEntity))
                                {
                                    var newItem = charItemLookup.GetRefRW(request.ValueRO.ItemEntity);
                                    newItem.ValueRW.State = ItemState.Equipping;
                                    newItem.ValueRW.StateTime = 0f;
                                    newItem.ValueRW.SlotId = request.ValueRO.SlotId;
                                    newItem.ValueRW.OwnerEntity = entity;
                                }
                            }
                            else
                            {
                                // Just unequipping, no new item - request complete
                                // Update empty buffer
                                var bufferHandle = equippedBuffer;
                                var elem = bufferHandle[request.ValueRO.SlotId];
                                elem.ItemEntity = Entity.Null;
                                elem.QuickSlot = 0;
                                bufferHandle[request.ValueRO.SlotId] = elem;

                                request.ValueRW.Pending = false;
                            }
                        }
                    }
                    else if (anim.IsEquipping)
                    {
                        // Check if equip is complete
                        if (anim.CurrentTime >= anim.EquipDuration)
                        {
                            anim.IsEquipping = false;
                            anim.CurrentTime = 0f;

                            // Mark item as equipped
                            if (anim.TargetItem != Entity.Null && charItemLookup.HasComponent(anim.TargetItem))
                            {
                                var item = charItemLookup.GetRefRW(anim.TargetItem);
                                item.ValueRW.State = ItemState.Equipped;
                                item.ValueRW.StateTime = 0f;
                            }

                            // Update active slot index for ANY valid slot that was just equipped
                            // This ensures throwables (Slot 4) or any other slot gets input focus.
                            if (request.ValueRO.SlotId >= 0)
                            {
                                activeSlotIndex.ValueRW.Value = request.ValueRO.SlotId;
                            }
                            
                            // Update buffer (Use local handle to allow increment)
                            var bufferHandle = equippedBuffer;
                            var p = bufferHandle[request.ValueRO.SlotId];
                            p.ItemEntity = anim.TargetItem;
                            p.QuickSlot = request.ValueRO.QuickSlot;
                            bufferHandle[request.ValueRO.SlotId] = p;

                            // Request complete
                            request.ValueRW.Pending = false;
                        }
                    }
                    else if (anim.IsUnequipping) // Handle Unequip completion
                    {
                          if (anim.CurrentTime >= anim.UnequipDuration)
                          {
                                anim.IsUnequipping = false;
                                anim.CurrentTime = 0f;
                                if (currentItemEntity != Entity.Null && SystemAPI.HasComponent<CharacterItem>(currentItemEntity))
                                {
                                     var item = SystemAPI.GetComponentRW<CharacterItem>(currentItemEntity);
                                     item.ValueRW.State = ItemState.Unequipped;
                                     item.ValueRW.StateTime = 0f;
                                     item.ValueRW.SlotId = -1;
                                     item.ValueRW.OwnerEntity = Entity.Null;
                                }
                                
                                // Clear slot in buffer
                                var bufferHandle = equippedBuffer;
                                var p = bufferHandle[request.ValueRO.SlotId];
                                p.ItemEntity = Entity.Null;
                                p.QuickSlot = 0;
                                bufferHandle[request.ValueRO.SlotId] = p;
                                
                                // If this was just an unequip (no new item), we are done. 
                                // But if pending equip (TargetItem != Null), start equipping.
                                if (request.ValueRO.ItemEntity != Entity.Null)
                                {
                                     anim.IsEquipping = true;
                                     anim.TargetItem = request.ValueRO.ItemEntity;
                                     if (itemDefLookup.HasComponent(request.ValueRO.ItemEntity))
                                         anim.EquipDuration = itemDefLookup[request.ValueRO.ItemEntity].EquipDuration;
                                     else
                                         anim.EquipDuration = 0.5f;
                                     anim.CurrentTime = 0f;
                                     
                                     if (SystemAPI.HasComponent<CharacterItem>(request.ValueRO.ItemEntity))
                                     {
                                         var newItem = SystemAPI.GetComponentRW<CharacterItem>(request.ValueRO.ItemEntity);
                                         newItem.ValueRW.State = ItemState.Equipping;
                                         newItem.ValueRW.StateTime = 0f;
                                         newItem.ValueRW.SlotId = request.ValueRO.SlotId;
                                         newItem.ValueRW.OwnerEntity = entity;
                                     }
                                }
                                else
                                {
                                     request.ValueRW.Pending = false;
                                }
                          }
                    }
                    continue;
                }

                // Start new equip request
                // First, check if we need to unequip current item in this slot
                if (currentItemEntity != Entity.Null)
                {
                    // Need to unequip first
                    anim.IsUnequipping = true;
                    anim.CurrentTime = 0f;
                    anim.TargetItem = currentItemEntity;

                    if (itemDefLookup.HasComponent(currentItemEntity))
                    {
                        anim.UnequipDuration = itemDefLookup[currentItemEntity].UnequipDuration;
                    }
                    else
                    {
                        anim.UnequipDuration = 0.3f; // Default
                    }

                    // Start unequip on item
                    if (charItemLookup.HasComponent(currentItemEntity))
                    {
                        var oldItem = charItemLookup.GetRefRW(currentItemEntity);
                        oldItem.ValueRW.State = ItemState.Unequipping;
                        oldItem.ValueRW.StateTime = 0f;
                    }
                }
                else if (request.ValueRO.ItemEntity != Entity.Null)
                {
                    // No current item, just equip the new one
                    anim.IsEquipping = true;
                    anim.CurrentTime = 0f;
                    anim.TargetItem = request.ValueRO.ItemEntity;

                    if (itemDefLookup.HasComponent(request.ValueRO.ItemEntity))
                    {
                        anim.EquipDuration = itemDefLookup[request.ValueRO.ItemEntity].EquipDuration;
                    }
                    else
                    {
                        anim.EquipDuration = 0.5f; // Default
                    }

                    // Start equip on item
                    if (charItemLookup.HasComponent(request.ValueRO.ItemEntity))
                    {
                        var newItem = charItemLookup.GetRefRW(request.ValueRO.ItemEntity);
                        newItem.ValueRW.State = ItemState.Equipping;
                        newItem.ValueRW.StateTime = 0f;
                        newItem.ValueRW.SlotId = request.ValueRO.SlotId;
                        newItem.ValueRW.OwnerEntity = entity;
                    }
                }
                else
                {
                    // Request for null item with no current item - nothing to do
                    request.ValueRW.Pending = false;
                }
            }

        }
    }
}
