using Unity.Entities;
using DIG.Targeting.Theming;
using DIG.Combat.Resolvers;
using DIG.Combat.Resources;
using DIG.Weapons;

namespace DIG.AI.Components
{
    /// <summary>
    /// EPIC 15.32: How an ability selects its targets.
    /// </summary>
    public enum AbilityTargetingMode : byte
    {
        Self = 0,             // Buff/heal on self
        CurrentTarget = 1,    // Whoever aggro system selected
        HighestThreat = 2,    // Highest threat (may differ from current target)
        LowestHP = 3,         // Weakest player
        RandomPlayer = 4,     // Random valid target
        AllInRange = 5,       // Every entity within Range
        GroundAtTarget = 6,   // AOE centered on target position
        GroundAtSelf = 7,     // AOE centered on self
        Cone = 8,             // Cone in facing direction
        Line = 9,             // Line from self toward target
        Ring = 10             // Donut around self
    }

    /// <summary>
    /// EPIC 15.32: Movement behavior during ability cast.
    /// </summary>
    public enum AbilityMovement : byte
    {
        Free = 0,             // Can move during cast
        Locked = 1,           // Rooted during cast
        Slowed = 2            // 50% speed during cast
    }

    /// <summary>
    /// EPIC 15.32: Shape of telegraph ground indicator.
    /// </summary>
    public enum TelegraphShape : byte
    {
        None = 0,
        Circle = 1,
        Cone = 2,
        Line = 3,
        Ring = 4,
        Cross = 5
    }

    /// <summary>
    /// EPIC 15.32: Selection mode for AI ability picking.
    /// </summary>
    public enum AbilitySelectionMode : byte
    {
        Priority = 0,   // First valid ability wins (ordered list)
        Utility = 1     // Weighted score, highest wins (adaptive)
    }

    /// <summary>
    /// EPIC 15.32: Per-ability data definition, baked from AbilityDefinitionSO via AbilityProfileAuthoring.
    /// Read-only at runtime. One entry per ability in the enemy's rotation.
    /// Abilities carry up to 2 on-hit modifier slots that flow through existing WeaponModifier pipeline.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct AbilityDefinition : IBufferElementData
    {
        // Identity
        public ushort AbilityId;

        // Targeting
        public AbilityTargetingMode TargetingMode;
        public float Range;
        public float Radius;
        public float Angle;
        public int MaxTargets;
        public bool RequiresLineOfSight;

        // Timing
        public float CastTime;
        public float ActiveDuration;
        public float RecoveryTime;
        public float Cooldown;
        public float GlobalCooldown;
        public float TelegraphDuration;
        public float TickInterval;

        // Charges
        public int MaxCharges;
        public float ChargeRegenTime;

        // Cooldown Group
        public byte CooldownGroupId;
        public float CooldownGroupDuration;

        // Damage
        public float DamageBase;
        public float DamageVariance;
        public DamageType DamageType;
        public int HitCount;
        public bool CanCrit;
        public float HitboxMultiplier;
        public CombatResolverType ResolverType;

        // Status Effects (Modifiers) — flows through existing WeaponModifier pipeline
        public ModifierType Modifier0Type;
        public float Modifier0Chance;
        public float Modifier0Duration;
        public float Modifier0Intensity;
        public ModifierType Modifier1Type;
        public float Modifier1Chance;
        public float Modifier1Duration;
        public float Modifier1Intensity;

        // Conditions
        public byte PhaseMin;
        public byte PhaseMax;
        public float HPThresholdMin;
        public float HPThresholdMax;
        public int MinTargetsInRange;

        // Behavior
        public AbilityMovement MovementDuringCast;
        public bool Interruptible;
        public float PriorityWeight;

        // Telegraph
        public TelegraphShape TelegraphShape;
        public bool TelegraphDamageOnExpire;

        // Animation
        public int AnimationTriggerHash;

        // Resource Cost (EPIC 16.8)
        public ResourceType ResourceCostType;
        public CostTiming ResourceCostTiming;
        public float ResourceCostAmount;

        public static AbilityDefinition DefaultMelee(float range, float castTime, float activeDuration,
            float recoveryTime, float cooldown, float damageBase, float damageVariance, DamageType damageType)
        {
            return new AbilityDefinition
            {
                AbilityId = 0,
                TargetingMode = AbilityTargetingMode.CurrentTarget,
                Range = range,
                Radius = 0f,
                Angle = 360f,
                MaxTargets = 1,
                RequiresLineOfSight = true,
                CastTime = castTime,
                ActiveDuration = activeDuration,
                RecoveryTime = recoveryTime,
                Cooldown = cooldown,
                GlobalCooldown = 0.5f,
                TelegraphDuration = 0f,
                TickInterval = 0f,
                MaxCharges = 0,
                ChargeRegenTime = 0f,
                CooldownGroupId = 0,
                CooldownGroupDuration = 0f,
                DamageBase = damageBase,
                DamageVariance = damageVariance,
                DamageType = damageType,
                HitCount = 1,
                CanCrit = true,
                HitboxMultiplier = 1.0f,
                ResolverType = CombatResolverType.Hybrid,
                Modifier0Type = ModifierType.None,
                Modifier1Type = ModifierType.None,
                PhaseMin = 0,
                PhaseMax = 255,
                HPThresholdMin = 0f,
                HPThresholdMax = 1f,
                MinTargetsInRange = 0,
                MovementDuringCast = AbilityMovement.Locked,
                Interruptible = false,
                PriorityWeight = 1.0f,
                TelegraphShape = TelegraphShape.None,
                TelegraphDamageOnExpire = false,
                AnimationTriggerHash = 0,
                ResourceCostType = ResourceType.None,
                ResourceCostTiming = CostTiming.OnCast,
                ResourceCostAmount = 0f
            };
        }
    }
}
