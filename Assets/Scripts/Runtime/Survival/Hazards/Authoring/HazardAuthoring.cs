using UnityEngine;
using Unity.Entities;

namespace DIG.Survival.Hazards.Authoring
{
    /// <summary>
    /// Authoring component for environment zone prefabs.
    /// </summary>
    public class EnvironmentZoneAuthoring : MonoBehaviour
    {
        [Header("Zone Settings")]
        [Tooltip("Type of environment zone")]
        public ZoneType ZoneType = ZoneType.Normal;

        [Tooltip("Zone temperature in Celsius")]
        public float Temperature = 20f;

        [Tooltip("Radiation accumulation rate per second (0 for no radiation)")]
        public float RadiationLevel = 0f;

        [Tooltip("True if breathable air is available")]
        public bool OxygenAvailable = true;

        [Tooltip("Priority for overlapping zones (higher wins)")]
        public int Priority = 0;
    }

    /// <summary>
    /// Baker for EnvironmentZoneAuthoring.
    /// </summary>
    public class EnvironmentZoneBaker : Baker<EnvironmentZoneAuthoring>
    {
        public override void Bake(EnvironmentZoneAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new EnvironmentZone
            {
                ZoneType = authoring.ZoneType,
                Temperature = authoring.Temperature,
                RadiationLevel = authoring.RadiationLevel,
                OxygenAvailable = authoring.OxygenAvailable,
                Priority = authoring.Priority
            });
        }
    }

    /// <summary>
    /// Authoring component for player hazard settings.
    /// Add to player prefabs to enable hazard tracking.
    /// </summary>
    public class PlayerHazardAuthoring : MonoBehaviour
    {
        [Header("Body Temperature")]
        [Tooltip("Starting body temperature (default: 37C)")]
        public float StartingTemperature = 37f;

        [Tooltip("Rate of temperature change per second")]
        public float TemperatureChangeRate = 0.5f;

        [Tooltip("Minimum safe body temperature")]
        public float MinSafeTemp = 30f;

        [Tooltip("Maximum safe body temperature")]
        public float MaxSafeTemp = 45f;

        [Tooltip("Damage per second at max temperature severity")]
        public float TemperatureDamagePerSecond = 5f;

        [Header("Suit Integrity")]
        [Tooltip("Enable suit system")]
        public bool HasSuit = true;

        [Tooltip("Starting suit integrity")]
        public float StartingSuitIntegrity = 100f;

        [Tooltip("Maximum suit integrity")]
        public float MaxSuitIntegrity = 100f;

        [Tooltip("Fraction of damage that transfers to suit (0-1)")]
        [Range(0f, 1f)]
        public float SuitDamageTransfer = 0.2f;

        [Header("Suit Repair")]
        [Tooltip("Repair rate per second when using welder")]
        public float SuitRepairRate = 10f;
    }

    /// <summary>
    /// Baker for PlayerHazardAuthoring.
    /// </summary>
    public class PlayerHazardBaker : Baker<PlayerHazardAuthoring>
    {
        public override void Bake(PlayerHazardAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add body temperature
            AddComponent(entity, new BodyTemperature
            {
                Current = authoring.StartingTemperature,
                TargetTemp = 20f,
                ChangeRatePerSecond = authoring.TemperatureChangeRate,
                MinSafe = authoring.MinSafeTemp,
                MaxSafe = authoring.MaxSafeTemp,
                DamagePerSecond = authoring.TemperatureDamagePerSecond
            });

            // Add temperature susceptible tag
            AddComponent<TemperatureSusceptible>(entity);

            // Add temperature effects for client presentation
            AddComponent(entity, new TemperatureEffects
            {
                IsCold = false,
                IsHot = false,
                Severity = 0f
            });

            // Add environment zone tracking
            AddComponent(entity, new InEnvironmentZone
            {
                ZoneEntity = Entity.Null,
                ZoneType = ZoneType.Normal,
                ZoneTemperature = 20f,
                RadiationLevel = 0f,
                OxygenAvailable = true
            });

            // Add suit if enabled
            if (authoring.HasSuit)
            {
                AddComponent<HasSuit>(entity);

                AddComponent(entity, new SuitIntegrity
                {
                    Current = authoring.StartingSuitIntegrity,
                    Max = authoring.MaxSuitIntegrity,
                    CrackLevel = 0,
                    DamageTransfer = authoring.SuitDamageTransfer
                });

                // Add suit crack visual state for client
                AddComponent(entity, new SuitCrackVisualState
                {
                    DisplayedCrackLevel = 0,
                    CrackOverlayAlpha = 0f
                });

                // Add suit repair capability
                AddComponent(entity, new SuitRepairRequest
                {
                    RepairRate = authoring.SuitRepairRate,
                    IsRepairing = false
                });
            }
        }
    }

    /// <summary>
    /// Quick presets for common zone types.
    /// </summary>
    public class EnvironmentZonePresets
    {
        /// <summary>
        /// Ship interior - normal breathable environment.
        /// </summary>
        public static EnvironmentZone ShipInterior => new()
        {
            ZoneType = ZoneType.Normal,
            Temperature = 22f,
            RadiationLevel = 0f,
            OxygenAvailable = true,
            Priority = 10
        };

        /// <summary>
        /// Space vacuum - extreme cold, no oxygen.
        /// </summary>
        public static EnvironmentZone SpaceVacuum => new()
        {
            ZoneType = ZoneType.Vacuum,
            Temperature = -270f,
            RadiationLevel = 0f,
            OxygenAvailable = false,
            Priority = 0
        };

        /// <summary>
        /// Hot zone - high temperature hazard.
        /// </summary>
        public static EnvironmentZone HotZone => new()
        {
            ZoneType = ZoneType.Hot,
            Temperature = 80f,
            RadiationLevel = 0f,
            OxygenAvailable = false,
            Priority = 5
        };

        /// <summary>
        /// Cold zone - low temperature hazard.
        /// </summary>
        public static EnvironmentZone ColdZone => new()
        {
            ZoneType = ZoneType.Cold,
            Temperature = -40f,
            RadiationLevel = 0f,
            OxygenAvailable = false,
            Priority = 5
        };

        /// <summary>
        /// Radioactive zone - radiation damage over time.
        /// </summary>
        public static EnvironmentZone RadioactiveZone => new()
        {
            ZoneType = ZoneType.Radioactive,
            Temperature = 30f,
            RadiationLevel = 10f,
            OxygenAvailable = false,
            Priority = 5
        };
    }
}
