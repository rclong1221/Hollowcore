#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Unity.Mathematics;
using System.Collections.Generic;
using DIG.Roguelite.Zones;
using Random = Unity.Mathematics.Random;

namespace DIG.Roguelite.Editor
{
    /// <summary>
    /// Dry-run simulator for rogue-lite balance testing.
    /// Given RunConfigSO + seed + ascension level, simulates zone difficulty,
    /// currency flow, and scoring. Pure data simulation — no play mode needed.
    ///
    /// Two modes:
    /// - Simulate(): Full result with zone details for single dry-run display
    /// - SimulateAggregate(): Lightweight numeric-only accumulation for Monte Carlo batches
    /// </summary>
    public static class RunSimulator
    {
        /// <summary>
        /// Full simulation — returns detailed result with zone details.
        /// Use for single dry-run display. NOT for Monte Carlo (allocates per run).
        /// </summary>
        public static RunSimulationResult Simulate(RunConfigSO config, uint seed, byte ascensionLevel = 0)
        {
            if (config == null)
            {
                Debug.LogError("[RunSimulator] RunConfigSO is null.");
                return null;
            }

            int zoneCount = config.ZoneCount;
            var result = new RunSimulationResult
            {
                Seed = seed,
                ZoneCount = zoneCount
            };

            float ascensionMult = GetAscensionMultiplier(ascensionLevel);

            // Resolve zone sequence once for the whole run
            var sequence = config.ZoneSequence;
            var resolvedZones = ResolveZoneSequence(sequence, seed, zoneCount);

            for (int z = 0; z < zoneCount; z++)
            {
                uint zoneSeed = RunSeedUtility.DeriveZoneSeed(seed, (byte)z);
                var rng = new Random(zoneSeed | 1);

                float baseDifficulty = config.GetDifficultyAtZone(z);
                ZoneDefinitionSO zoneDef = z < resolvedZones.Count ? resolvedZones[z] : null;

                float diffMult = zoneDef != null ? zoneDef.DifficultyMultiplier : 1f;
                float effectiveDifficulty = baseDifficulty * ascensionMult * diffMult;

                var zone = new SimulatedZone
                {
                    ZoneIndex = z,
                    ZoneType = zoneDef != null ? (byte)zoneDef.Type : (byte)0,
                    ZoneTypeName = zoneDef != null ? zoneDef.Type.ToString() : "Combat",
                    ZoneDisplayName = zoneDef != null && !string.IsNullOrEmpty(zoneDef.DisplayName) ? zoneDef.DisplayName : $"Zone {z}",
                    ClearModeName = zoneDef != null ? zoneDef.ClearMode.ToString() : "AllEnemiesDead",
                    EffectiveDifficulty = effectiveDifficulty,
                    InteractableBudget = zoneDef != null ? zoneDef.InteractableBudget : 0
                };

                // Simulate spawns from encounter pool if available
                var pool = zoneDef?.EncounterPool;
                var directorConfig = zoneDef?.SpawnDirectorConfig;

                if (pool != null && pool.Entries != null && pool.Entries.Count > 0 && directorConfig != null)
                {
                    float budget = directorConfig.InitialBudget;
                    zone.SpawnBudget = (int)budget;
                    int spawned = 0;

                    while (budget > 0 && spawned < directorConfig.MaxAliveEnemies)
                    {
                        int selected = SelectFromPool(pool, effectiveDifficulty, budget, ref rng);
                        if (selected < 0) break;

                        var entry = pool.Entries[selected];
                        string name = !string.IsNullOrEmpty(entry.DisplayName) ? entry.DisplayName
                            : entry.EnemyPrefab != null ? entry.EnemyPrefab.name : $"Entry {selected}";
                        bool isElite = entry.CanBeElite && effectiveDifficulty >= directorConfig.EliteMinDifficulty
                            && rng.NextFloat() < directorConfig.EliteChance;
                        zone.EnemyNames.Add(isElite ? $"{name} (ELITE)" : name);
                        budget -= entry.SpawnCost * (isElite ? directorConfig.EliteCostMultiplier : 1f);
                        spawned++;
                    }
                    zone.EnemyCount = spawned;
                }
                else
                {
                    // Fallback: estimate from difficulty
                    int spawnCount = math.max(1, (int)(3 + effectiveDifficulty * 2));
                    zone.EnemyCount = spawnCount;
                    for (int s = 0; s < spawnCount; s++)
                    {
                        rng.NextFloat();
                        zone.EnemyNames.Add($"Enemy (seed roll {s})");
                    }
                }

                result.TotalCurrencyEarned += config.RunCurrencyPerZoneClear;
                result.Zones.Add(zone);
            }

            result.FinalScore = CalculateScore(result.TotalCurrencyEarned, zoneCount, ascensionLevel);
            return result;
        }

