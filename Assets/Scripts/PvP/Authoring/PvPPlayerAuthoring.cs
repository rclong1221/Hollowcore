using Unity.Entities;
using UnityEngine;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Authoring component for PvP player data.
    /// Place on Player prefab (Warrok_Server) alongside existing authoring components.
    /// Baker creates child entity for ranking (same pattern as SaveStateAuthoring / TalentAuthoring).
    /// Player entity: ~72 bytes. Child entity: ~32 bytes.
    /// </summary>
    [AddComponentMenu("DIG/PvP/PvP Player")]
    public class PvPPlayerAuthoring : MonoBehaviour
    {
        [Header("Ranking Defaults")]
        [Tooltip("Starting Elo for new players.")]
        public int StartingElo = 1200;

        public class Baker : Baker<PvPPlayerAuthoring>
        {
            public override void Bake(PvPPlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Player entity components (~72 bytes total)
                AddComponent(entity, default(PvPPlayerStats));      // 24 bytes
                AddComponent(entity, default(PvPTeam));             // 4 bytes
                AddComponent(entity, default(PvPAntiGriefState));   // 8 bytes

                // IEnableableComponents (baked disabled)
                AddComponent(entity, default(PvPSpawnProtection));  // 4 bytes
                SetComponentEnabled<PvPSpawnProtection>(entity, false);

                AddComponent(entity, default(PvPRespawnTimer));     // 4 bytes
                SetComponentEnabled<PvPRespawnTimer>(entity, false);

                AddComponent(entity, default(PvPStatOverride));     // 20 bytes
                SetComponentEnabled<PvPStatOverride>(entity, false);

                AddComponent(entity, default(PvPKillMarker));       // 0 bytes (tag)
                SetComponentEnabled<PvPKillMarker>(entity, false);

                // Child entity for ranking data (not on player archetype)
                var childEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                AddComponent(childEntity, new PvPRankingTag());
                AddComponent(childEntity, new PvPRankingOwner { Owner = entity });
                AddComponent(childEntity, new PvPRanking
                {
                    Elo = authoring.StartingElo,
                    Tier = PvPTier.Bronze,
                    PlacementMatchesPlayed = 0,
                    Wins = 0,
                    Losses = 0,
                    WinStreak = 0,
                    HighestElo = authoring.StartingElo
                });

                // Link from player to ranking child
                AddComponent(entity, new PvPRankingLink { RankingChild = childEntity }); // 8 bytes
            }
        }
    }
}
