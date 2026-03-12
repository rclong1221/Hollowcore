using Unity.Entities;
using Unity.NetCode;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Link from player entity to child entity holding ranking data.
    /// Same pattern as SaveStateLink and TalentLink.
    /// 8 bytes on player entity.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PvPRankingLink : IComponentData
    {
        [GhostField] public Entity RankingChild;
    }

    /// <summary>
    /// Tag marking ranking child entities.
    /// </summary>
    public struct PvPRankingTag : IComponentData { }

    /// <summary>
    /// Back-reference from ranking child to player entity.
    /// </summary>
    public struct PvPRankingOwner : IComponentData
    {
        public Entity Owner;
    }

    /// <summary>
    /// EPIC 17.10: Persistent ranking data on child entity. NOT on player archetype.
    /// Persisted via PvPRankingSaveModule (TypeId=15).
    /// 24 bytes.
    /// </summary>
    public struct PvPRanking : IComponentData
    {
        public int Elo;
        public PvPTier Tier;
        public byte PlacementMatchesPlayed;
        public byte Padding0;
        public byte Padding1;
        public int Wins;
        public int Losses;
        public int WinStreak;
        public int HighestElo;
    }
}
