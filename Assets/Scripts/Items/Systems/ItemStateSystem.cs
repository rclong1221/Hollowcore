using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Items.Systems
{
    /// <summary>
    /// Updates CharacterItem state based on animation timers.
    /// Handles Equipping → Equipped and Unequipping → Unequipped transitions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ItemStateSystem : ISystem
    {
        // Toggle debug logging for this system. Set to true to enable logs.
        private const bool DebugEnabled = false;
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (item, definition, entity) in 
                     SystemAPI.Query<RefRW<CharacterItem>, RefRO<ItemDefinition>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                ref var itemRef = ref item.ValueRW;
                
                // Update state time
                itemRef.StateTime += deltaTime;

                switch (itemRef.State)
                {
                    case ItemState.Equipping:
                        if (itemRef.StateTime >= definition.ValueRO.EquipDuration)
                        {
                            itemRef.State = ItemState.Equipped;
                            itemRef.StateTime = 0f;
                        }
                        break;

                    case ItemState.Unequipping:
                        if (itemRef.StateTime >= definition.ValueRO.UnequipDuration)
                        {
                            itemRef.State = ItemState.Unequipped;
                            itemRef.StateTime = 0f;
                            itemRef.SlotId = -1; // Clear slot assignment
                        }
                        break;

                    case ItemState.Dropping:
                        // Dropping is handled by a separate system that spawns the world item
                        break;
                }
            }
        }
    }
}
