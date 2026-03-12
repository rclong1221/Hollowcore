using UnityEngine;

namespace Audio.Accessibility
{
    /// <summary>
    /// Renders the sound radar overlay on the HUD.
    /// Shows directional pips for significant sounds, color-coded by type.
    /// Reads from SoundRadarSystem.ActivePips each frame.
    /// EPIC 15.27 Phase 7.
    /// </summary>
    public class SoundRadarRenderer : MonoBehaviour
    {
        [Header("Radar Settings")]
        [Tooltip("Radar center offset from bottom-left corner (screen-space fraction)")]
        public Vector2 ScreenPosition = new Vector2(0.12f, 0.15f);

        [Tooltip("Radar radius in pixels")]
        public float BaseRadius = 60f;

        [Tooltip("Pip size in pixels")]
        public float PipSize = 8f;

        [Header("Colors")]
        public Color DangerColor = new Color(1f, 0.2f, 0.2f, 0.9f);
        public Color FriendlyColor = new Color(0.3f, 0.5f, 1f, 0.9f);
        public Color NeutralColor = new Color(1f, 0.9f, 0.3f, 0.9f);
        public Color AmbientColor = new Color(1f, 1f, 1f, 0.5f);
        public Color BackgroundColor = new Color(0f, 0f, 0f, 0.3f);

        private Texture2D _pipTexture;
        private Texture2D _bgTexture;

        private void OnEnable()
        {
            _pipTexture = new Texture2D(1, 1);
            _pipTexture.SetPixel(0, 0, Color.white);
            _pipTexture.Apply();

            _bgTexture = new Texture2D(1, 1);
            _bgTexture.SetPixel(0, 0, Color.white);
            _bgTexture.Apply();
        }

        private void OnGUI()
        {
            if (!SoundRadarSystem.IsActive) return;

            float scale = 1f; // Could read from config for RadarSize

            float radius = BaseRadius * scale;
            Vector2 center = new Vector2(
                Screen.width * ScreenPosition.x,
                Screen.height * (1f - ScreenPosition.y)
            );

            // Draw radar background circle (approximate with a square for simplicity)
            var prevColor = GUI.color;
            GUI.color = BackgroundColor;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), _bgTexture);
            GUI.color = prevColor;

            // Draw center dot (listener)
            GUI.color = Color.white;
            float dotSize = 4f;
            GUI.DrawTexture(new Rect(center.x - dotSize / 2, center.y - dotSize / 2, dotSize, dotSize), _pipTexture);

            // Draw pips
            var pips = SoundRadarSystem.ActivePips;
            for (int i = 0; i < pips.Count; i++)
            {
                var pip = pips[i];

                // Calculate pip position on radar circle
                float rad = pip.Angle * Mathf.Deg2Rad;
                float pipDist = Mathf.Clamp01(pip.Distance / 60f) * radius * 0.85f;
                float px = center.x + Mathf.Sin(rad) * pipDist;
                float py = center.y - Mathf.Cos(rad) * pipDist;

                // Select color based on type
                Color pipColor;
                switch (pip.Type)
                {
                    case SoundRadarSystem.RadarPipType.Danger: pipColor = DangerColor; break;
                    case SoundRadarSystem.RadarPipType.Friendly: pipColor = FriendlyColor; break;
                    case SoundRadarSystem.RadarPipType.Neutral: pipColor = NeutralColor; break;
                    default: pipColor = AmbientColor; break;
                }

                // Scale pip by intensity
                float size = PipSize * scale * (0.5f + pip.Intensity * 0.5f);
                pipColor.a *= pip.Intensity;
                GUI.color = pipColor;
                GUI.DrawTexture(new Rect(px - size / 2, py - size / 2, size, size), _pipTexture);
            }

            GUI.color = prevColor;
        }
    }
}
