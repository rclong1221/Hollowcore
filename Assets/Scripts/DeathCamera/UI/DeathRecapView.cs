using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DIG.DeathCamera
{
    /// <summary>
    /// EPIC 18.13: Death recap overlay. Shows killer info, damage breakdown,
    /// and respawn countdown. Built programmatically via uGUI.
    /// Singleton — created once, shown/hidden per death.
    /// </summary>
    public class DeathRecapView : MonoBehaviour
    {
        public static DeathRecapView Instance { get; private set; }

        // Root
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private GameObject _panel;

        // Header
        private TextMeshProUGUI _killedByLabel;
        private TextMeshProUGUI _killerNameText;

        // Damage breakdown
        private Transform _damageListParent;
        private readonly List<GameObject> _damageRows = new();

        // Respawn
        private TextMeshProUGUI _respawnText;

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
        // Public API (called by DeathRecapPhase)
        // ====================================================================

        public void Show(DeathCameraContext context)
        {
            if (context == null) return;

            // Killer info
            _killerNameText.text = string.IsNullOrEmpty(context.KillerName) ? "Environment" : context.KillerName;

            // Damage breakdown
            ClearDamageRows();
            foreach (var contributor in context.DamageContributors)
            {
                CreateDamageRow(contributor.Name, contributor.DamageDealt, contributor.TimeAgo);
            }

            if (context.DamageContributors.Count == 0)
            {
                CreateDamageRow(context.KillerName ?? "Unknown", 0f, 0f);
            }

            UpdateRespawnCountdown(context.RespawnTimeRemaining);
            SetVisible(true);
        }

        public void UpdateRespawnCountdown(float timeRemaining)
        {
            if (_respawnText == null) return;
            if (timeRemaining > 0f)
                _respawnText.text = $"Respawning in {timeRemaining:F1}s";
            else
                _respawnText.text = "Respawning...";
        }

        public void Hide()
        {
            SetVisible(false);
        }

        // ====================================================================
        // UI Construction
        // ====================================================================

        private void BuildUI()
        {
            // Canvas
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 95;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            gameObject.AddComponent<GraphicRaycaster>();

            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            // Full-screen dark overlay
            var overlay = CreateRect("Overlay", transform);
            StretchFull(overlay);
            var overlayImg = overlay.gameObject.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.6f);

            // Center panel
            _panel = new GameObject("RecapPanel");
            _panel.transform.SetParent(transform, false);
            var panelRect = _panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.3f, 0.25f);
            panelRect.anchorMax = new Vector2(0.7f, 0.75f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var panelImg = _panel.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);

            var panelLayout = _panel.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(30, 30, 25, 25);
            panelLayout.spacing = 12;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = false;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            // "KILLED BY" header
            _killedByLabel = CreateText(_panel.transform, "KilledByLabel", "KILLED BY", 18, FontStyles.Bold, new Color(0.8f, 0.3f, 0.3f, 1f));
            SetPreferredHeight(_killedByLabel, 30f);

            // Killer name
            _killerNameText = CreateText(_panel.transform, "KillerName", "Unknown", 36, FontStyles.Bold, Color.white);
            _killerNameText.alignment = TextAlignmentOptions.Center;
            SetPreferredHeight(_killerNameText, 50f);

            // Separator
            CreateSeparator(_panel.transform);

            // "DAMAGE BREAKDOWN" label
            var breakdownLabel = CreateText(_panel.transform, "BreakdownLabel", "DAMAGE BREAKDOWN", 14, FontStyles.Bold, new Color(0.6f, 0.6f, 0.7f, 1f));
            SetPreferredHeight(breakdownLabel, 24f);

            // Damage list container
            var listGO = new GameObject("DamageList");
            listGO.transform.SetParent(_panel.transform, false);
            var listRect = listGO.AddComponent<RectTransform>();
            var listLayout = listGO.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 4;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = false;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            var listLE = listGO.AddComponent<LayoutElement>();
            listLE.flexibleHeight = 1;
            _damageListParent = listGO.transform;

            // Separator
            CreateSeparator(_panel.transform);

            // Respawn countdown
            _respawnText = CreateText(_panel.transform, "RespawnText", "Respawning...", 22, FontStyles.Normal, new Color(1f, 0.85f, 0.3f, 1f));
            _respawnText.alignment = TextAlignmentOptions.Center;
            SetPreferredHeight(_respawnText, 35f);

            // Skip hint
            var hint = CreateText(_panel.transform, "SkipHint", "Press any key to skip", 14, FontStyles.Italic, new Color(0.5f, 0.5f, 0.5f, 1f));
            hint.alignment = TextAlignmentOptions.Center;
            SetPreferredHeight(hint, 20f);
        }

        // ====================================================================
        // Damage Row Helpers
        // ====================================================================

        private void CreateDamageRow(string contributorName, float damage, float timeAgo)
        {
            var row = new GameObject("DamageRow");
            row.transform.SetParent(_damageListParent, false);
            row.AddComponent<RectTransform>();

            var hLayout = row.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 8;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = true;

            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 26;

            // Name
            var nameText = CreateText(row.transform, "Name", contributorName ?? "Unknown", 16, FontStyles.Normal, Color.white);
            var nameLE = nameText.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;

            // Damage amount
            string dmgStr = damage > 0 ? $"{damage:F0}" : "-";
            var dmgText = CreateText(row.transform, "Damage", dmgStr, 16, FontStyles.Bold, new Color(1f, 0.4f, 0.4f, 1f));
            dmgText.alignment = TextAlignmentOptions.Right;
            var dmgLE = dmgText.gameObject.AddComponent<LayoutElement>();
            dmgLE.preferredWidth = 80;

            // Time ago
            if (timeAgo > 0f)
            {
                var timeText = CreateText(row.transform, "Time", $"{timeAgo:F1}s ago", 14, FontStyles.Italic, new Color(0.5f, 0.5f, 0.5f, 1f));
                timeText.alignment = TextAlignmentOptions.Right;
                var timeLE = timeText.gameObject.AddComponent<LayoutElement>();
                timeLE.preferredWidth = 80;
            }

            _damageRows.Add(row);
        }

        private void ClearDamageRows()
        {
            foreach (var row in _damageRows)
            {
                if (row != null) Destroy(row);
            }
            _damageRows.Clear();
        }

        // ====================================================================
        // Utility
        // ====================================================================

        private void SetVisible(bool visible)
        {
            _fadeTarget = visible ? 1f : 0f;
            if (visible && _canvasGroup != null)
                _canvasGroup.alpha = 0f; // Start from 0 for fade-in
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

        private static void SetPreferredHeight(TextMeshProUGUI tmp, float height)
        {
            var le = tmp.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void CreateSeparator(Transform parent)
        {
            var sep = new GameObject("Separator");
            sep.transform.SetParent(parent, false);
            sep.AddComponent<RectTransform>();
            var img = sep.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.4f, 0.5f);
            var le = sep.AddComponent<LayoutElement>();
            le.preferredHeight = 1;
        }
    }
}
