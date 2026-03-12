using Unity.Entities;
using Unity.Mathematics;
using HitboxRegion = Player.Components.HitboxRegion;

namespace DIG.Combat.Resolvers
{
    /// <summary>
    /// Context data passed to combat resolvers.
    /// Contains all information needed to resolve an attack.
    /// Blittable struct for Burst compatibility.
    /// </summary>
    public struct CombatContext
    {
        // ========== ENTITIES ==========
        
        /// <summary>
        /// Entity performing the attack.
        /// </summary>
        public Entity AttackerEntity;
        
        /// <summary>
        /// Entity being attacked (may be Entity.Null for ground-target attacks).
        /// </summary>
        public Entity TargetEntity;
        
        /// <summary>
        /// Weapon entity used for the attack.
        /// </summary>
        public Entity WeaponEntity;
        
        // ========== PHYSICS DATA ==========
        
        /// <summary>
        /// World position where the hit occurred.
        /// </summary>
        public float3 HitPoint;
        
        /// <summary>
        /// Surface normal at the hit location.
        /// </summary>
        public float3 HitNormal;
        
        /// <summary>
        /// Distance from attacker to hit point.
        /// </summary>
        public float HitDistance;
        
        /// <summary>
        /// Whether a physics check (raycast/collider) confirmed contact.
        /// Required for PhysicsHitbox and Hybrid resolvers.
        /// </summary>
        public bool WasPhysicsHit;
        
        // ========== STATS ==========
        
        /// <summary>
        /// Attacker's combat statistics.
        /// </summary>
        public StatBlock AttackerStats;
        
        /// <summary>
        /// Target's combat statistics.
        /// </summary>
        public StatBlock TargetStats;
        
        /// <summary>
        /// Weapon statistics.
        /// </summary>
        public WeaponStats WeaponData;

        // ========== HITBOX DATA (EPIC 15.28) ==========

        /// <summary>
        /// EPIC 15.28: Which body region was hit.
        /// </summary>
        public HitboxRegion HitRegion;

        /// <summary>
        /// EPIC 15.28: Damage multiplier for the hit region (2.0 for head, 0.5 for legs).
        /// </summary>
        public float HitboxMultiplier;

        /// <summary>
        /// EPIC 15.28: Normalized attack direction (attacker → target) for backstab detection.
        /// </summary>
        public float3 AttackDirection;

        /// <summary>
        /// EPIC 15.28: Target's forward direction for backstab dot product.
        /// </summary>
        public float3 TargetForward;

        // ========== HELPERS ==========
        
        /// <summary>
        /// Create a minimal context for physics-based combat (no stats).
        /// </summary>
        public static CombatContext CreatePhysicsHit(
            Entity attacker,
            Entity target,
            Entity weapon,
            float3 hitPoint,
            float3 hitNormal,
            float hitDistance)
        {
            return new CombatContext
            {
                AttackerEntity = attacker,
                TargetEntity = target,
                WeaponEntity = weapon,
                HitPoint = hitPoint,
                HitNormal = hitNormal,
                HitDistance = hitDistance,
                WasPhysicsHit = true
            };
        }
        
        /// <summary>
        /// Create a full context for stat-based combat.
        /// </summary>
        public static CombatContext CreateStatBased(
            Entity attacker,
            Entity target,
            Entity weapon,
            StatBlock attackerStats,
            StatBlock targetStats,
            WeaponStats weaponData,
            float distance)
        {
            return new CombatContext
            {
                AttackerEntity = attacker,
                TargetEntity = target,
                WeaponEntity = weapon,
                HitDistance = distance,
                WasPhysicsHit = false,
                AttackerStats = attackerStats,
                TargetStats = targetStats,
                WeaponData = weaponData
            };
        }
    }
    
    /// <summary>
    /// Combat statistics for an entity (attacker or defender).
    /// Blittable for Burst compatibility.
    /// </summary>
    public struct StatBlock
    {
        // ========== OFFENSIVE STATS ==========
        
        /// <summary>
        /// Base attack power (flat damage bonus).
        /// </summary>
        public float AttackPower;
        
        /// <summary>
        /// Spell/magic power for magical attacks.
        /// </summary>
        public float SpellPower;
        
        /// <summary>
        /// Critical hit chance (0.0 to 1.0).
        /// </summary>
        public float CritChance;
        
        /// <summary>
        /// Critical hit damage multiplier (e.g., 1.5 = 150% damage).
        /// </summary>
        public float CritMultiplier;
        
        /// <summary>
        /// Hit accuracy bonus (for stat-roll systems).
        /// </summary>
        public float Accuracy;
        
        // ========== DEFENSIVE STATS ==========
        
        /// <summary>
        /// Base defense (damage reduction).
        /// </summary>
        public float Defense;
        
        /// <summary>
        /// Armor rating (alternative damage reduction).
        /// </summary>
        public float Armor;
        
