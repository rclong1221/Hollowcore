using UnityEngine;

namespace DIG.Lobby
{
    /// <summary>
    /// EPIC 17.4: Panel switcher for lobby UI. Manages visibility of sub-panels
    /// and subscribes to LobbyManager events.
    /// </summary>
    public class LobbyUIManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private LobbyBrowserPanel _browserPanel;
        [SerializeField] private LobbyRoomPanel _roomPanel;
        [SerializeField] private TransitionLoadingPanel _transitionPanel;

        [Header("Root")]
        [SerializeField] private GameObject _lobbyRoot;

        private enum ActivePanel { None, Browser, Room, Transition }
        private ActivePanel _currentPanel = ActivePanel.None;
        private bool _subscribed;

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void Start()
        {
            // Retry subscription in case LobbyManager.Awake hadn't run during OnEnable
            TrySubscribe();

            // Show browser panel by default on startup
            if (_currentPanel == ActivePanel.None)
                ShowPanel(ActivePanel.Browser);
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                var mgr = LobbyManager.Instance;
                if (mgr != null)
                {
                    mgr.OnLobbyStateChanged -= HandleLobbyStateChanged;
                    mgr.OnTransitionStarted -= HandleTransitionStarted;
                    mgr.OnError -= HandleError;
                    mgr.OnJoinDenied -= HandleJoinDenied;
                }
                _subscribed = false;
            }
        }

        private void Update()
        {
            // Hide lobby UI once the game has fully loaded
            if (_currentPanel != ActivePanel.None && GameBootstrap.HasInitialized)
            {
                var mgr = LobbyManager.Instance;
                if (mgr != null && mgr.Phase == LobbyPhase.InGame)
                    ShowPanel(ActivePanel.None);
            }
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            var mgr = LobbyManager.Instance;
            if (mgr == null) return;

            mgr.OnLobbyStateChanged += HandleLobbyStateChanged;
            mgr.OnTransitionStarted += HandleTransitionStarted;
            mgr.OnError += HandleError;
            mgr.OnJoinDenied += HandleJoinDenied;
            _subscribed = true;
        }

        public void ShowBrowser()
        {
            ShowPanel(ActivePanel.Browser);
        }

        public void ShowRoom()
        {
            ShowPanel(ActivePanel.Room);
        }

        public void ShowTransition()
        {
            ShowPanel(ActivePanel.Transition);
        }

        public void HideAll()
        {
            ShowPanel(ActivePanel.None);
        }

        private void ShowPanel(ActivePanel panel)
        {
            _currentPanel = panel;

            if (_browserPanel != null) _browserPanel.gameObject.SetActive(panel == ActivePanel.Browser);
            if (_roomPanel != null) _roomPanel.gameObject.SetActive(panel == ActivePanel.Room);
            if (_transitionPanel != null) _transitionPanel.gameObject.SetActive(panel == ActivePanel.Transition);
            if (_lobbyRoot != null) _lobbyRoot.SetActive(panel != ActivePanel.None);
        }

        private void HandleLobbyStateChanged(LobbyState state)
        {
            if (state == null)
            {
                ShowPanel(ActivePanel.Browser);
                return;
            }

            var mgr = LobbyManager.Instance;
            if (mgr != null && mgr.Phase == LobbyPhase.InLobby)
            {
                ShowPanel(ActivePanel.Room);
                if (_roomPanel != null) _roomPanel.RefreshState(state);
            }
        }

        private void HandleTransitionStarted()
        {
            ShowPanel(ActivePanel.Transition);
        }

        private void HandleError(string error)
        {
            Debug.LogWarning($"[LobbyUI] Error: {error}");
            if (_roomPanel != null && _currentPanel == ActivePanel.Room)
                _roomPanel.ShowError(error);
            if (_browserPanel != null && _currentPanel == ActivePanel.Browser)
                _browserPanel.ShowError(error);
        }

        private void HandleJoinDenied(DenyReason reason)
        {
            ShowPanel(ActivePanel.Browser);
            Debug.LogWarning($"[LobbyUI] Join denied: {reason}");
        }
    }
}
