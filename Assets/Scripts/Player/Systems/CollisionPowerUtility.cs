using Unity.Entities;
using DIG.Player.Components;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Utility methods for collision power calculations (Epic 7.3.5).
    /// 
    /// These methods calculate "collision power" which determines who "wins" a collision:
    /// - Higher power = less stagger, less knockback
    /// - Lower power = more stagger, more knockback
    /// 
    /// Power formula: power = effectiveMass * horizontalSpeed * stanceMultiplier * movementMultiplier
    /// 
    /// Note: These are pure static functions that will be inlined by Burst when called from Burst jobs.
    /// We don't use [BurstCompile] on individual methods to avoid BC1063 errors with bool fields in structs.
    /// </summary>
    public static class CollisionPowerUtility
    {
        /// <summary>
        /// Gets the stance multiplier for collision power calculation.
        /// Lower center of mass = more stable = higher multiplier.
        /// 
        /// Standing: 1.0 (normal)
        /// Crouching: 1.3 (lower CoM, more stable)
        /// Prone: 0.5 (can be stepped over, less resistance)
        /// </summary>
        /// <param name="stance">Player's current stance.</param>
        /// <param name="settings">Collision settings with multiplier values.</param>
        /// <returns>Stance multiplier for power calculation.</returns>
        public static float GetStanceMultiplier(PlayerStance stance, in PlayerCollisionSettings settings)
        {
            return stance switch
            {
                PlayerStance.Crouching => settings.StanceMultiplierCrouching,
                PlayerStance.Prone => settings.StanceMultiplierProne,
                _ => settings.StanceMultiplierStanding
            };
        }
        
        /// <summary>
        /// Gets the movement state multiplier for collision power calculation.
        /// Faster movement = more momentum = higher multiplier.
        /// 
        /// Tackling: 2.0 (high momentum, committed action) - Epic 7.4.2
        /// Sprinting: 1.5 (full momentum)
        /// Running: 1.0 (normal)
        /// Walking: 0.8 (reduced momentum)
        /// Idle: 0.6 (minimal momentum, easily knocked)
        /// </summary>
        /// <param name="movementState">Player's current movement state.</param>
        /// <param name="settings">Collision settings with multiplier values.</param>
        /// <returns>Movement multiplier for power calculation.</returns>
        public static float GetMovementMultiplier(PlayerMovementState movementState, in PlayerCollisionSettings settings)
        {
            return movementState switch
            {
                PlayerMovementState.Tackling => 2.0f, // Epic 7.4.2: Tackling has highest power
                PlayerMovementState.Sprinting => settings.MovementMultiplierSprinting,
                PlayerMovementState.Running => settings.MovementMultiplierRunning,
                PlayerMovementState.Walking => settings.MovementMultiplierWalking,
                _ => settings.MovementMultiplierIdle
            };
        }
        
        /// <summary>
        /// Calculates the total collision power for a player.
        /// 
        /// Formula: power = effectiveMass * horizontalSpeed * stanceMultiplier * movementMultiplier
        /// 
        /// This determines "who wins" in a collision:
        /// - Sprinting player vs idle player → sprinter has much higher power
        /// - Crouching player vs standing player → crouching player has 30% power boost
        /// </summary>
        /// <param name="horizontalSpeed">Player's horizontal speed in m/s.</param>
        /// <param name="stance">Player's current stance.</param>
        /// <param name="movementState">Player's current movement state.</param>
        /// <param name="settings">Collision settings with mass and multipliers.</param>
        /// <returns>Collision power value (higher = stronger in collision).</returns>
        public static float CalculatePower(
            float horizontalSpeed,
            PlayerStance stance,
            PlayerMovementState movementState,
            in PlayerCollisionSettings settings)
        {
            float stanceMult = GetStanceMultiplier(stance, settings);
            float movementMult = GetMovementMultiplier(movementState, settings);
            
            return settings.EffectiveMass * horizontalSpeed * stanceMult * movementMult;
        }
        
        /// <summary>
        /// Calculates collision power using PlayerState component.
        /// Convenience overload that extracts stance/movement from PlayerState.
        /// </summary>
        public static float CalculatePower(
            float horizontalSpeed,
            in PlayerState playerState,
            in PlayerCollisionSettings settings)
        {
            return CalculatePower(horizontalSpeed, playerState.Stance, playerState.MovementState, settings);
        }
        
        /// <summary>
        /// Calculates power ratio between two players.
        /// 
        /// Returns value between 0 and 1:
        /// - 0.5 = equal power (symmetric outcome)
        /// - > 0.5 = player A is stronger (A staggers less, B staggers more)
        /// - &lt; 0.5 = player A is weaker (A staggers more, B staggers less)
        /// </summary>
        /// <param name="powerA">Power of player A.</param>
        /// <param name="powerB">Power of player B.</param>
        /// <returns>Power ratio for player A (1 - this gives player B's ratio).</returns>
        public static float CalculatePowerRatio(float powerA, float powerB)
        {
            float totalPower = powerA + powerB;
            return totalPower > 0.001f ? powerA / totalPower : 0.5f;
        }
        
        /// <summary>
        /// Calculates stagger duration multiplier based on power ratio.
        /// 
        /// Winner (higher power): duration *= (1 - powerAdvantage)
        /// Loser (lower power): duration *= (1 + |powerAdvantage|)
        /// 
        /// Extreme advantage (ratio > 0.7): winner gets NO stagger (returns 0)
        /// </summary>
        /// <param name="powerRatio">This player's power ratio (0-1).</param>
        /// <returns>Duration multiplier to apply to base stagger duration.</returns>
        public static float CalculateStaggerDurationMultiplier(float powerRatio)
        {
            float powerAdvantage = powerRatio - 0.5f;
            
            // Extreme advantage: no stagger for winner
            if (powerAdvantage >= 0.2f)
                return 0f;
            
            // Scale duration inversely with power
            // Loser gets longer duration, winner gets shorter
            return 1f - powerAdvantage;
        }
        
        /// <summary>
        /// Calculates knockback velocity multiplier based on power ratio.
        /// 
        /// Winner knocked back less: multiplier = (1 - powerRatio)
        /// Loser knocked back more: multiplier = powerRatio (which is < 0.5, so they get more)
        /// 
        /// Note: The "loser" has a LOW power ratio, so (1 - ratio) gives HIGHER knockback.
        /// </summary>
        /// <param name="powerRatio">This player's power ratio (0-1).</param>
        /// <returns>Knockback multiplier (lower power ratio = higher knockback).</returns>
        public static float CalculateKnockbackMultiplier(float powerRatio)
        {
            // Loser (low ratio) gets high knockback: 1 - 0.3 = 0.7
            // Winner (high ratio) gets low knockback: 1 - 0.7 = 0.3
            return 1f - powerRatio;
        }
        
        /// <summary>
        /// Determines if power imbalance should trigger knockdown instead of stagger.
        /// </summary>
        /// <param name="powerRatio">This player's power ratio (0-1).</param>
        /// <param name="knockdownThreshold">Threshold from settings (typically 0.8).</param>
        /// <returns>True if this player should be knocked down.</returns>
        public static bool ShouldTriggerKnockdown(float powerRatio, float knockdownThreshold)
        {
            // If our ratio is below (1 - threshold), we're extremely outpowered
            // e.g., threshold = 0.8 means if ratio < 0.2, we get knocked down
            return powerRatio < (1f - knockdownThreshold);
        }
        
        /// <summary>
        /// Determines if this player should stagger based on power ratio.
        /// Winners with extreme advantage (ratio >= 0.7) don't stagger.
        /// </summary>
        /// <param name="powerRatio">This player's power ratio (0-1).</param>
        /// <returns>True if this player should stagger.</returns>
        public static bool ShouldTriggerStagger(float powerRatio)
        {
            float powerAdvantage = powerRatio - 0.5f;
            // Stagger if not at extreme advantage
            return powerAdvantage < 0.2f;
        }
    }
}
