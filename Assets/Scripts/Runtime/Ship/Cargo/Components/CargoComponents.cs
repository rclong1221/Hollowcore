using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Shared;

namespace DIG.Ship.Cargo
{
    /// <summary>
    /// Buffer element for ship cargo storage.
    /// Stores resource type and quantity in ship's cargo hold.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    [InternalBufferCapacity(16)]
    public struct ShipCargoItem : IBufferElementData
    {
        /// <summary>
        /// Type of resource stored.
        /// </summary>
        [GhostField]
        public ResourceType ResourceType;

        /// <summary>
        /// Quantity of this resource in cargo.
        /// </summary>
        [GhostField]
        public int Quantity;
    }

    /// <summary>
    /// Tracks ship's cargo capacity and current weight.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ShipCargoCapacity : IComponentData
    {
        /// <summary>
        /// Maximum cargo weight in kg.
        /// </summary>
        public float MaxWeight;

        /// <summary>
        /// Current total cargo weight in kg.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float CurrentWeight;

        /// <summary>
        /// True if cargo is at or over capacity.
        /// </summary>
        [GhostField]
        public bool IsOverCapacity;

        /// <summary>
        /// Default cargo capacity for a ship.
        /// </summary>
        public static ShipCargoCapacity Default => new()
        {
            MaxWeight = 1000f,
            CurrentWeight = 0f,
            IsOverCapacity = false
        };
    }

    /// <summary>
    /// Request to transfer resources between player inventory and ship cargo.
    /// Positive quantity = deposit (player -> ship)
    /// Negative quantity = withdraw (ship -> player)
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    [InternalBufferCapacity(4)]
    public struct CargoTransferRequest : IBufferElementData
    {
        /// <summary>
        /// Ship entity to transfer cargo to/from.
        /// </summary>
        [GhostField]
        public Entity ShipEntity;

        /// <summary>
        /// Type of resource to transfer.
        /// </summary>
        [GhostField]
        public ResourceType ResourceType;

        /// <summary>
        /// Quantity to transfer. Positive = deposit, negative = withdraw.
        /// </summary>
        [GhostField]
        public int Quantity;

        /// <summary>
        /// Client tick when request was made (for ordering/anti-spam).
        /// </summary>
        [GhostField]
        public uint ClientTick;
    }

    /// <summary>
    /// Component on cargo terminal entities that allow cargo interaction.
    /// Place on a terminal/console in the ship interior.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct CargoTerminal : IComponentData
    {
        /// <summary>
        /// Ship entity this terminal belongs to.
        /// </summary>
        [GhostField]
        public Entity ShipEntity;

        /// <summary>
        /// Interaction range in meters.
        /// </summary>
        public float Range;

        /// <summary>
        /// Stable ID for network resolution.
        /// </summary>
        public int StableId;
    }

    /// <summary>
    /// Added to player when they are interacting with a cargo terminal.
    /// Used to track which terminal/ship they're interacting with.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct InteractingWithCargo : IComponentData
    {
        /// <summary>
        /// The cargo terminal entity being interacted with.
        /// </summary>
        [GhostField]
        public Entity TerminalEntity;

        /// <summary>
        /// The ship entity whose cargo is being accessed.
        /// </summary>
        [GhostField]
        public Entity ShipEntity;
    }

    /// <summary>
    /// Interactable marker for cargo terminals.
    /// Contains prompt text for UI.
    /// </summary>
    public struct CargoTerminalInteractable : IComponentData
    {
        /// <summary>
        /// Prompt shown when player can interact.
        /// </summary>
        public FixedString64Bytes PromptText;
    }

    /// <summary>
    /// Static utility methods for cargo operations.
    /// </summary>
    public static class CargoUtility
    {
        /// <summary>
        /// Finds cargo of a specific type in the buffer.
        /// Returns -1 if not found.
        /// </summary>
        public static int FindCargoIndex(in DynamicBuffer<ShipCargoItem> cargo, ResourceType type)
        {
            for (int i = 0; i < cargo.Length; i++)
            {
                if (cargo[i].ResourceType == type)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Gets the quantity of a specific resource in cargo.
        /// Returns 0 if not found.
        /// </summary>
        public static int GetCargoQuantity(in DynamicBuffer<ShipCargoItem> cargo, ResourceType type)
        {
            int index = FindCargoIndex(cargo, type);
            return index >= 0 ? cargo[index].Quantity : 0;
        }

        /// <summary>
        /// Adds quantity to cargo. Creates new entry if type doesn't exist.
        /// </summary>
        public static void AddToCargo(ref DynamicBuffer<ShipCargoItem> cargo, ResourceType type, int quantity)
        {
            int index = FindCargoIndex(cargo, type);
            if (index >= 0)
            {
                var item = cargo[index];
                item.Quantity += quantity;
                cargo[index] = item;
            }
            else
            {
                cargo.Add(new ShipCargoItem
                {
                    ResourceType = type,
                    Quantity = quantity
                });
            }
        }

        /// <summary>
        /// Removes quantity from cargo. Returns false if insufficient.
        /// </summary>
        public static bool RemoveFromCargo(ref DynamicBuffer<ShipCargoItem> cargo, ResourceType type, int quantity)
        {
            int index = FindCargoIndex(cargo, type);
            if (index < 0) return false;

            var item = cargo[index];
            if (item.Quantity < quantity) return false;

            item.Quantity -= quantity;
            if (item.Quantity <= 0)
            {
                cargo.RemoveAt(index);
            }
            else
            {
                cargo[index] = item;
            }
            return true;
        }

        /// <summary>
        /// Finds inventory item of a specific type in the buffer.
        /// Returns -1 if not found.
        /// </summary>
        public static int FindInventoryIndex(in DynamicBuffer<InventoryItem> inventory, ResourceType type)
        {
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ResourceType == type)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Gets the quantity of a specific resource in player inventory.
        /// Returns 0 if not found.
        /// </summary>
        public static int GetInventoryQuantity(in DynamicBuffer<InventoryItem> inventory, ResourceType type)
        {
            int index = FindInventoryIndex(inventory, type);
            return index >= 0 ? inventory[index].Quantity : 0;
        }

        /// <summary>
        /// Adds quantity to player inventory. Creates new entry if type doesn't exist.
        /// </summary>
        public static void AddToInventory(ref DynamicBuffer<InventoryItem> inventory, ResourceType type, int quantity)
        {
            int index = FindInventoryIndex(inventory, type);
            if (index >= 0)
            {
                var item = inventory[index];
                item.Quantity += quantity;
                inventory[index] = item;
            }
            else
            {
                inventory.Add(new InventoryItem
                {
                    ResourceType = type,
                    Quantity = quantity
                });
            }
        }

        /// <summary>
        /// Removes quantity from player inventory. Returns false if insufficient.
        /// </summary>
        public static bool RemoveFromInventory(ref DynamicBuffer<InventoryItem> inventory, ResourceType type, int quantity)
        {
            int index = FindInventoryIndex(inventory, type);
            if (index < 0) return false;

            var item = inventory[index];
            if (item.Quantity < quantity) return false;

            item.Quantity -= quantity;
            if (item.Quantity <= 0)
            {
                inventory.RemoveAt(index);
            }
            else
            {
                inventory[index] = item;
            }
            return true;
        }
    }
}
