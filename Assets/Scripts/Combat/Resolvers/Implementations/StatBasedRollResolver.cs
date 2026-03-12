using Unity.Entities;
using Unity.Mathematics;
using DIG.Targeting.Theming;
using HitboxRegion = Player.Components.HitboxRegion;

namespace DIG.Combat.Resolvers
{
    /// <summary>
    /// Stat-based combat resolver with accuracy vs evasion rolls.
    /// More tactical feel with potential misses and grazes.
    /// Best for: Tactical ARPGs, turn-based games, when "dice rolling" matters.
    /// </summary>
    public class StatBasedRollResolver : ICombatResolver
    {
        public string ResolverID => "StatBasedRoll";
        public string DisplayName => "Stat-Based Roll (Tactical)";
        
        private Unity.Mathematics.Random _random;
        
        // Configuration
        public float MinHitChance { get; set; } = 0.05f;
        public float MaxHitChance { get; set; } = 0.95f;
        public bool EnableGraze { get; set; } = true;
        public float GrazeThreshold { get; set; } = 0.1f;
        public float GrazeDamageMultiplier { get; set; } = 0.5f;
        
        public StatBasedRollResolver()
        {
            _random = new Unity.Mathematics.Random((uint)System.Environment.TickCount | 1u);
        }
        
        public StatBasedRollResolver(uint seed)
        {
            _random = new Unity.Mathematics.Random(seed);
        }
        
        /// <summary>
        /// Resolve an attack using stat-based accuracy rolls.
        /// </summary>
        public CombatResult ResolveAttack(in CombatContext context)
        {
            // Step 1: Calculate hit chance (Accuracy vs Evasion)
            float hitChance = CalculateHitChance(in context);
            
            // Step 2: Roll for hit
            var hitType = RollForHit(hitChance, in context);
            
            // Step 3: Handle miss
            if (hitType == HitType.Miss)
            {
                return CombatResult.Miss();
            }
            
            // Step 4: Calculate base damage
            float rawDamage = CalculateDamage(in context, hitType);
            
            // Step 5: Apply graze multiplier if applicable
            if (hitType == HitType.Graze)
            {
                rawDamage *= GrazeDamageMultiplier;
            }
            
            // Step 6: Roll for crit (only on full hits, not grazes)
            // EPIC 15.28: Headshots guarantee crit
            float critMultiplier = 1f;
            if (hitType == HitType.Hit)
            {
                bool isHeadshot = context.HitRegion == HitboxRegion.Head;
                float critChance = GetCritChance(in context);
                if (isHeadshot || RollCrit(critChance))
                {
                    hitType = HitType.Critical;
                    critMultiplier = GetCritMultiplier(in context);
                    rawDamage *= critMultiplier;
                }
            }
            
            // Step 7: Apply mitigation
            float finalDamage = ApplyMitigation(rawDamage, in context);
            
            // Step 8: Apply elemental resistance
            finalDamage = ApplyResistance(finalDamage, context.WeaponData.DamageType, in context);
            
            // Create result based on hit type
            var result = hitType switch
            {
                HitType.Critical => CombatResult.Critical(rawDamage, finalDamage, context.WeaponData.DamageType, critMultiplier),
                HitType.Graze => CombatResult.Graze(rawDamage, finalDamage, context.WeaponData.DamageType),
                _ => CombatResult.Hit(rawDamage, finalDamage, context.WeaponData.DamageType)
            };

            // EPIC 15.28: Set contextual flags
            if (context.HitRegion == HitboxRegion.Head)
                result.Flags |= ResultFlags.Headshot;

            // Backstab: attacker facing same direction as target (behind them)
            if (math.lengthsq(context.AttackDirection) > 0.01f &&
                math.lengthsq(context.TargetForward) > 0.01f)
            {
                float dot = math.dot(context.AttackDirection, context.TargetForward);
                if (dot > 0.5f)
                    result.Flags |= ResultFlags.Backstab;
            }

            // Elemental weakness/resistance flags
            float resistance = context.TargetStats.GetResistance(context.WeaponData.DamageType);
            if (resistance > 0.25f)
                result.Flags |= ResultFlags.Resistance;
            if (resistance < -0.1f)
                result.Flags |= ResultFlags.Weakness;

            return result;
        }
        
