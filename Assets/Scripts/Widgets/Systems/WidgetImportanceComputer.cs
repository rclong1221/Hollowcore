using Unity.Mathematics;

namespace DIG.Widgets.Systems
{
    /// <summary>
    /// EPIC 15.26 Phase 1: Static utility for computing widget importance scores.
    /// Higher importance = more likely to survive budget enforcement.
    ///
    /// Formula:
    ///   Importance = DistanceBonus + TierBonus + CombatBonus + TargetBonus + DamageRecencyBonus
    ///
    /// Where:
    ///   DistanceBonus = max(0, 100 - distance * distanceFalloff)
    ///   TierBonus = Boss:50, Elite:30, Champion:20, MiniBoss:40, Normal:0
    ///   CombatBonus = isInCombat ? 40 : 0
    ///   TargetBonus = isTargeted ? 100 : (isHovered ? 30 : 0)
    ///   DamageRecencyBonus = timeSinceDamage < 2s ? 25 : 0
    /// </summary>
    public static class WidgetImportanceComputer
    {
        // Tier bonus values indexed by EntityTier enum (Normal=0..WorldBoss=5)
        private static readonly float[] TierBonusTable = { 0f, 30f, 20f, 40f, 50f, 50f };

        /// <summary>
        /// Compute importance score for a single widget entity.
        /// </summary>
        /// <param name="distance">Distance from camera in meters.</param>
        /// <param name="entityTierIndex">Index into EntityTier enum (0=Normal..5=WorldBoss).</param>
        /// <param name="isTargeted">Entity is the player's lock-on or click-select target.</param>
        /// <param name="isHovered">Entity is under the cursor.</param>
        /// <param name="isInCombat">Entity is currently in combat.</param>
        /// <param name="timeSinceDamage">Seconds since entity last took damage.</param>
        /// <param name="distanceFalloff">Per-paradigm distance falloff rate (Shooter:3.0, ARPG:1.0).</param>
        /// <returns>Importance score. Higher is more important.</returns>
        public static float Compute(
            float distance,
            int entityTierIndex,
            bool isTargeted,
            bool isHovered,
            bool isInCombat,
            float timeSinceDamage,
            float distanceFalloff)
        {
            // Distance bonus: closer = higher (0 at max distance, 100 at camera)
            float distanceBonus = math.max(0f, 100f - distance * distanceFalloff);

            // Tier bonus
            int idx = math.clamp(entityTierIndex, 0, TierBonusTable.Length - 1);
            float tierBonus = TierBonusTable[idx];

            // Combat bonus
            float combatBonus = isInCombat ? 40f : 0f;

            // Target/hover bonus
            float targetBonus = isTargeted ? 100f : (isHovered ? 30f : 0f);

            // Damage recency bonus (damaged in last 2 seconds)
            float damageRecencyBonus = timeSinceDamage < 2f ? 25f : 0f;

            return distanceBonus + tierBonus + combatBonus + targetBonus + damageRecencyBonus;
        }

        /// <summary>
        /// Whether an entity is exempt from budget enforcement (always visible).
        /// Targeted entities and Boss/WorldBoss tier are exempt.
        /// </summary>
        public static bool IsExemptFromBudget(bool isTargeted, int entityTierIndex)
        {
            // Boss (4) and WorldBoss (5) are always exempt
            return isTargeted || entityTierIndex >= 4;
        }
    }
}
