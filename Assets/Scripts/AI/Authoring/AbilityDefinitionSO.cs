using UnityEngine;
using DIG.AI.Components;
using DIG.Targeting.Theming;
using DIG.Combat.Resolvers;
using DIG.Combat.Resources;
using DIG.Weapons;

namespace DIG.AI.Authoring
{
    /// <summary>
    /// EPIC 15.32: Modifier entry for ability on-hit status effects.
    /// Uses existing ModifierType enum from WeaponModifier pipeline.
    /// </summary>
    [System.Serializable]
    public class AbilityModifierEntry
    {
        [Tooltip("Effect type (uses existing ModifierType enum from WeaponModifier)")]
        public ModifierType Type = ModifierType.None;

        [Range(0, 1)]
        [Tooltip("Proc chance (0 = never, 1 = always)")]
        public float Chance = 0f;

        [Tooltip("Effect duration in seconds")]
        public float Duration = 0f;

        [Tooltip("Effect severity/intensity")]
        public float Intensity = 0f;
    }

    /// <summary>
    /// EPIC 15.32: Individual ability ScriptableObject — shareable across enemy types.
    /// Created once, referenced by any number of AbilityProfileSO assets.
    /// Baker converts to ECS AbilityDefinition struct via ToDefinition().
    /// </summary>
    [CreateAssetMenu(fileName = "NewAbility", menuName = "DIG/AI/Ability Definition")]
    public class AbilityDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string AbilityName;
        [Tooltip("Unique numeric ID — used by triggers and logging")]
        public ushort AbilityId;
        [TextArea(2, 4)]
        public string Description;
        [Tooltip("Icon for Encounter Designer tool")]
        public Sprite Icon;

        [Header("Targeting")]
        public AbilityTargetingMode TargetingMode = AbilityTargetingMode.CurrentTarget;
        [Tooltip("Max engagement distance")]
        public float Range = 2.5f;
        [Tooltip("AOE radius (0 = single target)")]
        public float Radius = 0f;
        [Tooltip("Cone angle in degrees (360 = full circle)")]
        public float Angle = 360f;
        public int MaxTargets = 1;
        public bool RequiresLineOfSight = true;

        [Header("Timing")]
        [Tooltip("Wind-up / interruptible window")]
        public float CastTime = 0.4f;
        [Tooltip("Hit window / channel duration")]
        public float ActiveDuration = 0.15f;
        [Tooltip("Post-attack lockout")]
        public float RecoveryTime = 0.5f;
        [Tooltip("Per-ability cooldown")]
        public float Cooldown = 1.5f;
        [Tooltip("Shared cooldown across all abilities")]
        public float GlobalCooldown = 0.5f;
        [Tooltip("Warning time before cast (0 = no telegraph)")]
        public float TelegraphDuration = 0f;
        [Tooltip("For channeled/DoT (0 = single hit)")]
        public float TickInterval = 0f;

        [Header("Charges")]
        [Tooltip("0 = no charges. >0 = charge-based ability")]
        public int MaxCharges = 0;
        [Tooltip("Seconds per charge regeneration")]
        public float ChargeRegenTime = 0f;

        [Header("Cooldown Group")]
        [Tooltip("0 = no group. 1-255 = shared cooldown group")]
        public byte CooldownGroupId = 0;
        [Tooltip("Duration applied to all abilities sharing this group")]
        public float CooldownGroupDuration = 0f;

        [Header("Damage")]
        public float DamageBase = 15f;
        public float DamageVariance = 5f;
        public DamageType DamageType = DamageType.Physical;
        public int HitCount = 1;
        public bool CanCrit = true;
        public float HitboxMultiplier = 1.0f;
        public CombatResolverType ResolverType = CombatResolverType.Hybrid;

        [Header("On-Hit Status Effects")]
        [Tooltip("Uses existing WeaponModifier pipeline")]
        public AbilityModifierEntry PrimaryEffect = new();
        public AbilityModifierEntry SecondaryEffect = new();