        /// <summary>
        /// Evasion/dodge chance (0.0 to 1.0).
        /// </summary>
        public float Evasion;
        
        // ========== ATTRIBUTES ==========
        
        /// <summary>
        /// Strength attribute.
        /// </summary>
        public float Strength;
        
        /// <summary>
        /// Dexterity attribute.
        /// </summary>
        public float Dexterity;
        
        /// <summary>
        /// Intelligence attribute.
        /// </summary>
        public float Intelligence;
        
        // ========== LEVEL & HEALTH ==========
        
        /// <summary>
        /// Character level.
        /// </summary>
        public int Level;
        
        /// <summary>
        /// Current health as percentage (0.0 to 1.0).
        /// </summary>
        public float HealthPercent;
        
        // ========== RESISTANCES ==========
        
        /// <summary>
        /// Physical damage resistance (0.0 to 1.0).
        /// </summary>
        public float PhysicalResistance;
        
        /// <summary>
        /// Fire damage resistance (0.0 to 1.0).
        /// </summary>
        public float FireResistance;
        
        /// <summary>
        /// Ice damage resistance (0.0 to 1.0).
        /// </summary>
        public float IceResistance;
        
        /// <summary>
        /// Lightning damage resistance (0.0 to 1.0).
        /// </summary>
        public float LightningResistance;
        
        /// <summary>
        /// Poison damage resistance (0.0 to 1.0).
        /// </summary>
        public float PoisonResistance;
        
        /// <summary>
        /// Holy damage resistance (0.0 to 1.0).
        /// </summary>
        public float HolyResistance;
        
        /// <summary>
        /// Shadow damage resistance (0.0 to 1.0).
        /// </summary>
        public float ShadowResistance;
        
        /// <summary>
        /// Arcane damage resistance (0.0 to 1.0).
        /// </summary>
        public float ArcaneResistance;
        
        /// <summary>
        /// Creates default stat block with sensible values.
        /// </summary>
        public static StatBlock Default => new StatBlock
        {
            AttackPower = 0f,
            SpellPower = 0f,
            CritChance = 0.05f,
            CritMultiplier = 1.5f,
            Accuracy = 1f,
            Defense = 0f,
            Armor = 0f,
            Evasion = 0f,
            Strength = 10f,
            Dexterity = 10f,
            Intelligence = 10f,
            Level = 1,
            HealthPercent = 1f
        };
        
        /// <summary>
        /// Get resistance value for a specific damage type.
        /// </summary>
        public float GetResistance(DIG.Targeting.Theming.DamageType damageType)
        {
            return damageType switch
            {
                DIG.Targeting.Theming.DamageType.Physical => PhysicalResistance,
                DIG.Targeting.Theming.DamageType.Fire => FireResistance,
                DIG.Targeting.Theming.DamageType.Ice => IceResistance,
                DIG.Targeting.Theming.DamageType.Lightning => LightningResistance,
                DIG.Targeting.Theming.DamageType.Poison => PoisonResistance,
                DIG.Targeting.Theming.DamageType.Holy => HolyResistance,
                DIG.Targeting.Theming.DamageType.Shadow => ShadowResistance,
                DIG.Targeting.Theming.DamageType.Arcane => ArcaneResistance,
                _ => 0f
            };
        }
    }
    
    /// <summary>
    /// Weapon statistics for damage calculation.
    /// Blittable for Burst compatibility.
    /// </summary>
    public struct WeaponStats
    {
        /// <summary>
        /// Base damage value.
        /// </summary>
        public float BaseDamage;
        
        /// <summary>
        /// Minimum damage for random range.
        /// </summary>
        public float DamageMin;
        
        /// <summary>
        /// Maximum damage for random range.
        /// </summary>
        public float DamageMax;
        
        /// <summary>
        /// Attacks per second.
        /// </summary>
        public float AttackSpeed;
        
        /// <summary>
        /// Primary damage type of this weapon.
        /// </summary>
        public DIG.Targeting.Theming.DamageType DamageType;
        
        /// <summary>
        /// Weapon category ID for resolver lookup.
        /// </summary>
        public int CategoryID;
        
        /// <summary>
        /// Weapon crit chance bonus (added to character crit).
        /// </summary>
        public float CritChanceBonus;
        
        /// <summary>
        /// Weapon crit multiplier bonus (added to character crit mult).
        /// </summary>
        public float CritMultiplierBonus;

        /// <summary>
        /// Whether this weapon can critically hit.
        /// </summary>
        public bool CanCrit;

        
        /// <summary>
        /// Creates default weapon stats.
        /// </summary>
        public static WeaponStats Default => new WeaponStats
        {
            BaseDamage = 10f,
            DamageMin = 8f,
            DamageMax = 12f,
            AttackSpeed = 1f,
            DamageType = DIG.Targeting.Theming.DamageType.Physical
        };
    }
}
