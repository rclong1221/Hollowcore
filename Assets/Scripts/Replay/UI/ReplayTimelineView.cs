using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DIG.Replay.UI
{
    /// <summary>
    /// EPIC 18.10: uGUI timeline overlay for replay playback.
    /// Runtime-created Canvas with scrub bar, controls, event markers.
    /// Follows PowerHUDBuilder/CargoUIBuilder pattern.
    /// </summary>
    public class ReplayTimelineView : MonoBehaviour
    {
        public static ReplayTimelineView Instance { get; private set; }

        // Auto-created UI elements
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private Slider _timelineSlider;
        private Text _timeLabel;
        private Text _frameLabel;
        private Text _speedLabel;
        private Text _playPauseLabel;
        private RectTransform _eventMarkersContainer;
        private RectTransform _playerListContainer;

        private ReplayTimelineController _controller;
        private readonly List<GameObject> _eventMarkers = new();
        private bool _isDragging;

        // Cached font (avoid Resources.GetBuiltinResource per button/label)
        private static Font _cachedFont;

        // Cached previous values to avoid string allocation when unchanged
        private int _prevTimeMinutes = -1;
        private int _prevTimeSeconds = -1;
        private int _prevFrame = -1;
        private int _prevFrameCount = -1;
        private float _prevSpeed = -1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_cachedFont == null)
                _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() { Instance = null; }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Creates the full timeline UI hierarchy programmatically.
        /// Called when replay mode starts.
        /// </summary>
        public void Initialize(ReplayTimelineController controller)
        {
            _controller = controller;
            CreateUI();
            BindEvents();
        }

        public void Show()
        {
            if (_canvas != null) _canvas.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        private void CreateUI()
        {
            // Canvas
            var canvasGO = new GameObject("ReplayTimelineCanvas");
            canvasGO.transform.SetParent(transform, false);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 110; // Above other HUD
            _canvasGroup = canvasGO.AddComponent<CanvasGroup>();

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            var canvasRect = canvasGO.GetComponent<RectTransform>();

            // Bottom panel background
            var panelGO = CreatePanel(canvasRect, "TimelinePanel",
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 0), new Vector2(0, 100),
                new Color(0.1f, 0.1f, 0.1f, 0.85f));

            var panelRect = panelGO.GetComponent<RectTransform>();

            // Timeline slider
            var sliderGO = CreateSlider(panelRect, "TimelineSlider",
                new Vector2(120, -30), new Vector2(-120, -30), 30);
            _timelineSlider = sliderGO.GetComponent<Slider>();
            _timelineSlider.minValue = 0f;
            _timelineSlider.maxValue = 1f;
            _timelineSlider.onValueChanged.AddListener(OnSliderValueChanged);

            // Event markers container (overlaid on slider)
            var markersGO = new GameObject("EventMarkers");
            markersGO.transform.SetParent(sliderGO.transform, false);
            _eventMarkersContainer = markersGO.AddComponent<RectTransform>();
            _eventMarkersContainer.anchorMin = Vector2.zero;
            _eventMarkersContainer.anchorMax = Vector2.one;
            _eventMarkersContainer.sizeDelta = Vector2.zero;
            _eventMarkersContainer.anchoredPosition = Vector2.zero;

            // Control buttons row
            float btnY = -70f;
            float btnX = 20f;

            // Step backward
            CreateButton(panelRect, "<<", new Vector2(btnX, btnY), () => _controller?.StepBackward());
            btnX += 50f;

            // Play/Pause
            var playBtn = CreateButton(panelRect, "Play", new Vector2(btnX, btnY), () => _controller?.TogglePlayPause());
            _playPauseLabel = playBtn.GetComponentInChildren<Text>();
            btnX += 70f;

            // Step forward
            CreateButton(panelRect, ">>", new Vector2(btnX, btnY), () => _controller?.StepForward());
            btnX += 70f;

            // Speed down
            CreateButton(panelRect, "-", new Vector2(btnX, btnY), () => _controller?.CycleSpeedDown());
            btnX += 40f;

            // Speed label
            _speedLabel = CreateLabel(panelRect, "1.00x", new Vector2(btnX, btnY), 60);
            btnX += 70f;

            // Speed up
            CreateButton(panelRect, "+", new Vector2(btnX, btnY), () => _controller?.CycleSpeedUp());

            // Time label (right side)
            _timeLabel = CreateLabel(panelRect, "00:00", new Vector2(-200, btnY), 80);
            _timeLabel.rectTransform.anchorMin = new Vector2(1, 1);
            _timeLabel.rectTransform.anchorMax = new Vector2(1, 1);

            // Frame label
            _frameLabel = CreateLabel(panelRect, "Frame 0/0", new Vector2(-80, btnY), 120);
            _frameLabel.rectTransform.anchorMin = new Vector2(1, 1);
            _frameLabel.rectTransform.anchorMax = new Vector2(1, 1);

            // Camera mode buttons (top of panel)
            float camBtnX = -400f;
            float camBtnY = -30f;
            string[] modes = { "Free", "Follow", "FP", "Orbit" };
            for (int i = 0; i < modes.Length; i++)
            {
                int modeIndex = i;
                var btn = CreateButton(panelRect, modes[i], new Vector2(camBtnX, camBtnY),
                    () => SpectatorCamera.Instance?.SetMode((SpectatorCameraMode)modeIndex));
                btn.GetComponent<RectTransform>().anchorMin = new Vector2(1, 1);
                btn.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
                camBtnX += 60f;
            }

            // Player list container (left side, above panel for spectator mode)
            var playerListGO = new GameObject("PlayerList");
            playerListGO.transform.SetParent(canvasRect, false);
            _playerListContainer = playerListGO.AddComponent<RectTransform>();
            _playerListContainer.anchorMin = new Vector2(0, 0);
            _playerListContainer.anchorMax = new Vector2(0, 0);
            _playerListContainer.pivot = new Vector2(0, 0);
            _playerListContainer.anchoredPosition = new Vector2(10, 110);
            _playerListContainer.sizeDelta = new Vector2(200, 300);
            var vlg = playerListGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
        }

        private void BindEvents()
        {
            var player = ReplayPlayer.Instance;
            if (player == null) return;

            player.OnTimeChanged += OnTimeChanged;
            player.OnFrameChanged += OnFrameChanged;
            player.OnPlaybackStarted += () => { if (_playPauseLabel != null) _playPauseLabel.text = "Pause"; };
            player.OnPlaybackPaused += () => { if (_playPauseLabel != null) _playPauseLabel.text = "Play"; };
            player.OnPlaybackStopped += () => { if (_playPauseLabel != null) _playPauseLabel.text = "Play"; };
        }

        private void Update()
        {
            var player = ReplayPlayer.Instance;
            if (player == null || player.State == ReplayState.Idle) return;

            // Update time label — only when values change to avoid string allocation
            float t = player.CurrentTime;
            int minutes = (int)(t / 60);
            int seconds = (int)(t % 60);
            if (_timeLabel != null && (minutes != _prevTimeMinutes || seconds != _prevTimeSeconds))
            {
                _prevTimeMinutes = minutes;
                _prevTimeSeconds = seconds;
                _timeLabel.text = $"{minutes:D2}:{seconds:D2}";
            }

            int curFrame = player.CurrentFrame;
            int frameCount = player.FrameCount;
            if (_frameLabel != null && (curFrame != _prevFrame || frameCount != _prevFrameCount))
            {
                _prevFrame = curFrame;
                _prevFrameCount = frameCount;
                _frameLabel.text = $"Frame {curFrame}/{frameCount}";
            }

            if (_speedLabel != null && _controller != null)
            {
                float speed = _controller.CurrentSpeed;
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (speed != _prevSpeed)
                {
                    _prevSpeed = speed;
                    _speedLabel.text = $"{speed:F2}x";
                }
            }

            // Update slider without triggering callback
            if (_timelineSlider != null && !_isDragging && player.Duration > 0)
                _timelineSlider.SetValueWithoutNotify(player.CurrentTime / player.Duration);
        }

        private void OnSliderValueChanged(float value)
        {
            _isDragging = true;
            _controller?.SeekTo(value);
            _isDragging = false;
        }

        private void OnTimeChanged(float normalized)
        {
            if (_timelineSlider != null && !_isDragging)
                _timelineSlider.SetValueWithoutNotify(normalized);
        }

        private void OnFrameChanged(int frameIndex)
        {
            // Could trigger event marker highlights here
        }

        /// <summary>
        /// Populate event markers on the timeline for kills, deaths, etc.
        /// </summary>
        public void PopulateEventMarkers(ReplayEventData[] events, float totalDuration)
        {
            // Clear existing
            foreach (var marker in _eventMarkers)
            {
                if (marker != null) Destroy(marker);
            }
            _eventMarkers.Clear();

            if (events == null || _eventMarkersContainer == null || totalDuration <= 0) return;

            var player = ReplayPlayer.Instance;
            if (player == null) return;

            foreach (var evt in events)
            {
                float normalizedTime = player.FrameCount > 0
                    ? evt.Tick / (float)player.Header.TotalFrames
                    : 0f;

                var color = evt.EventType switch
                {
                    ReplayEventType.Kill => Color.red,
                    ReplayEventType.Death => new Color(0.3f, 0.3f, 0.3f),
                    ReplayEventType.Objective => new Color(1f, 0.84f, 0f),
                    _ => Color.white
                };

                var markerGO = new GameObject($"Marker_{evt.EventType}_{evt.Tick}");
                markerGO.transform.SetParent(_eventMarkersContainer, false);
                var markerRect = markerGO.AddComponent<RectTransform>();
                markerRect.anchorMin = new Vector2(normalizedTime, 0);
                markerRect.anchorMax = new Vector2(normalizedTime, 1);
                markerRect.sizeDelta = new Vector2(4, 0);
                markerRect.anchoredPosition = Vector2.zero;

                var img = markerGO.AddComponent<Image>();
                img.color = color;
                img.raycastTarget = false;

                _eventMarkers.Add(markerGO);
            }
        }

        /// <summary>
        /// Populate the player list for spectator camera follow.
        /// </summary>
        public void PopulatePlayerList(List<ReplayPlayerInfo> players)
        {
            if (_playerListContainer == null || players == null) return;

            // Clear existing
            for (int i = _playerListContainer.childCount - 1; i >= 0; i--)
                Destroy(_playerListContainer.GetChild(i).gameObject);

            for (int i = 0; i < players.Count; i++)
            {
                int idx = i;
                var p = players[i];
                var btn = CreateButton(_playerListContainer, $"P{p.NetworkId} (Ghost:{p.GhostId})",
                    Vector2.zero, () => SpectatorCamera.Instance?.FollowGhostId(p.GhostId));
                var layout = btn.AddComponent<LayoutElement>();
                layout.preferredHeight = 28;
            }
        }

        // === UI Helpers ===

        private static GameObject CreatePanel(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.offsetMin = new Vector2(0, 0);
            rect.offsetMax = new Vector2(0, 100);
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private static GameObject CreateSlider(RectTransform parent, string name,
            Vector2 offsetMin, Vector2 offsetMax, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, -10);
            rect.sizeDelta = new Vector2(-240, height);

            var slider = go.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;

            // Background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(go.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            // Fill area
            var fillAreaGO = new GameObject("Fill Area");
            fillAreaGO.transform.SetParent(go.transform, false);
            var fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = Vector2.zero;

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.color = new Color(0.3f, 0.6f, 1f, 0.8f);

            slider.fillRect = fillRect;

            // Handle
            var handleAreaGO = new GameObject("Handle Slide Area");
            handleAreaGO.transform.SetParent(go.transform, false);
            var handleAreaRect = handleAreaGO.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.sizeDelta = Vector2.zero;

            var handleGO = new GameObject("Handle");
            handleGO.transform.SetParent(handleAreaGO.transform, false);
            var handleRect = handleGO.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(12, 0);
            var handleImg = handleGO.AddComponent<Image>();
            handleImg.color = Color.white;

            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;

            return go;
        }

        private static GameObject CreateButton(RectTransform parent, string text, Vector2 position,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"Btn_{text}");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(Mathf.Max(40, text.Length * 12), 28);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.3f, 0.9f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            var textComp = textGO.AddComponent<Text>();
            textComp.text = text;
            textComp.font = _cachedFont;
            textComp.fontSize = 14;
            textComp.alignment = TextAnchor.MiddleCenter;
            textComp.color = Color.white;

            return go;
        }

        private static Text CreateLabel(RectTransform parent, string text, Vector2 position, float width)
        {
            var go = new GameObject($"Label_{text}");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, 28);

            var textComp = go.AddComponent<Text>();
            textComp.text = text;
            textComp.font = _cachedFont;
            textComp.fontSize = 14;
            textComp.alignment = TextAnchor.MiddleCenter;
            textComp.color = Color.white;

            return textComp;
        }
    }
}
