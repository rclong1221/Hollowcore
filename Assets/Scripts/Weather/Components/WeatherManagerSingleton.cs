using Unity.Entities;

namespace DIG.Weather
{
    /// <summary>
    /// Managed singleton holding BlobAsset references and runtime state
    /// that cannot be ghost-replicated. Created by WeatherBootstrapSystem.
    /// </summary>
    public class WeatherManagerSingleton : IComponentData
    {
        public BlobAssetReference<WeatherTransitionBlob> TransitionTable;
        public BlobAssetReference<DayNightBlob> DayNightConfig;
        public BlobAssetReference<WeatherParamsBlob> WeatherParams;
        public uint RandomSeed;
        public float TimeSinceLastTransition;
        public float NextTransitionInterval;
        public float CurrentTransitionDuration;
        public bool IsInitialized;
    }
}
