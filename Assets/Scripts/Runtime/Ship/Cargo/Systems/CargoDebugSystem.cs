using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using DIG.Shared;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DIG.Ship.Cargo
{
    /// <summary>
    /// Debug system that logs ship cargo contents when player presses T near a cargo terminal.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class CargoDebugSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<NetworkTime>();
        }

        protected override void OnUpdate()
        {
            // Check for T key press using new Input System
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.tKey.wasPressedThisFrame)
                return;
#else
            if (!Input.GetKeyDown(KeyCode.T))
                return;
#endif

            // Find local player with cargo interaction
            foreach (var (interaction, playerEntity) in
                     SystemAPI.Query<RefRO<InteractingWithCargo>>()
                     .WithAll<GhostOwnerIsLocal>()
                     .WithEntityAccess())
            {
                Entity shipEntity = interaction.ValueRO.ShipEntity;
                
                if (shipEntity == Entity.Null)
                {
                    UnityEngine.Debug.Log("[Cargo Debug] No ship entity found for interaction");
                    continue;
                }

                // Log cargo contents
                if (SystemAPI.HasBuffer<ShipCargoItem>(shipEntity))
                {
                    var cargoBuffer = SystemAPI.GetBuffer<ShipCargoItem>(shipEntity);
                    
                    UnityEngine.Debug.Log($"========== SHIP CARGO ==========");
                    
                    if (cargoBuffer.Length == 0)
                    {
                        UnityEngine.Debug.Log("  [Empty - No cargo stored]");
                    }
                    else
                    {
                        for (int i = 0; i < cargoBuffer.Length; i++)
                        {
                            var item = cargoBuffer[i];
                            UnityEngine.Debug.Log($"  {item.ResourceType}: {item.Quantity}");
                        }
                    }
                    
                    // Also log capacity if available
                    if (SystemAPI.HasComponent<ShipCargoCapacity>(shipEntity))
                    {
                        var capacity = SystemAPI.GetComponent<ShipCargoCapacity>(shipEntity);
                        UnityEngine.Debug.Log($"  --------------------------------");
                        UnityEngine.Debug.Log($"  Weight: {capacity.CurrentWeight:F1} / {capacity.MaxWeight:F0} kg");
                        if (capacity.IsOverCapacity)
                        {
                            UnityEngine.Debug.Log($"  !! OVER CAPACITY !!");
                        }
                    }
                    
                    UnityEngine.Debug.Log($"================================");
                }
                else
                {
                    UnityEngine.Debug.Log($"[Cargo Debug] Ship {shipEntity.Index} has no cargo buffer!");
                }

                // Also log player inventory for comparison
                if (SystemAPI.HasBuffer<InventoryItem>(playerEntity))
                {
                    var invBuffer = SystemAPI.GetBuffer<InventoryItem>(playerEntity);
                    
                    UnityEngine.Debug.Log($"========= PLAYER INVENTORY =========");
                    
                    if (invBuffer.Length == 0)
                    {
                        UnityEngine.Debug.Log("  [Empty - No items]");
                    }
                    else
                    {
                        for (int i = 0; i < invBuffer.Length; i++)
                        {
                            var item = invBuffer[i];
                            UnityEngine.Debug.Log($"  {item.ResourceType}: {item.Quantity}");
                        }
                    }
                    
                    UnityEngine.Debug.Log($"====================================");
                }
            }
        }
    }
}
