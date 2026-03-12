using Unity.Entities;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Request sent to a Downed entity to revive it.
    /// Used by server to validate and process revives.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct ReviveRequest : IBufferElementData
    {
        [GhostField]
        public Entity Reviver;
        
        [GhostField]
        public uint ClientTick;
    }
}
