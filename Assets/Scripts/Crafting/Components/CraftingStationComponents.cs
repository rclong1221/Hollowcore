using Unity.Entities;
using Unity.NetCode;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: Marks an entity as a crafting station. Placed on STATION entities.
    /// Ghost-replicated so clients can display station type and tier.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct CraftingStation : IComponentData
    {
        [GhostField] public StationType StationType;
        [GhostField] public byte StationTier;
        [GhostField] public float SpeedMultiplier;
        [GhostField] public byte MaxQueueSize;
    }

    /// <summary>
    /// EPIC 16.13: An entry in the crafting queue. Buffer on STATION entities.
    /// Ghost-replicated so clients see real-time queue progress.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    [InternalBufferCapacity(4)]
    public struct CraftQueueElement : IBufferElementData
    {
        [GhostField] public int RecipeId;
        [GhostField] public Entity RequestingPlayer;
        [GhostField(Quantization = 100)] public float CraftTimeTotal;
        [GhostField(Quantization = 100)] public float CraftTimeElapsed;
        [GhostField] public CraftState State;
        [GhostField] public uint RandomSeed;
    }

    /// <summary>
    /// EPIC 16.13: Completed craft output waiting for collection. Buffer on STATION entities.
    /// Ghost-replicated so clients see available outputs.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    [InternalBufferCapacity(4)]
    public struct CraftOutputElement : IBufferElementData
    {
        [GhostField] public int RecipeId;
        [GhostField] public int OutputItemTypeId;
        [GhostField] public int OutputQuantity;
        [GhostField] public byte OutputType;
        [GhostField] public byte OutputResourceType;
        [GhostField] public Entity ForPlayer;
    }
}
