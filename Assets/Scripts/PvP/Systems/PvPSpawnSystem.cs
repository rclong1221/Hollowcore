using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Player.Components;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Handles respawn after PvP death.
    /// Queries PvPRespawnTimer, teleports to spawn point, enables PvPSpawnProtection.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PvPScoringSystem))]
    public partial class PvPSpawnSystem : SystemBase
    {
        private EntityQuery _respawnQuery;
        private EntityQuery _spawnPointQuery;

        protected override void OnCreate()
        {
            _respawnQuery = GetEntityQuery(
                ComponentType.ReadWrite<PvPRespawnTimer>(),
                ComponentType.ReadOnly<PvPTeam>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<Health>(),
                ComponentType.ReadOnly<PlayerTag>());

            _spawnPointQuery = GetEntityQuery(
                ComponentType.ReadWrite<PvPSpawnPoint>(),
                ComponentType.ReadOnly<LocalTransform>());

            RequireForUpdate<PvPMatchState>();
            RequireForUpdate<PvPConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            var matchState = SystemAPI.GetSingleton<PvPMatchState>();
            if (matchState.Phase != PvPMatchPhase.Active && matchState.Phase != PvPMatchPhase.Overtime)
                return;

            ref var config = ref SystemAPI.GetSingleton<PvPConfigSingleton>().Config.Value;
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = networkTime.ServerTick.TickIndexForValidTick;
            int tickRate = SystemAPI.HasSingleton<ClientServerTickRate>()
                ? SystemAPI.GetSingleton<ClientServerTickRate>().SimulationTickRate
                : 30;
            uint protectionTicks = (uint)(config.SpawnProtectionDuration * tickRate);

            var entities = _respawnQuery.ToEntityArray(Allocator.Temp);
            var timers = _respawnQuery.ToComponentDataArray<PvPRespawnTimer>(Allocator.Temp);
            var teams = _respawnQuery.ToComponentDataArray<PvPTeam>(Allocator.Temp);
            var transforms = _respawnQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var healths = _respawnQuery.ToComponentDataArray<Health>(Allocator.Temp);

            // Cache spawn points
            var spawnEntities = _spawnPointQuery.ToEntityArray(Allocator.Temp);
            var spawnPoints = _spawnPointQuery.ToComponentDataArray<PvPSpawnPoint>(Allocator.Temp);
            var spawnTransforms = _spawnPointQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                if (!EntityManager.IsComponentEnabled<PvPRespawnTimer>(entities[i]))
                    continue;

                if (currentTick < timers[i].RespawnAtTick)
                    continue;

                // Find best spawn point for this team
                byte teamId = teams[i].TeamId;
                int bestSpawn = FindBestSpawnPoint(teamId, spawnPoints, currentTick);

                if (bestSpawn >= 0)
                {
                    // Teleport to spawn
                    var transform = transforms[i];
                    transform.Position = spawnTransforms[bestSpawn].Position;
                    transform.Rotation = spawnTransforms[bestSpawn].Rotation;
                    EntityManager.SetComponentData(entities[i], transform);

                    // Update spawn point usage
                    var sp = spawnPoints[bestSpawn];
                    sp.LastUsedTick = currentTick;
                    EntityManager.SetComponentData(spawnEntities[bestSpawn], sp);
                }

                // Reset health to max
                var health = healths[i];
                health.Current = health.Max;
                EntityManager.SetComponentData(entities[i], health);

                // Enable spawn protection
                if (EntityManager.HasComponent<PvPSpawnProtection>(entities[i]))
                {
                    EntityManager.SetComponentData(entities[i], new PvPSpawnProtection
                    {
                        ExpirationTick = currentTick + protectionTicks
                    });
                    EntityManager.SetComponentEnabled<PvPSpawnProtection>(entities[i], true);
                }

                // Clear DamageEvent buffer
                if (EntityManager.HasComponent<DamageEvent>(entities[i]))
                {
                    var buffer = EntityManager.GetBuffer<DamageEvent>(entities[i]);
                    buffer.Clear();
                }

                // Disable respawn timer
                EntityManager.SetComponentEnabled<PvPRespawnTimer>(entities[i], false);
            }

            entities.Dispose();
            timers.Dispose();
            teams.Dispose();
            transforms.Dispose();
            healths.Dispose();
            spawnEntities.Dispose();
            spawnPoints.Dispose();
            spawnTransforms.Dispose();
        }

        private static int FindBestSpawnPoint(byte teamId, NativeArray<PvPSpawnPoint> spawnPoints, uint currentTick)
        {
            int bestIndex = -1;
            uint oldestTick = uint.MaxValue;

            for (int s = 0; s < spawnPoints.Length; s++)
            {
                var sp = spawnPoints[s];
                if (sp.IsActive == 0) continue;
                if (sp.TeamId != 0 && sp.TeamId != teamId) continue;

                if (sp.LastUsedTick < oldestTick)
                {
                    oldestTick = sp.LastUsedTick;
                    bestIndex = s;
                }
            }

            return bestIndex;
        }
    }
}
