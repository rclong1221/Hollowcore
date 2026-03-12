using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;
using Player.Components;
using DIG.Items;
using DIG.Items.Systems;
using DIG.Weapons;
using DIG.Core.Input;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// Propagates player input to the currently equipped item's UseRequest.
    /// Bridges the gap between PlayerInput (on Player) and UseRequest (on Item).
    /// Uses ItemSetEntry buffer to find the weapon entity by QuickSlot number.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ItemEquipSystem))]
    [UpdateBefore(typeof(WeaponFireSystem))]
    [UpdateBefore(typeof(WeaponAmmoSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PlayerToItemInputSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            bool isServer = state.WorldUnmanaged.IsServer();

            // Iterate over players who have ActiveEquipmentSlot and PlayerInput
            // Server: Process all players (authoritative)
            // Client: Only process local player's input - remote weapon state comes from server replication
            foreach (var (playerInput, activeSlotIndex, equippedBuffer, entity) in
                     SystemAPI.Query<RefRO<PlayerInput>, RefRO<ActiveSlotIndex>, DynamicBuffer<EquippedItemElement>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                // Client-side: Only process local player's input
                // Remote players' weapon state (MeleeState, WeaponFireState) comes from server replication
                // Note: GhostOwnerIsLocal is an enableable component - must check both HasComponent AND IsComponentEnabled
                bool hasGhostOwnerIsLocal = SystemAPI.HasComponent<GhostOwnerIsLocal>(entity);
                bool isGhostOwnerEnabled = hasGhostOwnerIsLocal && SystemAPI.IsComponentEnabled<GhostOwnerIsLocal>(entity);

                if (!isServer && !isGhostOwnerEnabled)
                    continue;

                var input = playerInput.ValueRO;
                int slotIndex = activeSlotIndex.ValueRO.Value;

                // Validate index
                bool isValidSlot = slotIndex >= 0 && slotIndex < equippedBuffer.Length;

                Entity currentItem = Entity.Null;

                // Direct lookup from buffer (Replicated by NetCode)
                if (isValidSlot)
                {
                    currentItem = equippedBuffer[slotIndex].ItemEntity;
                }

                // FALLBACK for Client: If buffer is empty, search for owned equipped item
                // This is specifically for the local player on the client where buffers
                // might not have replicated or predicted yet.
                if (!isServer && isGhostOwnerEnabled && currentItem == Entity.Null)
                {
                    foreach (var (charItemFallback, itemEntityFallback) in SystemAPI.Query<RefRO<CharacterItem>>().WithEntityAccess())
                    {
                        if (charItemFallback.ValueRO.OwnerEntity == entity &&
                            charItemFallback.ValueRO.State == ItemState.Equipped)
                        {
                            if (charItemFallback.ValueRO.SlotId == slotIndex)
                            {
                                currentItem = itemEntityFallback;
                                break;
                            }

                            if (currentItem == Entity.Null && SystemAPI.HasComponent<ThrowableState>(itemEntityFallback))
                            {
                                currentItem = itemEntityFallback;
                            }
                        }
                    }
                }

                if (currentItem == Entity.Null)
                    continue;

                // Check if current item is valid and has UseRequest (is a usable item)
                if (SystemAPI.HasComponent<UseRequest>(currentItem))
                {
                    var useRequest = SystemAPI.GetComponent<UseRequest>(currentItem);

                    // Map Input
                    useRequest.StartUse = input.Use.IsSet;
                    useRequest.StopUse = !input.Use.IsSet;
                    useRequest.Reload = input.Reload.IsSet;

                    // Map Aim Direction (Convert Camera Angles to Direction Vector)
                    // Pitch = X rotation, Yaw = Y rotation
                    quaternion aimRot = quaternion.Euler(math.radians(input.CameraPitch), math.radians(input.CameraYaw), 0f);
                    float3 aimDir = math.mul(aimRot, math.forward());
                    useRequest.AimDirection = aimDir;

                    SystemAPI.SetComponent(currentItem, useRequest);
                }

                // Update Aim State if component exists
                // In MMO/RPG mode, RMB is used for steering, not aiming
                if (currentItem != Entity.Null && SystemAPI.HasComponent<WeaponAimState>(currentItem))
                {
                    var aimState = SystemAPI.GetComponent<WeaponAimState>(currentItem);
                    aimState.IsAiming = input.AltUse.IsSet;
                    SystemAPI.SetComponent(currentItem, aimState);
                }
            }
        }
    }
}
