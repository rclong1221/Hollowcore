using Unity.Profiling;

namespace DIG.Surface.Debug
{
    /// <summary>
    /// EPIC 15.24 Phase 12: Profiler markers and counters for surface FX pipeline.
    /// Used by SurfaceImpactPresenterSystem and RicochetPenetrationSystem.
    /// </summary>
    public static class SurfaceFXProfiler
    {
        // Profiler markers (visible in Unity Profiler timeline)
        public static readonly ProfilerMarker ImpactPresenterMarker =
            new ProfilerMarker("Surface.ImpactPresenter");

        public static readonly ProfilerMarker ProcessImpactMarker =
            new ProfilerMarker("Surface.ProcessImpact");

        public static readonly ProfilerMarker RicochetPenetrationMarker =
            new ProfilerMarker("Surface.RicochetPenetration");

        public static readonly ProfilerMarker LODComputeMarker =
            new ProfilerMarker("Surface.LODCompute");

        public static readonly ProfilerMarker DecalClusterMarker =
            new ProfilerMarker("Surface.DecalCluster");

        // Frame counters (reset each frame by presenter system)
        public static int EventsProcessedThisFrame;
        public static int EventsCulledThisFrame;
        public static int DecalsSpawnedThisFrame;
        public static int VFXSpawnedThisFrame;
        public static int RicochetsThisFrame;
        public static int PenetrationsThisFrame;
        public static int QueueDepthAtFrameStart;

        public static void ResetFrameCounters()
        {
            EventsProcessedThisFrame = 0;
            EventsCulledThisFrame = 0;
            DecalsSpawnedThisFrame = 0;
            VFXSpawnedThisFrame = 0;
            RicochetsThisFrame = 0;
            PenetrationsThisFrame = 0;
            QueueDepthAtFrameStart = 0;
        }
    }
}
