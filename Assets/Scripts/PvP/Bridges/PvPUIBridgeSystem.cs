using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Player.Components;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Managed SystemBase in PresentationSystemGroup.
    /// Reads PvPMatchState singleton, local player PvPPlayerStats/PvPRanking,
    /// dequeues PvPKillFeedQueue, dispatches to PvPUIRegistry -> IPvPUIProvider.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class PvPUIBridgeSystem : SystemBase
    {
        private EntityQuery _matchStateQuery;
        private EntityQuery _localPlayerQuery;
        private PvPMatchPhase _previousPhase;

        protected override void OnCreate()
        {
            _matchStateQuery = GetEntityQuery(ComponentType.ReadOnly<PvPMatchState>());
            _localPlayerQuery = GetEntityQuery(
                ComponentType.ReadOnly<PvPPlayerStats>(),
                ComponentType.ReadOnly<PvPTeam>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>());
            RequireForUpdate(_matchStateQuery);
        }

        protected override void OnUpdate()
        {
            if (!PvPUIRegistry.HasProvider) return;

            var matchState = SystemAPI.GetSingleton<PvPMatchState>();

            // Phase change notification
            if (matchState.Phase != _previousPhase)
            {
                PvPUIRegistry.OnMatchPhaseChange(_previousPhase, matchState.Phase);
                _previousPhase = matchState.Phase;
            }

            // Build match UI state (no managed allocation — uses inline fields)
            var uiState = new PvPMatchUIState
            {
                Phase = matchState.Phase,
                GameMode = matchState.GameMode,
                TimeRemaining = matchState.Timer,
                TeamScore0 = matchState.TeamScore0,
                TeamScore1 = matchState.TeamScore1,
                TeamScore2 = matchState.TeamScore2,
                TeamScore3 = matchState.TeamScore3,
                MaxScore = matchState.MaxScore
            };

            // Get local player stats and ranking in one pass
            if (_localPlayerQuery.CalculateEntityCount() > 0)
            {
                var localEntities = _localPlayerQuery.ToEntityArray(Allocator.Temp);
                var localStats = _localPlayerQuery.ToComponentDataArray<PvPPlayerStats>(Allocator.Temp);

                if (localStats.Length > 0)
                {
                    uiState.LocalPlayerKills = localStats[0].Kills;
                    uiState.LocalPlayerDeaths = localStats[0].Deaths;
                    uiState.LocalPlayerAssists = localStats[0].Assists;
                }

                // Update ranking from same query result (avoids second CalculateEntityCount)
                if (localEntities.Length > 0 && EntityManager.HasComponent<PvPRankingLink>(localEntities[0]))
                {
                    var link = EntityManager.GetComponentData<PvPRankingLink>(localEntities[0]);
                    if (link.RankingChild != Entity.Null && EntityManager.HasComponent<PvPRanking>(link.RankingChild))
                    {
                        var ranking = EntityManager.GetComponentData<PvPRanking>(link.RankingChild);
                        int totalGames = ranking.Wins + ranking.Losses;
                        PvPUIRegistry.UpdateRanking(new PvPRankingUI
                        {
                            Elo = ranking.Elo,
                            Tier = ranking.Tier,
                            Wins = ranking.Wins,
                            Losses = ranking.Losses,
                            WinStreak = ranking.WinStreak,
                            HighestElo = ranking.HighestElo,
                            WinRate = totalGames > 0 ? (float)ranking.Wins / totalGames : 0f
                        });
                    }
                }

                localEntities.Dispose();
                localStats.Dispose();
            }

            PvPUIRegistry.UpdateMatchState(uiState);

            // Process kill feed queue
            while (PvPKillFeedQueue.TryDequeue(out var killEntry))
            {
                PvPUIRegistry.OnKillFeedEvent(new PvPKillFeedUIEntry
                {
                    KillerName = "Player",
                    VictimName = "Player",
                    KillerTeam = killEntry.KillerTeam,
                    VictimTeam = killEntry.VictimTeam,
                    IsLocalPlayerKiller = false,
                    IsLocalPlayerVictim = false
                });
            }
        }
    }
}
