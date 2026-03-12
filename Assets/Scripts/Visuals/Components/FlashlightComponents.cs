using Unity.Entities;
using Unity.NetCode;

namespace Visuals.Components
{
    /// <summary>
    /// Minimal flashlight visual state - replicated to ALL clients.
    /// Remote clients only need this for rendering flashlight visuals.
    /// 
    /// BANDWIDTH: ~2 bits per player (IsOn + IsFlickering)
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct FlashlightState : IComponentData
    {
        /// <summary>
        /// Whether the flashlight is currently on.
        /// </summary>
        [GhostField]
        public bool IsOn;

        /// <summary>
        /// Whether the flashlight is flickering (low battery effect).
        /// Computed server-side, replicated to all for visuals.
        /// </summary>
        [GhostField]
        public bool IsFlickering;
    }

    /// <summary>
    /// Flashlight configuration and battery state - replicated only to predicted clients.
    /// The owner needs this for simulation; remote clients don't need it.
    /// 
    /// BANDWIDTH: ~200 bits, but only sent to owner (not all clients)
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct FlashlightConfig : IComponentData
    {
        /// <summary>
        /// Current battery level in seconds.
        /// </summary>
        [GhostField]
        public float BatteryCurrent;

        /// <summary>
        /// Maximum battery capacity in seconds.
        /// </summary>
        [GhostField]
        public float BatteryMax;

        /// <summary>
        /// Light intensity (for presentation system).
        /// </summary>
        [GhostField]
        public float Intensity;

        /// <summary>
        /// Light range in meters (for presentation system).
        /// </summary>
        [GhostField]
        public float Range;

        /// <summary>
        /// Battery drain rate per second while on.
        /// </summary>
        [GhostField]
        public float DrainRate;

        /// <summary>
        /// Battery recharge rate per second while off.
        /// </summary>
        [GhostField]
        public float RechargeRate;

        /// <summary>
        /// Whether battery recharge is enabled.
        /// </summary>
        [GhostField]
        public bool RechargeEnabled;

        /// <summary>
        /// Input tracking - frame count of last toggle input.
        /// Not replicated - local only.
        /// </summary>
        public uint LastInputFrame;

        /// <summary>
        /// Battery percentage (0-1).
        /// </summary>
        public float BatteryPercent => BatteryMax > 0 ? BatteryCurrent / BatteryMax : 0f;

        /// <summary>
        /// Whether the battery is critically low (<5%).
        /// </summary>
        public bool IsLowBattery => BatteryPercent < 0.05f;
    }
}
