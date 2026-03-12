using System.Diagnostics;
using UnityEngine;

namespace DIG.Voxel.Debug
{
    /// <summary>
    /// OPTIMIZATION 10.9.7: Lightweight profiler for voxel system operations.
    /// 
    /// Optimizations:
    /// - Fixed-size ring buffer instead of List with RemoveAt(0)
    /// - Pre-computed running average instead of LINQ
    /// - Conditional compilation: stripped in release builds
    /// - Dictionary lookups only in debug builds
    /// </summary>
    public static class VoxelProfiler
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const int RING_BUFFER_SIZE = 64; // Power of 2 for fast modulo
        private const int RING_BUFFER_MASK = RING_BUFFER_SIZE - 1;
        
        /// <summary>
        /// Fixed-size ring buffer profile data - no allocations after init.
        /// </summary>
        private class ProfileData
        {
            public float[] Samples = new float[RING_BUFFER_SIZE];
            public int WriteIndex = 0;
            public int SampleCount = 0;
            public Stopwatch Watch = new Stopwatch();
            public float MaxMs = 0f;
            public int Calls = 0;
            public float RunningSum = 0f; // Pre-computed sum for O(1) average
        }
        
        // Use array with known indices instead of Dictionary for hot paths
        // Common profiler keys mapped to indices
        private static readonly string[] _knownKeys = new string[]
        {
            "MeshSystem",       // 0
            "GenerationSystem", // 1
            "LODSystem",        // 2
            "StreamingSystem",  // 3
            "VisibilitySystem", // 4
            "FluidSystem",      // 5
            "PhysicsSystem",    // 6
            "Generation.Total", // 7
            "Generation.Complete", // 8
            "Generation.Schedule", // 9
        };
        
        private static readonly ProfileData[] _fastData = new ProfileData[10];
        private static readonly System.Collections.Generic.Dictionary<string, ProfileData> _slowData = new();
        
        static VoxelProfiler()
        {
            for (int i = 0; i < _fastData.Length; i++)
            {
                _fastData[i] = new ProfileData();
            }
        }
        
        private static int GetFastIndex(string name)
        {
            // Fast path: check known keys
            for (int i = 0; i < _knownKeys.Length; i++)
            {
                if (ReferenceEquals(name, _knownKeys[i]) || name == _knownKeys[i])
                    return i;
            }
            return -1;
        }
        
        private static ProfileData GetOrCreateData(string name)
        {
            int idx = GetFastIndex(name);
            if (idx >= 0) return _fastData[idx];
            
            // Slow path for unknown keys
            if (!_slowData.TryGetValue(name, out var data))
            {
                data = new ProfileData();
                _slowData[name] = data;
            }
            return data;
        }
        
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void BeginSample(string name)
        {
            var d = GetOrCreateData(name);
            d.Calls++;
            d.Watch.Restart();
        }
        
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void EndSample(string name)
        {
            var d = GetOrCreateData(name);
            d.Watch.Stop();
            float ms = (float)d.Watch.Elapsed.TotalMilliseconds;
            
            // Ring buffer: subtract old value, add new value to running sum
            int writeIdx = d.WriteIndex & RING_BUFFER_MASK;
            
            if (d.SampleCount >= RING_BUFFER_SIZE)
            {
                d.RunningSum -= d.Samples[writeIdx];
            }
            else
            {
                d.SampleCount++;
            }
            
            d.Samples[writeIdx] = ms;
            d.RunningSum += ms;
            d.WriteIndex++;
            
            if (ms > d.MaxMs) d.MaxMs = ms;
        }
        
        public static float GetAverageMs(string name)
        {
            var d = GetOrCreateData(name);
            if (d.SampleCount == 0) return 0f;
            return d.RunningSum / d.SampleCount;
        }
        
        public static float GetMaxMs(string name)
        {
            var d = GetOrCreateData(name);
            return d.MaxMs;
        }
        
        public static int GetCallCount(string name)
        {
            var d = GetOrCreateData(name);
            return d.Calls;
        }
        
        public static void PopulateTimings(System.Collections.Generic.Dictionary<string, float> dict)
        {
            dict.Clear();
            for (int i = 0; i < _knownKeys.Length; i++)
            {
                if (_fastData[i].SampleCount > 0)
                {
                    dict[_knownKeys[i]] = GetAverageMs(_knownKeys[i]);
                }
            }
            foreach (var kvp in _slowData)
            {
                dict[kvp.Key] = GetAverageMs(kvp.Key);
            }
        }

        [System.Obsolete("Use PopulateTimings to avoid allocation.")]
        public static System.Collections.Generic.Dictionary<string, float> GetAllTimings()
        {
            var dict = new System.Collections.Generic.Dictionary<string, float>();
            PopulateTimings(dict);
            return dict;
        }
        
        public static void Reset()
        {
            for (int i = 0; i < _fastData.Length; i++)
            {
                var d = _fastData[i];
                d.WriteIndex = 0;
                d.SampleCount = 0;
                d.MaxMs = 0f;
                d.Calls = 0;
                d.RunningSum = 0f;
            }
            _slowData.Clear();
        }
        
        public static void LogStats()
        {
            UnityEngine.Debug.Log("=== Voxel Performance Stats ===");
            for (int i = 0; i < _knownKeys.Length; i++)
            {
                var d = _fastData[i];
                if (d.SampleCount > 0)
                {
                    UnityEngine.Debug.Log($"{_knownKeys[i]}: Avg {GetAverageMs(_knownKeys[i]):F2} ms, Max {d.MaxMs:F2} ms, Calls {d.Calls}");
                }
            }
            foreach (var kvp in _slowData)
            {
                UnityEngine.Debug.Log($"{kvp.Key}: Avg {GetAverageMs(kvp.Key):F2} ms, Max {kvp.Value.MaxMs:F2} ms");
            }
        }
#else
        // Release build: all methods are no-ops
        [Conditional("UNITY_EDITOR")]
        public static void BeginSample(string name) { }
        
        [Conditional("UNITY_EDITOR")]
        public static void EndSample(string name) { }
        
        public static float GetAverageMs(string name) => 0f;
        public static float GetMaxMs(string name) => 0f;
        public static int GetCallCount(string name) => 0;
        public static void PopulateTimings(System.Collections.Generic.Dictionary<string, float> dict) { }
        public static System.Collections.Generic.Dictionary<string, float> GetAllTimings() => new();
        public static void Reset() { }
        public static void LogStats() { }
#endif
        
        // Proxy methods (always available)
        public static void Log(object message) => UnityEngine.Debug.Log(message);
        public static void LogWarning(object message) => UnityEngine.Debug.LogWarning(message);
        public static void LogError(object message) => UnityEngine.Debug.LogError(message);
        public static void DrawLine(Vector3 start, Vector3 end, Color color) => UnityEngine.Debug.DrawLine(start, end, color);
    }
}

