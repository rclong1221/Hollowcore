using UnityEngine;
using UnityEngine.UI;

namespace DIG.Lobby
{
    /// <summary>
    /// EPIC 17.4: Active lobby room view with player slots, host controls,
    /// ready/start buttons, and chat.
    /// </summary>
    public class LobbyRoomPanel : MonoBehaviour
    {
        [Header("Player Slots")]
        [SerializeField] private LobbyPlayerSlotUI[] _playerSlots;

        [Header("Host Controls")]
        [SerializeField] private Dropdown _mapDropdown;
        [SerializeField] private Dropdown _difficultyDropdown;
        [SerializeField] private Text _joinCodeText;
        [SerializeField] private Button _copyCodeButton;

        [Header("Actions")]
        [SerializeField] private Button _readyButton;
        [SerializeField] private Text _readyButtonText;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _leaveButton;

        [Header("Chat")]
        [SerializeField] private ScrollRect _chatScrollRect;
        [SerializeField] private Text _chatText;
        [SerializeField] private InputField _chatInput;
        [SerializeField] private Button _chatSendButton;

        [Header("Status")]
        [SerializeField] private Text _statusText;

        private bool _isReady;
        private readonly System.Text.StringBuilder _chatBuilder = new System.Text.StringBuilder(1024);
        private int _chatLineCount;
        private const int MaxChatLines = 50;
        private bool _scrollToBottom;
        private float _errorClearTime;

        private void OnEnable()
        {
            // Clear chat from previous lobby session
            _chatBuilder.Clear();
            _chatLineCount = 0;
            if (_chatText != null) _chatText.text = "";

            var mgr = LobbyManager.Instance;
            if (mgr != null)
            {
                mgr.OnChatReceived += HandleChatReceived;
            }

            if (_readyButton != null) _readyButton.onClick.AddListener(OnReadyClicked);
            if (_startButton != null) _startButton.onClick.AddListener(OnStartClicked);
            if (_leaveButton != null) _leaveButton.onClick.AddListener(OnLeaveClicked);
            if (_copyCodeButton != null) _copyCodeButton.onClick.AddListener(OnCopyCodeClicked);
            if (_chatSendButton != null) _chatSendButton.onClick.AddListener(OnChatSendClicked);
            if (_chatInput != null) _chatInput.onEndEdit.AddListener(OnChatEndEdit);
            if (_mapDropdown != null) _mapDropdown.onValueChanged.AddListener(OnMapChanged);
            if (_difficultyDropdown != null) _difficultyDropdown.onValueChanged.AddListener(OnDifficultyChanged);
        }

        private void OnDisable()
        {
            var mgr = LobbyManager.Instance;
            if (mgr != null)
            {
                mgr.OnChatReceived -= HandleChatReceived;
            }

            if (_readyButton != null) _readyButton.onClick.RemoveListener(OnReadyClicked);
            if (_startButton != null) _startButton.onClick.RemoveListener(OnStartClicked);
            if (_leaveButton != null) _leaveButton.onClick.RemoveListener(OnLeaveClicked);
            if (_copyCodeButton != null) _copyCodeButton.onClick.RemoveListener(OnCopyCodeClicked);
            if (_chatSendButton != null) _chatSendButton.onClick.RemoveListener(OnChatSendClicked);
            if (_chatInput != null) _chatInput.onEndEdit.RemoveListener(OnChatEndEdit);
            if (_mapDropdown != null) _mapDropdown.onValueChanged.RemoveListener(OnMapChanged);
            if (_difficultyDropdown != null) _difficultyDropdown.onValueChanged.RemoveListener(OnDifficultyChanged);
        }

