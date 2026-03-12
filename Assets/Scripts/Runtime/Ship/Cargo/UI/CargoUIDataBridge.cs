using UnityEngine;
using Unity.Entities;
using Unity.NetCode;
using System.Collections.Generic;
using DIG.Shared;

namespace DIG.Ship.Cargo.UI
{
    /// <summary>
    /// Bridge between ECS world and cargo UI.
    /// Reads player inventory, ship cargo, and requests transfers.
    /// </summary>
    public class CargoUIDataBridge : MonoBehaviour
    {
        /// <summary>
        /// Item data for UI display
        /// </summary>
        public struct ItemData
        {
            public ResourceType ResourceType;
            public int Quantity;
        }

        private World clientWorld;
        private EntityQuery playerQuery;
        private EntityQuery cargoTerminalQuery;

        /// <summary>
        /// Is the local player near a cargo terminal?
        /// </summary>
        public bool IsNearCargoTerminal { get; private set; }

        /// <summary>
        /// Current ship entity being interacted with
        /// </summary>
        public Entity CurrentShipEntity { get; private set; }

        private void Update()
        {
            // Find client world
            if (clientWorld == null || !clientWorld.IsCreated)
            {
                clientWorld = GetClientWorld();
                if (clientWorld != null)
                {
                    var em = clientWorld.EntityManager;
                    playerQuery = em.CreateEntityQuery(
                        ComponentType.ReadOnly<PlayerState>(),
                        ComponentType.ReadOnly<GhostOwnerIsLocal>()
                    );
                }
            }

            if (clientWorld == null) return;

            // Check if player is near cargo terminal
            UpdateInteractionState();
        }

        private World GetClientWorld()
        {
            foreach (var world in World.All)
            {
                if (world.IsClient())
                    return world;
            }
            return null;
        }

        private void UpdateInteractionState()
        {
            IsNearCargoTerminal = false;
            CurrentShipEntity = Entity.Null;

            if (clientWorld == null || !clientWorld.IsCreated) return;

            var em = clientWorld.EntityManager;

            // Find local player
            if (playerQuery.IsEmpty) return;

            var players = playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (players.Length == 0)
            {
                players.Dispose();
                return;
            }

            Entity playerEntity = players[0];
            players.Dispose();

            // Check if player has InteractingWithCargo
            if (em.HasComponent<InteractingWithCargo>(playerEntity))
            {
                var interaction = em.GetComponentData<InteractingWithCargo>(playerEntity);
                IsNearCargoTerminal = true;
                CurrentShipEntity = interaction.ShipEntity;
            }
        }

        /// <summary>
        /// Get player inventory items for UI display
        /// </summary>
        public List<ItemData> GetPlayerInventory()
        {
            var items = new List<ItemData>();

            if (clientWorld == null || !clientWorld.IsCreated) return items;

            var em = clientWorld.EntityManager;
            if (playerQuery.IsEmpty) return items;

            var players = playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (players.Length == 0)
            {
                players.Dispose();
                return items;
            }

            Entity playerEntity = players[0];
            players.Dispose();

            if (!em.HasBuffer<InventoryItem>(playerEntity)) return items;

            var buffer = em.GetBuffer<InventoryItem>(playerEntity);
            for (int i = 0; i < buffer.Length; i++)
            {
                items.Add(new ItemData
                {
                    ResourceType = buffer[i].ResourceType,
                    Quantity = buffer[i].Quantity
                });
            }

            return items;
        }

        /// <summary>
        /// Get ship cargo items for UI display
        /// </summary>
        public List<ItemData> GetShipCargo()
        {
            var items = new List<ItemData>();

            if (clientWorld == null || !clientWorld.IsCreated) return items;
            if (CurrentShipEntity == Entity.Null) return items;

            var em = clientWorld.EntityManager;
            if (!em.Exists(CurrentShipEntity)) return items;
            if (!em.HasBuffer<ShipCargoItem>(CurrentShipEntity)) return items;

            var buffer = em.GetBuffer<ShipCargoItem>(CurrentShipEntity);
            for (int i = 0; i < buffer.Length; i++)
            {
                items.Add(new ItemData
                {
                    ResourceType = buffer[i].ResourceType,
                    Quantity = buffer[i].Quantity
                });
            }

            return items;
        }

