using UnityEngine;
using Unity.Entities;
using DIG.Aggro.Components;
using DIG.Targeting;

namespace DIG.Aggro.Authoring
{
    /// <summary>
    /// EPIC 15.19 + 15.33: Authoring component that bakes aggro/threat system components.
    /// Add to AI entities that should use the threat-based targeting system.
    ///
    /// Requires: DetectionSensorAuthoring (for sight-based aggro)
    /// Optional: CombatStateAuthoring (for combat state integration)
    /// </summary>
    [AddComponentMenu("DIG/Aggro/Aggro Authoring")]
    public class AggroAuthoring : MonoBehaviour
    {
        [Header("Threat Multipliers")]
        [Tooltip("Multiplier applied to damage for threat calculation")]
        public float DamageThreatMultiplier = 1.0f;

        [Tooltip("Base threat added when first seeing a target")]
        public float SightThreatValue = 10.0f;

        [Tooltip("Base threat added when hearing a target")]
        public float HearingThreatValue = 3.0f;

        [Header("Decay Settings")]
        [Tooltip("Threat reduction per second for visible targets")]
        public float VisibleDecayRate = 0.5f;

        [Tooltip("Threat reduction per second for hidden targets")]
        public float HiddenDecayRate = 0.5f;

        [Tooltip("Time before forgetting a hidden target (seconds)")]
        public float MemoryDuration = 30.0f;

        [Header("Target Selection")]
        [Tooltip("Only switch targets if new threat exceeds current by this ratio (1.1 = 110%)")]
        [Range(1.0f, 2.0f)]
        public float HysteresisRatio = 1.1f;

        [Tooltip("Maximum number of targets to track")]
        [Range(1, 16)]
        public int MaxTrackedTargets = 8;

        [Tooltip("Minimum threat to remain in table")]
        public float MinimumThreat = 0.1f;

        [Header("Leashing & Territory")]
        [Tooltip("Max distance from spawn before dropping aggro and returning. 0 = no leash (bosses).")]
        public float LeashDistance = 50.0f;

        [Header("Social Behavior")]
        [Tooltip("Radius to alert nearby allies when aggroed. 0 = lone wolf.")]
        public float AggroShareRadius = 20.0f;

        [Tooltip("Detection multiplier when already alert/suspicious. 1.0 = normal, 1.5 = 50% better.")]
        [Range(1.0f, 3.0f)]
        public float AlertStateMultiplier = 1.5f;

        [Header("Proximity (EPIC 15.33)")]
        [Tooltip("Radius for 360-degree proximity threat (body pull). 0 = disabled.")]
        public float ProximityThreatRadius = 0f;

        [Tooltip("Threat added per second while target is within proximity radius.")]
        public float ProximityThreatPerSecond = 5.0f;

        [Header("Advanced Target Selection (EPIC 15.33)")]
        [Tooltip("How the target selector picks from the threat table.")]
        public TargetSelectionMode SelectionMode = TargetSelectionMode.HighestThreat;

        [Tooltip("Weight for distance factor in WeightedScore mode.")]
        [Range(0f, 1f)]
        public float DistanceWeight = 0f;

        [Tooltip("Weight for health factor in WeightedScore mode.")]
        [Range(0f, 1f)]
        public float HealthWeight = 0f;

        [Tooltip("Weight for recency factor in WeightedScore mode.")]
        [Range(0f, 1f)]
        public float RecencyWeight = 0f;

        [Tooltip("Minimum seconds between target switches. 0 = no cooldown.")]
        public float TargetSwitchCooldown = 0f;

        [Tooltip("Per-second probability of random target switch. 0 = deterministic.")]
        [Range(0f, 1f)]
        public float RandomSwitchChance = 0f;

        class Baker : Baker<AggroAuthoring>
        {
            public override void Bake(AggroAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new AggroConfig
                {
                    DamageThreatMultiplier = authoring.DamageThreatMultiplier,
                    SightThreatValue = authoring.SightThreatValue,
                    HearingThreatValue = authoring.HearingThreatValue,
                    VisibleDecayRate = authoring.VisibleDecayRate,
                    HiddenDecayRate = authoring.HiddenDecayRate,
                    MemoryDuration = authoring.MemoryDuration,
                    HysteresisRatio = authoring.HysteresisRatio,
                    MaxTrackedTargets = authoring.MaxTrackedTargets,
                    MinimumThreat = authoring.MinimumThreat,
                    LeashDistance = authoring.LeashDistance,
                    AggroShareRadius = authoring.AggroShareRadius,
                    AlertStateMultiplier = authoring.AlertStateMultiplier,
                    ProximityThreatRadius = authoring.ProximityThreatRadius,
                    ProximityThreatPerSecond = authoring.ProximityThreatPerSecond,
                    SelectionMode = authoring.SelectionMode,
                    DistanceWeight = authoring.DistanceWeight,
                    HealthWeight = authoring.HealthWeight,
                    RecencyWeight = authoring.RecencyWeight,
                    TargetSwitchCooldown = authoring.TargetSwitchCooldown,
                    RandomSwitchChance = authoring.RandomSwitchChance
                });

                AddComponent(entity, AggroState.Default);
                AddBuffer<ThreatEntry>(entity);
                AddComponent(entity, new SpawnPosition { IsInitialized = false });
                AddComponent(entity, AlertState.Default);
                AddBuffer<HearingEvent>(entity);
                AddComponent(entity, new TargetData());

                // EPIC 15.33: ThreatFixate — baked disabled for encounter trigger use
                AddComponent(entity, new ThreatFixate());
                SetComponentEnabled<ThreatFixate>(entity, false);
            }
        }
    }
}
