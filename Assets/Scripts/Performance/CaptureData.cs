using System;

namespace DIG.Performance
{
    /// <summary>
    /// Data structures for PerformanceCaptureSession.
    /// All structs are value types to avoid GC allocations during capture.
    /// </summary>

    /// <summary>
    /// Per-frame snapshot of all performance metrics.
    /// </summary>
    public struct FrameSnapshot
    {
        public float CpuFrameTimeMs;
        public float GpuFrameTimeMs;
        public float DeltaTimeMs;
        public long ManagedHeapBytes;
        public long NativeAllocBytes;
        public int GCGen0Count;
        public int GCGen1Count;
        public int GCGen2Count;
        public int DrawCalls;
        public int Batches;
        public int Triangles;
        public int SetPassCalls;
    }

    /// <summary>
    /// Statistical summary for a metric over the capture period.
    /// </summary>
    public struct MetricSummary
    {
        public float Average;
        public float Min;
        public float Max;
        public float Percentile95;
        public float Percentile99;

        public static MetricSummary Calculate(float[] data, int count)
        {
            if (count == 0)
                return default;

            var summary = new MetricSummary
            {
                Min = float.MaxValue,
                Max = float.MinValue
            };

            float sum = 0;
            for (int i = 0; i < count; i++)
            {
                float val = data[i];
                sum += val;
                if (val < summary.Min) summary.Min = val;
                if (val > summary.Max) summary.Max = val;
            }
            summary.Average = sum / count;

            // Sort for percentiles (copy to avoid mutating original)
            var sorted = new float[count];
            Array.Copy(data, sorted, count);
            Array.Sort(sorted);

            summary.Percentile95 = GetPercentile(sorted, 0.95f);
            summary.Percentile99 = GetPercentile(sorted, 0.99f);

            return summary;
        }

        private static float GetPercentile(float[] sortedData, float percentile)
        {
            if (sortedData.Length == 0) return 0;
            float index = percentile * (sortedData.Length - 1);
            int lower = (int)index;
            int upper = Math.Min(lower + 1, sortedData.Length - 1);
            float fraction = index - lower;
            return sortedData[lower] * (1 - fraction) + sortedData[upper] * fraction;
        }
    }

    /// <summary>
    /// Timing data for a specific system.
    /// </summary>
    public struct SystemTiming
    {
        public string Name;
        public float TotalMs;
        public float MaxMs;
        public int SampleCount;

        public float AverageMs => SampleCount > 0 ? TotalMs / SampleCount : 0;
    }

    /// <summary>
    /// Memory snapshot at a point in time.
    /// </summary>
    public struct MemorySnapshot
    {
        public float TimeSeconds;
        public long ManagedBytes;
        public long NativeBytes;
    }

    /// <summary>
    /// Memory trend classification.
    /// </summary>
    public enum MemoryTrend
    {
        Stable,
        Growing,
        Shrinking
    }

    /// <summary>
    /// Per-second aggregate for timeline display.
    /// </summary>
    public struct SecondAggregate
    {
        public int SecondIndex;
        public float AvgFps;
        public float AvgCpuMs;
        public float AvgGpuMs;
        public int AvgDrawCalls;
        public float MemoryMB;
        public int FrameCount;
    }
}
