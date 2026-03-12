using UnityEngine;
using UnityEngine.UI;

namespace DIG.PvP.UI
{
    /// <summary>
    /// EPIC 17.10: Elo/Tier display with progress bar and win rate.
    /// </summary>
    public class RankingView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Text _eloText;
        [SerializeField] private Text _tierText;
        [SerializeField] private Text _recordText;
        [SerializeField] private Text _winRateText;
        [SerializeField] private Text _winStreakText;
        [SerializeField] private Image _tierIcon;
        [SerializeField] private Slider _eloProgressBar;
        [SerializeField] private GameObject _panel;

        [Header("Tier Colors")]
        [SerializeField] private Color _bronzeColor = new Color(0.8f, 0.5f, 0.2f);
        [SerializeField] private Color _silverColor = new Color(0.75f, 0.75f, 0.8f);
        [SerializeField] private Color _goldColor = new Color(1f, 0.84f, 0f);
        [SerializeField] private Color _platinumColor = new Color(0.4f, 0.8f, 0.8f);
        [SerializeField] private Color _diamondColor = new Color(0.5f, 0.5f, 1f);
        [SerializeField] private Color _masterColor = new Color(1f, 0.2f, 0.2f);

        public void UpdateRanking(PvPRankingUI ranking)
        {
            if (_eloText != null)
                _eloText.text = ranking.Elo.ToString();

            if (_tierText != null)
            {
                _tierText.text = ranking.Tier.ToString();
                _tierText.color = GetTierColor(ranking.Tier);
            }

            if (_recordText != null)
                _recordText.text = $"{ranking.Wins}W / {ranking.Losses}L";

            if (_winRateText != null)
                _winRateText.text = $"{ranking.WinRate * 100f:F1}%";

            if (_winStreakText != null)
            {
                _winStreakText.text = ranking.WinStreak > 0 ? $"{ranking.WinStreak} Win Streak" : "";
                _winStreakText.gameObject.SetActive(ranking.WinStreak > 0);
            }

            if (_tierIcon != null)
                _tierIcon.color = GetTierColor(ranking.Tier);

            if (_eloProgressBar != null)
            {
                // Progress within current tier (0-500 range per tier)
                int tierBase = GetTierBaseElo(ranking.Tier);
                int tierRange = 500;
                _eloProgressBar.value = (float)(ranking.Elo - tierBase) / tierRange;
            }
        }

        private Color GetTierColor(PvPTier tier)
        {
            return tier switch
            {
                PvPTier.Bronze => _bronzeColor,
                PvPTier.Silver => _silverColor,
                PvPTier.Gold => _goldColor,
                PvPTier.Platinum => _platinumColor,
                PvPTier.Diamond => _diamondColor,
                PvPTier.Master => _masterColor,
                _ => Color.white
            };
        }

        private static int GetTierBaseElo(PvPTier tier)
        {
            return tier switch
            {
                PvPTier.Bronze => 0,
                PvPTier.Silver => 1000,
                PvPTier.Gold => 1500,
                PvPTier.Platinum => 2000,
                PvPTier.Diamond => 2500,
                PvPTier.Master => 3000,
                _ => 0
            };
        }
    }
}
