using UnityEngine;
using Unity.Entities;
using DIG.Combat.UI;
using DIG.Widgets.Config;
using DIG.Widgets.Rendering;

namespace DIG.Widgets.Adapters
{
    /// <summary>
    /// EPIC 15.26 Phase 3: Wraps DamageNumbersProAdapter in the IWidgetRenderer pattern.
    ///
    /// Damage numbers are event-driven (not per-frame positioned), so this adapter
    /// primarily applies paradigm DamageNumberScale and accessibility font scaling.
    /// The existing DamageVisualQueue → CombatUIBridgeSystem → DamageNumbersProAdapter
    /// pipeline continues to work. This adapter hooks into the scale and culling layer.
    /// </summary>
    public class DamageNumberWidgetAdapter : MonoBehaviour, IWidgetRenderer
    {
        public WidgetType SupportedType => WidgetType.DamageNumber;

        /// <summary>
        /// Current damage number scale multiplier from the active paradigm profile.
        /// Read by DamageNumbersProAdapter if it checks this value.
        /// </summary>
        public static float CurrentDamageNumberScale { get; private set; } = 1f;

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
            // Update damage number scale from paradigm profile
            if (ParadigmWidgetConfig.HasInstance && ParadigmWidgetConfig.Instance.ActiveProfile != null)
            {
                CurrentDamageNumberScale = ParadigmWidgetConfig.Instance.ActiveProfile.DamageNumberScale;
            }
            else
            {
                CurrentDamageNumberScale = 1f;
            }
        }

        public void OnWidgetVisible(in WidgetRenderData data)
        {
            // Damage numbers are event-driven, not per-entity-per-frame.
            // The existing pipeline handles spawn. This adapter manages scale.
        }

        public void OnWidgetUpdate(in WidgetRenderData data)
        {
            // No-op: damage numbers float independently after spawn
        }

        public void OnWidgetHidden(Entity entity)
        {
            // No-op: damage numbers have their own lifetime/fade
        }

        public void OnFrameEnd()
        {
        }
    }
}
