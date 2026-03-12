using Unity.Entities;
using Unity.NetCode;

namespace DIG.Survival.Tools
{
    /// <summary>
    /// Welder tool component for repairing ship hull and damaging creatures.
    /// Placed on welder tool entities alongside Tool, ToolDurability, ToolUsageState.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct WelderTool : IComponentData
    {
        /// <summary>
        /// Health restored to ship hull per second while welding.
        /// </summary>
        public float HealPerSecond;

        /// <summary>
        /// Damage dealt to creatures per second (defensive use).
        /// </summary>
        public float DamagePerSecond;

        /// <summary>
        /// Maximum range the welder can reach (meters).
        /// </summary>
        public float Range;

        /// <summary>
        /// Creates a default welder configuration.
        /// </summary>
        public static WelderTool Default => new()
        {
            HealPerSecond = 5f,
            DamagePerSecond = 15f,
            Range = 2f
        };
    }

    /// <summary>
    /// Tag component for entities that can be repaired by the welder.
    /// Add to ship hull sections, equipment, etc.
    /// </summary>
    public struct WeldRepairable : IComponentData
    {
        /// <summary>
        /// Current health of the repairable entity.
        /// </summary>
        public float CurrentHealth;

        /// <summary>
        /// Maximum health of the repairable entity.
        /// </summary>
        public float MaxHealth;
    }
}
