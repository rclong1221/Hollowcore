using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DIG.UI;
using DIG.UI.Core.Navigation;

namespace DIG.Settings
{
    /// <summary>
    /// Escape menu with tab support. Opens/closes with ESC key.
    /// Game keeps running (no Time.timeScale pause — NetCode requires it).
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panelRoot;

        [Header("Buttons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _quitButton;

        [Header("Tabs")]
        [SerializeField] private Button[] _tabButtons;
        [SerializeField] private GameObject[] _tabPanels;
        [SerializeField] private Color _activeTabColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color _inactiveTabColor = new Color(0.15f, 0.15f, 0.15f, 1f);

        private bool _isOpen;
        private int _activeTab;

        private void Start()
        {
            if (_panelRoot != null)
                _panelRoot.SetActive(false);

            if (_resumeButton != null)
                _resumeButton.onClick.AddListener(Close);

            if (_quitButton != null)
                _quitButton.onClick.AddListener(OnQuit);

            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int tabIndex = i;
                if (_tabButtons[i] != null)
                    _tabButtons[i].onClick.AddListener(() => SwitchTab(tabIndex));
            }
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
                return;

            // EPIC 18.2: Defer ESC to NavigationManager when UI Toolkit screens are open (e.g., Settings)
            var nav = NavigationManager.Instance;
            if (nav != null && nav.StackDepth > 0)
                return;

            if (_isOpen)
                Close();
            else
                Open();
        }

        public void Open()
        {
            if (_panelRoot == null) return;

            _isOpen = true;
            _panelRoot.SetActive(true);
            MenuState.RegisterMenu(this, true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SwitchTab(0);
        }

        public void Close()
        {
            if (_panelRoot == null) return;

            _isOpen = false;
            _panelRoot.SetActive(false);
            MenuState.RegisterMenu(this, false);

            if (!MenuState.IsAnyMenuOpen())
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void SwitchTab(int index)
        {
            _activeTab = index;

            for (int i = 0; i < _tabPanels.Length; i++)
            {
                if (_tabPanels[i] != null)
                    _tabPanels[i].SetActive(i == index);
            }

            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] != null)
                {
                    var colors = _tabButtons[i].colors;
                    colors.normalColor = i == index ? _activeTabColor : _inactiveTabColor;
                    _tabButtons[i].colors = colors;
                }
            }
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (_resumeButton != null)
                _resumeButton.onClick.RemoveListener(Close);

            if (_quitButton != null)
                _quitButton.onClick.RemoveListener(OnQuit);
        }
    }
}
