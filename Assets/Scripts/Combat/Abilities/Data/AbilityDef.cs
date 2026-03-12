using Unity.Entities;
using DIG.Targeting.Theming;
using DIG.Combat.Resolvers;
using DIG.Combat.Resources;
using DIG.Weapons;

namespace DIG.Combat.Abilities
{
    /// <summary>
    /// Category of player ability.
    /// </summary>
    public enum AbilityCategory : byte
    {
        Attack   = 0,
        Heal     = 1,
        Buff     = 2,
        Debuff   = 3,
        Utility  = 4,
        Movement = 5
    }

    /// <summary>
    /// How an ability selects/shapes its target area.
    /// </summary>
    public enum AbilityTargetType : byte
    {
        Self         = 0,   // Buff/heal on self
        SingleTarget = 1,   // One enemy (from TargetData.TargetEntity)
        GroundTarget = 2,   // AOE at TargetData.TargetPoint
        Cone         = 3,   // Cone in aim direction
        Line         = 4,   // Line from self toward target
        AoE          = 5,   // Circle around self
        Cleave       = 6,   // Arc in front
        Projectile   = 7    // Spawns projectile entity
    }

    /// <summary>
    /// Movement behavior during ability cast.
    /// </summary>
    public enum AbilityCastMovement : byte
    {
        Free   = 0,   // Full movement
        Slowed = 1,   // Reduced speed
        Rooted = 2    // Cannot move
    }

    /// <summary>
    /// Bitmask for paradigm compatibility.
    /// </summary>
    [System.Flags]
    public enum AbilityParadigmFlags : byte
    {
        None      = 0,
        Shooter   = 1 << 0,
        MMO       = 1 << 1,
        ARPG      = 1 << 2,
        MOBA      = 1 << 3,
        TwinStick = 1 << 4,
        All       = 0xFF
    }

    /// <summary>
    /// Blittable ability definition for BlobAsset storage.
    /// Contains all static data for a player ability: timing, cost, targeting, damage, etc.
    /// Loaded into AbilityDatabaseBlob at bootstrap.
    ///
    /// Mirrors the AI AbilityDefinition structure for consistency but tailored
    /// for player-specific needs (paradigm overrides, input slots, etc.).
    ///
    /// EPIC 18.19 - Phase 3
    /// </summary>
    public struct AbilityDef
    {
        // ===== IDENTITY =====
        public int AbilityId;
        public AbilityCategory Category;
        public AbilityParadigmFlags ParadigmFlags;  // Which paradigms can use this ability

        // ===== TARGETING =====
        public AbilityTargetType TargetType;
        public float Range;
        public float Radius;        // For AoE/Cone
        public float Angle;         // For Cone/Cleave (degrees)
        public int MaxTargets;
        public bool RequiresLineOfSight;
        public bool RequiresTarget;  // Must have valid TargetData.TargetEntity

        // ===== TIMING =====
        public float TelegraphDuration;
        public float CastTime;
        public float ActiveDuration;
        public float RecoveryTime;
        public float Cooldown;
        public float GlobalCooldown;
        public float TickInterval;   // For channeled/DOT abilities (0 = single hit)

        // ===== CHARGES =====
        public byte MaxCharges;      // 0 = no charge system (uses standard cooldown)
        public float ChargeRegenTime;

        // ===== COST =====
        public ResourceType CostResource;
        public CostTiming CostTiming;
        public float CostAmount;

        // ===== DAMAGE / HEALING =====
        public float DamageBase;
        public float DamageVariance;
        public DamageType DamageType;
        public int HitCount;
        public bool CanCrit;
        public CombatResolverType ResolverType;

        // ===== CAST MOVEMENT =====
        public AbilityCastMovement CastMovement;
        public bool Interruptible;

        // ===== MODIFIERS (on-hit status effects) =====
        public ModifierType Modifier0Type;
        public float Modifier0Chance;
        public float Modifier0Duration;
        public float Modifier0Intensity;
        public ModifierType Modifier1Type;
        public float Modifier1Chance;
        public float Modifier1Duration;
        public float Modifier1Intensity;

        // ===== ANIMATION =====
        public int AnimationTriggerHash;

        // ===== VFX =====
        public int CastVFXTypeId;    // VFXTypeDatabase ID for cast effect (0 = none)
        public int ImpactVFXTypeId;  // VFXTypeDatabase ID for impact effect (0 = none)
    }
}