        /// <summary>
        /// Calculate hit chance: Accuracy - TargetEvasion, clamped to min/max.
        /// </summary>
        public float CalculateHitChance(in CombatContext context)
        {
            // Base hit chance starts at 90%
            float baseHitChance = 0.9f;
            
            // Add attacker accuracy
            float accuracy = context.AttackerStats.Accuracy;
            
            // Subtract target evasion
            float evasion = context.TargetStats.Evasion;
            
            // Calculate final hit chance
            float hitChance = baseHitChance + (accuracy * 0.01f) - evasion;
            
            // Clamp to bounds
            return math.clamp(hitChance, MinHitChance, MaxHitChance);
        }
        
        /// <summary>
        /// Roll for hit type: Miss, Graze, or Hit.
        /// </summary>
        public HitType RollForHit(float hitChance, in CombatContext context)
        {
            float roll = _random.NextFloat();
            
            if (roll < hitChance)
            {
                return HitType.Hit;
            }
            
            // Check for graze (near-miss)
            if (EnableGraze)
            {
                float grazeChance = hitChance + GrazeThreshold;
                if (roll < grazeChance)
                {
                    return HitType.Graze;
                }
            }
            
            return HitType.Miss;
        }
        
        /// <summary>
        /// Calculate damage using full stat formula.
        /// </summary>
        public float CalculateDamage(in CombatContext context, HitType hitType)
        {
            if (hitType == HitType.Miss)
                return 0f;
            
            // Random roll within weapon damage range
            float weaponDamage = _random.NextFloat(context.WeaponData.DamageMin, context.WeaponData.DamageMax);

            // EPIC 15.28: Apply hitbox multiplier
            weaponDamage *= context.HitboxMultiplier;

            // Scale with attack power
            float attackPowerMod = 1f + (context.AttackerStats.AttackPower / 100f);

            // Add strength scaling
            float strengthMod = 1f + (context.AttackerStats.Strength * 0.02f);

            return weaponDamage * attackPowerMod * strengthMod;
        }
        
        /// <summary>
        /// Apply damage to target. Placeholder for health system integration.
        /// </summary>
        public float ApplyDamage(EntityManager entityManager, Entity target, float damage, DamageType damageType)
        {
            // TODO: Integrate with health system in Phase 5
            return damage;
        }
        
        /// <summary>
        /// Trigger on-hit effects. Placeholder for effect system integration.
        /// </summary>
        public void TriggerEffects(EntityManager entityManager, in CombatContext context, HitType hitType)
        {
            // TODO: Integrate with effect system in Phase 5
        }
        
        // ========== HELPER METHODS ==========
        
        private float GetCritChance(in CombatContext context)
        {
            return context.AttackerStats.CritChance + context.WeaponData.CritChanceBonus;
        }
        
        private float GetCritMultiplier(in CombatContext context)
        {
            return context.AttackerStats.CritMultiplier + context.WeaponData.CritMultiplierBonus;
        }
        
        private bool RollCrit(float critChance)
        {
            return _random.NextFloat() < critChance;
        }
        
        /// <summary>
        /// Apply damage mitigation from defense.
        /// </summary>
        private float ApplyMitigation(float damage, in CombatContext context)
        {
            float defense = context.TargetStats.Defense;
            float armor = context.TargetStats.Armor;
            float totalDefense = defense + armor;
            
            if (totalDefense <= 0f)
                return damage;
            
            // Diminishing returns formula
            float mitigation = 100f / (100f + totalDefense);
            return damage * mitigation;
        }
        
        /// <summary>
        /// Apply elemental resistance.
        /// </summary>
        private float ApplyResistance(float damage, DamageType damageType, in CombatContext context)
        {
            float resistance = context.TargetStats.GetResistance(damageType);
            return damage * (1f - resistance);
        }
    }
}
