using Unity.Entities;
using Unity.NetCode;

namespace DIG.Interaction
{
    // ─────────────────────────────────────────────────────
    //  EPIC 16.1 Phase 5: Proximity Zones
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// What effect a proximity zone applies to occupants.
    /// Game-specific systems read this to decide how to apply effects.
    /// </summary>
    public enum ProximityEffect : byte
    {
        None = 0,
        Heal = 1,
        Damage = 2,
        Buff = 3,
        Debuff = 4,
        Shop = 5,
        Dialogue = 6,
        Custom = 7
    }

    /// <summary>
    /// EPIC 16.1 Phase 5: Defines a passive proximity zone on an entity.
    /// Placed on the ZONE entity (scene-placed, server-owned).
    /// Players within Radius are tracked as occupants and receive periodic effects.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ProximityZone : IComponentData
    {
        /// <summary>Detection radius around the zone entity.</summary>
        public float Radius;

        /// <summary>What effect to apply to occupants.</summary>
        public ProximityEffect Effect;

        /// <summary>Seconds between effect ticks. 0 = every frame.</summary>
        public float EffectInterval;

        /// <summary>Magnitude of the effect (heal amount, damage per tick, etc.).</summary>
        public float EffectValue;

        /// <summary>Maximum simultaneous occupants. 0 = unlimited.</summary>
        public int MaxOccupants;

        /// <summary>Whether occupants need line of sight to zone center.</summary>
        public bool RequiresLineOfSight;

        /// <summary>Whether to display a world-space radius indicator.</summary>
        public bool ShowWorldSpaceUI;

        /// <summary>Internal tick timer for effect intervals.</summary>
        [GhostField(Quantization = 100)]
        public float EffectTimer;

        /// <summary>True when a tick should be applied this frame. Reset by consumers.</summary>
        [GhostField]
        public bool EffectTickReady;

        /// <summary>How many entities are currently inside the zone.</summary>
        [GhostField]
        public int CurrentOccupantCount;
    }

    /// <summary>
    /// EPIC 16.1 Phase 5: Tracks an entity currently inside a ProximityZone.
    /// Buffer placed on the ZONE entity (NOT on ghost-replicated player entities).
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ProximityZoneOccupant : IBufferElementData
    {
        /// <summary>The entity inside the zone.</summary>
        public Entity OccupantEntity;

        /// <summary>How long this entity has been inside the zone.</summary>
        public float TimeInZone;
    }
}
