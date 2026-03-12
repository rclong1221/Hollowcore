using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Voxel.Debug;

namespace DIG.Voxel.Systems
{
    /// <summary>
    /// OPTIMIZATION 10.9.17: Frame Time Budgeting System
    /// 
    /// Provides global coordination of frame budget across voxel systems.
    /// Systems query remaining budget before performing expensive work.
    /// Auto-throttles when approaching 60 FPS target (16.67ms).
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial class FrameBudgetSystem : SystemBase
    {
        // Budget configuration
        private const float TARGET_FRAME_TIME_MS = 16.67f; // 60 FPS
        private const float BUDGET_RESERVE_MS = 2.0f; // Reserve for render/vsync
        private const float TOTAL_BUDGET_MS = TARGET_FRAME_TIME_MS - BUDGET_RESERVE_MS; // ~14.67ms
        
        // Per-system budget allocation (percentages of total)
        private const float GENERATION_BUDGET_PERCENT = 0.25f; // 25% for chunk generation
        private const float MESHING_BUDGET_PERCENT = 0.35f;    // 35% for meshing
        private const float VISIBILITY_BUDGET_PERCENT = 0.10f; // 10% for visibility
        private const float OTHER_BUDGET_PERCENT = 0.30f;      // 30% for everything else
        
        // Runtime tracking
        private double _frameStartTime;
        private float _usedBudgetMs;
        private float _lastFrameTimeMs;
        
        // Smoothed frame time for adaptive throttling
        private float _smoothedFrameTimeMs;
        private const float SMOOTHING_FACTOR = 0.1f;
        
        // Singleton data exposed to other systems
        public static FrameBudgetSystem Instance { get; private set; }
        
        /// <summary>
        /// Total budget available this frame (ms).
        /// </summary>
        public float TotalBudgetMs => TOTAL_BUDGET_MS;
        
        /// <summary>
        /// Budget already used this frame (ms).
        /// </summary>
        public float UsedBudgetMs => _usedBudgetMs;
        
        /// <summary>
        /// Remaining budget this frame (ms).
        /// </summary>
        public float RemainingBudgetMs => math.max(0f, TOTAL_BUDGET_MS - _usedBudgetMs);
        
        /// <summary>
        /// Last frame's total time (ms).
        /// </summary>
        public float LastFrameTimeMs => _lastFrameTimeMs;
        
        /// <summary>
        /// Smoothed frame time for trend analysis (ms).
        /// </summary>
        public float SmoothedFrameTimeMs => _smoothedFrameTimeMs;
        
        /// <summary>
        /// Whether the system is currently throttling due to high frame times.
        /// </summary>
        public bool IsThrottling => _smoothedFrameTimeMs > TARGET_FRAME_TIME_MS;
        
        /// <summary>
        /// Get the budget allocated to a specific system category (ms).
        /// </summary>
        public float GetSystemBudgetMs(SystemCategory category)
        {
            float percent = category switch
            {
                SystemCategory.Generation => GENERATION_BUDGET_PERCENT,
                SystemCategory.Meshing => MESHING_BUDGET_PERCENT,
                SystemCategory.Visibility => VISIBILITY_BUDGET_PERCENT,
                _ => OTHER_BUDGET_PERCENT
            };
            
            // When throttling, reduce budget further
            float multiplier = IsThrottling ? 0.5f : 1.0f;
            return TOTAL_BUDGET_MS * percent * multiplier;
        }
        
        /// <summary>
        /// Check if there's enough budget remaining for work.
        /// </summary>
        public bool HasBudget(float requiredMs = 0.5f)
        {
            return RemainingBudgetMs >= requiredMs;
        }
        
        /// <summary>
        /// Check if a system category has budget remaining.
        /// </summary>
        public bool HasSystemBudget(SystemCategory category, float usedBySystem)
        {
            return usedBySystem < GetSystemBudgetMs(category);
        }
        
        /// <summary>
        /// Record budget usage. Call when expensive work is done.
        /// </summary>
        public void RecordUsage(float usedMs)
        {
            _usedBudgetMs += usedMs;
        }
        
        /// <summary>
        /// Get elapsed time since frame start (ms).
        /// Useful for systems to track their own usage.
        /// </summary>
        public float GetElapsedMs()
        {
            return (float)((UnityEngine.Time.realtimeSinceStartupAsDouble - _frameStartTime) * 1000.0);
        }
        
        protected override void OnCreate()
        {
            Instance = this;
            _smoothedFrameTimeMs = TARGET_FRAME_TIME_MS;
        }
        
        protected override void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
        
        protected override void OnUpdate()
        {
            using var _ = VoxelProfilerMarkers.FrameBudget.Auto();

            // Record last frame's total time
            _lastFrameTimeMs = UnityEngine.Time.deltaTime * 1000f;
            
            // Update smoothed frame time (exponential moving average)
            _smoothedFrameTimeMs = math.lerp(_smoothedFrameTimeMs, _lastFrameTimeMs, SMOOTHING_FACTOR);
            
            // Reset for new frame
            _frameStartTime = UnityEngine.Time.realtimeSinceStartupAsDouble;
            _usedBudgetMs = 0f;
        }
    }
    
    /// <summary>
    /// Categories of voxel systems for budget allocation.
    /// </summary>
    public enum SystemCategory
    {
        Generation,
        Meshing,
        Visibility,
        Streaming,
        Physics,
        Decorators,
        Other
    }
}
