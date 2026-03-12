using UnityEngine;
using UnityEngine.UI;

namespace DIG.PvP.UI
{
    /// <summary>
    /// EPIC 17.10: Queue/lobby UI for joining PvP matches.
    /// Allows game mode selection and displays queue status.
    /// </summary>
    public class PvPQueueView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Dropdown _gameModeDropdown;
        [SerializeField] private Dropdown _mapDropdown;
        [SerializeField] private Button _joinQueueButton;
        [SerializeField] private Button _leaveQueueButton;
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _estimatedWaitText;

        private bool _inQueue;

        private void OnEnable()
        {
            if (_joinQueueButton != null)
                _joinQueueButton.onClick.AddListener(OnJoinQueue);
            if (_leaveQueueButton != null)
                _leaveQueueButton.onClick.AddListener(OnLeaveQueue);
        }

        private void OnDisable()
        {
            if (_joinQueueButton != null)
                _joinQueueButton.onClick.RemoveListener(OnJoinQueue);
            if (_leaveQueueButton != null)
                _leaveQueueButton.onClick.RemoveListener(OnLeaveQueue);
        }

        public void Show()
        {
            if (_panel != null) _panel.SetActive(true);
            UpdateButtons();
        }

        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnJoinQueue()
        {
            _inQueue = true;
            UpdateButtons();

            if (_statusText != null)
                _statusText.text = "Searching for match...";
        }

        private void OnLeaveQueue()
        {
            _inQueue = false;
            UpdateButtons();

            if (_statusText != null)
                _statusText.text = "";
        }

        private void UpdateButtons()
        {
            if (_joinQueueButton != null)
                _joinQueueButton.gameObject.SetActive(!_inQueue);
            if (_leaveQueueButton != null)
                _leaveQueueButton.gameObject.SetActive(_inQueue);
        }

        public PvPGameMode GetSelectedGameMode()
        {
            if (_gameModeDropdown == null) return PvPGameMode.TeamDeathmatch;
            return (PvPGameMode)_gameModeDropdown.value;
        }

        public byte GetSelectedMapId()
        {
            if (_mapDropdown == null) return 0;
            return (byte)_mapDropdown.value;
        }
    }
}
