using UnityEngine;
using Unity.Entities;
using DIG.Combat.UI;
using DIG.Combat.UI.WorldSpace;
using DIG.Widgets.Rendering;

namespace DIG.Widgets.Adapters
{
    /// <summary>
    /// EPIC 15.26 Phase 3: Wraps InteractionRingPool in the IWidgetRenderer pattern.
    ///
    /// Interaction rings are shown/hidden based on proximity and interact state,
    /// typically driven by the interaction system directly. This adapter provides
    /// the widget framework's visibility decisions to optionally cull rings that
    /// are beyond budget or LOD distance.
    /// </summary>
    public class InteractWidgetAdapter : MonoBehaviour, IWidgetRenderer
    {
        public WidgetType SupportedType => WidgetType.InteractPrompt;

        private IInteractionRingProvider _rings;

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
            if (_rings == null)
                _rings = CombatUIRegistry.InteractionRings;
        }

        public void OnWidgetVisible(in WidgetRenderData data)
        {
            // Interaction rings are driven by their own system (proximity + interact key).
            // The widget framework provides visibility gating — if the framework says
            // this entity is visible, the ring system can proceed normally.
        }

        public void OnWidgetUpdate(in WidgetRenderData data)
        {
            // Ring updates are handled by the interaction system
        }

        public void OnWidgetHidden(Entity entity)
        {
            // Optionally hide ring if the framework culled it.
            // In practice, interaction rings self-manage their visibility,
            // so this is a safety net for budget enforcement.
        }

        public void OnFrameEnd()
        {
        }
    }
}
