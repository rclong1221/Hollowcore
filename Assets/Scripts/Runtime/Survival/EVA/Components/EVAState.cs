using Unity.Entities;
using Unity.NetCode;

namespace DIG.Survival.EVA
{
    /// <summary>
    /// EVA (Extra-Vehicular Activity) state for entities that can operate in vacuum.
    /// Tracks whether entity is in EVA mode, time spent, and tether status.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct EVAState : IComponentData
    {
        /// <summary>
        /// True when entity is outside a pressurized area (in vacuum/EVA).
        /// Set by zone detection or airlock systems.
        /// </summary>
        [GhostField]
        public bool IsInEVA;

        /// <summary>
        /// Seconds spent in current EVA session. Resets when entering pressurized area.
        /// Used for mission tracking, achievements, survival challenges.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float TimeInEVA;

        /// <summary>
        /// Optional reference to connected ship entity for tethered EVA.
        /// Entity.Null if untethered (free-floating).
        /// </summary>
        [GhostField]
        public Entity TetheredToEntity;

        /// <summary>
        /// Server timestamp when EVA session started. Used for sync validation.
        /// Not replicated - server authoritative only.
        /// </summary>
        public float EnteredEVATime;
    }

    /// <summary>
    /// Tag component indicating this entity can perform EVA.
    /// Add to players, NPCs, or any entity that can survive in vacuum with proper equipment.
    /// </summary>
    public struct EVACapable : IComponentData { }
}
