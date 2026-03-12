using UnityEngine;
using Unity.Entities;
using DIG.Survival.Core;
using DIG.Survival.EVA;
using DIG.Survival.Oxygen;
using DIG.Survival.Radiation;
using DIG.Survival.Environment;

namespace DIG.Survival.Authoring
{
    /// <summary>
    /// Authoring component for entities that use the survival systems.
    /// Add to player prefabs or any entity that needs oxygen, radiation, etc.
    /// </summary>
    public class SurvivalAuthoring : MonoBehaviour
    {
        [Header("EVA Capability")]
        [Tooltip("Can this entity perform EVA (survive in vacuum with suit)?")]
        public bool CanPerformEVA = true;

        [Header("EVA Movement")]
        [Tooltip("Does this entity have EVA movement modifiers?")]
        public bool HasEVAMovement = true;

        [Tooltip("Movement speed multiplier in EVA (0.6 = 60% of normal)")]
        [Range(0.1f, 1f)]
        public float EVASpeedMultiplier = 0.6f;

        [Tooltip("Jump force multiplier in EVA (0.5 = 50% of normal)")]
        [Range(0.1f, 1f)]
        public float EVAJumpMultiplier = 0.5f;

        [Tooltip("Air control multiplier in EVA (0.3 = 30% of normal)")]
        [Range(0.1f, 1f)]
        public float EVAAirControlMultiplier = 0.3f;

        [Tooltip("Gravity override in EVA (-1 = use default)")]
        public float EVAGravityOverride = -1f;

        [Header("Jetpack")]
        [Tooltip("Does this entity have a jetpack?")]
        public bool HasJetpack = true;

        [Tooltip("Maximum jetpack fuel capacity")]
        public float JetpackMaxFuel = 100f;

        [Tooltip("Vertical thrust force (m/s²)")]
        public float JetpackThrustForce = 8f;

        [Tooltip("Fuel consumption rate per second while thrusting")]
        public float JetpackFuelConsumption = 10f;

        [Tooltip("Fuel regeneration rate per second when not thrusting")]
        public float JetpackFuelRegen = 2f;

        [Tooltip("Delay before fuel starts regenerating (seconds)")]
        public float JetpackRegenDelay = 1f;

        [Header("Magnetic Boots")]
        [Tooltip("Does this entity have magnetic boots?")]
        public bool HasMagneticBoots = true;

        [Tooltip("Downward force when attached to surface (m/s²)")]
        public float MagBootAttachForce = 20f;

        [Tooltip("Raycast distance to detect metal surfaces (meters)")]
        public float MagBootDetectRange = 2f;

        [Tooltip("Velocity required to break attachment (m/s)")]
        public float MagBootDetachThreshold = 5f;

        [Header("Oxygen System")]
        [Tooltip("Does this entity consume oxygen?")]
        public bool HasOxygenTank = true;
        
        [Tooltip("Maximum oxygen capacity")]
        public float MaxOxygen = 100f;
        
        [Tooltip("Oxygen depletion rate per second in EVA")]
        public float OxygenDepletionRate = 1f;
        
        [Tooltip("Damage per second when oxygen is depleted")]
        public float SuffocationDamage = 10f;
        
        [Tooltip("Oxygen percentage to trigger warning (0-1)")]
        [Range(0f, 1f)]
        public float OxygenWarningThreshold = 0.25f;
        
        [Tooltip("Oxygen percentage to trigger critical warning (0-1)")]
        [Range(0f, 1f)]
        public float OxygenCriticalThreshold = 0.10f;

        [Header("Radiation System")]
        [Tooltip("Is this entity affected by radiation?")]
        public bool RadiationSusceptible = true;
        
        [Tooltip("Radiation level at which damage starts")]
        public float RadiationDamageThreshold = 100f;
        
        [Tooltip("Natural radiation decay rate per second")]
        public float RadiationDecayRate = 5f;
        
        [Tooltip("Damage per second when at 2x radiation threshold")]
        public float RadiationDamagePerSecond = 5f;

        [Header("Environment")]
        [Tooltip("Is this entity affected by environment zones?")]
        public bool EnvironmentSensitive = true;
    }