        /// <summary>
        /// Lightweight simulation for Monte Carlo — no string/list allocations.
        /// Accumulates numeric results directly into the provided accumulator.
        /// </summary>
        public static void SimulateAggregate(RunConfigSO config, uint seed, byte ascensionLevel,
            ref MonteCarloAccumulator acc)
        {
            int zoneCount = config.ZoneCount;
            float ascensionMult = GetAscensionMultiplier(ascensionLevel);
            int totalCurrency = 0;

            // Resolve zone sequence once for the whole run
            var sequence = config.ZoneSequence;
            var resolvedZones = ResolveZoneSequence(sequence, seed, zoneCount);

            for (int z = 0; z < zoneCount; z++)
            {
                uint zoneSeed = RunSeedUtility.DeriveZoneSeed(seed, (byte)z);
                var rng = new Random(zoneSeed | 1);

                float baseDifficulty = config.GetDifficultyAtZone(z);
                ZoneDefinitionSO zoneDef = z < resolvedZones.Count ? resolvedZones[z] : null;
                float diffMult = zoneDef != null ? zoneDef.DifficultyMultiplier : 1f;
                float difficulty = baseDifficulty * ascensionMult * diffMult;

                if (z < acc.DifficultyPerZone.Length)
                    acc.DifficultyPerZone[z] += difficulty;

                // Count encounters from pool or estimate
                var pool = zoneDef?.EncounterPool;
                var directorConfig = zoneDef?.SpawnDirectorConfig;
                int spawnCount;

                if (pool != null && pool.Entries != null && pool.Entries.Count > 0 && directorConfig != null)
                {
                    float budget = directorConfig.InitialBudget;
                    spawnCount = 0;
                    while (budget > 0 && spawnCount < directorConfig.MaxAliveEnemies)
                    {
                        int selected = SelectFromPool(pool, difficulty, budget, ref rng);
                        if (selected < 0) break;
                        budget -= pool.Entries[selected].SpawnCost;
                        spawnCount++;
                    }
                }
                else
                {
                    spawnCount = math.max(1, (int)(3 + difficulty * 2));
                    for (int s = 0; s < spawnCount; s++)
                        rng.NextFloat();
                }

                acc.TotalEnemies += spawnCount;
                totalCurrency += config.RunCurrencyPerZoneClear;
                if (z < acc.CurrencyPerZone.Length)
                    acc.CurrencyPerZone[z] += config.RunCurrencyPerZoneClear;
            }

            int score = CalculateScore(totalCurrency, zoneCount, ascensionLevel);
            acc.TotalScore += score;
            acc.TotalCurrency += totalCurrency;
            acc.TotalZonesCleared += zoneCount;
        }

        // ==================== Helpers ====================

        /// <summary>
        /// Weighted random selection from an encounter pool, filtering by difficulty and budget.
        /// Returns pool entry index, or -1 if nothing is affordable/eligible.
        /// </summary>
        private static int SelectFromPool(EncounterPoolSO pool, float difficulty, float budget, ref Random rng)
        {
            float totalWeight = 0f;
            for (int i = 0; i < pool.Entries.Count; i++)
            {
                var e = pool.Entries[i];
                if (e.MinDifficulty > 0 && difficulty < e.MinDifficulty) continue;
                if (e.MaxDifficulty > 0 && difficulty > e.MaxDifficulty) continue;
                if (e.SpawnCost > budget) continue;
                totalWeight += e.Weight;
            }
            if (totalWeight <= 0f) return -1;

            float roll = rng.NextFloat() * totalWeight;
            float acc = 0f;
            for (int i = 0; i < pool.Entries.Count; i++)
            {
                var e = pool.Entries[i];
                if (e.MinDifficulty > 0 && difficulty < e.MinDifficulty) continue;
                if (e.MaxDifficulty > 0 && difficulty > e.MaxDifficulty) continue;
                if (e.SpawnCost > budget) continue;
                acc += e.Weight;
                if (roll <= acc) return i;
            }
            return -1;
        }

