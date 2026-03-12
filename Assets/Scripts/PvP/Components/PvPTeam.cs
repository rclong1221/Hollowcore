using Unity.Entities;
using Unity.NetCode;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: PvP team assignment on the player entity.
    /// TeamId mirrors the existing TeamId component for collision filtering.
    /// SpawnPointIndex selects which spawn point this player uses.
    /// 4 bytes.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PvPTeam : IComponentData
    {
        [GhostField] public byte TeamId;
        [GhostField] public byte SpawnPointIndex;
        public byte Padding0;
        public byte Padding1;
    }
}
