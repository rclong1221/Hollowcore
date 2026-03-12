using UnityEngine;
using Unity.Entities;
using DIG.Widgets.Config;
using DIG.Widgets.Rendering;

namespace DIG.Widgets.Adapters
{
    /// <summary>
    /// EPIC 15.26 Phase 3: Wraps FloatingTextManager in the IWidgetRenderer pattern.
    ///
    /// Floating text is event-driven (status applied, combat verbs, XP gains), not
    /// per-entity per-frame. This adapter primarily exposes the current font scale
    /// from paradigm profile and accessibility settings for the floating text manager
    /// to read when spawning new text instances.
    /// </summary>
    public class FloatingTextWidgetAdapter : MonoBehaviour, IWidgetRenderer
    {
        public WidgetType SupportedType => WidgetType.FloatingText;

        /// <summary>
        /// Current font scale multiplier combining paradigm and accessibility settings.
        /// FloatingTextManager reads this when spawning new text.
        /// </summary>
        public static float CurrentFontScale { get; private set; } = 1f;

        private void Awake()
        {
            WidgetRendererRegistry.Register(this);
        }

        private void OnDestroy()
        {
            WidgetRendererRegistry.Unregister(this);
        }

        public void OnFrameBegin()
        {
            float paradigmScale = 1f;
            if (ParadigmWidgetConfig.HasInstance && ParadigmWidgetConfig.Instance.ActiveProfile != null)
            {
                paradigmScale = ParadigmWidgetConfig.Instance.ActiveProfile.AccessibilityFontScale;
            }

            // Accessibility font scale applied on top (Phase 7)
            float accessibilityScale = 1f;
            if (WidgetAccessibilityManager.HasInstance)
            {
                accessibilityScale = WidgetAccessibilityManager.Instance.FontScale;
            }

            CurrentFontScale = paradigmScale * accessibilityScale;
        }

        public void OnWidgetVisible(in WidgetRenderData data)
        {
            // Floating text is event-driven, not per-entity
        }

        public void OnWidgetUpdate(in WidgetRenderData data)
        {
        }

        public void OnWidgetHidden(Entity entity)
        {
        }

        public void OnFrameEnd()
        {
        }
    }
}
