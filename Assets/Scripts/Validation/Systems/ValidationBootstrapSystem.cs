using Unity.Entities;
using UnityEngine;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: One-time initialization system.
    /// Loads SOs from Resources, creates ValidationConfig singleton, initializes BanListManager.
    /// Self-disables after first run.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ValidationBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;
            _initialized = true;
            Enabled = false;

            // Load SOs from Resources
            var profile = Resources.Load<ValidationProfileSO>("ValidationProfile");
            var penalty = Resources.Load<PenaltyConfigSO>("PenaltyConfig");
            var movement = Resources.Load<MovementLimitsSO>("MovementLimits");

            if (profile == null)
                Debug.LogWarning("[ValidationBootstrap] ValidationProfile SO not found in Resources/. Using defaults.");
            if (penalty == null)
                Debug.LogWarning("[ValidationBootstrap] PenaltyConfig SO not found in Resources/. Using defaults.");
            if (movement == null)
                Debug.LogWarning("[ValidationBootstrap] MovementLimits SO not found in Resources/. Using defaults.");

            // Create ValidationConfig singleton
            var configEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(configEntity, new ValidationConfig
            {
                // Rate limiting
                DefaultTokensPerSecond = profile != null ? profile.DefaultTokensPerSecond : 2f,
                DefaultMaxBurst = profile != null ? profile.DefaultMaxBurst : 5f,

                // Movement
                MaxSpeedStanding = movement != null ? movement.MaxSpeedStanding : 5f,
                MaxSpeedSprinting = movement != null ? movement.MaxSpeedSprinting : 10f,
                MaxSpeedCrouching = movement != null ? movement.MaxSpeedCrouching : 2.5f,
                MaxSpeedFalling = movement != null ? movement.MaxSpeedFalling : 50f,
                SpeedToleranceMultiplier = movement != null ? movement.SpeedToleranceMultiplier : 1.3f,
                TeleportThreshold = movement != null ? movement.TeleportThreshold : 20f,
                ErrorDecayRate = movement != null ? movement.ErrorDecayRate : 2f,
                MaxAccumulatedError = movement != null ? movement.MaxAccumulatedError : 10f,
                TeleportGraceTicks = movement != null ? movement.TeleportGraceTicks : 10u,

                // Violations
                ViolationDecayRate = penalty != null ? penalty.ViolationDecayRate : 0.5f,
                WarnThreshold = penalty != null ? penalty.WarnThreshold : 5f,
                KickThreshold = penalty != null ? penalty.KickThreshold : 20f,
                TempBanThreshold = penalty != null ? penalty.KickThreshold * 2f : 40f,

                // Weights
                RateLimitWeight = penalty != null ? penalty.RateLimitWeight : 1f,
                MovementWeight = penalty != null ? penalty.MovementWeight : 2f,
                EconomyWeight = penalty != null ? penalty.EconomyWeight : 3f,
                CooldownWeight = penalty != null ? penalty.CooldownWeight : 1.5f,

                // Penalty
                TempBanDurationMinutes = penalty != null ? penalty.TempBanDurationMinutes : 30,
                ConsecutiveKicksForBan = penalty != null ? penalty.ConsecutiveKicksForTempBan : 3,
                WarnCooldownSeconds = penalty != null ? penalty.WarnCooldownSeconds : 10f,
            });

            // Initialize ban list
            BanListManager.Initialize();
        }
    }
}
