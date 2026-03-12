using System;
using UnityEngine;

namespace DIG.Analytics
{
    [CreateAssetMenu(menuName = "DIG/Analytics/Analytics Profile")]
    public class AnalyticsProfile : ScriptableObject
    {
        public AnalyticsCategory EnabledCategories = AnalyticsCategory.All;

        [Range(0f, 1f)]
        public float GlobalSampleRate = 1.0f;

        public CategorySampleEntry[] CategorySampleRates = Array.Empty<CategorySampleEntry>();

        [Min(1f)]
        public float FlushIntervalSeconds = 30f;

        [Min(10)]
        public int BatchSize = 100;

        [Min(100)]
        public int RingBufferCapacity = 10000;

        public DispatchTargetConfig[] DispatchTargets = Array.Empty<DispatchTargetConfig>();

        public bool IncludeSuperProperties = true;
        public bool EnableDebugLogging;
    }

    [Serializable]
    public class CategorySampleEntry
    {
        public AnalyticsCategory Category = AnalyticsCategory.Session;

        [Range(0f, 1f)]
        public float SampleRate = 1.0f;
    }
}
