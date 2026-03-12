using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Player.Components;
using DIG.Items;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// Syncs ShieldState from equipped shield to PlayerBlockingState on player.
    /// Enables the damage system to check blocking without traversing equipment.
    /// EPIC 15.7: Shield Block System
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ShieldActionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct SyncShieldBlockingSystem : ISystem
    {
        private const int OFF_HAND_SLOT = 1;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Iterate over players with PlayerBlockingState and equipped items
            foreach (var (blockingState, equippedBuffer, localTransform, entity) in
                     SystemAPI.Query<RefRW<PlayerBlockingState>, DynamicBuffer<EquippedItemElement>, RefRO<LocalTransform>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                ref var blocking = ref blockingState.ValueRW;
                
                // Default to not blocking
                blocking.IsBlocking = false;
                blocking.IsParrying = false;
                blocking.DamageReduction = 0f;
                blocking.BlockAngle = 0f;
                blocking.ShieldEntity = Entity.Null;

                // Check if off-hand slot has an item
                if (equippedBuffer.Length <= OFF_HAND_SLOT)
                    continue;

                var offHandItem = equippedBuffer[OFF_HAND_SLOT];
                if (offHandItem.ItemEntity == Entity.Null)
                    continue;

                // Check if the off-hand item is a shield
                if (!SystemAPI.HasComponent<ShieldAction>(offHandItem.ItemEntity))
                    continue;
                if (!SystemAPI.HasComponent<ShieldState>(offHandItem.ItemEntity))
                    continue;

                var shieldConfig = SystemAPI.GetComponent<ShieldAction>(offHandItem.ItemEntity);
                var shieldState = SystemAPI.GetComponent<ShieldState>(offHandItem.ItemEntity);

                // Sync blocking state
                blocking.IsBlocking = shieldState.IsBlocking;
                blocking.IsParrying = shieldState.ParryActive;
                blocking.DamageReduction = shieldConfig.BlockDamageReduction;
                blocking.BlockAngle = shieldConfig.BlockAngle;
                blocking.ShieldEntity = offHandItem.ItemEntity;
                
                // Store player's forward direction when blocking
                if (shieldState.IsBlocking)
                {
                    blocking.BlockDirection = localTransform.ValueRO.Forward();
                }
            }
        }
    }
}
