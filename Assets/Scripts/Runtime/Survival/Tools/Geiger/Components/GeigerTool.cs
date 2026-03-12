using Unity.Entities;
using Unity.NetCode;

namespace DIG.Survival.Tools
{
    /// <summary>
    /// Geiger counter tool component for detecting radiation.
    /// Placed on Geiger tool entities alongside Tool, ToolDurability, ToolUsageState.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct GeigerTool : IComponentData
    {
        /// <summary>
        /// Radius around the player to scan for radiation sources (meters).
        /// </summary>
        public float ScanRadius;

        /// <summary>
        /// How often to update the radiation reading (seconds).
        /// Lower values = more responsive but more CPU.
        /// </summary>
        public float UpdateInterval;

        /// <summary>
        /// Time since last radiation scan update.
        /// </summary>
        public float TimeSinceUpdate;

        /// <summary>
        /// Last measured radiation level (for display).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float CurrentRadiationLevel;

        /// <summary>
        /// Creates a default Geiger counter configuration.
        /// </summary>
        public static GeigerTool Default => new()
        {
            ScanRadius = 10f,
            UpdateInterval = 0.25f,
            TimeSinceUpdate = 0f,
            CurrentRadiationLevel = 0f
        };
    }

    /// <summary>
    /// Client-side state for Geiger counter display.
    /// Used by presentation layer to show radiation UI.
    /// </summary>
    public struct GeigerDisplayState : IComponentData
    {
        /// <summary>
        /// The radiation level to display in UI.
        /// Interpolated for smooth visual updates.
        /// </summary>
        public float DisplayLevel;

        /// <summary>
        /// Whether the Geiger HUD should be visible.
        /// True when Geiger is the active tool.
        /// </summary>
        public bool IsVisible;
    }
}
