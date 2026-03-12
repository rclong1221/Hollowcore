using System;
using UnityEngine;
using UnityEngine.UI;

namespace DIG.Lobby
{
    /// <summary>
    /// EPIC 17.4: Poolable list entry for lobby browser.
    /// Shows host name, map, difficulty, player count, and ping.
    /// </summary>
    public class LobbyListEntryUI : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private Text _hostNameText;
        [SerializeField] private Text _mapText;
        [SerializeField] private Text _difficultyText;
        [SerializeField] private Text _playerCountText;
        [SerializeField] private Text _pingText;
        [SerializeField] private Button _joinButton;
        [SerializeField] private Image _background;

        [Header("Colors")]
        [SerializeField] private Color _normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color _selectedColor = new Color(0.3f, 0.3f, 0.5f, 0.8f);

        private LobbyInfo _data;
        private bool _isSelected;

        public event Action<string> OnJoinRequested;

        private void OnEnable()
        {
            if (_joinButton != null) _joinButton.onClick.AddListener(OnJoinClicked);
        }

        private void OnDisable()
        {
            if (_joinButton != null) _joinButton.onClick.RemoveListener(OnJoinClicked);
        }

        public void SetData(LobbyInfo info)
        {
            _data = info;

            if (_hostNameText != null) _hostNameText.text = info.HostName;
            if (_mapText != null) _mapText.text = $"Map {info.MapId}";
            if (_difficultyText != null) _difficultyText.text = $"Diff {info.DifficultyId}";
            if (_playerCountText != null) _playerCountText.text = $"{info.CurrentPlayers}/{info.MaxPlayers}";
            if (_pingText != null) _pingText.text = $"{info.PingMs}ms";
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            if (_background != null) _background.color = selected ? _selectedColor : _normalColor;
        }

        private void OnJoinClicked()
        {
            OnJoinRequested?.Invoke(_data.JoinCode);
        }
    }
}
