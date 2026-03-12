using System.IO;
using Unity.Entities;
using DIG.PvP;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 17.10: ISaveModule implementation for PvP ranking persistence.
    /// TypeId = 15. Serializes Elo/Tier/Wins/Losses/WinStreak from the
    /// PvPRanking child entity via PvPRankingLink.
    /// 23 bytes per save.
    /// </summary>
    public class PvPRankingSaveModule : ISaveModule
    {
        public int TypeId => SaveModuleTypeIds.PvPRanking;
        public string DisplayName => "PvP Ranking";
        public int ModuleVersion => 1;

        public int Serialize(in SaveContext context, BinaryWriter writer)
        {
            var em = context.EntityManager;
            var player = context.PlayerEntity;

            if (!em.HasComponent<PvPRankingLink>(player))
                return 0;

            var link = em.GetComponentData<PvPRankingLink>(player);
            if (link.RankingChild == Entity.Null || !em.HasComponent<PvPRanking>(link.RankingChild))
                return 0;

            var ranking = em.GetComponentData<PvPRanking>(link.RankingChild);

            long start = writer.BaseStream.Position;

            writer.Write(ranking.Elo);                    // 4 bytes
            writer.Write((byte)ranking.Tier);             // 1 byte
            writer.Write(ranking.Wins);                   // 4 bytes
            writer.Write(ranking.Losses);                 // 4 bytes
            writer.Write(ranking.WinStreak);              // 4 bytes
            writer.Write(ranking.HighestElo);             // 4 bytes

            // Anti-grief persistence
            byte leaverPenalty = 0;
            byte placementMatches = ranking.PlacementMatchesPlayed;
            if (em.HasComponent<PvPAntiGriefState>(player))
                leaverPenalty = em.GetComponentData<PvPAntiGriefState>(player).LeaverPenaltyCount;

            writer.Write(leaverPenalty);                  // 1 byte
            writer.Write(placementMatches);               // 1 byte

            return (int)(writer.BaseStream.Position - start); // 23 bytes
        }

        public void Deserialize(in LoadContext context, BinaryReader reader, int blockVersion)
        {
            var em = context.EntityManager;
            var player = context.PlayerEntity;

            // Read all fields regardless of component presence
            int elo = reader.ReadInt32();
            PvPTier tier = (PvPTier)reader.ReadByte();
            int wins = reader.ReadInt32();
            int losses = reader.ReadInt32();
            int winStreak = reader.ReadInt32();
            int highestElo = reader.ReadInt32();
            byte leaverPenalty = reader.ReadByte();
            byte placementMatches = reader.ReadByte();

            // Apply to ranking child entity
            if (!em.HasComponent<PvPRankingLink>(player))
                return;

            var link = em.GetComponentData<PvPRankingLink>(player);
            if (link.RankingChild == Entity.Null || !em.HasComponent<PvPRanking>(link.RankingChild))
                return;

            em.SetComponentData(link.RankingChild, new PvPRanking
            {
                Elo = elo,
                Tier = tier,
                PlacementMatchesPlayed = placementMatches,
                Wins = wins,
                Losses = losses,
                WinStreak = winStreak,
                HighestElo = highestElo
            });

            // Restore leaver penalty
            if (em.HasComponent<PvPAntiGriefState>(player))
            {
                var grief = em.GetComponentData<PvPAntiGriefState>(player);
                grief.LeaverPenaltyCount = leaverPenalty;
                em.SetComponentData(player, grief);
            }
        }
    }
}
