using UnityEngine;
using Unity.Entities;
using DIG.AI.Components;
using DIG.Player.Abilities;
using DIG.Combat.Components;
using DIG.Targeting.Theming;
using DIG.Weapons;

namespace DIG.AI.Authoring
{
    /// <summary>
    /// EPIC 15.32: Authoring component that bakes AI brain components.
    /// Add to enemy prefabs alongside AggroAuthoring and DamageableAuthoring.
    /// If no AbilityProfileAuthoring is present, generates a fallback melee ability
    /// from the Inspector attack fields (backward-compatible with 15.31 BoxingJoe).
    /// </summary>
    [AddComponentMenu("DIG/AI/AI Brain")]
    public class AIBrainAuthoring : MonoBehaviour
    {
        [Header("Archetype")]
        [Tooltip("Enemy behavior category")]
        public AIBrainArchetype Archetype = AIBrainArchetype.Melee;

        [Header("Movement")]
        [Tooltip("Movement speed when chasing target (m/s)")]
        public float ChaseSpeed = 5.0f;

        [Tooltip("Movement speed when patrolling (m/s)")]
        public float PatrolSpeed = 1.5f;

        [Tooltip("Wander radius from spawn position (meters)")]
        public float PatrolRadius = 8.0f;

        [Header("Melee Attack (used as fallback if no AbilityProfileAuthoring)")]
        [Tooltip("Attack reach (meters)")]
        public float MeleeRange = 2.5f;

        [Tooltip("Seconds between attacks")]
        public float AttackCooldown = 1.5f;

        [Tooltip("Telegraph time before hit — player readability (seconds)")]
        public float AttackWindUp = 0.4f;

        [Tooltip("Hit window duration (seconds)")]
        public float AttackActiveDuration = 0.15f;

        [Tooltip("Vulnerable period after attack (seconds)")]
        public float AttackRecovery = 0.5f;

        [Header("Damage")]
        [Tooltip("Base attack damage")]
        public float BaseDamage = 15f;

        [Tooltip("Damage variance (± this value)")]
        public float DamageVariance = 5f;

        [Tooltip("Elemental damage type")]
        public DamageType DamageType = DamageType.Physical;

        [Header("Behavior Thresholds")]
        [Tooltip("Flee below this HP percentage (0.0-1.0). Phase 2 feature.")]
        [Range(0f, 1f)]
        public float FleeHealthPercent = 0.2f;

        [Header("Combat Stats (defaults if not already baked)")]
        public float AttackPower = 5f;
        [Range(0f, 1f)]
        public float CritChance = 0.1f;
        public float CritMultiplier = 1.5f;
        public float Accuracy = 1.0f;
        public float Defense = 5f;
        [Range(0f, 1f)]
        public float Evasion = 0.05f;

        class Baker : Baker<AIBrainAuthoring>
        {
            public override void Bake(AIBrainAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new AIBrain
                {
                    Archetype = authoring.Archetype,
                    MeleeRange = authoring.MeleeRange,
                    ChaseSpeed = authoring.ChaseSpeed,
                    PatrolSpeed = authoring.PatrolSpeed,
                    PatrolRadius = authoring.PatrolRadius,
                    AttackCooldown = authoring.AttackCooldown,
                    AttackWindUp = authoring.AttackWindUp,
                    AttackActiveDuration = authoring.AttackActiveDuration,
                    AttackRecovery = authoring.AttackRecovery,
                    FleeHealthPercent = authoring.FleeHealthPercent,
                    BaseDamage = authoring.BaseDamage,
                    DamageVariance = authoring.DamageVariance,
                    DamageType = authoring.DamageType
                });

                AddComponent(entity, AIState.Default((uint)entity.GetHashCode()));

                // EPIC 15.32: AbilityExecutionState replaces AIAttackState
                AddComponent(entity, AbilityExecutionState.Default);

                // MovementOverride — enableable tag, starts disabled
                AddComponent(entity, new MovementOverride());
                SetComponentEnabled<MovementOverride>(entity, false);

                AddComponent(entity, new MoveTowardsAbility
                {
                    IsMoving = false,
                    StopDistance = 0.5f,
                    MoveSpeed = authoring.PatrolSpeed,
                    FaceTargetOnArrival = true
                });

                AddComponent(entity, new AttackStats
                {
                    AttackPower = authoring.AttackPower,
                    CritChance = authoring.CritChance,
                    CritMultiplier = authoring.CritMultiplier,
                    Accuracy = authoring.Accuracy
                });

                AddComponent(entity, new DefenseStats
                {
                    Defense = authoring.Defense,
                    Evasion = authoring.Evasion
                });

                // Only add default CombatState if no dedicated CombatStateAuthoring is present
                // (CombatStateAuthoring bakes a richer CombatState with CombatDropTime etc.)
                if (GetComponent<CombatStateAuthoring>() == null)
                {
                    AddComponent(entity, new CombatState());
                }

                // EPIC 15.32: Fallback ability generation
                // If no AbilityProfileAuthoring is present on the GameObject,
                // generate a single melee ability from the Inspector attack fields.
                // This preserves backward compatibility with EPIC 15.31 BoxingJoe.
                bool hasAbilityProfile = GetComponent<AbilityProfileAuthoring>() != null;

                if (!hasAbilityProfile)
                {
                    var abilities = AddBuffer<DIG.AI.Components.AbilityDefinition>(entity);
                    abilities.Add(DIG.AI.Components.AbilityDefinition.DefaultMelee(
                        authoring.MeleeRange,
                        authoring.AttackWindUp,
                        authoring.AttackActiveDuration,
                        authoring.AttackRecovery,
                        authoring.AttackCooldown,
                        authoring.BaseDamage,
                        authoring.DamageVariance,
                        authoring.DamageType
                    ));

                    var cooldowns = AddBuffer<AbilityCooldownState>(entity);
                    cooldowns.Add(new AbilityCooldownState
                    {
                        CooldownRemaining = 0f,
                        GlobalCooldownRemaining = 0f,
                        CooldownGroupRemaining = 0f,
                        ChargesRemaining = 0,
                        MaxCharges = 0,
                        ChargeRegenTimer = 0f
                    });
                }

                // WeaponModifier buffer for status effect passthrough
                // AbilityExecutionSystem writes modifier entries here,
                // CombatResolutionSystem reads them via WeaponEntity = aiEntity
                AddBuffer<WeaponModifier>(entity);
            }
        }
    }
}
