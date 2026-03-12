using UnityEngine;
using Unity.Entities;
using Player.Components;
using Player.Authoring; // For HitboxOwnerMarker
using Player.Systems; // For DeathPresentationState
using DIG.Combat.Components;

namespace DIG.Combat.Authoring
{
    /// <summary>
    /// EPIC 15.9: Generic damageable entity authoring.
    /// Add this to any GameObject to make it participate in the ECS damage system.
    ///
    /// Bakes: Health, DeathState, DamageEvent buffer, DamageResistance, DeathPresentationState
    ///
    /// Use for: Enemies, NPCs, Destructible Objects, Props
    /// (Players should use PlayerAuthoring which includes additional components)
    /// </summary>
    [AddComponentMenu("DIG/Combat/Damageable Authoring")]
    [DisallowMultipleComponent]
    public class DamageableAuthoring : MonoBehaviour
    {
        [Header("Health")]
        [Tooltip("Maximum health points")]
        public float MaxHealth = 100f;

        [Header("Shield (Optional)")]
        public bool UseShield = false;
        [Tooltip("Maximum shield capacity")]
        public float MaxShield = 50f;
        [Tooltip("Seconds before shield starts regenerating after damage")]
        public float ShieldRegenDelay = 3f;
        [Tooltip("Shield points regenerated per second")]
        public float ShieldRegenRate = 10f;

        [Header("Death & Respawn")]
        [Tooltip("Seconds before respawn (0 = no respawn)")]
        public float RespawnDelay = 0f;
        [Tooltip("Seconds of invulnerability after respawn")]
        public float InvulnerabilityDuration = 1f;
        [Tooltip("Automatically respawn after death")]
        public bool AutoRespawn = false;

        [Header("Damage Resistances")]
        [Tooltip("Multiplier for physical damage (1 = normal, 0.5 = 50% reduction, 0 = immune)")]
        [Range(0f, 2f)] public float PhysicalResist = 1f;
        [Range(0f, 2f)] public float HeatResist = 1f;
        [Range(0f, 2f)] public float RadiationResist = 1f;
        [Range(0f, 2f)] public float SuffocationResist = 1f;
        [Range(0f, 2f)] public float ExplosionResist = 1f;
        [Range(0f, 2f)] public float ToxicResist = 1f;

        [Header("Damage Policy (Cooldowns)")]
        [Tooltip("Enable damage cooldowns to prevent rapid damage ticks")]
        public bool UseDamagePolicy = false;
        public float PhysicalCooldown = 0f;
        public float HeatCooldown = 0.5f;
        public float RadiationCooldown = 1f;
        public float ExplosionCooldown = 0.1f;
        public float ToxicCooldown = 1f;

        [Header("Visual Feedback")]
        [Tooltip("Show world-space health bar above entity")]
        public bool ShowHealthBar = true;
        [Tooltip("Show floating damage numbers")]
        public bool ShowDamageNumbers = true;
    }

    /// <summary>
    /// Baker for DamageableAuthoring - converts MonoBehaviour data to ECS components.
    /// </summary>
    public class DamageableAuthoringBaker : Baker<DamageableAuthoring>
    {
        public override void Bake(DamageableAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Core health component
            AddComponent(entity, new Health
            {
                Current = authoring.MaxHealth,
                Max = authoring.MaxHealth
            });

            // Death state machine
            AddComponent(entity, new DeathState
            {
                Phase = DeathPhase.Alive,
                RespawnDelay = authoring.RespawnDelay,
                InvulnerabilityDuration = authoring.InvulnerabilityDuration,
                StateStartTime = 0,
                InvulnerabilityEndTime = 0
            });

            // Damage event buffer (all damage goes through this)
            AddBuffer<DamageEvent>(entity);

            // Status effect buffers — required for weapon modifier DOTs
            // (Bleed, Burn, Freeze, Shock, Poison, etc.)
            AddBuffer<StatusEffect>(entity);
            AddBuffer<StatusEffectRequest>(entity);

            // Client-side presentation state
            AddComponent(entity, DeathPresentationState.Default);

            // Damage resistances
            AddComponent(entity, new DamageResistance
            {
                PhysicalMult = authoring.PhysicalResist,
                HeatMult = authoring.HeatResist,
                RadiationMult = authoring.RadiationResist,
                SuffocationMult = authoring.SuffocationResist,
                ExplosionMult = authoring.ExplosionResist,
                ToxicMult = authoring.ToxicResist
            });

            // Damage cooldown tracking
            AddComponent(entity, default(DamageCooldown));
            AddComponent(entity, default(DamageInvulnerabilityWindow));

            // Damage policy (if enabled)
            if (authoring.UseDamagePolicy)
            {
                AddComponent(entity, new DamagePolicy
                {
                    DefaultPhysicalCooldown = authoring.PhysicalCooldown,
                    DefaultHeatCooldown = authoring.HeatCooldown,
                    DefaultRadiationCooldown = authoring.RadiationCooldown,
                    DefaultSuffocationCooldown = 0f,
                    DefaultExplosionCooldown = authoring.ExplosionCooldown,
                    DefaultToxicCooldown = authoring.ToxicCooldown
                });
            }

            // Shield (if enabled)
            if (authoring.UseShield)
            {
                AddComponent(entity, new Shield
                {
                    Current = authoring.MaxShield,
                    Max = authoring.MaxShield,
                    RegenDelay = authoring.ShieldRegenDelay,
                    RegenRate = authoring.ShieldRegenRate,
                    LastDamageTime = 0
                });
            }

            // Death events (enableable) — required by DeathTransitionSystem
            AddComponent(entity, default(WillDieEvent));
            SetComponentEnabled<WillDieEvent>(entity, false);
            AddComponent(entity, default(DiedEvent));
            SetComponentEnabled<DiedEvent>(entity, false);

            // EPIC 16.3: Corpse lifecycle — enabled by DeathTransitionSystem on death
            AddComponent(entity, default(CorpseState));
            SetComponentEnabled<CorpseState>(entity, false);

            // Tag for damageable entity queries (carries MaxHealth for runtime fixup)
            AddComponent(entity, new DamageableTag { MaxHealth = authoring.MaxHealth });

            // Auto-respawn flag
            if (authoring.AutoRespawn)
            {
                AddComponent(entity, new AutoRespawnTag());
            }

            // Visual feedback flags (consumed by MonoBehaviour bridges)
            if (authoring.ShowHealthBar)
            {
                AddComponent(entity, new ShowHealthBarTag());
            }

            if (authoring.ShowDamageNumbers)
            {
                AddComponent(entity, new ShowDamageNumbersTag());
            }

            // Reverse link: ROOT → CHILD so physics-based damage systems
            // (hitscan, projectiles, explosions) redirect compound collider hits
            // from ROOT to the hitbox owner entity where DamageEvent is handled.
            // Must be added here (not in HitboxOwnerMarker.Baker) because only
            // this baker owns the ROOT entity.
            var hitboxOwner = GetComponentInChildren<HitboxOwnerMarker>();
            if (hitboxOwner != null && hitboxOwner.gameObject != authoring.gameObject)
            {
                var childEntity = GetEntity(hitboxOwner, TransformUsageFlags.Dynamic);
                AddComponent(entity, new HitboxOwnerLink { HitboxOwner = childEntity });
            }
        }
    }
}
