using Unity.Entities;

namespace DIG.Widgets.Rendering
{
    /// <summary>
    /// EPIC 15.26 Phase 2: Adapter interface for widget rendering backends.
    /// Each existing UI system (EnemyHealthBarPool, DamageNumbersProAdapter,
    /// FloatingTextManager, InteractionRingPool) wraps in an implementation.
    /// New widget types (cast bars, buff rows, boss plates) also implement this.
    ///
    /// Lifecycle per frame:
    ///   1. OnFrameBegin() — prepare for batch
    ///   2. OnWidgetVisible() — first frame entity becomes visible
    ///   3. OnWidgetUpdate() — subsequent frames while visible (dirty-checked)
    ///   4. OnWidgetHidden() — first frame entity becomes hidden
    ///   5. OnFrameEnd() — finalize batch
    /// </summary>
    public interface IWidgetRenderer
    {
        /// <summary>Which widget type this renderer handles.</summary>
        WidgetType SupportedType { get; }

        /// <summary>Called once per frame before any widget callbacks.</summary>
        void OnFrameBegin();

        /// <summary>
        /// Called the first frame a widget becomes visible (passed budget + frustum).
        /// Use this for pool acquire / initial setup.
        /// </summary>
        void OnWidgetVisible(in WidgetRenderData data);

        /// <summary>
        /// Called each frame a widget remains visible and its data has changed
        /// (position moved >0.5px, health changed, or LOD tier changed).
        /// Skipped if nothing changed (dirty-check optimization).
        /// </summary>
        void OnWidgetUpdate(in WidgetRenderData data);

        /// <summary>
        /// Called the first frame a widget becomes hidden (budget culled, LOD culled,
        /// off-screen, or entity destroyed). Use this for pool release / fade-out.
        /// </summary>
        void OnWidgetHidden(Entity entity);

        /// <summary>Called once per frame after all widget callbacks.</summary>
        void OnFrameEnd();
    }
}
