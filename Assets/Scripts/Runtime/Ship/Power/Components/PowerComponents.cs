using Unity.Entities;
using Unity.NetCode;

namespace DIG.Ship.Power
{
    /// <summary>
    /// Power producer component for entities that generate power (reactors, generators, solar panels).
    /// Add to any entity that produces power for the ship.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ShipPowerProducer : IComponentData
    {
        /// <summary>
        /// Maximum power output per second (in watts/units).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float MaxOutput;

        /// <summary>
        /// Current power output (may be less than max if damaged or throttled).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float CurrentOutput;

        /// <summary>
        /// Is this producer currently online?
        /// </summary>
        [GhostField]
        public bool IsOnline;

        /// <summary>
        /// Reference to the ship this producer belongs to.
        /// </summary>
        [GhostField]
        public Entity ShipEntity;

        /// <summary>
        /// Creates a default power producer.
        /// </summary>
        public static ShipPowerProducer Default(float maxOutput) => new()
        {
            MaxOutput = maxOutput,
            CurrentOutput = maxOutput,
            IsOnline = true,
            ShipEntity = Entity.Null
        };
    }

    /// <summary>
    /// Power consumer component for entities that require power.
    /// Add to systems like life support, weapons, shields, etc.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ShipPowerConsumer : IComponentData
    {
        /// <summary>
        /// Power required for full operation (in watts/units).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float RequiredPower;

        /// <summary>
        /// Priority for power allocation. Higher values get power first.
        /// Typical priorities: Life Support = 100, Engines = 80, Weapons = 60, Shields = 40
        /// </summary>
        [GhostField]
        public int Priority;

        /// <summary>
        /// Power currently allocated to this consumer (0 to RequiredPower).
        /// Set by ShipPowerAllocationSystem.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float CurrentPower;

        /// <summary>
        /// Is this consumer currently receiving full power?
        /// </summary>
        public bool IsFullyPowered => CurrentPower >= RequiredPower * 0.99f;

        /// <summary>
        /// Is this consumer receiving any power?
        /// </summary>
        public bool IsReceivingPower => CurrentPower > 0f;

        /// <summary>
        /// Is this consumer starved (receiving less than half required)?
        /// </summary>
        public bool IsStarved => CurrentPower < RequiredPower * 0.5f;

        /// <summary>
        /// Reference to the ship this consumer belongs to.
        /// </summary>
        [GhostField]
        public Entity ShipEntity;

        /// <summary>
        /// Creates a default power consumer.
        /// </summary>
        public static ShipPowerConsumer Default(float required, int priority) => new()
        {
            RequiredPower = required,
            Priority = priority,
            CurrentPower = 0f,
            ShipEntity = Entity.Null
        };
    }

    /// <summary>
    /// Aggregated power state for the entire ship.
    /// Added to ship root entity by ShipPowerAllocationSystem.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ShipPowerState : IComponentData
    {
        /// <summary>
        /// Total power produced by all online producers.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float TotalProduced;

        /// <summary>
        /// Total power demanded by all consumers.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float TotalDemand;

        /// <summary>
        /// Total power currently being consumed (allocated).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float TotalConsumed;

        /// <summary>
        /// Is the ship in a brownout state (demand > supply)?
        /// </summary>
        [GhostField]
        public bool IsBrownout;

        /// <summary>
        /// Power efficiency ratio (0.0 to 1.0).
        /// 1.0 = all demand met, 0.5 = only half demand met.
        /// </summary>
        public float PowerEfficiency => TotalDemand > 0f ? TotalConsumed / TotalDemand : 1f;

        /// <summary>
        /// Power surplus (positive) or deficit (negative).
        /// </summary>
        public float PowerBalance => TotalProduced - TotalDemand;

        /// <summary>
        /// Creates a default powered state.
        /// </summary>
        public static ShipPowerState Default => new()
        {
            TotalProduced = 0f,
            TotalDemand = 0f,
            TotalConsumed = 0f,
            IsBrownout = false
        };
    }

    /// <summary>
    /// Life support system component.
    /// Controls whether the ship interior is pressurized and safe.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct LifeSupport : IComponentData
    {
        /// <summary>
        /// Is life support currently online and functioning?
        /// Requires power and no critical damage.
        /// </summary>
        [GhostField]
        public bool IsOnline;

        /// <summary>
        /// Power required for life support to function.
        /// </summary>
        public float PowerRequired;

        /// <summary>
        /// Oxygen generation rate when online (units per second).
        /// Future use: can refill tanks or maintain atmosphere.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float OxygenGenerationRate;

        /// <summary>
        /// Reference to the interior environment zone entity.
        /// Life support status affects this zone's properties.
        /// </summary>
        [GhostField]
        public Entity InteriorZoneEntity;

        /// <summary>
        /// Reference to the ship this life support belongs to.
        /// </summary>
        [GhostField]
        public Entity ShipEntity;

        /// <summary>
        /// Is life support damaged (reduces effectiveness)?
        /// </summary>
        [GhostField]
        public bool IsDamaged;

        /// <summary>
        /// Creates default life support.
        /// </summary>
        public static LifeSupport Default(float powerRequired) => new()
        {
            IsOnline = true,
            PowerRequired = powerRequired,
            OxygenGenerationRate = 1f,
            InteriorZoneEntity = Entity.Null,
            ShipEntity = Entity.Null,
            IsDamaged = false
        };
    }

    /// <summary>
    /// Tag component for ship interior zones that can be affected by life support.
    /// Used to identify which zones should change based on life support status.
    /// </summary>
    public struct ShipInteriorZone : IComponentData
    {
        /// <summary>
        /// Reference to the ship this zone belongs to.
        /// </summary>
        public Entity ShipEntity;

        /// <summary>
        /// The environment zone entity representing this interior.
        /// </summary>
        public Entity ZoneEntity;
    }

    /// <summary>
    /// Power priority constants for common systems.
    /// </summary>
    public static class PowerPriority
    {
        public const int LifeSupport = 100;
        public const int EngineCore = 90;
        public const int Engines = 80;
        public const int Navigation = 70;
        public const int Communications = 60;
        public const int Weapons = 50;
        public const int Shields = 40;
        public const int Sensors = 30;
        public const int Lighting = 20;
        public const int Luxury = 10;
    }
}
