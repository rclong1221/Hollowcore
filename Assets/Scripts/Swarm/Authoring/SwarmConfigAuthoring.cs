using Unity.Entities;
using UnityEngine;
using DIG.Swarm.Components;

namespace DIG.Swarm.Authoring
{
    /// <summary>
    /// EPIC 16.2: Authoring component for the SwarmConfig singleton.
    /// Place on a single GameObject in your subscene to configure swarm behavior.
    /// </summary>
    public class SwarmConfigAuthoring : MonoBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("Minimal prefab for particle entities (not used for rendering — particles are GPU instanced)")]
        public GameObject ParticlePrefab;
        [Tooltip("Combat prefab: stripped-down enemy with Health, AIBrain(Swarm), single ability, capsule collider")]
        public GameObject CombatPrefab;

        [Header("Movement")]
        [Tooltip("Base movement speed for swarm particles (m/s)")]
        public float BaseSpeed = 3.5f;
        [Tooltip("Random speed variance (±m/s) for visual diversity")]
        public float SpeedVariance = 0.8f;

        [Header("Tier Thresholds")]
        [Tooltip("Distance to promote Particle → Aware (meters)")]
        public float AwareRange = 30f;
        [Tooltip("Distance to promote Aware → Combat (meters)")]
        public float CombatRange = 8f;
        [Tooltip("Distance to demote Combat → Aware (meters). Must be > CombatRange for hysteresis.")]
        public float DemoteRange = 15f;
        [Tooltip("Extra distance before Aware → Particle demotion (hysteresis buffer)")]
        public float AwareHysteresis = 5f;

        [Header("Hard Caps")]
        [Tooltip("Maximum simultaneous combat-tier entities (physics bodies — keep low)")]
        public int MaxCombatEntities = 20;
        [Tooltip("Maximum simultaneous aware-tier entities")]
        public int MaxAwareEntities = 100;

        [Header("Flocking")]
        [Tooltip("Minimum distance between particles before separation force applies")]
        public float SeparationRadius = 0.5f;
        [Tooltip("Separation force strength (push apart)")]
        public float SeparationWeight = 1.0f;
        [Tooltip("Cohesion force strength (pull together) — aware tier only")]
        public float CohesionWeight = 0.3f;
        [Tooltip("Alignment force strength (match neighbors' direction) — aware tier only")]
        public float AlignmentWeight = 0.2f;
        [Tooltip("Flow field following strength")]
        public float FlowFieldWeight = 2.0f;

        [Header("Noise")]
        [Tooltip("Perlin noise spatial scale (smaller = more uniform, larger = more chaotic)")]
        public float NoiseScale = 0.1f;
        [Tooltip("Perlin noise amplitude (strength of organic movement offset)")]
        public float NoiseStrength = 0.5f;

        [Header("Performance")]
        [Tooltip("Tier evaluation runs every N frames (higher = less CPU, slower reactions)")]
        public int TierEvalFrameInterval = 4;

        [Header("Combat (Promoted Entities)")]
        [Tooltip("Melee attack range for promoted swarm entities")]
        public float CombatMeleeRange = 2.0f;
        [Tooltip("Chase speed for promoted swarm entities")]
        public float CombatChaseSpeed = 5.0f;
        [Tooltip("Base damage for promoted swarm entities")]
        public float CombatDamage = 10f;
        [Tooltip("Attack cooldown for promoted swarm entities")]
        public float CombatAttackCooldown = 1.5f;

        public class Baker : Baker<SwarmConfigAuthoring>
        {
            public override void Bake(SwarmConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new SwarmConfig
                {
                    ParticlePrefab = authoring.ParticlePrefab != null
                        ? GetEntity(authoring.ParticlePrefab, TransformUsageFlags.Dynamic)
                        : Entity.Null,
                    CombatPrefab = authoring.CombatPrefab != null
                        ? GetEntity(authoring.CombatPrefab, TransformUsageFlags.Dynamic)
                        : Entity.Null,

                    BaseSpeed = authoring.BaseSpeed,
                    SpeedVariance = authoring.SpeedVariance,

                    AwareRange = authoring.AwareRange,
                    CombatRange = authoring.CombatRange,
                    DemoteRange = authoring.DemoteRange,
                    AwareHysteresis = authoring.AwareHysteresis,

                    MaxCombatEntities = authoring.MaxCombatEntities,
                    MaxAwareEntities = authoring.MaxAwareEntities,

                    SeparationRadius = authoring.SeparationRadius,
                    SeparationWeight = authoring.SeparationWeight,
                    CohesionWeight = authoring.CohesionWeight,
                    AlignmentWeight = authoring.AlignmentWeight,
                    FlowFieldWeight = authoring.FlowFieldWeight,

                    NoiseScale = authoring.NoiseScale,
                    NoiseStrength = authoring.NoiseStrength,

                    TierEvalFrameInterval = authoring.TierEvalFrameInterval,

                    CombatMeleeRange = authoring.CombatMeleeRange,
                    CombatChaseSpeed = authoring.CombatChaseSpeed,
                    CombatDamage = authoring.CombatDamage,
                    CombatAttackCooldown = authoring.CombatAttackCooldown,
                });
            }
        }
    }
}
