using Unity.Entities;
using Unity.NetCode;

namespace DIG.Ship.Hull
{
    /// <summary>
    /// Represents a section of the ship's hull that can be damaged and repaired.
    /// When health drops below a threshold, it becomes breached.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ShipHullSection : IComponentData
    {
        /// <summary>
        /// Current hull integrity (HP).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float Current;

        /// <summary>
        /// Maximum hull integrity.
        /// </summary>
        public float Max;

        /// <summary>
        /// Is this section currently breached?
        /// </summary>
        [GhostField]
        public bool IsBreached;

        /// <summary>
        /// Severity of the breach (0.0 to 1.0).
        /// Used for visual effects and leak rate calculations.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float BreachSeverity;

        /// <summary>
        /// Reference to the parent ship unit.
        /// </summary>
        [GhostField]
        public Entity ShipEntity;

        public static ShipHullSection Default(float maxHp) => new()
        {
            Current = maxHp,
            Max = maxHp,
            IsBreached = false,
            BreachSeverity = 0f,
            ShipEntity = Entity.Null
        };
    }


}
