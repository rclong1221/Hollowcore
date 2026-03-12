using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DIG.Lobby
{
    /// <summary>
    /// EPIC 17.4: Public lobby browsing panel with filtering, sorting, and join-by-code.
    /// </summary>
    public class LobbyBrowserPanel : MonoBehaviour
    {
        [Header("List")]
        [SerializeField] private Transform _listContainer;
        [SerializeField] private LobbyListEntryUI _entryPrefab;

        [Header("Filters")]
        [SerializeField] private InputField _searchField;
        [SerializeField] private Dropdown _mapDropdown;
        [SerializeField] private Dropdown _difficultyDropdown;
        [SerializeField] private Toggle _showFullToggle;
        [SerializeField] private Slider _maxPingSlider;
        [SerializeField] private Text _maxPingLabel;

        [Header("Join by Code")]
        [SerializeField] private InputField _joinCodeInput;
        [SerializeField] private InputField _hostIpInput;
        [SerializeField] private Button _joinByCodeButton;

        [Header("Actions")]
        [SerializeField] private Button _refreshButton;
        [SerializeField] private Button _createGameButton;
        [SerializeField] private Button _quickMatchButton;

        [Header("Status")]
        [SerializeField] private Text _statusText;

        private LobbyDiscoveryService _discovery;
        private readonly List<LobbyListEntryUI> _entries = new List<LobbyListEntryUI>();
        private readonly List<LobbyInfo> _filteredCache = new List<LobbyInfo>();
        private LobbyFilter _currentFilter;
        private float _errorClearTime;

        private void Awake()
        {
            var config = LobbyManager.Instance?.Config;
            int maxResults = config != null ? config.DiscoveryMaxResults : 50;
            float refreshMs = config != null ? config.DiscoveryRefreshIntervalMs : 3000;
            _discovery = new LobbyDiscoveryService(maxResults, refreshMs);
            _currentFilter = new LobbyFilter();
        }

        private void OnEnable()
        {
            if (_refreshButton != null) _refreshButton.onClick.AddListener(OnRefreshClicked);
            if (_createGameButton != null) _createGameButton.onClick.AddListener(OnCreateGameClicked);
            if (_joinByCodeButton != null)
            {
                _joinByCodeButton.onClick.AddListener(OnJoinByCodeClicked);
                _joinByCodeButton.interactable = true; // Re-enable on panel show
            }
            if (_quickMatchButton != null) _quickMatchButton.onClick.AddListener(OnQuickMatchClicked);
            if (_searchField != null) _searchField.onValueChanged.AddListener(OnFilterChanged);
            if (_maxPingSlider != null) _maxPingSlider.onValueChanged.AddListener(OnPingSliderChanged);

            if (_discovery != null)
                _discovery.OnLobbiesUpdated += RefreshList;

            RefreshList();
        }

        private void OnDisable()
        {
            if (_refreshButton != null) _refreshButton.onClick.RemoveListener(OnRefreshClicked);
            if (_createGameButton != null) _createGameButton.onClick.RemoveListener(OnCreateGameClicked);
            if (_joinByCodeButton != null) _joinByCodeButton.onClick.RemoveListener(OnJoinByCodeClicked);
            if (_quickMatchButton != null) _quickMatchButton.onClick.RemoveListener(OnQuickMatchClicked);
            if (_searchField != null) _searchField.onValueChanged.RemoveListener(OnFilterChanged);
            if (_maxPingSlider != null) _maxPingSlider.onValueChanged.RemoveListener(OnPingSliderChanged);

            if (_discovery != null)
                _discovery.OnLobbiesUpdated -= RefreshList;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i] != null)
                    _entries[i].OnJoinRequested -= OnEntryJoinRequested;
            }
        }

        private void RefreshList()
        {
            UpdateFilter();
            _discovery.GetFiltered(_currentFilter, _filteredCache);
            var filtered = _filteredCache;

            int needed = filtered.Count;

            // Expand pool if needed
            while (_entries.Count < needed && _entryPrefab != null && _listContainer != null)
            {
                var entry = Instantiate(_entryPrefab, _listContainer);
                entry.OnJoinRequested += OnEntryJoinRequested;
                _entries.Add(entry);
            }

            // Update visible entries, hide surplus
            for (int i = 0; i < _entries.Count; i++)
            {
                if (i < needed)
                {
                    _entries[i].SetData(filtered[i]);
                    _entries[i].gameObject.SetActive(true);
                }
                else
                {
                    _entries[i].gameObject.SetActive(false);
                }
            }
        }

        private void UpdateFilter()
        {
            _currentFilter.SearchText = _searchField != null ? _searchField.text : "";
            _currentFilter.MapId = _mapDropdown != null ? _mapDropdown.value - 1 : -1; // 0 = Any → -1
            _currentFilter.DifficultyId = _difficultyDropdown != null ? _difficultyDropdown.value - 1 : -1;
            _currentFilter.ShowFull = _showFullToggle != null && _showFullToggle.isOn;
            _currentFilter.MaxPing = _maxPingSlider != null ? (int)_maxPingSlider.value : 999;
        }

        private void OnRefreshClicked()
        {
            _discovery.Clear();
            RefreshList();
        }

        private void OnCreateGameClicked()
        {
            // Switch to room panel in create mode
            var uiManager = GetComponentInParent<LobbyUIManager>();
            if (uiManager != null)
            {
                LobbyManager.Instance?.CreateLobby(null, null, false);
            }
        }

        private void OnJoinByCodeClicked()
        {
            if (_joinCodeInput == null) return;
            string code = _joinCodeInput.text.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code)) return;

            string hostIp = _hostIpInput != null ? _hostIpInput.text.Trim() : "";
            if (string.IsNullOrEmpty(hostIp)) hostIp = "127.0.0.1";

            // Disable to prevent double-click
            if (_joinByCodeButton != null) _joinByCodeButton.interactable = false;
            LobbyManager.Instance?.JoinLobby(code, hostIp);
        }

        private void OnQuickMatchClicked()
        {
            // Quick match: join first available non-full lobby
            var lobbies = _discovery.GetFiltered(new LobbyFilter { ShowFull = false });
            if (lobbies.Count > 0)
            {
                LobbyManager.Instance?.JoinLobby(lobbies[0].JoinCode);
            }
            else
            {
                Debug.Log("[LobbyBrowser] No lobbies available for quick match.");
            }
        }

        private void OnEntryJoinRequested(string joinCode)
        {
            LobbyManager.Instance?.JoinLobby(joinCode);
        }

        public void ShowError(string error)
        {
            if (_statusText != null)
            {
                _statusText.text = error;
                _statusText.color = new Color(1f, 0.4f, 0.4f);
                _statusText.gameObject.SetActive(true);
                _errorClearTime = Time.unscaledTime + 4f;
            }

            // Re-enable join button so user can retry
            if (_joinByCodeButton != null) _joinByCodeButton.interactable = true;
        }

        private void LateUpdate()
        {
            if (_errorClearTime > 0f && Time.unscaledTime >= _errorClearTime)
            {
                _errorClearTime = 0f;
                if (_statusText != null) _statusText.gameObject.SetActive(false);
            }
        }

        private void OnFilterChanged(string _) => RefreshList();

        private void OnPingSliderChanged(float value)
        {
            if (_maxPingLabel != null) _maxPingLabel.text = $"{(int)value}ms";
            RefreshList();
        }
    }
}
