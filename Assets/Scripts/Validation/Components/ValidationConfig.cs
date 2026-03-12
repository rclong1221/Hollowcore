using Unity.Entities;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Singleton holding all validation configuration.
    /// Created by ValidationBootstrapSystem from ScriptableObjects.
    /// ~64 bytes.
    /// </summary>
    public struct ValidationConfig : IComponentData
    {
        // Rate Limiting defaults
        public float DefaultTokensPerSecond;
        public float DefaultMaxBurst;

        // Movement
        public float MaxSpeedStanding;
        public float MaxSpeedSprinting;
        public float MaxSpeedCrouching;
        public float MaxSpeedFalling;
        public float SpeedToleranceMultiplier;
        public float TeleportThreshold;
        public float ErrorDecayRate;
        public float MaxAccumulatedError;
        public uint TeleportGraceTicks;

        // Violations
        public float ViolationDecayRate;
        public float WarnThreshold;
        public float KickThreshold;
        public float TempBanThreshold;

        // Weights
        public float RateLimitWeight;
        public float MovementWeight;
        public float EconomyWeight;
        public float CooldownWeight;

        // Penalty
        public int TempBanDurationMinutes;
        public int ConsecutiveKicksForBan;
        public float WarnCooldownSeconds;
    }
}