    /// <summary>
    /// Baker for SurvivalAuthoring.
    /// </summary>
    public class SurvivalBaker : Baker<SurvivalAuthoring>
    {
        public override void Bake(SurvivalAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Environment sensitivity (required for zone detection)
            if (authoring.EnvironmentSensitive)
            {
                AddComponent<EnvironmentSensitive>(entity);
                AddComponent(entity, CurrentEnvironmentZone.Default);
                // AddBuffer<StatefulTriggerEvent>(entity); // Removed: Switching to stateless zone detection
            }

            // EVA capability
            if (authoring.CanPerformEVA)
            {
                AddComponent<EVACapable>(entity);
                AddComponent(entity, new EVAState
                {
                    IsInEVA = false,
                    TimeInEVA = 0f,
                    TetheredToEntity = Entity.Null,
                    EnteredEVATime = 0f
                });

                // EVA movement modifiers (Epic 2.2)
                if (authoring.HasEVAMovement)
                {
                    AddComponent(entity, new EVAMovementModifier
                    {
                        SpeedMultiplier = authoring.EVASpeedMultiplier,
                        JumpForceMultiplier = authoring.EVAJumpMultiplier,
                        AirControlMultiplier = authoring.EVAAirControlMultiplier,
                        GravityOverride = authoring.EVAGravityOverride
                    });
                    AddComponent(entity, EVAOriginalMovementSettings.Default);
                }

                // Jetpack (Epic 2.2)
                if (authoring.HasJetpack)
                {
                    AddComponent(entity, new JetpackState
                    {
                        Fuel = authoring.JetpackMaxFuel,
                        MaxFuel = authoring.JetpackMaxFuel,
                        IsThrusting = false,
                        ThrustForce = authoring.JetpackThrustForce,
                        FuelConsumptionRate = authoring.JetpackFuelConsumption,
                        FuelRegenRate = authoring.JetpackFuelRegen,
                        RegenDelay = authoring.JetpackRegenDelay,
                        TimeSinceThrust = 0f
                    });
                }

                // Magnetic boots (Epic 2.2)
                if (authoring.HasMagneticBoots)
                {
                    AddComponent(entity, new MagneticBootState
                    {
                        IsEnabled = false,
                        IsAttached = false,
                        AttachedNormal = new Unity.Mathematics.float3(0, 1, 0),
                        AttachForce = authoring.MagBootAttachForce,
                        DetectRange = authoring.MagBootDetectRange,
                        DetachVelocityThreshold = authoring.MagBootDetachThreshold
                    });
                }
            }

            // Oxygen system
            if (authoring.HasOxygenTank)
            {
                AddComponent<OxygenConsumer>(entity);
                AddComponent(entity, new OxygenTank
                {
                    Current = authoring.MaxOxygen,
                    Max = authoring.MaxOxygen,
                    DepletionRatePerSecond = authoring.OxygenDepletionRate,
                    LeakMultiplier = 1f,
                    WarningThreshold = authoring.OxygenWarningThreshold,
                    CriticalThreshold = authoring.OxygenCriticalThreshold,
                    SuffocationDamagePerSecond = authoring.SuffocationDamage
                });
                AddComponent(entity, new OxygenWarningState());
            }

            // Radiation system
            if (authoring.RadiationSusceptible)
            {
                AddComponent<RadiationSusceptible>(entity);
                AddComponent(entity, new RadiationExposure
                {
                    Current = 0f,
                    DamageThreshold = authoring.RadiationDamageThreshold,
                    DecayRatePerSecond = authoring.RadiationDecayRate,
                    CurrentAccumulationRate = 0f,
                    DamagePerSecond = authoring.RadiationDamagePerSecond,
                    WarningThreshold = 0.5f
                });
                AddComponent(entity, new RadiationWarningState());
            }

            // Survival damage event (for bridge to Health system)
            // Added if any damage-causing survival system is enabled
            if (authoring.HasOxygenTank || authoring.RadiationSusceptible)
            {
                AddComponent(entity, new SurvivalDamageEvent
                {
                    PendingDamage = 0f,
                    Source = SurvivalDamageSource.None
                });
            }
        }
    }
}
