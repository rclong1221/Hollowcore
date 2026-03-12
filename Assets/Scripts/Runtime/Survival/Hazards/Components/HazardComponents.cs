using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Survival.Hazards
{
    /// <summary>
    /// Types of environment zones.
    /// </summary>
    public enum ZoneType : byte
    {
        Normal = 0,
        Hot = 1,
        Cold = 2,
        Radioactive = 3,
        Vacuum = 4
    }

    /// <summary>
    /// Tracks body temperature for an entity.
    /// Temperature changes based on environment zone.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct BodyTemperature : IComponentData
    {
        /// <summary>
        /// Current core temperature in Celsius (normal: 37).
        /// </summary>
        [GhostField(Quantization = 10)]
        public float Current;

        /// <summary>
        /// Target temperature from environment.
        /// </summary>
        public float TargetTemp;

        /// <summary>
        /// Rate of temperature change per second.
        /// </summary>
        public float ChangeRatePerSecond;

        /// <summary>
        /// Minimum safe temperature (default: 30C).
        /// </summary>
        public float MinSafe;

        /// <summary>
        /// Maximum safe temperature (default: 45C).
        /// </summary>
        public float MaxSafe;

        /// <summary>
        /// Damage per second when outside safe range at max severity.
        /// </summary>
        public float DamagePerSecond;

        /// <summary>
        /// Returns true if temperature is below safe range.
        /// </summary>
        public readonly bool IsCold => Current < MinSafe;

        /// <summary>
        /// Returns true if temperature is above safe range.
        /// </summary>
        public readonly bool IsHot => Current > MaxSafe;

        /// <summary>
        /// Returns true if temperature is outside safe range.
        /// </summary>
        public readonly bool IsTakingDamage => IsCold || IsHot;

        /// <summary>
        /// Returns severity (0-1) of temperature danger.
        /// </summary>
        public readonly float Severity
        {
            get
            {
                if (Current < MinSafe)
                    return math.saturate((MinSafe - Current) / 10f);
                if (Current > MaxSafe)
                    return math.saturate((Current - MaxSafe) / 10f);
                return 0f;
            }
        }

        /// <summary>
        /// Default body temperature settings.
        /// </summary>
        public static BodyTemperature Default => new()
        {
            Current = 37f,
            TargetTemp = 20f,
            ChangeRatePerSecond = 0.5f,
            MinSafe = 30f,
            MaxSafe = 45f,
            DamagePerSecond = 5f
        };
    }

    /// <summary>
    /// Client-side temperature effects state.
    /// </summary>
    public struct TemperatureEffects : IComponentData
    {
        /// <summary>
        /// True if below safe range (trigger shivering VFX).
        /// </summary>
        public bool IsCold;

        /// <summary>
        /// True if above safe range (trigger heat distortion).
        /// </summary>
        public bool IsHot;

        /// <summary>
        /// Severity (0-1) based on how far outside range.
        /// </summary>
        public float Severity;
    }

    /// <summary>
    /// Tracks suit integrity for EVA survival.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct SuitIntegrity : IComponentData
    {
        /// <summary>
        /// Current integrity (0-100).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float Current;

        /// <summary>
        /// Maximum integrity (default: 100).
        /// </summary>
        public float Max;

        /// <summary>
        /// Visual crack level (0=none, 1=minor, 2=major, 3=critical).
        /// </summary>
        [GhostField]
        public int CrackLevel;

        /// <summary>
        /// Fraction of damage that affects suit (0-1).
        /// </summary>
        public float DamageTransfer;

        /// <summary>
        /// Returns integrity as percentage (0-1).
        /// </summary>
        public readonly float Percent => Max > 0 ? Current / Max : 0f;

        /// <summary>
        /// Calculates oxygen leak multiplier (1.0 at full, up to 3.0 at 0).
        /// </summary>
        public readonly float LeakMultiplier => 1f + (1f - Percent) * 2f;

        /// <summary>
        /// Default suit integrity settings.
        /// </summary>
        public static SuitIntegrity Default => new()
        {
            Current = 100f,
            Max = 100f,
            CrackLevel = 0,
            DamageTransfer = 0.2f
        };
    }

    /// <summary>
    /// Client-side suit crack visual state.
    /// </summary>
    public struct SuitCrackVisualState : IComponentData
    {
        /// <summary>
        /// Current crack overlay level displayed.
        /// </summary>
        public int DisplayedCrackLevel;

        /// <summary>
        /// Alpha of crack overlay (0-1).
        /// </summary>
        public float CrackOverlayAlpha;
    }

    /// <summary>
    /// Defines an environment zone in the world.
    /// </summary>
    public struct EnvironmentZone : IComponentData
    {
        /// <summary>
        /// Type of zone.
        /// </summary>
        public ZoneType ZoneType;

        /// <summary>
        /// Zone temperature in Celsius.
        /// </summary>
        public float Temperature;

        /// <summary>
        /// Radiation accumulation rate per second.
        /// </summary>
        public float RadiationLevel;

        /// <summary>
        /// True if breathable (inside ship).
        /// </summary>
        public bool OxygenAvailable;

        /// <summary>
        /// Priority for overlapping zones (higher wins).
        /// </summary>
        public int Priority;
    }

    /// <summary>
    /// Tracks which environment zone an entity is currently in.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct InEnvironmentZone : IComponentData
    {
        /// <summary>
        /// Current zone entity (Entity.Null if no zone).
        /// </summary>
        [GhostField]
        public Entity ZoneEntity;

        /// <summary>
        /// Cached zone type for quick access.
        /// </summary>
        [GhostField]
        public ZoneType ZoneType;

        /// <summary>
        /// Cached zone temperature.
        /// </summary>
        [GhostField(Quantization = 10)]
        public float ZoneTemperature;

        /// <summary>
        /// Cached radiation level.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float RadiationLevel;

        /// <summary>
        /// Cached oxygen availability.
        /// </summary>
        [GhostField]
        public bool OxygenAvailable;
    }

    /// <summary>
    /// Damage event for temperature-based damage.
    /// Consumed by bridge system.
    /// </summary>
    public struct TemperatureDamageEvent : IComponentData
    {
        /// <summary>
        /// Entity taking damage.
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Amount of damage.
        /// </summary>
        public float Damage;

        /// <summary>
        /// True if cold damage, false if heat.
        /// </summary>
        public bool IsCold;
    }

    /// <summary>
    /// Request to repair suit.
    /// </summary>
    public struct SuitRepairRequest : IComponentData
    {
        /// <summary>
        /// Amount to repair per second.
        /// </summary>
        public float RepairRate;

        /// <summary>
        /// True if currently repairing.
        /// </summary>
        public bool IsRepairing;
    }

    /// <summary>
    /// Tag indicating entity is susceptible to temperature.
    /// </summary>
    public struct TemperatureSusceptible : IComponentData { }

    /// <summary>
    /// Tag indicating entity has a suit that can be damaged.
    /// </summary>
    public struct HasSuit : IComponentData { }
}
