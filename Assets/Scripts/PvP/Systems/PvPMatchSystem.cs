using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Player.Components;
using DIG.Player.Components;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Match lifecycle state machine.
    /// Drives PvPMatchState.Phase: Waiting -> Warmup -> Active -> Overtime -> Results -> Ended.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial class PvPMatchSystem : SystemBase
    {
        private EntityQuery _matchStateQuery;
        private EntityQuery _requestQuery;
        private EntityQuery _playerQuery;
        private PvPMatchPhase _previousPhase;

        protected override void OnCreate()
        {
            _matchStateQuery = GetEntityQuery(ComponentType.ReadWrite<PvPMatchState>());
            _requestQuery = GetEntityQuery(ComponentType.ReadOnly<PvPMatchRequest>());
            _playerQuery = GetEntityQuery(
                ComponentType.ReadWrite<PvPPlayerStats>(),
                ComponentType.ReadWrite<PvPTeam>(),
                ComponentType.ReadOnly<PlayerTag>());
            RequireForUpdate<PvPConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            ref var config = ref SystemAPI.GetSingleton<PvPConfigSingleton>().Config.Value;
            float dt = SystemAPI.Time.DeltaTime;

            // Handle match creation from PvPMatchRequest
            if (_matchStateQuery.CalculateEntityCount() == 0 && _requestQuery.CalculateEntityCount() > 0)
            {
                HandleMatchRequest(ref config);
                return;
            }

            if (_matchStateQuery.CalculateEntityCount() == 0)
                return;

            var stateEntity = _matchStateQuery.GetSingletonEntity();
            var state = EntityManager.GetComponentData<PvPMatchState>(stateEntity);

            switch (state.Phase)
            {
                case PvPMatchPhase.WaitingForPlayers:
                    UpdateWaitingForPlayers(ref state, ref config);
                    break;

                case PvPMatchPhase.Warmup:
                    state.Timer -= dt;
                    if (state.Timer <= 0f)
                    {
                        state.Phase = PvPMatchPhase.Active;
                        state.Timer = state.MatchDuration;
                    }
                    break;

                case PvPMatchPhase.Active:
                    state.Timer -= dt;
                    if (CheckWinCondition(ref state, ref config))
                    {
                        state.Phase = PvPMatchPhase.Results;
                        state.Timer = config.ResultsDuration;
                    }
                    else if (state.Timer <= 0f)
                    {
                        if (IsScoreTied(ref state) && state.OvertimeEnabled == 1)
                        {
                            state.Phase = PvPMatchPhase.Overtime;
                            state.Timer = config.OvertimeDuration;
                        }
                        else
                        {
                            state.Phase = PvPMatchPhase.Results;
                            state.Timer = config.ResultsDuration;
                        }
                    }
                    break;

                case PvPMatchPhase.Overtime:
                    state.Timer -= dt;
                    if (CheckWinCondition(ref state, ref config) || state.Timer <= 0f)
                    {
                        state.Phase = PvPMatchPhase.Results;
                        state.Timer = config.ResultsDuration;
                    }
                    break;

                case PvPMatchPhase.Results:
                    state.Timer -= dt;
                    if (state.Timer <= 0f)
                    {
                        state.Phase = PvPMatchPhase.Ended;
                        state.Timer = 0f;
                    }
                    break;

                case PvPMatchPhase.Ended:
                    HandleMatchEnded(stateEntity);
                    return;
            }

            EntityManager.SetComponentData(stateEntity, state);
        }

        private void HandleMatchRequest(ref PvPConfigBlob config)
        {
            var requestEntities = _requestQuery.ToEntityArray(Allocator.Temp);
            var requests = _requestQuery.ToComponentDataArray<PvPMatchRequest>(Allocator.Temp);

            if (requests.Length > 0)
            {
                var req = requests[0];

                // Create match state singleton
                var matchEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(matchEntity, new PvPMatchState
                {
                    Phase = PvPMatchPhase.WaitingForPlayers,
                    GameMode = req.GameMode,
                    MapId = req.MapId,
                    OvertimeEnabled = 1,
                    Timer = 0f,
                    MatchDuration = req.MatchDuration > 0f ? req.MatchDuration : 600f,
                    MaxScore = req.MaxScore > 0 ? req.MaxScore : config.GetMaxScoreForMode(req.GameMode),
                    TeamScore0 = 0,
                    TeamScore1 = 0,
                    TeamScore2 = 0,
                    TeamScore3 = 0
                });

                AssignTeams(req.GameMode);
            }

            // Destroy all requests
            for (int i = 0; i < requestEntities.Length; i++)
                EntityManager.DestroyEntity(requestEntities[i]);

            requestEntities.Dispose();
            requests.Dispose();
        }

        private void AssignTeams(PvPGameMode mode)
        {
            var entities = _playerQuery.ToEntityArray(Allocator.Temp);
            var teams = _playerQuery.ToComponentDataArray<PvPTeam>(Allocator.Temp);
            var teamIdLookup = GetComponentLookup<DIG.Player.Components.TeamId>(false);

            for (int i = 0; i < entities.Length; i++)
            {
                var team = teams[i];
                switch (mode)
                {
                    case PvPGameMode.FreeForAll:
                        team.TeamId = (byte)(i + 1);
                        break;
                    case PvPGameMode.TeamDeathmatch:
                        team.TeamId = (byte)((i % 2) + 1);
                        break;
                    case PvPGameMode.CapturePoint:
                        team.TeamId = (byte)((i % 2) + 1);
                        break;
                    case PvPGameMode.Duel:
                        team.TeamId = (byte)(i < 1 ? 1 : 2);
                        break;
                }
                team.SpawnPointIndex = (byte)i;
                EntityManager.SetComponentData(entities[i], team);

                // Sync to existing TeamId component for collision filtering
                if (teamIdLookup.HasComponent(entities[i]))
                {
                    teamIdLookup[entities[i]] = new DIG.Player.Components.TeamId { Value = team.TeamId };
                }
            }

            entities.Dispose();
            teams.Dispose();
        }

        private void UpdateWaitingForPlayers(ref PvPMatchState state, ref PvPConfigBlob config)
        {
            int playerCount = _playerQuery.CalculateEntityCount();
            int minPlayers = 2;

            if (playerCount >= minPlayers)
            {
                state.Phase = PvPMatchPhase.Warmup;
                state.Timer = config.WarmupDuration;
            }
        }

        private bool CheckWinCondition(ref PvPMatchState state, ref PvPConfigBlob config)
        {
            switch (state.GameMode)
            {
                case PvPGameMode.FreeForAll:
                    return state.TeamScore0 >= state.MaxScore ||
                           state.TeamScore1 >= state.MaxScore ||
                           state.TeamScore2 >= state.MaxScore ||
                           state.TeamScore3 >= state.MaxScore;

                case PvPGameMode.TeamDeathmatch:
                    return state.TeamScore0 >= state.MaxScore ||
                           state.TeamScore1 >= state.MaxScore;

                case PvPGameMode.CapturePoint:
                    return state.TeamScore0 >= state.MaxScore ||
                           state.TeamScore1 >= state.MaxScore;

                case PvPGameMode.Duel:
                    int roundsToWin = (config.DuelRounds / 2) + 1;
                    return state.TeamScore0 >= roundsToWin ||
                           state.TeamScore1 >= roundsToWin;

                default:
                    return false;
            }
        }

        private bool IsScoreTied(ref PvPMatchState state)
        {
            switch (state.GameMode)
            {
                case PvPGameMode.TeamDeathmatch:
                case PvPGameMode.CapturePoint:
                    return state.TeamScore0 == state.TeamScore1;
                case PvPGameMode.Duel:
                    return state.TeamScore0 == state.TeamScore1;
                default:
                    return false;
            }
        }

        private void HandleMatchEnded(Entity stateEntity)
        {
            // Reset all PvPPlayerStats
            var entities = _playerQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                EntityManager.SetComponentData(entities[i], default(PvPPlayerStats));
                EntityManager.SetComponentData(entities[i], default(PvPTeam));

                // Reset TeamId
                if (EntityManager.HasComponent<DIG.Player.Components.TeamId>(entities[i]))
                    EntityManager.SetComponentData(entities[i], default(DIG.Player.Components.TeamId));

                // Disable spawn protection and respawn timer
                if (EntityManager.HasComponent<PvPSpawnProtection>(entities[i]))
                    EntityManager.SetComponentEnabled<PvPSpawnProtection>(entities[i], false);
                if (EntityManager.HasComponent<PvPRespawnTimer>(entities[i]))
                    EntityManager.SetComponentEnabled<PvPRespawnTimer>(entities[i], false);
            }
            entities.Dispose();

            // Destroy match state singleton
            EntityManager.DestroyEntity(stateEntity);
        }
    }
}
