using Unity.Profiling;

namespace DIG.Widgets.Debug
{
    /// <summary>
    /// EPIC 15.26 Phase 8: ProfilerMarker definitions for the widget framework.
    /// These markers appear in Unity Profiler → CPU Usage for frame-time analysis.
    ///
    /// Usage:
    ///   using (WidgetProfiler.Projection.Auto())
    ///   {
    ///       // projection code
    ///   }
    /// </summary>
    public static class WidgetProfiler
    {
        /// <summary>WidgetProjectionSystem.OnUpdate — VP multiply, importance, budget.</summary>
        public static readonly ProfilerMarker Projection =
            new ProfilerMarker(ProfilerCategory.Scripts, "Widget.Projection");

        /// <summary>WidgetBridgeSystem.OnUpdate — dirty-check, dispatch to renderers.</summary>
        public static readonly ProfilerMarker Bridge =
            new ProfilerMarker(ProfilerCategory.Scripts, "Widget.Bridge");

        /// <summary>WidgetStackingResolver.Resolve — overlap detection and displacement.</summary>
        public static readonly ProfilerMarker Stacking =
            new ProfilerMarker(ProfilerCategory.Scripts, "Widget.Stacking");

        /// <summary>Individual renderer OnWidgetVisible/OnWidgetUpdate/OnWidgetHidden.</summary>
        public static readonly ProfilerMarker Render =
            new ProfilerMarker(ProfilerCategory.Scripts, "Widget.Render");

        /// <summary>LOD tier computation and distance culling.</summary>
        public static readonly ProfilerMarker LOD =
            new ProfilerMarker(ProfilerCategory.Scripts, "Widget.LOD");

        /// <summary>Importance scoring and budget enforcement sort.</summary>
        public static readonly ProfilerMarker Importance =
            new ProfilerMarker(ProfilerCategory.Scripts, "Widget.Importance");
    }
}
