using Unity.Entities;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: Transient request buffer on STATION — "start crafting recipe X".
    /// Written by CraftRpcReceiveSystem, consumed by CraftValidationSystem.
    /// NOT ghost-replicated (server-only transient).
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct CraftRequest : IBufferElementData
    {
        public int RecipeId;
        public Entity RequestingPlayer;
    }

    /// <summary>
    /// EPIC 16.13: Transient request buffer on STATION — "collect output at index Y".
    /// Written by CraftRpcReceiveSystem, consumed by CraftOutputCollectionSystem.
    /// NOT ghost-replicated (server-only transient).
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct CollectCraftRequest : IBufferElementData
    {
        public int OutputIndex;
        public Entity RequestingPlayer;
    }

    /// <summary>
    /// EPIC 16.13: Transient request buffer on STATION — "cancel queued craft at index Z".
    /// Written by CraftRpcReceiveSystem, consumed by CraftCancellationSystem.
    /// NOT ghost-replicated (server-only transient).
    /// </summary>
    [InternalBufferCapacity(1)]
    public struct CancelCraftRequest : IBufferElementData
    {
        public int QueueIndex;
        public Entity RequestingPlayer;
    }
}
