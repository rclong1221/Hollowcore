using UnityEngine;
using DIG.Targeting.Theming;
using DIG.Combat.Resolvers;
using DIG.Combat.Resources;
using DIG.Weapons;

namespace DIG.Combat.Abilities
{
    /// <summary>
    /// ScriptableObject for designing a single player ability in the Inspector.
    /// Converts to the blittable AbilityDef struct for runtime use.
    ///
    /// Create via: Assets > Create > DIG/Combat/Ability Definition
    ///
    /// EPIC 18.19 - Phase 6
    /// </summary>
    [CreateAssetMenu(fileName = "NewAbility", menuName = "DIG/Combat/Ability Definition", order = 1)]
    public class AbilityDefinitionSO : ScriptableObject
    {
        // ============================================================
        // IDENTITY
        // ============================================================

        [Header("Identity")]
        [Tooltip("Unique ability ID. Must be unique across all abilities.")]
        public int abilityId;

        [Tooltip("Display name for UI.")]
        public string displayName;

        [Tooltip("Description for tooltip.")]
        [TextArea(2, 4)]
        public string description;

        [Tooltip("Ability icon for hotbar.")]
        public Sprite icon;

        [Tooltip("Ability category.")]
        public AbilityCategory category = AbilityCategory.Attack;

        [Tooltip("Which paradigms can use this ability.")]
        public AbilityParadigmFlags paradigmFlags = AbilityParadigmFlags.All;

        // ============================================================
        // TARGETING
        // ============================================================

        [Header("Targeting")]
        [Tooltip("How this ability targets.")]
        public AbilityTargetType targetType = AbilityTargetType.SingleTarget;

        [Tooltip("Maximum range in world units.")]
        public float range = 10f;

        [Tooltip("Radius for AoE/cone/cleave abilities.")]
        public float radius = 0f;

        [Tooltip("Angle in degrees for cone/cleave abilities.")]
        public float angle = 0f;

        [Tooltip("Maximum number of targets hit.")]
        public int maxTargets = 1;

        [Tooltip("Requires line of sight to target.")]
        public bool requiresLineOfSight = true;

        [Tooltip("Must have a valid target selected to cast.")]
        public bool requiresTarget = true;

        // ============================================================
        // TIMING
        // ============================================================

        [Header("Timing")]
        [Tooltip("Telegraph duration (ground indicator). 0 = no telegraph.")]
        public float telegraphDuration = 0f;

        [Tooltip("Cast time (wind-up). 0 = instant cast.")]
        public float castTime = 0f;

        [Tooltip("Active duration (damage delivery window).")]
        public float activeDuration = 0.5f;

        [Tooltip("Recovery time (post-cast). 0 = no recovery.")]
        public float recoveryTime = 0.3f;

        [Tooltip("Cooldown after use.")]
        public float cooldown = 5f;

        [Tooltip("Global cooldown (shared across all abilities).")]
        public float globalCooldown = 1f;

        [Tooltip("Tick interval for channeled abilities. 0 = single hit.")]
        public float tickInterval = 0f;

        // ============================================================
        // CHARGES
        // ============================================================

        [Header("Charges")]
        [Tooltip("Max charges (0 = standard cooldown, no charges).")]
        public int maxCharges = 0;

        [Tooltip("Time to regenerate one charge.")]
        public float chargeRegenTime = 10f;

        // ============================================================
        // COST
        // ============================================================

        [Header("Resource Cost")]
        [Tooltip("Resource type consumed by this ability.")]
        public ResourceType costResource = ResourceType.None;

        [Tooltip("When the cost is deducted.")]
        public CostTiming costTiming = CostTiming.OnCast;

        [Tooltip("Amount of resource consumed.")]
        public float costAmount = 0f;

        // ============================================================
        // DAMAGE / HEALING
        // ============================================================

        [Header("Damage / Healing")]
        [Tooltip("Base damage or healing amount.")]
        public float damageBase = 50f;

