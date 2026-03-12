using Unity.Entities;
using Unity.Mathematics;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: ECS singleton holding BlobAsset reference to PvP configuration.
    /// Created by PvPBootstrapSystem on startup.
    /// </summary>
    public struct PvPConfigSingleton : IComponentData
    {
        public BlobAssetReference<PvPConfigBlob> Config;
    }

    /// <summary>
    /// EPIC 17.10: BlobAsset containing all PvP arena, scoring, normalization,
    /// anti-grief, ranking, and XP configuration. Built from ScriptableObjects.
    /// </summary>
    public struct PvPConfigBlob
    {
        // Match Timers
        public float WarmupDuration;
        public float ResultsDuration;
        public float OvertimeDuration;
        public float RespawnDelay;
        public float SpawnProtectionDuration;

        // Scoring
        public int FreeForAllKillLimit;
        public int TeamDeathmatchKillLimit;
        public int CapturePointScoreLimit;
        public int DuelRounds;

        // Normalization
        public byte NormalizationEnabled;
        public float NormalizedMaxHealth;
        public float NormalizedAttackPower;
        public float NormalizedSpellPower;
        public float NormalizedDefense;
        public float NormalizedArmor;

        // Anti-grief
        public float AFKTimeoutSeconds;
        public int AFKWarningsBeforeKick;
        public float LeaverPenaltyCooldown;
        public int SpawnCampingRadius;
        public float SpawnCampingWindow;

        // Ranking
        public int EloStarting;
        public int EloKFactor;
        public int EloKFactorHighRating;
        public int EloHighRatingThreshold;
        public int PlacementMatchCount;
        public float PlacementKMultiplier;
        public BlobArray<int> TierThresholds;

        // XP
        public float PvPKillXPMultiplier;
        public float PvPWinBonusXP;
        public float PvPLossBonusXP;

        // Map Data
        public BlobArray<PvPMapBlob> Maps;

        public int GetMaxScoreForMode(PvPGameMode mode)
        {
            switch (mode)
            {
                case PvPGameMode.FreeForAll: return FreeForAllKillLimit;
                case PvPGameMode.TeamDeathmatch: return TeamDeathmatchKillLimit;
                case PvPGameMode.CapturePoint: return CapturePointScoreLimit;
                case PvPGameMode.Duel: return DuelRounds;
                default: return FreeForAllKillLimit;
            }
        }
    }

    public struct PvPMapBlob
    {
        public byte MapId;
        public BlobString MapName;
        public byte MaxPlayers;
        public byte TeamCount;
        public BlobArray<PvPSpawnPointBlob> SpawnPoints;
        public BlobArray<PvPCaptureZoneBlob> CaptureZones;
    }

    public struct PvPSpawnPointBlob
    {
        public byte TeamId;
        public byte SpawnIndex;
        public float3 Position;
        public quaternion Rotation;
    }

    public struct PvPCaptureZoneBlob
    {
        public byte ZoneId;
        public float3 Position;
        public float Radius;
        public float PointsPerSecond;
    }
}
