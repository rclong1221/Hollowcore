using Unity.Entities;
using Unity.NetCode;

namespace DIG.Survival.Tools
{
    /// <summary>
    /// Flashlight tool component for illumination.
    /// Placed on flashlight tool entities alongside Tool, ToolDurability, ToolUsageState.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct FlashlightTool : IComponentData
    {
        /// <summary>
        /// Reference to the light entity (for enable/disable).
        /// Set via authoring or runtime instantiation.
        /// </summary>
        public Entity LightEntity;

        /// <summary>
        /// Whether the flashlight is currently on.
        /// </summary>
        [GhostField]
        public bool IsOn;

        /// <summary>
        /// Battery drain rate per second while on.
        /// </summary>
        public float BatteryDrainPerSecond;

        /// <summary>
        /// Creates a default flashlight configuration.
        /// </summary>
        public static FlashlightTool Default => new()
        {
            LightEntity = Entity.Null,
            IsOn = false,
            BatteryDrainPerSecond = 0.5f
        };
    }

    /// <summary>
    /// Tag component for light entities that can be toggled.
    /// Add to actual Unity light entities controlled by flashlight.
    /// </summary>
    public struct ToggleableLight : IComponentData
    {
        /// <summary>
        /// True if the light should be enabled.
        /// Presentation layer reads this to control actual light.
        /// </summary>
        public bool ShouldBeEnabled;
    }
}
