using Unity.Entities;

namespace DIG.Weather
{
    /// <summary>
    /// Applied to entities with VisionSettings by WeatherGameplaySystem.
    /// Read by DetectionSystem to scale effective detection range.
    /// </summary>
    public struct WeatherVisionModifier : IComponentData
    {
        /// <summary>1.0 = full range, less than 1.0 = reduced by fog/rain.</summary>
        public float RangeMultiplier;
    }

    /// <summary>
    /// Applied to player entities by WeatherGameplaySystem.
    /// Read by movement systems for snow/storm speed penalties.
    /// </summary>
    public struct WeatherMovementModifier : IComponentData
    {
        /// <summary>1.0 = full speed, less than 1.0 = slowed by snow/storm.</summary>
        public float SpeedMultiplier;
    }

    /// <summary>
    /// Singleton written by WeatherGameplaySystem, read by SurfaceSlipSystem
    /// to apply wet-surface friction reduction during rain/snow.
    /// </summary>
    public struct WeatherWetness : IComponentData
    {
        /// <summary>0.0 = dry, 1.0 = fully wet.</summary>
        public float Value;
    }

    /// <summary>
    /// Unmanaged singleton caching BlobAssetReferences from WeatherManagerSingleton.
    /// Enables Burst-compiled systems to read config without touching managed memory.
    /// Created by WeatherBootstrapSystem alongside the managed singleton.
    /// </summary>
    public struct WeatherConfigRefs : IComponentData
    {
        public BlobAssetReference<WeatherTransitionBlob> TransitionTable;
        public BlobAssetReference<DayNightBlob> DayNightConfig;
        public BlobAssetReference<WeatherParamsBlob> WeatherParams;
    }

    /// <summary>
    /// Unmanaged singleton for WeatherTransitionSystem mutable state.
    /// Decoupled from managed WeatherManagerSingleton so the system can be Burst-compiled.
    /// </summary>
    public struct WeatherTransitionRuntimeState : IComponentData
    {
        public uint RandomState;
        public float TimeSinceLastTransition;
        public float NextTransitionInterval;
        public float CurrentTransitionDuration;
    }
}
