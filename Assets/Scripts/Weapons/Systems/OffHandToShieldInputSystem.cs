using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using DIG.Items;
using Player.Systems;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// Propagates PlayerInputState.Aim (right-click) to UseRequest on equipped off-hand item.
    /// Supports both shield blocking AND dual-wield weapon firing.
    /// EPIC 15.7: Shield Block System + Dual Wield
    /// 
    /// Input Flow:
    /// DIGInputActions.Aim → PlayerInputReader.OnAim → PlayerInputState.Aim → This System → ShieldActionSystem / WeaponFireSystem
    /// 
    /// Behavior:
    /// - If off-hand has ShieldAction → UseRequest controls blocking (ShieldActionSystem)
    /// - If off-hand has WeaponFireComponent → UseRequest controls firing (WeaponFireSystem)
    /// - If off-hand has MeleeAction → UseRequest controls attacking (MeleeActionSystem)
    /// - If off-hand has ChannelAction → UseRequest controls channeling (ChannelActionSystem)
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(ShieldActionSystem))]
    [UpdateBefore(typeof(WeaponFireSystem))]
    [UpdateBefore(typeof(MeleeActionSystem))]
    [UpdateBefore(typeof(ChannelActionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct OffHandToShieldInputSystem : ISystem
    {
        private const int OFF_HAND_SLOT = 1; // Slot 1 is off-hand

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        // Cannot be Burst compiled due to PlayerInputState static access
        public void OnUpdate(ref SystemState state)
        {
            bool isServer = state.WorldUnmanaged.IsServer();
            
            // Read aim input from PlayerInputState (set by PlayerInputReader from DIGInputActions)
            bool aimPressed = PlayerInputState.Aim;

            // Iterate over players with equipped items
            foreach (var (equippedBuffer, entity) in
                     SystemAPI.Query<DynamicBuffer<EquippedItemElement>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                // Client-side: Only process local player
                if (!isServer)
                {
                    if (!SystemAPI.HasComponent<GhostOwnerIsLocal>(entity) ||
                        !SystemAPI.IsComponentEnabled<GhostOwnerIsLocal>(entity))
                        continue;
                }

                // Check if off-hand slot has an item
                if (equippedBuffer.Length <= OFF_HAND_SLOT)
                    continue;

                var offHandItem = equippedBuffer[OFF_HAND_SLOT];
                if (offHandItem.ItemEntity == Entity.Null)
                    continue;

                // Check if the off-hand item has UseRequest (can be used)
                if (!SystemAPI.HasComponent<UseRequest>(offHandItem.ItemEntity))
                    continue;

                // Check if this is a usable off-hand item (shield, weapon, etc.)
                bool isUsableOffHand = SystemAPI.HasComponent<ShieldAction>(offHandItem.ItemEntity) ||
                                       SystemAPI.HasComponent<WeaponFireComponent>(offHandItem.ItemEntity) ||
                                       SystemAPI.HasComponent<MeleeAction>(offHandItem.ItemEntity) ||
                                       SystemAPI.HasComponent<ChannelAction>(offHandItem.ItemEntity);

                if (!isUsableOffHand)
                    continue;

                // Map PlayerInputState.Aim to off-hand item's UseRequest
                var useRequest = SystemAPI.GetComponent<UseRequest>(offHandItem.ItemEntity);
                bool wasUsing = useRequest.StartUse;
                
                useRequest.StartUse = aimPressed;
                useRequest.StopUse = !aimPressed && wasUsing;

                SystemAPI.SetComponent(offHandItem.ItemEntity, useRequest);
            }
        }
    }
}