        [Header("Conditions")]
        [Tooltip("Available from this encounter phase")]
        public byte PhaseMin = 0;
        [Tooltip("Available until this encounter phase")]
        public byte PhaseMax = 255;
        [Tooltip("Only usable above this HP%")]
        [Range(0, 1)] public float HPThresholdMin = 0f;
        [Tooltip("Only usable below this HP%")]
        [Range(0, 1)] public float HPThresholdMax = 1f;
        public int MinTargetsInRange = 0;

        [Header("Behavior")]
        public AbilityMovement MovementDuringCast = AbilityMovement.Locked;
        public bool Interruptible = false;
        [Tooltip("Higher = preferred in selection")]
        public float PriorityWeight = 1.0f;

        [Header("Telegraph")]
        public TelegraphShape TelegraphShape = TelegraphShape.None;
        public bool TelegraphDamageOnExpire = false;

        [Header("Animation")]
        [Tooltip("Animator trigger name (hashed during bake)")]
        public string AnimationTriggerName;

        [Header("Resource Cost (EPIC 16.8)")]
        [Tooltip("Which resource this ability costs. None = free.")]
        public ResourceType ResourceCostType = ResourceType.None;
        [Tooltip("When the resource cost is deducted")]
        public CostTiming ResourceCostTiming = CostTiming.OnCast;
        [Tooltip("Amount of resource consumed")]
        public float ResourceCostAmount = 0f;

        /// <summary>
        /// Convert to ECS AbilityDefinition struct for baking.
        /// </summary>
        public AbilityDefinition ToDefinition()
        {
            return new AbilityDefinition
            {
                AbilityId = AbilityId,
                TargetingMode = TargetingMode,
                Range = Range,
                Radius = Radius,
                Angle = Angle,
                MaxTargets = MaxTargets,
                RequiresLineOfSight = RequiresLineOfSight,
                CastTime = CastTime,
                ActiveDuration = ActiveDuration,
                RecoveryTime = RecoveryTime,
                Cooldown = Cooldown,
                GlobalCooldown = GlobalCooldown,
                TelegraphDuration = TelegraphDuration,
                TickInterval = TickInterval,
                MaxCharges = MaxCharges,
                ChargeRegenTime = ChargeRegenTime,
                CooldownGroupId = CooldownGroupId,
                CooldownGroupDuration = CooldownGroupDuration,
                DamageBase = DamageBase,
                DamageVariance = DamageVariance,
                DamageType = DamageType,
                HitCount = HitCount,
                CanCrit = CanCrit,
                HitboxMultiplier = HitboxMultiplier,
                ResolverType = ResolverType,
                Modifier0Type = PrimaryEffect != null ? PrimaryEffect.Type : ModifierType.None,
                Modifier0Chance = PrimaryEffect != null ? PrimaryEffect.Chance : 0f,
                Modifier0Duration = PrimaryEffect != null ? PrimaryEffect.Duration : 0f,
                Modifier0Intensity = PrimaryEffect != null ? PrimaryEffect.Intensity : 0f,
                Modifier1Type = SecondaryEffect != null ? SecondaryEffect.Type : ModifierType.None,
                Modifier1Chance = SecondaryEffect != null ? SecondaryEffect.Chance : 0f,
                Modifier1Duration = SecondaryEffect != null ? SecondaryEffect.Duration : 0f,
                Modifier1Intensity = SecondaryEffect != null ? SecondaryEffect.Intensity : 0f,
                PhaseMin = PhaseMin,
                PhaseMax = PhaseMax,
                HPThresholdMin = HPThresholdMin,
                HPThresholdMax = HPThresholdMax,
                MinTargetsInRange = MinTargetsInRange,
                MovementDuringCast = MovementDuringCast,
                Interruptible = Interruptible,
                PriorityWeight = PriorityWeight,
                TelegraphShape = TelegraphShape,
                TelegraphDamageOnExpire = TelegraphDamageOnExpire,
                AnimationTriggerHash = string.IsNullOrEmpty(AnimationTriggerName)
                    ? 0 : Animator.StringToHash(AnimationTriggerName),
                ResourceCostType = ResourceCostType,
                ResourceCostTiming = ResourceCostTiming,
                ResourceCostAmount = ResourceCostAmount
            };
        }
    }
}
