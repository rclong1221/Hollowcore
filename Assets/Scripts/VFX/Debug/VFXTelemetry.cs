using System.Threading;

namespace DIG.VFX
{
    /// <summary>
    /// EPIC 16.7: Static counters for VFX debug window and profiling.
    /// Reset each frame by VFXExecutionSystem.
    /// </summary>
    public static class VFXTelemetry
    {
        // Per-frame counters (reset each frame)
        public static int RequestedThisFrame;
        public static int ExecutedThisFrame;
        public static int CulledByBudgetThisFrame;
        public static int CulledByLODThisFrame;
        public static int PoolHitsThisFrame;

        // Per-category this frame
        public static readonly int[] RequestedPerCategory = new int[7];
        public static readonly int[] ExecutedPerCategory = new int[7];
        public static readonly int[] CulledPerCategory = new int[7];

        // Per-LOD tier this frame
        public static readonly int[] ExecutedPerLODTier = new int[4];

        // Session totals
        public static long TotalRequested;
        public static long TotalExecuted;
        public static long TotalCulled;

        public static void ResetFrame()
        {
            RequestedThisFrame = 0;
            ExecutedThisFrame = 0;
            CulledByBudgetThisFrame = 0;
            CulledByLODThisFrame = 0;
            PoolHitsThisFrame = 0;

            for (int i = 0; i < 7; i++)
            {
                RequestedPerCategory[i] = 0;
                ExecutedPerCategory[i] = 0;
                CulledPerCategory[i] = 0;
            }

            for (int i = 0; i < 4; i++)
                ExecutedPerLODTier[i] = 0;
        }
    }
}
