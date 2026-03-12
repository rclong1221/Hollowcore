using UnityEngine;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: ScriptableObject defining Elo ranking parameters.
    /// Loaded from Resources/PvPRankingConfig by PvPBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(fileName = "PvPRankingConfig", menuName = "DIG/PvP/Ranking Config")]
    public class PvPRankingConfigSO : ScriptableObject
    {
        [Header("Starting Values")]
        [Tooltip("Initial Elo for new players.")]
        [Min(0)] public int StartingElo = 1200;

        [Header("K-Factor")]
        [Tooltip("Standard Elo K-factor.")]
        [Min(1)] public int KFactor = 32;

        [Tooltip("Reduced K above high rating threshold.")]
        [Min(1)] public int KFactorHighRating = 16;

        [Tooltip("Elo threshold for reduced K.")]
        [Min(0)] public int HighRatingThreshold = 2400;

        [Header("Placement")]
        [Tooltip("Matches before stable ranking.")]
        [Min(1)] public int PlacementMatchCount = 10;

        [Tooltip("K multiplier during placements.")]
        [Range(1f, 5f)] public float PlacementKMultiplier = 2.0f;

        [Header("Tier Thresholds")]
        [Tooltip("Elo thresholds for Bronze/Silver/Gold/Platinum/Diamond/Master.")]
        public int[] TierThresholds = { 0, 1000, 1500, 2000, 2500, 3000 };

        [Header("Win Streak")]
        [Tooltip("Extra Elo per win in streak of 3+.")]
        [Min(0)] public int WinStreakBonus = 5;

        [Tooltip("Cap on streak bonus.")]
        [Min(0)] public int MaxWinStreakBonus = 25;

        public PvPTier GetTierForElo(int elo)
        {
            if (TierThresholds == null || TierThresholds.Length == 0)
                return PvPTier.Bronze;

            PvPTier tier = PvPTier.Bronze;
            for (int i = 0; i < TierThresholds.Length; i++)
            {
                if (elo >= TierThresholds[i])
                    tier = (PvPTier)i;
                else
                    break;
            }
            return tier;
        }
    }
}