        [Tooltip("Random variance (+/- this value).")]
        public float damageVariance = 5f;

        [Tooltip("Damage type for resistance/vulnerability calculation.")]
        public DamageType damageType = DamageType.Physical;

        [Tooltip("Number of hits per activation.")]
        public int hitCount = 1;

        [Tooltip("Whether this ability can critically hit.")]
        public bool canCrit = true;

        [Tooltip("Combat resolver type.")]
        public CombatResolverType resolverType = CombatResolverType.Hybrid;

        // ============================================================
        // CAST BEHAVIOR
        // ============================================================

        [Header("Cast Behavior")]
        [Tooltip("Movement allowed during cast.")]
        public AbilityCastMovement castMovement = AbilityCastMovement.Free;

        [Tooltip("Can be interrupted by damage/CC.")]
        public bool interruptible = false;

        // ============================================================
        // MODIFIERS (ON-HIT STATUS EFFECTS)
        // ============================================================

        [Header("On-Hit Modifier 1")]
        public ModifierType modifier0Type = ModifierType.None;
        [Range(0f, 1f)] public float modifier0Chance = 0f;
        public float modifier0Duration = 0f;
        public float modifier0Intensity = 0f;

        [Header("On-Hit Modifier 2")]
        public ModifierType modifier1Type = ModifierType.None;
        [Range(0f, 1f)] public float modifier1Chance = 0f;
        public float modifier1Duration = 0f;
        public float modifier1Intensity = 0f;

        // ============================================================
        // ANIMATION
        // ============================================================

        [Header("Animation")]
        [Tooltip("Animator trigger parameter name.")]
        public string animationTrigger = "";

        // ============================================================
        // VFX
        // ============================================================

        [Header("VFX")]
        [Tooltip("VFXTypeDatabase ID for cast effect. 0 = none.")]
        public int castVFXTypeId = 0;

        [Tooltip("VFXTypeDatabase ID for impact effect. 0 = none.")]
        public int impactVFXTypeId = 0;

        // ============================================================
        // CONVERSION
        // ============================================================

        /// <summary>
        /// Converts this ScriptableObject into a blittable AbilityDef for BlobAsset storage.
        /// </summary>
        public AbilityDef ToBlobDef()
        {
            return new AbilityDef
            {
                AbilityId = abilityId,
                Category = category,
                ParadigmFlags = paradigmFlags,
                TargetType = targetType,
                Range = range,
                Radius = radius,
                Angle = angle,
                MaxTargets = maxTargets,
                RequiresLineOfSight = requiresLineOfSight,
                RequiresTarget = requiresTarget,
                TelegraphDuration = telegraphDuration,
                CastTime = castTime,
                ActiveDuration = activeDuration,
                RecoveryTime = recoveryTime,
                Cooldown = cooldown,
                GlobalCooldown = globalCooldown,
                TickInterval = tickInterval,
                MaxCharges = (byte)Mathf.Clamp(maxCharges, 0, 255),
                ChargeRegenTime = chargeRegenTime,
                CostResource = costResource,
                CostTiming = costTiming,
                CostAmount = costAmount,
                DamageBase = damageBase,
                DamageVariance = damageVariance,
                DamageType = damageType,
                HitCount = hitCount,
                CanCrit = canCrit,
                ResolverType = resolverType,
                CastMovement = castMovement,
                Interruptible = interruptible,
                Modifier0Type = modifier0Type,
                Modifier0Chance = modifier0Chance,
                Modifier0Duration = modifier0Duration,
                Modifier0Intensity = modifier0Intensity,
                Modifier1Type = modifier1Type,
                Modifier1Chance = modifier1Chance,
                Modifier1Duration = modifier1Duration,
                Modifier1Intensity = modifier1Intensity,
                AnimationTriggerHash = string.IsNullOrEmpty(animationTrigger) ? 0 : Animator.StringToHash(animationTrigger),
                CastVFXTypeId = castVFXTypeId,
                ImpactVFXTypeId = impactVFXTypeId
            };
        }
    }
}
