using UnityEngine;
using UnityEngine.UI;

namespace DIG.PvP.UI
{
    /// <summary>
    /// EPIC 17.10: Countdown timer display with phase indicator.
    /// Shows match time remaining, current scores, and phase text.
    /// </summary>
    public class MatchTimerView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Text _timerText;
        [SerializeField] private Text _phaseText;
        [SerializeField] private Text _scoreText;
        [SerializeField] private GameObject _panel;

        public void UpdateMatchState(PvPMatchUIState state)
        {
            if (_panel != null)
                _panel.SetActive(state.Phase != PvPMatchPhase.WaitingForPlayers && state.Phase != PvPMatchPhase.Ended);

            // Timer
            if (_timerText != null)
            {
                int minutes = (int)(state.TimeRemaining / 60f);
                int seconds = (int)(state.TimeRemaining % 60f);
                _timerText.text = $"{minutes:D2}:{seconds:D2}";

                _timerText.color = state.TimeRemaining < 30f
                    ? Color.red
                    : Color.white;
            }

            // Phase
            if (_phaseText != null)
            {
                _phaseText.text = state.Phase switch
                {
                    PvPMatchPhase.Warmup => "WARMUP",
                    PvPMatchPhase.Active => "",
                    PvPMatchPhase.Overtime => "OVERTIME",
                    PvPMatchPhase.Results => "MATCH OVER",
                    _ => ""
                };
            }

            // Scores
            if (_scoreText != null)
            {
                _scoreText.text = state.GameMode switch
                {
                    PvPGameMode.TeamDeathmatch => $"{state.TeamScore0} - {state.TeamScore1}",
                    PvPGameMode.CapturePoint => $"{state.TeamScore0} - {state.TeamScore1}",
                    PvPGameMode.Duel => $"{state.TeamScore0} - {state.TeamScore1}",
                    _ => $"K: {state.LocalPlayerKills}  D: {state.LocalPlayerDeaths}  A: {state.LocalPlayerAssists}"
                };
            }
        }
    }
}
