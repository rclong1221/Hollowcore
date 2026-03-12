using Unity.Profiling;

namespace DIG.AI.Profiling
{
    /// <summary>
    /// EPIC 15.23: Profiler markers for AI performance systems.
    /// Usage: using (AIProfilerMarkers.EnemySeparation.Auto()) { ... }
    /// </summary>
    public static class AIProfilerMarkers
    {
        public static readonly ProfilerMarker EnemySeparation =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.AI.Enemy.Separation");

        public static readonly ProfilerMarker EnemyCollisionFilter =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.AI.Enemy.CollisionFilter");
    }
}
