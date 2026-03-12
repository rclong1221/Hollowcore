using Unity.Entities;
using Unity.NetCode;

namespace DIG.Weather
{
    /// <summary>
    /// Server-authoritative world clock singleton (16 bytes).
    /// Ghost-replicated to all clients for presentation systems.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct WorldTimeState : IComponentData
    {
        /// <summary>Current time of day in hours (0.0 - 23.99). Wraps at 24.</summary>
        [GhostField(Quantization = 100)] public float TimeOfDay;

        /// <summary>Days elapsed since world start. Monotonically increasing.</summary>
        [GhostField] public int DayCount;

        /// <summary>Current season.</summary>
        [GhostField] public Season Season;

        /// <summary>Time scale multiplier. 1.0 = normal, 0 = frozen, 2 = double speed.</summary>
        [GhostField(Quantization = 100)] public float TimeScale;

        /// <summary>Explicit pause flag for admin/cutscene use.</summary>
        [GhostField] public bool IsPaused;
    }
}
