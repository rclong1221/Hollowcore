using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Player.Components;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Capture point zone logic for CapturePoint game mode.
    /// Counts players per team within zone radius, updates capture progress
    /// and controlling team. Awards score over time for controlled zones.
    /// Uses per-zone Radius from PvPCaptureZone component and accumulates
    /// fractional score to avoid rounding loss at low PointsPerSecond.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PvPSpawnSystem))]
    public partial class PvPObjectiveSystem : SystemBase
    {
        private EntityQuery _captureZoneQuery;
        private EntityQuery _playerQuery;
        private EntityQuery _matchStateQuery;

        // Per-zone fractional score accumulators (index by zone iteration order).
        // Avoids (int)(PointsPerSecond * dt) always rounding to 0 when < 1.
        private NativeList<float> _scoreAccumulators;

        protected override void OnCreate()
        {
            _captureZoneQuery = GetEntityQuery(
                ComponentType.ReadWrite<PvPCaptureZone>(),
                ComponentType.ReadOnly<LocalTransform>());

            _playerQuery = GetEntityQuery(
                ComponentType.ReadOnly<PvPTeam>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<PlayerTag>());

            _matchStateQuery = GetEntityQuery(ComponentType.ReadWrite<PvPMatchState>());
            RequireForUpdate<PvPMatchState>();

            _scoreAccumulators = new NativeList<float>(8, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (_scoreAccumulators.IsCreated)
                _scoreAccumulators.Dispose();
        }

        protected override void OnUpdate()
        {
            var matchState = SystemAPI.GetSingleton<PvPMatchState>();
            if (matchState.GameMode != PvPGameMode.CapturePoint)
                return;
            if (matchState.Phase != PvPMatchPhase.Active && matchState.Phase != PvPMatchPhase.Overtime)
                return;

            float dt = SystemAPI.Time.DeltaTime;
            float captureSpeed = 0.2f; // 5 seconds to capture at base rate

            var zoneEntities = _captureZoneQuery.ToEntityArray(Allocator.Temp);
            var zones = _captureZoneQuery.ToComponentDataArray<PvPCaptureZone>(Allocator.Temp);
            var zoneTransforms = _captureZoneQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            var playerTeams = _playerQuery.ToComponentDataArray<PvPTeam>(Allocator.Temp);
            var playerTransforms = _playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            // Ensure score accumulators match zone count
            while (_scoreAccumulators.Length < zones.Length)
                _scoreAccumulators.Add(0f);

            bool scoreChanged = false;

            for (int z = 0; z < zones.Length; z++)
            {
                var zone = zones[z];
                float3 zonePos = zoneTransforms[z].Position;
                float radiusSq = zone.Radius * zone.Radius;

                // Count players per team in zone (supports teams 1-4)
                int team1Count = 0, team2Count = 0, team3Count = 0, team4Count = 0;
                for (int p = 0; p < playerTeams.Length; p++)
                {
                    float distSq = math.distancesq(playerTransforms[p].Position, zonePos);
                    if (distSq > radiusSq) continue;

                    byte tid = playerTeams[p].TeamId;
                    if (tid == 1) team1Count++;
                    else if (tid == 2) team2Count++;
                    else if (tid == 3) team3Count++;
                    else if (tid == 4) team4Count++;
                }

                int totalInZone = team1Count + team2Count + team3Count + team4Count;
                zone.PlayersInZone = (byte)math.min(totalInZone, 255);

                // Determine dominant team (only one team present = capturing)
                int teamsPresent = (team1Count > 0 ? 1 : 0) + (team2Count > 0 ? 1 : 0) +
                                   (team3Count > 0 ? 1 : 0) + (team4Count > 0 ? 1 : 0);

                if (teamsPresent == 1)
                {
                    // Single team capturing
                    byte capturingTeam = team1Count > 0 ? (byte)1 :
                                         team2Count > 0 ? (byte)2 :
                                         team3Count > 0 ? (byte)3 : (byte)4;
                    int capturingCount = math.max(math.max(team1Count, team2Count),
                                                  math.max(team3Count, team4Count));

                    zone.ContestingTeam = capturingTeam;
                    if (zone.ControllingTeam != capturingTeam)
                    {
                        zone.CaptureProgress += captureSpeed * dt * capturingCount;
                        if (zone.CaptureProgress >= 1.0f)
                        {
                            zone.ControllingTeam = capturingTeam;
                            zone.CaptureProgress = 1.0f;
                        }
                    }
                }
                else if (teamsPresent > 1)
                {
                    // Contested — no capture progress change
                    zone.ContestingTeam = 0;
                }
                else
                {
                    // Empty zone — slowly decay capture progress if unclaimed
                    zone.ContestingTeam = 0;
                    if (zone.CaptureProgress > 0f && zone.ControllingTeam == 0)
                        zone.CaptureProgress = math.max(0f, zone.CaptureProgress - captureSpeed * dt * 0.5f);
                }

                // Award score for controlled zones using fractional accumulator
                if (zone.ControllingTeam > 0 && zone.PointsPerSecond > 0f)
                {
                    _scoreAccumulators[z] += zone.PointsPerSecond * dt;
                    int wholePoints = (int)_scoreAccumulators[z];
                    if (wholePoints > 0)
                    {
                        _scoreAccumulators[z] -= wholePoints;
                        matchState.AddTeamScore(zone.ControllingTeam - 1, wholePoints);
                        scoreChanged = true;
                    }
                }

                EntityManager.SetComponentData(zoneEntities[z], zone);
            }

            if (scoreChanged)
            {
                var stateEntity = _matchStateQuery.GetSingletonEntity();
                EntityManager.SetComponentData(stateEntity, matchState);
            }

            zoneEntities.Dispose();
            zones.Dispose();
            zoneTransforms.Dispose();
            playerTeams.Dispose();
            playerTransforms.Dispose();
        }
    }
}