        public void RefreshState(LobbyState state)
        {
            if (state == null) return;

            var mgr = LobbyManager.Instance;
            bool isHost = mgr != null && mgr.IsHost;
            int localSlot = mgr != null ? mgr.LocalSlotIndex : -1;

            // Sync local ready state from authoritative server state
            if (localSlot >= 0 && localSlot < state.Players.Count && !state.Players[localSlot].IsEmpty)
            {
                _isReady = state.Players[localSlot].IsReady;
                if (_readyButtonText != null)
                    _readyButtonText.text = _isReady ? "Unready" : "Ready";
            }

            // Update player slots
            if (_playerSlots != null)
            {
                for (int i = 0; i < _playerSlots.Length; i++)
                {
                    if (i < state.Players.Count)
                    {
                        var slot = state.Players[i];
                        if (slot.IsEmpty)
                            _playerSlots[i].SetEmpty();
                        else
                            _playerSlots[i].SetSlot(slot, isHost && !slot.IsHost);
                    }
                    else
                    {
                        _playerSlots[i].SetEmpty();
                    }
                }
            }

            // Join code
            if (_joinCodeText != null) _joinCodeText.text = state.JoinCode ?? "";

            // Host controls visibility
            if (_mapDropdown != null) _mapDropdown.interactable = isHost;
            if (_difficultyDropdown != null) _difficultyDropdown.interactable = isHost;
            if (_startButton != null)
            {
                _startButton.gameObject.SetActive(isHost);
                _startButton.interactable = AllPlayersReady(state);
            }

            // Ready button shows for non-host
            if (_readyButton != null) _readyButton.gameObject.SetActive(!isHost);
        }

        private bool AllPlayersReady(LobbyState state)
        {
            for (int i = 0; i < state.Players.Count; i++)
            {
                var p = state.Players[i];
                if (!p.IsEmpty && !p.IsHost && !p.IsReady) return false;
            }
            return true;
        }

        private void OnReadyClicked()
        {
            _isReady = !_isReady;
            LobbyManager.Instance?.SetReady(_isReady);
            if (_readyButtonText != null) _readyButtonText.text = _isReady ? "Unready" : "Ready";
        }

        private void OnStartClicked()
        {
            LobbyManager.Instance?.StartGame();
        }

        private void OnLeaveClicked()
        {
            LobbyManager.Instance?.LeaveLobby();
            _isReady = false;
            if (_readyButtonText != null) _readyButtonText.text = "Ready";
        }

        private void OnCopyCodeClicked()
        {
            var lobby = LobbyManager.Instance?.CurrentLobby;
            if (lobby != null && !string.IsNullOrEmpty(lobby.JoinCode))
            {
                GUIUtility.systemCopyBuffer = lobby.JoinCode;
                Debug.Log($"[LobbyRoom] Copied join code: {lobby.JoinCode}");
            }
        }

        private void OnChatSendClicked()
        {
            SendChat();
        }

        private void OnChatEndEdit(string text)
        {
            // wasCanceled is true when user presses Escape or clicks away;
            // false means Enter was pressed (compatible with new Input System)
            if (!_chatInput.wasCanceled)
                SendChat();
        }

        private void SendChat()
        {
            if (_chatInput == null || string.IsNullOrWhiteSpace(_chatInput.text)) return;
            LobbyManager.Instance?.SendChat(_chatInput.text);
            _chatInput.text = "";
            _chatInput.ActivateInputField();
        }

        private void OnMapChanged(int index)
        {
            LobbyManager.Instance?.SetMap(index);
        }

        private void OnDifficultyChanged(int index)
        {
            LobbyManager.Instance?.SetDifficulty(index);
        }

        private void HandleChatReceived(string senderName, string text)
        {
            if (_chatText != null)
            {
                _chatBuilder.Append('\n').Append("<b>").Append(senderName).Append("</b>: ").Append(text);
                _chatLineCount++;

                // Trim oldest lines when exceeding cap to prevent unbounded growth
                if (_chatLineCount > MaxChatLines)
                {
                    var full = _chatBuilder.ToString();
                    // Find the start of the second line (skip first '\n' then find next)
                    int cutIdx = full.IndexOf('\n', 1);
                    if (cutIdx >= 0)
                    {
                        _chatBuilder.Clear();
                        _chatBuilder.Append(full, cutIdx, full.Length - cutIdx);
                        _chatLineCount--;
                    }
                }

                _chatText.text = _chatBuilder.ToString();
                _scrollToBottom = true;
            }
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
        }

        private void LateUpdate()
        {
            if (_scrollToBottom && _chatScrollRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_chatScrollRect.content);
                _chatScrollRect.verticalNormalizedPosition = 0f;
                _scrollToBottom = false;
            }

            if (_errorClearTime > 0f && Time.unscaledTime >= _errorClearTime)
            {
                _errorClearTime = 0f;
                if (_statusText != null) _statusText.gameObject.SetActive(false);
            }
        }

    }
}