        /// <summary>
        /// Resolves a ZoneSequenceSO into an ordered list of ZoneDefinitionSOs.
        /// Uses Fixed entries directly; WeightedRandom entries use seed-deterministic selection.
        /// Returns empty list if sequence is null.
        /// </summary>
        private static List<ZoneDefinitionSO> ResolveZoneSequence(ZoneSequenceSO sequence, uint seed, int zoneCount)
        {
            var result = new List<ZoneDefinitionSO>(zoneCount);
            if (sequence == null || sequence.Layers == null) return result;

            for (int z = 0; z < zoneCount; z++)
            {
                int layerIndex = z < sequence.Layers.Count
                    ? z
                    : (sequence.EnableLooping && sequence.Layers.Count > 0
                        ? sequence.LoopStartIndex + ((z - sequence.Layers.Count) % (sequence.Layers.Count - sequence.LoopStartIndex))
                        : -1);

                if (layerIndex < 0 || layerIndex >= sequence.Layers.Count)
                {
                    result.Add(null);
                    continue;
                }

                var layer = sequence.Layers[layerIndex];
                if (layer.Entries == null || layer.Entries.Count == 0)
                {
                    result.Add(null);
                    continue;
                }

                if (layer.Mode == ZoneSelectionMode.Fixed || layer.Entries.Count == 1)
                {
                    result.Add(layer.Entries[0].Zone);
                }
                else
                {
                    // Weighted random selection using zone seed
                    uint zoneSeed = RunSeedUtility.DeriveZoneSeed(seed, (byte)z);
                    var rng = new Random(zoneSeed | 1);

                    float totalWeight = 0f;
                    for (int i = 0; i < layer.Entries.Count; i++)
                        totalWeight += layer.Entries[i].Weight;

                    if (totalWeight <= 0f)
                    {
                        result.Add(layer.Entries[0].Zone);
                        continue;
                    }

                    float roll = rng.NextFloat() * totalWeight;
                    float acc = 0f;
                    ZoneDefinitionSO selected = layer.Entries[0].Zone;
                    for (int i = 0; i < layer.Entries.Count; i++)
                    {
                        acc += layer.Entries[i].Weight;
                        if (roll <= acc)
                        {
                            selected = layer.Entries[i].Zone;
                            break;
                        }
                    }
                    result.Add(selected);
                }
            }

            return result;
        }

        private static float GetAscensionMultiplier(byte ascensionLevel)
        {
            // Simple exponential scaling — will be replaced with AscensionDefinitionSO
            // lookup once RunConfigSO gains an AscensionDefinition reference (23.4 integration)
            return 1f + ascensionLevel * 0.25f;
        }

        private static int CalculateScore(int totalCurrency, int zoneCount, byte ascensionLevel)
        {
            int score = totalCurrency * 10;
            score += zoneCount * 100;
            score = (int)(score * (1f + ascensionLevel * 0.25f));
            return score;
        }
    }

    /// <summary>
    /// Lightweight accumulator for Monte Carlo — no managed allocations per run.
    /// Reused across all runs in a batch. Only Dictionary allocates (amortized).
    /// </summary>
    public class MonteCarloAccumulator
    {
        public long TotalScore;
        public long TotalCurrency;
        public float TotalZonesCleared;
        public int TotalEnemies;
        public float[] DifficultyPerZone;
        public float[] CurrencyPerZone;
        public Dictionary<int, int> RewardFrequency;
        public Dictionary<int, int> ModifierFrequency;

        public MonteCarloAccumulator(int maxZones)
        {
            DifficultyPerZone = new float[maxZones];
            CurrencyPerZone = new float[maxZones];
            RewardFrequency = new Dictionary<int, int>();
            ModifierFrequency = new Dictionary<int, int>();
        }
    }
}
#endif
