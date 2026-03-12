using System.Collections.Generic;
using UnityEngine;

namespace Audio.Accessibility
{
    /// <summary>
    /// Manages directional subtitles for dialogue and NPC barks.
    /// Shows speaker name with directional arrows and distance indicators.
    /// EPIC 15.27 Phase 7.
    /// </summary>
    public class DirectionalSubtitleManager : MonoBehaviour
    {
        public static DirectionalSubtitleManager Instance { get; private set; }

        [Header("Settings")]
        public float SubtitleDuration = 4f;
        public float FadeOutTime = 0.5f;

        [Header("Display")]
        public int MaxVisibleSubtitles = 4;
        public float YSpacing = 30f;
        public float BottomMargin = 150f;

        private AudioAccessibilityConfig _config;

        public struct SubtitleEntry
        {
            public string SpeakerName;
            public string Text;
            public Vector3 SourcePosition;
            public Color SpeakerColor;
            public float TimeRemaining;
            public float Alpha;
        }

        private readonly List<SubtitleEntry> _activeSubtitles = new List<SubtitleEntry>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Show a directional subtitle from a world-space source.
        /// </summary>
        public void ShowSubtitle(string speaker, string text, Vector3 sourcePosition, Color speakerColor)
        {
            if (_config == null)
                _config = Object.FindAnyObjectByType<AudioAccessibilityConfigHolder>()?.Config;

            if (_config == null || !_config.EnableDirectionalSubtitles) return;

            _activeSubtitles.Add(new SubtitleEntry
            {
                SpeakerName = speaker,
                Text = text,
                SourcePosition = sourcePosition,
                SpeakerColor = speakerColor,
                TimeRemaining = SubtitleDuration,
                Alpha = 1f
            });

            // Trim to max
            while (_activeSubtitles.Count > MaxVisibleSubtitles)
                _activeSubtitles.RemoveAt(0);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = _activeSubtitles.Count - 1; i >= 0; i--)
            {
                var entry = _activeSubtitles[i];
                entry.TimeRemaining -= dt;

                // Fade out
                if (entry.TimeRemaining < FadeOutTime)
                    entry.Alpha = Mathf.Clamp01(entry.TimeRemaining / FadeOutTime);

                if (entry.TimeRemaining <= 0f)
                {
                    _activeSubtitles.RemoveAt(i);
                    continue;
                }

                _activeSubtitles[i] = entry;
            }
        }

        private void OnGUI()
        {
            if (_activeSubtitles.Count == 0) return;

            float fontScale = _config != null ? _config.SubtitleFontScale : 1f;
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 listenerPos = cam.transform.position;
            Vector3 listenerFwd = cam.transform.forward;

            float yPos = Screen.height - BottomMargin;

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(16 * fontScale),
                richText = true
            };

            for (int i = _activeSubtitles.Count - 1; i >= 0; i--)
            {
                var entry = _activeSubtitles[i];

                // Calculate direction
                Vector3 toSource = entry.SourcePosition - listenerPos;
                Vector3 flatDir = new Vector3(toSource.x, 0, toSource.z).normalized;
                Vector3 flatFwd = new Vector3(listenerFwd.x, 0, listenerFwd.z).normalized;
                float angle = Vector3.SignedAngle(flatFwd, flatDir, Vector3.up);

                // Direction arrow
                string arrow = GetDirectionArrow(angle);

                // Distance indicator
                float distance = toSource.magnitude;
                string distLabel = distance < 10f ? "[Near]" : distance < 30f ? "[Mid]" : "[Far]";

                // Build subtitle string
                string colorHex = ColorUtility.ToHtmlStringRGB(entry.SpeakerColor);
                string subtitle = $"{arrow} <color=#{colorHex}>{entry.SpeakerName}</color> {distLabel}: {entry.Text} {GetOppositeArrow(arrow)}";

                var prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, entry.Alpha);

                // Background
                float width = Screen.width * 0.6f;
                float height = style.fontSize + 10;
                Rect bgRect = new Rect((Screen.width - width) / 2f, yPos - height, width, height);
                GUI.Box(bgRect, GUIContent.none);

                // Text
                GUI.Label(bgRect, subtitle, style);
                GUI.color = prevColor;

                yPos -= YSpacing;
            }
        }

        private string GetDirectionArrow(float angle)
        {
            if (angle > -22.5f && angle <= 22.5f) return "\u2191"; // Up (ahead)
            if (angle > 22.5f && angle <= 67.5f) return "\u2197";  // Up-Right
            if (angle > 67.5f && angle <= 112.5f) return "\u2192";  // Right
            if (angle > 112.5f && angle <= 157.5f) return "\u2198"; // Down-Right
            if (angle > 157.5f || angle <= -157.5f) return "\u2193"; // Down (behind)
            if (angle > -157.5f && angle <= -112.5f) return "\u2199"; // Down-Left
            if (angle > -112.5f && angle <= -67.5f) return "\u2190"; // Left
            if (angle > -67.5f && angle <= -22.5f) return "\u2196";  // Up-Left
            return "\u2191";
        }

        private string GetOppositeArrow(string arrow)
        {
            switch (arrow)
            {
                case "\u2191": return "\u2193";
                case "\u2193": return "\u2191";
                case "\u2190": return "\u2192";
                case "\u2192": return "\u2190";
                case "\u2196": return "\u2198";
                case "\u2197": return "\u2199";
                case "\u2198": return "\u2196";
                case "\u2199": return "\u2197";
                default: return "";
            }
        }

        // Static helper for ECS systems
        public static void Show(string speaker, string text, Vector3 pos, Color color)
        {
            Instance?.ShowSubtitle(speaker, text, pos, color);
        }
    }
}
