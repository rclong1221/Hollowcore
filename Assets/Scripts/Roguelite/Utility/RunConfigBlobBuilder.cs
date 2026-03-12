using Unity.Collections;
using Unity.Entities;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.1: Burst-readable run configuration. Built once from RunConfigSO.
    /// BlobArray for per-zone difficulty — O(1) indexed lookup.
    /// </summary>
    public struct RunConfigBlob
    {
        public int ConfigId;
        public int ZoneCount;
        public float BaseZoneTimeLimit;
        public float RunTimeLimit;
        public int StartingRunCurrency;
        public int RunCurrencyPerZoneClear;
        public float MetaCurrencyConversionRate;
        public BlobArray<float> DifficultyMultiplierPerZone; // Sampled from AnimationCurve
    }

    /// <summary>
    /// EPIC 23.1: Singleton holding the baked run config blob.
    /// </summary>
    public struct RunConfigSingleton : IComponentData
    {
        public BlobAssetReference<RunConfigBlob> Config;
    }

    /// <summary>
    /// EPIC 23.1: Builds RunConfigBlob from RunConfigSO.
    /// Follows ProgressionBootstrapSystem BlobBuilder pattern.
    /// </summary>
    public static class RunConfigBlobBuilder
    {
        public static BlobAssetReference<RunConfigBlob> Build(RunConfigSO so)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<RunConfigBlob>();

            root.ConfigId = so.ConfigId;
            root.ZoneCount = so.ZoneCount;
            root.BaseZoneTimeLimit = so.BaseZoneTimeLimit;
            root.RunTimeLimit = so.RunTimeLimit;
            root.StartingRunCurrency = so.StartingRunCurrency;
            root.RunCurrencyPerZoneClear = so.RunCurrencyPerZoneClear;
            root.MetaCurrencyConversionRate = so.MetaCurrencyConversionRate;

            int zoneCount = so.ZoneCount > 0 ? so.ZoneCount : 1;
            var diffArray = builder.Allocate(ref root.DifficultyMultiplierPerZone, zoneCount);
            for (int i = 0; i < zoneCount; i++)
                diffArray[i] = so.GetDifficultyAtZone(i);

            var result = builder.CreateBlobAssetReference<RunConfigBlob>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }
    }
}
