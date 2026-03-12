using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Items;

namespace DIG.Items.Systems
{
    /// <summary>
    /// Processes off-hand use requests (e.g., shield block, torch attack).
    /// This is a minimal implementation for EPIC 14.4.
    /// Will be refactored into InputProfileDefinition system in EPIC 14.5.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [BurstCompile]
    public partial struct OffHandUseInputSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            if (!networkTime.IsFirstTimeFullyPredictingTick)
                return;

            // Process off-hand use requests
            foreach (var (offHandRequest, equippedItems, entity) in 
                SystemAPI.Query<RefRW<OffHandUseRequest>, DynamicBuffer<EquippedItemElement>>()
                .WithEntityAccess())
            {
                if (!offHandRequest.ValueRO.IsPressed)
                    continue;

                // Check if there's an off-hand item (slot 1)
                if (equippedItems.Length <= 1)
                    continue;

                var offHandItem = equippedItems[1];
                if (offHandItem.ItemEntity == Entity.Null)
                    continue;

                // Get the item's animation config to determine behavior
                if (!SystemAPI.HasComponent<ItemAnimationConfig>(offHandItem.ItemEntity))
                    continue;

                var config = SystemAPI.GetComponent<ItemAnimationConfig>(offHandItem.ItemEntity);
                
                // Check category for specific behavior
                // Check category for specific behavior
                if (config.CategoryID == "Shield")
                {
                    // Shield block - handled by WeaponEquipVisualBridge animator params
                    // Just maintain the pressed state, animator bridge reads it
                }
                else if (config.CategoryID == "Torch")
                {
                    // Torch attack - could trigger a light melee attack
                    // For now, just passthrough to animator
                }
                else
                {
                    // Other off-hand items (daggers, etc.) - trigger attack
                }
            }
        }
    }
}