        /// <summary>
        /// Get player inventory weight (placeholder - needs InventoryCapacity)
        /// </summary>
        public float GetPlayerInventoryWeight()
        {
            if (clientWorld == null || !clientWorld.IsCreated) return 0f;

            var em = clientWorld.EntityManager;
            if (playerQuery.IsEmpty) return 0f;

            var players = playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (players.Length == 0)
            {
                players.Dispose();
                return 0f;
            }

            Entity playerEntity = players[0];
            players.Dispose();

            if (em.HasComponent<InventoryCapacity>(playerEntity))
            {
                return em.GetComponentData<InventoryCapacity>(playerEntity).CurrentWeight;
            }

            return 0f;
        }

        /// <summary>
        /// Get player max weight capacity
        /// </summary>
        public float GetPlayerMaxWeight()
        {
            if (clientWorld == null || !clientWorld.IsCreated) return 100f;

            var em = clientWorld.EntityManager;
            if (playerQuery.IsEmpty) return 100f;

            var players = playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (players.Length == 0)
            {
                players.Dispose();
                return 100f;
            }

            Entity playerEntity = players[0];
            players.Dispose();

            if (em.HasComponent<InventoryCapacity>(playerEntity))
            {
                return em.GetComponentData<InventoryCapacity>(playerEntity).MaxWeight;
            }

            return 100f;
        }

        /// <summary>
        /// Get ship cargo weight
        /// </summary>
        public float GetShipCargoWeight()
        {
            if (clientWorld == null || !clientWorld.IsCreated) return 0f;
            if (CurrentShipEntity == Entity.Null) return 0f;

            var em = clientWorld.EntityManager;
            if (!em.Exists(CurrentShipEntity)) return 0f;
            if (!em.HasComponent<ShipCargoCapacity>(CurrentShipEntity)) return 0f;

            return em.GetComponentData<ShipCargoCapacity>(CurrentShipEntity).CurrentWeight;
        }

        /// <summary>
        /// Get ship max cargo weight
        /// </summary>
        public float GetShipMaxWeight()
        {
            if (clientWorld == null || !clientWorld.IsCreated) return 1000f;
            if (CurrentShipEntity == Entity.Null) return 1000f;

            var em = clientWorld.EntityManager;
            if (!em.Exists(CurrentShipEntity)) return 1000f;
            if (!em.HasComponent<ShipCargoCapacity>(CurrentShipEntity)) return 1000f;

            return em.GetComponentData<ShipCargoCapacity>(CurrentShipEntity).MaxWeight;
        }

        /// <summary>
        /// Is ship over capacity?
        /// </summary>
        public bool IsShipOverCapacity()
        {
            if (clientWorld == null || !clientWorld.IsCreated) return false;
            if (CurrentShipEntity == Entity.Null) return false;

            var em = clientWorld.EntityManager;
            if (!em.Exists(CurrentShipEntity)) return false;
            if (!em.HasComponent<ShipCargoCapacity>(CurrentShipEntity)) return false;

            return em.GetComponentData<ShipCargoCapacity>(CurrentShipEntity).IsOverCapacity;
        }

        /// <summary>
        /// Request a cargo transfer. 
        /// Positive quantity = deposit (player → ship)
        /// Negative quantity = withdraw (ship → player)
        /// </summary>
        public void RequestTransfer(ResourceType resourceType, int quantity)
        {
            if (clientWorld == null || !clientWorld.IsCreated) return;
            if (CurrentShipEntity == Entity.Null) return;

            var em = clientWorld.EntityManager;
            if (playerQuery.IsEmpty) return;

            var players = playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (players.Length == 0)
            {
                players.Dispose();
                return;
            }

            Entity playerEntity = players[0];
            players.Dispose();

            if (!em.HasBuffer<CargoTransferRequest>(playerEntity)) return;

            // Get network time for tick
            uint tick = 0;
            var networkTimeQuery = em.CreateEntityQuery(typeof(NetworkTime));
            if (!networkTimeQuery.IsEmpty)
            {
                var networkTime = networkTimeQuery.GetSingleton<NetworkTime>();
                tick = networkTime.ServerTick.TickIndexForValidTick;
            }

            var buffer = em.GetBuffer<CargoTransferRequest>(playerEntity);
            buffer.Add(new CargoTransferRequest
            {
                ShipEntity = CurrentShipEntity,
                ResourceType = resourceType,
                Quantity = quantity,
                ClientTick = tick
            });

            UnityEngine.Debug.Log($"[CargoUI] Requested transfer: {resourceType} x{quantity} (tick={tick})");
        }
    }
}
