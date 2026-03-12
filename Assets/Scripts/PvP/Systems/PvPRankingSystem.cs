using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Player.Components;
using DIG.Progression;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Post-match Elo calculation. Server-only, authoritative ranking.
    /// Runs exactly once when match transitions to Results phase.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PvPAntiGriefSystem))]
    public partial class PvPRankingSystem : SystemBase
    {
        private EntityQuery _playerQuery;
        private PvPMatchPhase _previousPhase;
        private bool _rankingCalculated;

        protected override void OnCreate()
        {
            _playerQuery = GetEntityQuery(
                ComponentType.ReadOnly<PvPPlayerStats>(),
                ComponentType.ReadOnly<PvPTeam>(),
                ComponentType.ReadOnly<PvPRankingLink>(),
                ComponentType.ReadOnly<PlayerTag>());
            RequireForUpdate<PvPMatchState>();
            RequireForUpdate<PvPConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            var matchState = SystemAPI.GetSingleton<PvPMatchState>();

            // Reset flag when match starts
            if (matchState.Phase == PvPMatchPhase.Active && _previousPhase != PvPMatchPhase.Active)
                _rankingCalculated = false;

            // Calculate rankings exactly once on transition to Results
            if (matchState.Phase == PvPMatchPhase.Results && !_rankingCalculated)
            {
                _rankingCalculated = true;
                CalculateRankings(matchState);
            }

            _previousPhase = matchState.Phase;
        }

        private void CalculateRankings(PvPMatchState matchState)
        {
            ref var config = ref SystemAPI.GetSingleton<PvPConfigSingleton>().Config.Value;

            var entities = _playerQuery.ToEntityArray(Allocator.Temp);
            var stats = _playerQuery.ToComponentDataArray<PvPPlayerStats>(Allocator.Temp);
            var teams = _playerQuery.ToComponentDataArray<PvPTeam>(Allocator.Temp);
            var rankLinks = _playerQuery.ToComponentDataArray<PvPRankingLink>(Allocator.Temp);

            // Determine winning team
            byte winningTeam = DetermineWinner(matchState);

            // Create match result entity
            var resultEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(resultEntity, new PvPMatchResult
            {
                GameMode = matchState.GameMode,
                WinningTeam = winningTeam,
                PlayerCount = (byte)entities.Length,
                MatchDurationActual = matchState.MatchDuration - matchState.Timer
            });

            // Calculate Elo for each player
            int avgElo = CalculateAverageElo(entities, rankLinks);

            for (int i = 0; i < entities.Length; i++)
            {
                if (rankLinks[i].RankingChild == Entity.Null)
                    continue;
                if (!EntityManager.HasComponent<PvPRanking>(rankLinks[i].RankingChild))
                    continue;

                var ranking = EntityManager.GetComponentData<PvPRanking>(rankLinks[i].RankingChild);

                // Determine match result for this player
                float actualResult;
                if (winningTeam == 0)
                    actualResult = 0.5f; // Draw
                else if (teams[i].TeamId == winningTeam)
                    actualResult = 1.0f; // Win
                else
                    actualResult = 0.0f; // Loss

                // Calculate expected win probability
                double expectedWin = 1.0 / (1.0 + math.pow(10.0, (double)(avgElo - ranking.Elo) / 400.0));

                // Determine K factor
                float k = config.EloKFactor;
                if (ranking.Elo > config.EloHighRatingThreshold)
                    k = config.EloKFactorHighRating;
                if (ranking.PlacementMatchesPlayed < config.PlacementMatchCount)
                    k *= config.PlacementKMultiplier;

                // Elo delta
                float eloDelta = k * (actualResult - (float)expectedWin);

                // Win streak bonus
                if (actualResult > 0.5f)
                {
                    ranking.WinStreak++;
                    ranking.Wins++;
                    if (ranking.WinStreak >= 3)
                    {
                        int streakBonus = math.min(
                            config.EloKFactor > 0 ? 5 * (ranking.WinStreak - 2) : 0,
                            25);
                        eloDelta += streakBonus;
                    }
                }
                else if (actualResult < 0.5f)
                {
                    ranking.WinStreak = 0;
                    ranking.Losses++;
                }

                // Apply Elo change
                ranking.Elo = math.max(0, ranking.Elo + (int)math.round(eloDelta));
                if (ranking.Elo > ranking.HighestElo)
                    ranking.HighestElo = ranking.Elo;

                // Update placement counter
                if (ranking.PlacementMatchesPlayed < 255)
                    ranking.PlacementMatchesPlayed++;

                // Update tier from thresholds
                ranking.Tier = GetTierFromElo(ranking.Elo, ref config);

                EntityManager.SetComponentData(rankLinks[i].RankingChild, ranking);

                // Award XP
                if (EntityManager.HasComponent<PlayerProgression>(entities[i]))
                {
                    float xpBonus = actualResult > 0.5f ? config.PvPWinBonusXP : config.PvPLossBonusXP;
                    if (xpBonus > 0)
                        XPGrantAPI.GrantXP(EntityManager, entities[i], (int)xpBonus, XPSourceType.Bonus);
                }
            }

            entities.Dispose();
            stats.Dispose();
            teams.Dispose();
            rankLinks.Dispose();
        }

        private int CalculateAverageElo(NativeArray<Entity> entities, NativeArray<PvPRankingLink> rankLinks)
        {
            int total = 0;
            int count = 0;
            for (int i = 0; i < entities.Length; i++)
            {
                if (rankLinks[i].RankingChild == Entity.Null) continue;
                if (!EntityManager.HasComponent<PvPRanking>(rankLinks[i].RankingChild)) continue;

                total += EntityManager.GetComponentData<PvPRanking>(rankLinks[i].RankingChild).Elo;
                count++;
            }
            return count > 0 ? total / count : 1200;
        }

        private byte DetermineWinner(PvPMatchState state)
        {
            switch (state.GameMode)
            {
                case PvPGameMode.TeamDeathmatch:
                case PvPGameMode.CapturePoint:
                    if (state.TeamScore0 > state.TeamScore1) return 1;
                    if (state.TeamScore1 > state.TeamScore0) return 2;
                    return 0;

                case PvPGameMode.Duel:
                    if (state.TeamScore0 > state.TeamScore1) return 1;
                    if (state.TeamScore1 > state.TeamScore0) return 2;
                    return 0;

                case PvPGameMode.FreeForAll:
                    int maxScore = math.max(math.max(state.TeamScore0, state.TeamScore1),
                                            math.max(state.TeamScore2, state.TeamScore3));
                    if (maxScore == 0) return 0;
                    if (state.TeamScore0 == maxScore) return 1;
                    if (state.TeamScore1 == maxScore) return 2;
                    if (state.TeamScore2 == maxScore) return 3;
                    if (state.TeamScore3 == maxScore) return 4;
                    return 0;

                default:
                    return 0;
            }
        }

        private static PvPTier GetTierFromElo(int elo, ref PvPConfigBlob config)
        {
            PvPTier tier = PvPTier.Bronze;
            for (int i = 0; i < config.TierThresholds.Length; i++)
            {
                if (elo >= config.TierThresholds[i])
                    tier = (PvPTier)i;
                else
                    break;
            }
            return tier;
        }
    }
}
