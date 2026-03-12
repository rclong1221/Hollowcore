using Unity.Entities;
using Unity.NetCode;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Attribute types that players can allocate stat points into.
    /// Maps to CharacterAttributes fields.
    /// </summary>
    public enum StatAttributeType : byte
    {
        Strength = 0,
        Dexterity = 1,
        Intelligence = 2,
        Vitality = 3
    }

    /// <summary>
    /// EPIC 16.14: Request to spend stat points. Buffer on PLAYER entities.
    /// NOT ghost-replicated — written by server RPC handler, consumed by StatAllocationSystem.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct StatAllocationRequest : IBufferElementData
    {
        public StatAttributeType Attribute;
        public int Points;
    }

    /// <summary>
    /// EPIC 16.14: RPC from client requesting stat point allocation.
    /// Server validates UnspentStatPoints before processing.
    /// </summary>
    public struct StatAllocationRpc : IRpcCommand
    {
        public byte Attribute;
        public int Points;
    }
}
