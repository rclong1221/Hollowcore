using UnityEngine;

namespace DIG.Surface.Debug
{
    /// <summary>
    /// EPIC 15.24 Phase 12: Toggle-able debug HUD overlay for surface FX pipeline.
    /// Shows events/frame, queue depth, decal/VFX counts, ricochet/penetration stats.
    /// Toggle with F9 key (configurable).
    /// </summary>
    public class SurfaceDebugOverlay : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.F9;
        [SerializeField] private bool _showOnStart = false;

        [Header("Appearance")]
        [SerializeField] private int _fontSize = 14;
        [SerializeField] private Color _backgroundColor = new Color(0f, 0f, 0f, 0.7f);
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _warningColor = Color.yellow;

        private bool _visible;
        private GUIStyle _boxStyle;
        private GUIStyle _textStyle;
        private GUIStyle _warningStyle;

        // Rolling averages
        private float _avgEventsPerFrame;
        private const float SmoothFactor = 0.95f;

        private void Awake()
        {
            _visible = _showOnStart;
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                _visible = !_visible;
            }

            if (_visible)
            {
                // Smooth average
                _avgEventsPerFrame = _avgEventsPerFrame * SmoothFactor +
                    SurfaceFXProfiler.EventsProcessedThisFrame * (1f - SmoothFactor);
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;

            EnsureStyles();

            float w = 280f;
            float h = 210f;
            float x = Screen.width - w - 10f;
            float y = 10f;

            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _boxStyle);

            float lineH = _fontSize + 4f;
            float cx = x + 8f;
            float cy = y + 6f;

            DrawLabel(cx, cy, "--- Surface FX Debug ---", _textStyle); cy += lineH;

            int queueDepth = SurfaceFXProfiler.QueueDepthAtFrameStart;
            var qStyle = queueDepth > 24 ? _warningStyle : _textStyle;
            DrawLabel(cx, cy, $"Queue Depth:    {queueDepth}", qStyle); cy += lineH;

            int processed = SurfaceFXProfiler.EventsProcessedThisFrame;
            var pStyle = processed >= 30 ? _warningStyle : _textStyle;
            DrawLabel(cx, cy, $"Events/Frame:   {processed}  (avg: {_avgEventsPerFrame:F1})", pStyle); cy += lineH;

            DrawLabel(cx, cy, $"Culled/Frame:   {SurfaceFXProfiler.EventsCulledThisFrame}", _textStyle); cy += lineH;
            DrawLabel(cx, cy, $"VFX Spawned:    {SurfaceFXProfiler.VFXSpawnedThisFrame}", _textStyle); cy += lineH;
            DrawLabel(cx, cy, $"Decals Spawned: {SurfaceFXProfiler.DecalsSpawnedThisFrame}", _textStyle); cy += lineH;
            DrawLabel(cx, cy, $"Ricochets:      {SurfaceFXProfiler.RicochetsThisFrame}", _textStyle); cy += lineH;
            DrawLabel(cx, cy, $"Penetrations:   {SurfaceFXProfiler.PenetrationsThisFrame}", _textStyle); cy += lineH;

            cy += 4f;
            DrawLabel(cx, cy, $"[{_toggleKey}] to toggle", _textStyle);
        }

        private void DrawLabel(float x, float y, string text, GUIStyle style)
        {
            GUI.Label(new Rect(x, y, 300f, 20f), text, style);
        }

        private void EnsureStyles()
        {
            if (_boxStyle != null) return;

            var bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, _backgroundColor);
            bgTex.Apply();

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = bgTex;

            _textStyle = new GUIStyle(GUI.skin.label);
            _textStyle.fontSize = _fontSize;
            _textStyle.normal.textColor = _textColor;
            _textStyle.font = Font.CreateDynamicFontFromOSFont("Consolas", _fontSize);

            _warningStyle = new GUIStyle(_textStyle);
            _warningStyle.normal.textColor = _warningColor;
        }
    }
}
