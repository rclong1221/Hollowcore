using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Player.Components;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Reads KillCredited/AssistCredited to update PvP K/D/A stats.
    /// Only counts kills where both killer and victim have PlayerTag (PvP kill).
    /// Uses manual EntityQuery (NOT SystemAPI.Query) for transient entities.
    /// </summary>
    /// KillCredited/AssistCredited are created via EndSimulationECB by DeathTransitionSystem,
    /// so they appear next frame. No explicit UpdateAfter needed (same pattern as XPAwardSystem).
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PvPScoringSystem : SystemBase
    {
        private EntityQuery _killQuery;
        private EntityQuery _assistQuery;
        private EntityQuery _matchStateQuery;

        protected override void OnCreate()
        {
            _killQuery = GetEntityQuery(
                ComponentType.ReadOnly<KillCredited>(),
                ComponentType.ReadWrite<PvPPlayerStats>(),
                ComponentType.ReadOnly<PvPTeam>(),
                ComponentType.ReadOnly<PlayerTag>());

            _assistQuery = GetEntityQuery(
                ComponentType.ReadOnly<AssistCredited>(),
                ComponentType.ReadWrite<PvPPlayerStats>(),
                ComponentType.ReadOnly<PlayerTag>());

            _matchStateQuery = GetEntityQuery(ComponentType.ReadWrite<PvPMatchState>());
            RequireForUpdate<PvPConfigSingleton>();
            RequireForUpdate<PvPMatchState>();
        }

        protected override void OnUpdate()
        {
            var matchState = SystemAPI.GetSingleton<PvPMatchState>();
            if (matchState.Phase != PvPMatchPhase.Active && matchState.Phase != PvPMatchPhase.Overtime)
                return;

            ref var config = ref SystemAPI.GetSingleton<PvPConfigSingleton>().Config.Value;
            var playerTagLookup = GetComponentLookup<PlayerTag>(true);
            var pvpStatsLookup = GetComponentLookup<PvPPlayerStats>(false);
            var pvpTeamLookup = GetComponentLookup<PvPTeam>(true);
            var respawnTimerLookup = GetComponentLookup<PvPRespawnTimer>(false);
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = networkTime.ServerTick.TickIndexForValidTick;
            int tickRate = SystemAPI.HasSingleton<ClientServerTickRate>()
                ? SystemAPI.GetSingleton<ClientServerTickRate>().SimulationTickRate
                : 30;
            uint respawnTicks = (uint)(config.RespawnDelay * tickRate);

            bool scoreChanged = false;

            // Process kills
            {
                var entities = _killQuery.ToEntityArray(Allocator.Temp);
                var kills = _killQuery.ToComponentDataArray<KillCredited>(Allocator.Temp);
                var stats = _killQuery.ToComponentDataArray<PvPPlayerStats>(Allocator.Temp);
                var teams = _killQuery.ToComponentDataArray<PvPTeam>(Allocator.Temp);

                for (int i = 0; i < entities.Length; i++)
                {
                    var kill = kills[i];

                    // Only count PvP kills (victim must have PlayerTag)
                    if (kill.Victim == Entity.Null || !playerTagLookup.HasComponent(kill.Victim))
                        continue;

                    // Update killer stats
                    var killerStats = stats[i];
                    killerStats.Kills++;
                    EntityManager.SetComponentData(entities[i], killerStats);

                    // Update victim stats
                    if (pvpStatsLookup.HasComponent(kill.Victim))
                    {
                        var victimStats = pvpStatsLookup[kill.Victim];
                        victimStats.Deaths++;
                        pvpStatsLookup[kill.Victim] = victimStats;

                        // Enable respawn timer on victim
                        if (respawnTimerLookup.HasComponent(kill.Victim))
                        {
                            respawnTimerLookup[kill.Victim] = new PvPRespawnTimer
                            {
                                RespawnAtTick = currentTick + respawnTicks
                            };
                            EntityManager.SetComponentEnabled<PvPRespawnTimer>(kill.Victim, true);
                        }
                    }

                    // Update team scores
                    byte killerTeam = teams[i].TeamId;
                    if (matchState.GameMode == PvPGameMode.FreeForAll)
                    {
                        matchState.AddTeamScore(killerTeam - 1, 1);
                    }
                    else
                    {
                        matchState.AddTeamScore(killerTeam - 1, 1);
                    }
                    scoreChanged = true;

                    // Enqueue kill feed event
                    PvPKillFeedQueue.Enqueue(new PvPKillFeedEntry
                    {
                        KillerEntity = entities[i],
                        VictimEntity = kill.Victim,
                        KillerTeam = killerTeam,
                        VictimTeam = pvpTeamLookup.HasComponent(kill.Victim) ? pvpTeamLookup[kill.Victim].TeamId : (byte)0
                    });
                }

                entities.Dispose();
                kills.Dispose();
                stats.Dispose();
                teams.Dispose();
            }

            // Process assists
            {
                var entities = _assistQuery.ToEntityArray(Allocator.Temp);
                var assists = _assistQuery.ToComponentDataArray<AssistCredited>(Allocator.Temp);
                var stats = _assistQuery.ToComponentDataArray<PvPPlayerStats>(Allocator.Temp);

                for (int i = 0; i < entities.Length; i++)
                {
                    // Only count PvP assists
                    if (assists[i].Victim == Entity.Null || !playerTagLookup.HasComponent(assists[i].Victim))
                        continue;

                    var assisterStats = stats[i];
                    assisterStats.Assists++;
                    EntityManager.SetComponentData(entities[i], assisterStats);
                }

                entities.Dispose();
                assists.Dispose();
                stats.Dispose();
            }

            if (scoreChanged)
            {
                var stateEntity = _matchStateQuery.GetSingletonEntity();
                EntityManager.SetComponentData(stateEntity, matchState);
            }
        }
    }
}
