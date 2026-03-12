using UnityEngine;
using UnityEngine.UI;

namespace DIG.PvP.UI
{
    /// <summary>
    /// EPIC 17.10: Post-match results overlay showing final scores,
    /// Elo changes, and tier progression.
    /// </summary>
    public class MatchResultsView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _resultText;
        [SerializeField] private Text _eloDeltaText;
        [SerializeField] private Text _tierText;
        [SerializeField] private Text _bonusXPText;
        [SerializeField] private Transform _scoreboardContainer;
        [SerializeField] private GameObject _scoreEntryPrefab;

        public void OnMatchResult(PvPMatchResultUI result)
        {
            if (_panel != null)
                _panel.SetActive(true);

            // Win/Loss/Draw text
            if (_resultText != null)
            {
                if (result.WinningTeam == 0)
                {
                    _resultText.text = "DRAW";
                    _resultText.color = Color.yellow;
                }
                else if (result.LocalPlayerWon)
                {
                    _resultText.text = "VICTORY";
                    _resultText.color = Color.green;
                }
                else
                {
                    _resultText.text = "DEFEAT";
                    _resultText.color = Color.red;
                }
            }

            // Elo change
            if (_eloDeltaText != null)
            {
                string sign = result.EloDelta >= 0 ? "+" : "";
                _eloDeltaText.text = $"{sign}{result.EloDelta} Elo";
                _eloDeltaText.color = result.EloDelta >= 0 ? Color.green : Color.red;
            }

            // Tier change
            if (_tierText != null)
            {
                if (result.TierChanged)
                {
                    _tierText.text = $"{result.OldTier} -> {result.NewTier}";
                    _tierText.color = result.NewTier > result.OldTier ? Color.green : Color.red;
                }
                else
                {
                    _tierText.text = result.NewTier.ToString();
                }
            }

            // Bonus XP
            if (_bonusXPText != null)
                _bonusXPText.text = result.BonusXP > 0 ? $"+{result.BonusXP:F0} XP" : "";

            // Populate final scoreboard
            if (_scoreboardContainer != null && _scoreEntryPrefab != null && result.FinalScoreboard != null)
            {
                for (int i = _scoreboardContainer.childCount - 1; i >= 0; i--)
                    Destroy(_scoreboardContainer.GetChild(i).gameObject);

                for (int i = 0; i < result.FinalScoreboard.Length; i++)
                {
                    var entry = result.FinalScoreboard[i];
                    var go = Instantiate(_scoreEntryPrefab, _scoreboardContainer);
                    var texts = go.GetComponentsInChildren<Text>();
                    if (texts.Length >= 4)
                    {
                        texts[0].text = entry.PlayerName ?? "Player";
                        texts[1].text = entry.Kills.ToString();
                        texts[2].text = entry.Deaths.ToString();
                        texts[3].text = entry.Assists.ToString();
                    }
                }
            }
        }

        public void Hide()
        {
            if (_panel != null)
                _panel.SetActive(false);
        }
    }
}
