using Unity.Entities;
using Unity.NetCode;

namespace DIG.Survival.Tools
{
    /// <summary>
    /// Sprayer tool component for creating blocking foam barriers.
    /// Placed on sprayer tool entities alongside Tool, ToolDurability, ToolUsageState.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct SprayerTool : IComponentData
    {
        /// <summary>
        /// Prefab entity to spawn for foam (set via authoring).
        /// </summary>
        public Entity FoamPrefab;

        /// <summary>
        /// Amount of ammo consumed per foam spray.
        /// </summary>
        public float AmmoPerShot;

        /// <summary>
        /// Maximum range the sprayer can reach (meters).
        /// </summary>
        public float Range;

        /// <summary>
        /// Minimum time between spray shots (cooldown in seconds).
        /// </summary>
        public float Cooldown;

        /// <summary>
        /// Time since last shot (for cooldown tracking).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float TimeSinceLastShot;

        /// <summary>
        /// Creates a default sprayer configuration.
        /// </summary>
        public static SprayerTool Default => new()
        {
            FoamPrefab = Entity.Null,
            AmmoPerShot = 10f,
            Range = 5f,
            Cooldown = 0.5f,
            TimeSinceLastShot = 999f // Ready to fire immediately
        };
    }

    /// <summary>
    /// Component for foam entities spawned by the sprayer.
    /// Foam blocks movement and line of sight, then dissolves over time.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct FoamEntity : IComponentData
    {
        /// <summary>
        /// Time remaining before foam dissolves (seconds).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float TimeRemaining;

        /// <summary>
        /// The player who created this foam.
        /// </summary>
        [GhostField]
        public Entity CreatorEntity;
    }
}
