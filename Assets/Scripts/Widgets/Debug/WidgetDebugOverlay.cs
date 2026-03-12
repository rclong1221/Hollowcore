using UnityEngine;
using DIG.Widgets.Systems;
using DIG.Widgets.Rendering;
using DIG.Widgets.Config;

namespace DIG.Widgets.Debug
{
    /// <summary>
    /// EPIC 15.26 Phase 8: F10-togglable debug HUD overlay showing real-time
    /// widget framework statistics. Renders via OnGUI for zero-dependency display.
    ///
    /// Shows:
    ///   - Active/total widget counts
    ///   - Budget utilization percentage
    ///   - LOD tier distribution
    ///   - Per-type renderer counts
    ///   - Active paradigm profile name
    ///   - Projection/bridge/stacking frame times
    /// </summary>
    [AddComponentMenu("DIG/Widgets/Debug Overlay")]
    public class WidgetDebugOverlay : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.F10;
        [SerializeField] private bool _showOnStart = false;

        private bool _visible;
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;

        // LOD distribution computed each visible frame
        private int _lodFull;
        private int _lodReduced;
        private int _lodMinimal;
        private int _lodCulled;

        private void Start()
        {
            _visible = _showOnStart;
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
                _visible = !_visible;

            if (_visible)
                ComputeLODDistribution();
        }

        private void ComputeLODDistribution()
        {
            _lodFull = 0;
            _lodReduced = 0;
            _lodMinimal = 0;
            _lodCulled = 0;

            if (!WidgetProjectionSystem.FrameworkActive) return;
            var projected = WidgetProjectionSystem.ProjectedWidgets;
            if (!projected.IsCreated) return;

            using var values = projected.GetValueArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < values.Length; i++)
            {
                switch (values[i].LOD)
                {
                    case WidgetLODTier.Full: _lodFull++; break;
                    case WidgetLODTier.Reduced: _lodReduced++; break;
                    case WidgetLODTier.Minimal: _lodMinimal++; break;
                    case WidgetLODTier.Culled: _lodCulled++; break;
                }
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;

            EnsureStyles();

            float x = 10f;
            float y = 10f;
            float w = 280f;
            float lineH = 18f;
            float padding = 6f;

            // Background box
            float totalLines = 14;
            GUI.Box(new Rect(x, y, w, totalLines * lineH + padding * 2), "", _boxStyle);

            y += padding;

            // Header
            GUI.Label(new Rect(x + padding, y, w - padding * 2, lineH), "Widget Framework Debug", _headerStyle);
            y += lineH + 4f;

            // Framework active
            bool active = WidgetProjectionSystem.FrameworkActive;
            GUI.Label(new Rect(x + padding, y, w, lineH),
                $"Framework: {(active ? "<color=#4f4>ACTIVE</color>" : "<color=#f44>INACTIVE</color>")}",
                _labelStyle);
            y += lineH;

            // Paradigm
            string paradigm = "None";
            int budget = 40;
            if (ParadigmWidgetConfig.HasInstance && ParadigmWidgetConfig.Instance.ActiveProfile != null)
            {
                var profile = ParadigmWidgetConfig.Instance.ActiveProfile;
                paradigm = profile.Paradigm.ToString();
                budget = profile.MaxActiveWidgets;
            }
            GUI.Label(new Rect(x + padding, y, w, lineH), $"Paradigm: {paradigm}", _labelStyle);
            y += lineH;

            // Counts
            int totalProjected = WidgetProjectionSystem.TotalProjectedCount;
            int visible = WidgetProjectionSystem.VisibleCount;
            float utilization = budget > 0 ? (float)visible / budget * 100f : 0f;

            GUI.Label(new Rect(x + padding, y, w, lineH),
                $"Visible: {visible} / {totalProjected}  Budget: {budget}",
                _labelStyle);
            y += lineH;

            GUI.Label(new Rect(x + padding, y, w, lineH),
                $"Utilization: {utilization:F0}%",
                _labelStyle);
            y += lineH;

            // LOD distribution
            GUI.Label(new Rect(x + padding, y, w, lineH), "--- LOD Distribution ---", _labelStyle);
            y += lineH;
            GUI.Label(new Rect(x + padding, y, w, lineH),
                $"Full: {_lodFull}  Reduced: {_lodReduced}  Minimal: {_lodMinimal}  Culled: {_lodCulled}",
                _labelStyle);
            y += lineH;

            // Renderer counts
            GUI.Label(new Rect(x + padding, y, w, lineH), "--- Registered Renderers ---", _labelStyle);
            y += lineH;

            int totalRenderers = WidgetRendererRegistry.RendererCount;
            GUI.Label(new Rect(x + padding, y, w, lineH), $"Total: {totalRenderers}", _labelStyle);
            y += lineH;

            // Per-type counts
            DrawRendererCount(ref y, x + padding, w, lineH, WidgetType.HealthBar, "HealthBar");
            DrawRendererCount(ref y, x + padding, w, lineH, WidgetType.CastBar, "CastBar");
            DrawRendererCount(ref y, x + padding, w, lineH, WidgetType.BuffRow, "BuffRow");
            DrawRendererCount(ref y, x + padding, w, lineH, WidgetType.BossPlate, "BossPlate");
            DrawRendererCount(ref y, x + padding, w, lineH, WidgetType.LootLabel, "LootLabel");
        }

        private void DrawRendererCount(ref float y, float x, float w, float h, WidgetType type, string name)
        {
            bool has = WidgetRendererRegistry.HasRenderer(type);
            var renderers = WidgetRendererRegistry.GetRenderers(type);
            int count = renderers?.Count ?? 0;
            GUI.Label(new Rect(x, y, w, h),
                $"  {name}: {count} {(has ? "" : "(none)")}",
                _labelStyle);
            y += h;
        }

        private void EnsureStyles()
        {
            if (_boxStyle != null) return;

            _boxStyle = new GUIStyle(GUI.skin.box);
            var bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.8f));
            bgTex.Apply();
            _boxStyle.normal.background = bgTex;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                richText = true
            };
            _labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);

            _headerStyle = new GUIStyle(_labelStyle)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            _headerStyle.normal.textColor = new Color(0.4f, 0.85f, 1f, 1f);
        }
    }
}
