using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Loads PvP ScriptableObjects from Resources/, builds BlobAssets,
    /// creates PvPConfigSingleton. Runs once on startup then self-disables.
    /// Follows SurfaceGameplayConfigSystem / ProgressionBootstrapSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation |
                        WorldSystemFilterFlags.ClientSimulation |
                        WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class PvPBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;
            _initialized = true;
            Enabled = false;

            var arenaConfig = Resources.Load<PvPArenaConfigSO>("PvPArenaConfig");
            var rankingConfig = Resources.Load<PvPRankingConfigSO>("PvPRankingConfig");
            var maps = Resources.LoadAll<PvPMapDefinitionSO>("PvPMaps");

            if (arenaConfig == null)
            {
                Debug.LogWarning("[PvPBootstrapSystem] PvPArenaConfig not found in Resources/. PvP system disabled.");
                return;
            }
            if (rankingConfig == null)
            {
                Debug.LogWarning("[PvPBootstrapSystem] PvPRankingConfig not found in Resources/. PvP system disabled.");
                return;
            }

            var blobRef = BuildConfigBlob(arenaConfig, rankingConfig, maps);
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new PvPConfigSingleton { Config = blobRef });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[PvPBootstrapSystem] Initialized with {maps.Length} maps, Elo K={rankingConfig.KFactor}");
#endif
        }

        private static BlobAssetReference<PvPConfigBlob> BuildConfigBlob(
            PvPArenaConfigSO arena, PvPRankingConfigSO ranking, PvPMapDefinitionSO[] maps)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<PvPConfigBlob>();

            // Match Timers
            root.WarmupDuration = arena.WarmupDuration;
            root.ResultsDuration = arena.ResultsDuration;
            root.OvertimeDuration = arena.OvertimeDuration;
            root.RespawnDelay = arena.RespawnDelay;
            root.SpawnProtectionDuration = arena.SpawnProtectionDuration;

            // Scoring
            root.FreeForAllKillLimit = arena.FreeForAllKillLimit;
            root.TeamDeathmatchKillLimit = arena.TeamDeathmatchKillLimit;
            root.CapturePointScoreLimit = arena.CapturePointScoreLimit;
            root.DuelRounds = arena.DuelRounds;

            // Normalization
            root.NormalizationEnabled = arena.NormalizationEnabled ? (byte)1 : (byte)0;
            root.NormalizedMaxHealth = arena.NormalizedMaxHealth;
            root.NormalizedAttackPower = arena.NormalizedAttackPower;
            root.NormalizedSpellPower = arena.NormalizedSpellPower;
            root.NormalizedDefense = arena.NormalizedDefense;
            root.NormalizedArmor = arena.NormalizedArmor;

            // Anti-grief
            root.AFKTimeoutSeconds = arena.AFKTimeoutSeconds;
            root.AFKWarningsBeforeKick = arena.AFKWarningsBeforeKick;
            root.LeaverPenaltyCooldown = arena.LeaverPenaltyCooldown;
            root.SpawnCampingRadius = arena.SpawnCampingRadius;
            root.SpawnCampingWindow = arena.SpawnCampingWindow;

            // Ranking
            root.EloStarting = ranking.StartingElo;
            root.EloKFactor = ranking.KFactor;
            root.EloKFactorHighRating = ranking.KFactorHighRating;
            root.EloHighRatingThreshold = ranking.HighRatingThreshold;
            root.PlacementMatchCount = ranking.PlacementMatchCount;
            root.PlacementKMultiplier = ranking.PlacementKMultiplier;

            // Tier thresholds
            var tiers = builder.Allocate(ref root.TierThresholds, ranking.TierThresholds.Length);
            for (int i = 0; i < ranking.TierThresholds.Length; i++)
                tiers[i] = ranking.TierThresholds[i];

            // XP
            root.PvPKillXPMultiplier = arena.PvPKillXPMultiplier;
            root.PvPWinBonusXP = arena.PvPWinBonusXP;
            root.PvPLossBonusXP = arena.PvPLossBonusXP;

            // Maps
            if (maps != null && maps.Length > 0)
            {
                var mapArray = builder.Allocate(ref root.Maps, maps.Length);
                for (int m = 0; m < maps.Length; m++)
                {
                    var mapSO = maps[m];
                    mapArray[m].MapId = mapSO.MapId;
                    builder.AllocateString(ref mapArray[m].MapName, mapSO.MapName ?? "");
                    mapArray[m].MaxPlayers = mapSO.MaxPlayers;
                    mapArray[m].TeamCount = mapSO.TeamCount;

                    // Spawn points
                    int spCount = mapSO.SpawnPoints != null ? mapSO.SpawnPoints.Length : 0;
                    var spArray = builder.Allocate(ref mapArray[m].SpawnPoints, spCount);
                    for (int s = 0; s < spCount; s++)
                    {
                        var sp = mapSO.SpawnPoints[s];
                        spArray[s].TeamId = sp.TeamId;
                        spArray[s].SpawnIndex = sp.SpawnIndex;
                        spArray[s].Position = new float3(sp.Position.x, sp.Position.y, sp.Position.z);
                        spArray[s].Rotation = new quaternion(sp.Rotation.x, sp.Rotation.y, sp.Rotation.z, sp.Rotation.w);
                    }

                    // Capture zones
                    int czCount = mapSO.CaptureZones != null ? mapSO.CaptureZones.Length : 0;
                    var czArray = builder.Allocate(ref mapArray[m].CaptureZones, czCount);
                    for (int c = 0; c < czCount; c++)
                    {
                        var cz = mapSO.CaptureZones[c];
                        czArray[c].ZoneId = cz.ZoneId;
                        czArray[c].Position = new float3(cz.Position.x, cz.Position.y, cz.Position.z);
                        czArray[c].Radius = cz.Radius;
                        czArray[c].PointsPerSecond = cz.PointsPerSecond;
                    }
                }
            }
            else
            {
                builder.Allocate(ref root.Maps, 0);
            }

            var result = builder.CreateBlobAssetReference<PvPConfigBlob>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }
    }
}
