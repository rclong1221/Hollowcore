using UnityEngine;
using UnityEngine.UI;

namespace DIG.Lobby
{
    /// <summary>
    /// EPIC 17.4: Loading screen during lobby → game transition.
    /// Shows phase text, progress bar, and random gameplay tips.
    /// </summary>
    public class TransitionLoadingPanel : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private Text _phaseText;
        [SerializeField] private Slider _progressBar;
        [SerializeField] private Text _tipText;
        [SerializeField] private Button _cancelButton;

        [Header("Tips")]
        [SerializeField] private string[] _tips =
        {
            "Stick together with your party for bonus XP!",
            "Higher difficulty means better loot drops.",
            "Use the trade system to share gear with friends.",
            "Ready up to let the host know you're prepared.",
            "Check your map for spawn point locations."
        };

        private void OnEnable()
        {
            LobbyToGameTransition.OnProgressUpdated += HandleProgress;
            LobbyToGameTransition.OnTransitionError += HandleError;

            if (_cancelButton != null) _cancelButton.onClick.AddListener(OnCancelClicked);

            // Show random tip
            if (_tipText != null && _tips != null && _tips.Length > 0)
                _tipText.text = _tips[Random.Range(0, _tips.Length)];

            SetProgress("Preparing...", 0f);
        }

        private void OnDisable()
        {
            LobbyToGameTransition.OnProgressUpdated -= HandleProgress;
            LobbyToGameTransition.OnTransitionError -= HandleError;

            if (_cancelButton != null) _cancelButton.onClick.RemoveListener(OnCancelClicked);
        }

        private void HandleProgress(string phase, float progress)
        {
            SetProgress(phase, progress);
        }

        private void HandleError(string error)
        {
            if (_phaseText != null) _phaseText.text = $"Error: {error}";
            if (_cancelButton != null) _cancelButton.gameObject.SetActive(true);
        }

        private void SetProgress(string phase, float progress)
        {
            if (_phaseText != null) _phaseText.text = phase;
            if (_progressBar != null) _progressBar.value = progress;
        }

        private void OnCancelClicked()
        {
            LobbyManager.Instance?.LeaveLobby();
        }
    }
}
