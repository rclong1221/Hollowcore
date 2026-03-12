using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DIG.DeathCamera
{
    /// <summary>
    /// EPIC 18.13: Spectator HUD overlay. Shows followed player info,
    /// player list, camera mode indicator, and respawn countdown.
    /// Built programmatically via uGUI. Singleton — created once.
    /// </summary>
    public class SpectatorHUDView : MonoBehaviour
    {
        public static SpectatorHUDView Instance { get; private set; }

        // Root
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;

        // Top bar — camera mode + followed player
        private TextMeshProUGUI _cameraModeText;
        private TextMeshProUGUI _followedPlayerText;
        private TextMeshProUGUI _playerHealthText;

        // Bottom — respawn countdown
        private TextMeshProUGUI _respawnCountdownText;

        // Right side — player list
        private Transform _playerListParent;
        private readonly List<GameObject> _playerListRows = new();

        // Controls hint
        private TextMeshProUGUI _controlsHintText;

        // Animation
        private float _fadeTarget;
        private float _fadeSpeed = 4f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildUI();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        private void Update()
        {
            if (_canvasGroup == null) return;
            if (Mathf.Approximately(_canvasGroup.alpha, _fadeTarget)) return;
            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _fadeTarget, _fadeSpeed * Time.unscaledDeltaTime);
        }

        // ====================================================================
        // Public API (called by SpectatorPhase)
        // ====================================================================

        public void Show(List<PlayerListEntry> entries, string followedName, float health, float shield)
        {
            UpdatePlayerList(entries);
            UpdateFollowedPlayer(followedName, health, shield);
            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        public void UpdateFollowedPlayer(string name, float health, float maxHealth)
        {
            if (_followedPlayerText != null)
                _followedPlayerText.text = name ?? "No players";

            if (_playerHealthText != null)
            {
                if (maxHealth > 0f)
                    _playerHealthText.text = $"{health:F0} / {maxHealth:F0}";
                else
                    _playerHealthText.text = "";
            }
        }

        public void UpdateCameraMode(DeathSpectatorMode mode)
        {
            if (_cameraModeText != null)
                _cameraModeText.text = mode.DisplayName();

            if (_controlsHintText != null)
                _controlsHintText.text = mode.ControlsHint();
        }

        public void UpdateRespawnCountdown(float timeRemaining)
        {
            if (_respawnCountdownText == null) return;

            if (timeRemaining > 0f)
                _respawnCountdownText.text = $"Respawning in {timeRemaining:F1}s";
            else
                _respawnCountdownText.text = "Respawning...";
        }

        public void UpdatePlayerList(List<PlayerListEntry> entries)
        {
            ClearPlayerList();
            if (entries == null) return;

            for (int i = 0; i < entries.Count; i++)
            {
                CreatePlayerRow(i + 1, entries[i]);
            }
        }

        // ====================================================================
        // UI Construction
        // ====================================================================

        private void BuildUI()
        {
            // Canvas
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 94;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            gameObject.AddComponent<GraphicRaycaster>();

            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            // === Top Bar (camera mode + followed player) ===
            BuildTopBar();

            // === Bottom Center (respawn countdown) ===
            BuildBottomBar();

            // === Right Side (player list) ===
            BuildPlayerList();

            // === Bottom Left (controls hint) ===
            BuildControlsHint();
        }

        private void BuildTopBar()
        {
            var topBar = new GameObject("TopBar");
            topBar.transform.SetParent(transform, false);
            var topRect = topBar.AddComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0.3f, 0.9f);
            topRect.anchorMax = new Vector2(0.7f, 0.96f);
            topRect.offsetMin = Vector2.zero;
            topRect.offsetMax = Vector2.zero;

            var topImg = topBar.AddComponent<Image>();
            topImg.color = new Color(0.05f, 0.05f, 0.1f, 0.7f);

            var topLayout = topBar.AddComponent<HorizontalLayoutGroup>();
            topLayout.padding = new RectOffset(20, 20, 8, 8);
            topLayout.spacing = 15;
            topLayout.childControlWidth = true;
            topLayout.childControlHeight = true;
            topLayout.childForceExpandWidth = false;
            topLayout.childForceExpandHeight = true;

            // Camera mode badge
            _cameraModeText = CreateText(topBar.transform, "CameraMode", "SPECTATING", 16, FontStyles.Bold, new Color(0.3f, 0.8f, 1f, 1f));
            var modeLE = _cameraModeText.gameObject.AddComponent<LayoutElement>();
            modeLE.preferredWidth = 180;

            // Separator
            var sep = new GameObject("Sep");
            sep.transform.SetParent(topBar.transform, false);
            sep.AddComponent<RectTransform>();
            var sepImg = sep.AddComponent<Image>();
            sepImg.color = new Color(0.3f, 0.3f, 0.4f, 0.5f);
            var sepLE = sep.AddComponent<LayoutElement>();
            sepLE.preferredWidth = 2;

            // Followed player name
            _followedPlayerText = CreateText(topBar.transform, "FollowedPlayer", "No players", 20, FontStyles.Bold, Color.white);
            _followedPlayerText.alignment = TextAlignmentOptions.Center;
            var nameLE = _followedPlayerText.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;

            // Health text
            _playerHealthText = CreateText(topBar.transform, "PlayerHealth", "", 16, FontStyles.Normal, new Color(0.4f, 1f, 0.4f, 1f));
            _playerHealthText.alignment = TextAlignmentOptions.Right;
            var healthLE = _playerHealthText.gameObject.AddComponent<LayoutElement>();
            healthLE.preferredWidth = 120;
        }

        private void BuildBottomBar()
        {
            var bottom = new GameObject("BottomBar");
            bottom.transform.SetParent(transform, false);
            var bottomRect = bottom.AddComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0.35f, 0.06f);
            bottomRect.anchorMax = new Vector2(0.65f, 0.12f);
            bottomRect.offsetMin = Vector2.zero;
            bottomRect.offsetMax = Vector2.zero;

            var bottomImg = bottom.AddComponent<Image>();
            bottomImg.color = new Color(0.05f, 0.05f, 0.1f, 0.7f);

            _respawnCountdownText = CreateText(bottom.transform, "RespawnCountdown", "", 22, FontStyles.Bold, new Color(1f, 0.85f, 0.3f, 1f));
            _respawnCountdownText.alignment = TextAlignmentOptions.Center;
            var respawnRect = _respawnCountdownText.GetComponent<RectTransform>();
            StretchFull(respawnRect);
        }

        private void BuildPlayerList()
        {
            var listPanel = new GameObject("PlayerListPanel");
            listPanel.transform.SetParent(transform, false);
            var listRect = listPanel.AddComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0.82f, 0.4f);
            listRect.anchorMax = new Vector2(0.98f, 0.85f);
            listRect.offsetMin = Vector2.zero;
            listRect.offsetMax = Vector2.zero;

            var listImg = listPanel.AddComponent<Image>();
            listImg.color = new Color(0.05f, 0.05f, 0.1f, 0.6f);

            var listLayout = listPanel.AddComponent<VerticalLayoutGroup>();
            listLayout.padding = new RectOffset(10, 10, 10, 10);
            listLayout.spacing = 4;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = false;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;

            // Header
            var header = CreateText(listPanel.transform, "ListHeader", "PLAYERS", 14, FontStyles.Bold, new Color(0.6f, 0.6f, 0.7f, 1f));
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 22;

            // Scrollable content
            var content = new GameObject("PlayerListContent");
            content.transform.SetParent(listPanel.transform, false);
            content.AddComponent<RectTransform>();
            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 2;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            var contentLE = content.AddComponent<LayoutElement>();
            contentLE.flexibleHeight = 1;
            _playerListParent = content.transform;
        }

        private void BuildControlsHint()
        {
            var hint = new GameObject("ControlsHint");
            hint.transform.SetParent(transform, false);
            var hintRect = hint.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.02f, 0.04f);
            hintRect.anchorMax = new Vector2(0.3f, 0.12f);
            hintRect.offsetMin = Vector2.zero;
            hintRect.offsetMax = Vector2.zero;

            _controlsHintText = CreateText(hint.transform, "HintText",
                "[TAB] Cycle Camera  [1-9] Select Player  [Scroll] Zoom",
                13, FontStyles.Italic, new Color(0.5f, 0.5f, 0.5f, 0.8f));
            var hintTextRect = _controlsHintText.GetComponent<RectTransform>();
            StretchFull(hintTextRect);
        }

        // ====================================================================
        // Player List Helpers
        // ====================================================================

        private void CreatePlayerRow(int index, PlayerListEntry entry)
        {
            var row = new GameObject($"Player_{index}");
            row.transform.SetParent(_playerListParent, false);
            row.AddComponent<RectTransform>();

            var hLayout = row.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 6;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = true;

            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 22;

            // Index
            var indexColor = entry.IsAlive ? new Color(0.5f, 0.5f, 0.6f, 1f) : new Color(0.4f, 0.2f, 0.2f, 1f);
            var idxText = CreateText(row.transform, "Index", $"{index}.", 13, FontStyles.Normal, indexColor);
            var idxLE = idxText.gameObject.AddComponent<LayoutElement>();
            idxLE.preferredWidth = 22;

            // Name
            var nameColor = entry.IsAlive ? Color.white : new Color(0.5f, 0.3f, 0.3f, 1f);
            var nameStyle = entry.IsAlive ? FontStyles.Normal : FontStyles.Strikethrough;
            var nameText = CreateText(row.transform, "Name", entry.Name, 14, nameStyle, nameColor);
            var nameLE = nameText.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;

            _playerListRows.Add(row);
        }

        private void ClearPlayerList()
        {
            foreach (var row in _playerListRows)
            {
                if (row != null) Destroy(row);
            }
            _playerListRows.Clear();
        }

        // ====================================================================
        // Utility
        // ====================================================================

        private void SetVisible(bool visible)
        {
            _fadeTarget = visible ? 1f : 0f;
            if (visible && _canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, int fontSize, FontStyles style, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Left;
            return tmp;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
