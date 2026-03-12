using DIG.Roguelite;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Credit-based spawn director. Reads SpawnDirectorConfigSO + ZoneState, accrues credits
    /// per second, selects enemies from EncounterPoolSO by weight, and creates SpawnRequest
    /// transient entities. Supports all styles: burst (corridor), continuous (open-world),
    /// accelerating (arena), and idle (boss/rest).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ZoneTransitionSystem))]
    public partial class SpawnDirectorSystem : SystemBase
    {
        private EntityQuery _spawnRequestQuery;

        // Cached system references — avoid GetExistingSystemManaged per frame
        private ZoneSequenceResolverSystem _sequencer;
        private ZoneTransitionSystem _transitionSystem;
        private bool _systemsCached;

        // Pre-filtered eligible entries — reused across spawns within a frame
        private int[] _eligibleIndices;
        private float[] _eligibleWeights;

        protected override void OnCreate()
        {
            RequireForUpdate<RunState>();
            RequireForUpdate<ZoneState>();

            _spawnRequestQuery = GetEntityQuery(ComponentType.ReadOnly<SpawnRequest>());
            _eligibleIndices = new int[64]; // Generous initial size; grown if needed
            _eligibleWeights = new float[64];
        }

        protected override void OnUpdate()
        {
            var runEntity = SystemAPI.GetSingletonEntity<RunState>();
            var run = SystemAPI.GetSingleton<RunState>();

            // Only run during Active or BossEncounter
            if (run.Phase != RunPhase.Active && run.Phase != RunPhase.BossEncounter)
                return;

            var zoneState = SystemAPI.GetSingleton<ZoneState>();
            if (zoneState.IsCleared) return;

            // Cache system references once
            if (!_systemsCached)
            {
                _sequencer = World.GetExistingSystemManaged<ZoneSequenceResolverSystem>();
                _transitionSystem = World.GetExistingSystemManaged<ZoneTransitionSystem>();
                _systemsCached = true;
            }

            // Get zone definition from sequencer
            var zoneDef = _sequencer?.GetZoneAtIndex(zoneState.ZoneIndex);
            if (zoneDef == null) return;

            var directorConfig = zoneDef.SpawnDirectorConfig;
            var encounterPool = zoneDef.EncounterPool;

            // No director config or no encounter pool = no spawning
            if (directorConfig == null || encounterPool == null) return;
            if (encounterPool.Entries == null || encounterPool.Entries.Count == 0) return;

            float dt = SystemAPI.Time.DeltaTime;

            // Tick zone time
            zoneState.TimeInZone += dt;

            // Accrue credits
            if (directorConfig.CreditsPerSecond > 0f)
            {
                float rate = directorConfig.CreditsPerSecond
                    * (1f + zoneState.TimeInZone * directorConfig.Acceleration);

                if (directorConfig.DifficultyAffectsRate)
                    rate *= (1f + zoneState.EffectiveDifficulty * directorConfig.DifficultyRateMultiplier);

                zoneState.SpawnBudget += rate * dt;

                if (directorConfig.MaxBudget > 0f)
                    zoneState.SpawnBudget = math.min(zoneState.SpawnBudget, directorConfig.MaxBudget);
            }

            // Tick spawn timer
            zoneState.SpawnTimer -= dt;

            if (zoneState.SpawnTimer <= 0f)
            {
                // Spawn loop: attempt multiple spawns per frame while budget allows
                int maxSpawnsPerFrame = 10;
                int spawned = 0;
                var rng = new Random(RunSeedUtility.DeriveSpawnSeed(run.ZoneSeed, zoneState.EnemiesSpawned) | 1);

                // Get spawn position provider once per frame
                var posProvider = _transitionSystem?.SpawnPositionProvider;
                var activation = _transitionSystem?.LastActivation ?? default;

                // Pre-filter eligible pool entries once per frame (difficulty-gated only;
                // budget check happens per-spawn since budget decreases)
                int entryCount = encounterPool.Entries.Count;
                EnsureFilterArraySize(entryCount);

                int eligibleCount = 0;
                float cheapest = float.MaxValue;
                for (int i = 0; i < entryCount; i++)
                {
                    var entry = encounterPool.Entries[i];
                    if (!IsEntryAvailable(entry, zoneState)) continue;
                    _eligibleIndices[eligibleCount] = i;
                    _eligibleWeights[eligibleCount] = entry.Weight;
                    eligibleCount++;
                    if (entry.SpawnCost < cheapest) cheapest = entry.SpawnCost;
                }

                // Need CompleteDependency before EntityManager structural changes
                CompleteDependency();

                while (zoneState.SpawnBudget >= cheapest && spawned < maxSpawnsPerFrame)
                {
                    // Check alive cap
                    if (directorConfig.MaxAliveEnemies > 0 && zoneState.EnemiesAlive >= directorConfig.MaxAliveEnemies)
                        break;

                    // Single-pass weighted selection from pre-filtered entries
                    float totalWeight = 0f;
                    for (int i = 0; i < eligibleCount; i++)
                    {
                        int poolIdx = _eligibleIndices[i];
                        if (encounterPool.Entries[poolIdx].SpawnCost > zoneState.SpawnBudget) continue;
                        totalWeight += _eligibleWeights[i];
                    }

                    if (totalWeight <= 0f) break;

                    float roll = rng.NextFloat() * totalWeight;
                    float acc = 0f;
                    int selectedIndex = -1;

                    for (int i = 0; i < eligibleCount; i++)
                    {
                        int poolIdx = _eligibleIndices[i];
                        if (encounterPool.Entries[poolIdx].SpawnCost > zoneState.SpawnBudget) continue;
                        acc += _eligibleWeights[i];
                        if (roll <= acc)
                        {
                            selectedIndex = poolIdx;
                            break;
                        }
                    }

                    if (selectedIndex < 0) break;

                    var selected = encounterPool.Entries[selectedIndex];
                    float cost = selected.SpawnCost;

                    // Elite roll
                    bool isElite = false;
                    if (selected.CanBeElite
                        && zoneState.EffectiveDifficulty >= directorConfig.EliteMinDifficulty
                        && rng.NextFloat() < directorConfig.EliteChance)
                    {
                        isElite = true;
                        cost *= directorConfig.EliteCostMultiplier;
                    }

                    if (cost > zoneState.SpawnBudget) break;

                    // Get spawn position
                    float3 spawnPos = float3.zero;
                    bool hasPosition = false;

                    if (posProvider != null)
                    {
                        hasPosition = posProvider.TryGetSpawnPosition(
                            float3.zero,
                            directorConfig.MinSpawnDistance,
                            directorConfig.MaxSpawnDistance,
                            ref rng,
                            out spawnPos);
                    }
                    else if (activation.SpawnPoints != null && activation.SpawnPoints.Length > 0)
                    {
                        int idx = rng.NextInt(0, activation.SpawnPoints.Length);
                        spawnPos = activation.SpawnPoints[idx];
                        hasPosition = true;
                    }

                    if (!hasPosition) break;

                    // Create SpawnRequest entity
                    var requestEntity = EntityManager.CreateEntity();
                    EntityManager.AddComponentData(requestEntity, new SpawnRequest
                    {
                        Prefab = Entity.Null,
                        Position = spawnPos,
                        Seed = RunSeedUtility.DeriveSpawnSeed(run.ZoneSeed, zoneState.EnemiesSpawned),
                        Difficulty = zoneState.EffectiveDifficulty,
                        IsElite = isElite,
                        PoolEntryIndex = selectedIndex,
                    });

                    zoneState.SpawnBudget -= cost;
                    zoneState.EnemiesSpawned++;
                    zoneState.EnemiesAlive++;
                    spawned++;

                    zoneState.SpawnTimer = directorConfig.MinSpawnInterval;
                }
            }

            EntityManager.SetComponentData(runEntity, zoneState);
        }

        private void EnsureFilterArraySize(int needed)
        {
            if (_eligibleIndices.Length < needed)
            {
                _eligibleIndices = new int[needed];
                _eligibleWeights = new float[needed];
            }
        }

        private static bool IsEntryAvailable(EncounterPoolEntry entry, ZoneState zoneState)
        {
            if (entry.MinDifficulty > 0 && zoneState.EffectiveDifficulty < entry.MinDifficulty)
                return false;
            if (entry.MaxDifficulty > 0 && zoneState.EffectiveDifficulty > entry.MaxDifficulty)
                return false;
            return true;
        }
    }
}
