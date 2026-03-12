using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Link from player entity to save state child entity.
    /// Only 8 bytes on the player archetype.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct SaveStateLink : IComponentData
    {
        [GhostField] public Entity SaveChildEntity;
    }

    /// <summary>Marker tag on save state child entity.</summary>
    public struct SaveStateTag : IComponentData { }

    /// <summary>
    /// Dirty flags bitmask. Bit N = module with TypeId (N+1) has unsaved changes.
    /// </summary>
    public struct SaveDirtyFlags : IComponentData
    {
        public uint Flags;

        public bool IsDirty(int typeId) => (Flags & (1u << (typeId - 1))) != 0;
        public void SetDirty(int typeId) => Flags |= (1u << (typeId - 1));
        public void ClearDirty(int typeId) => Flags &= ~(1u << (typeId - 1));
        public void ClearAll() => Flags = 0;
        public bool AnyDirty => Flags != 0;
    }

    /// <summary>
    /// Stable player identity for save file naming. Populated from GhostOwner.NetworkId.
    /// </summary>
    public struct PlayerSaveId : IComponentData
    {
        public FixedString64Bytes PlayerId;
    }

    /// <summary>Back-reference from save child entity to owning player entity.</summary>
    public struct SaveStateOwner : IComponentData
    {
        public Entity Owner;
    }
}
