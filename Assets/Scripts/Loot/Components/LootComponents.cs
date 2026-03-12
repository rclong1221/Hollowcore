using Unity.Entities;
using Unity.Mathematics;
using DIG.Items;
using DIG.Shared;
using DIG.Loot.Definitions;

namespace DIG.Loot.Components
{
    /// <summary>
    /// EPIC 16.6: Ownership mode for dropped loot.
    /// </summary>
    public enum LootOwnershipMode : byte
    {
        FreeForAll = 0,
        KillerOnly = 1,
        GroupShared = 2
    }

    /// <summary>
    /// EPIC 16.6: Links an enemy entity to its loot table.
    /// Baked by LootTableAuthoring.
    /// </summary>
    public struct LootTableRef : IComponentData
    {
        public int LootTableId;
        public float DropChanceMultiplier;
        public float QuantityMultiplier;
        public bool HasDropped;

        public static LootTableRef Default => new LootTableRef
        {
            DropChanceMultiplier = 1f,
            QuantityMultiplier = 1f,
            HasDropped = false
        };
    }

    /// <summary>
    /// EPIC 16.6: Buffer of pending loot items to spawn.
    /// Written by DeathLootSystem, consumed by LootSpawnSystem.
    /// Placed on the DEAD ENEMY entity (not player).
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct PendingLootSpawn : IBufferElementData
    {
        public int ItemTypeId;
        public int Quantity;
        public ItemRarity Rarity;
        public LootEntryType Type;
        public Economy.CurrencyType Currency;
        public ResourceType Resource;
        public float3 SpawnPosition;
    }

    /// <summary>
    /// EPIC 16.6: Tag marking an entity as a spawned loot pickup.
    /// </summary>
    public struct LootEntity : IComponentData { }

    /// <summary>
    /// EPIC 16.6: Lifetime tracking for loot entities.
    /// Loot despawns after Lifetime seconds.
    /// </summary>
    public struct LootLifetimeECS : IComponentData
    {
        public float SpawnTime;
        public float Lifetime;
    }

    /// <summary>
    /// EPIC 16.6: Ownership tracking for loot entities.
    /// Controls who can pick up the loot.
    /// </summary>
    public struct LootOwnership : IComponentData
    {
        public LootOwnershipMode Mode;
        public Entity OwnerEntity;
        public float OwnershipTimer;
    }

    /// <summary>
    /// EPIC 16.6: Resolved loot drop result (not an ECS component).
    /// Used as intermediate data between LootTableResolver and spawn systems.
    /// </summary>
    public struct LootDrop
    {
        public int ItemTypeId;
        public int Quantity;
        public ItemRarity Rarity;
        public Economy.CurrencyType Currency;
        public ResourceType Resource;
        public LootEntryType Type;
    }
}
