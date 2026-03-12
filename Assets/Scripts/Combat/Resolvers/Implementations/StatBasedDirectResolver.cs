using Unity.Entities;
using Unity.Mathematics;
using DIG.Targeting.Theming;
using HitboxRegion = Player.Components.HitboxRegion;

namespace DIG.Combat.Resolvers
{
    /// <summary>
    /// Stat-based combat resolver where attacks in range always hit.
    /// Damage scales with stats, crits are possible.
    /// Best for: Fast-paced ARPGs where "game feel" matters more than dice rolls.
    /// </summary>
    public class StatBasedDirectResolver : ICombatResolver
    {
        public string ResolverID => "StatBasedDirect";
        public string DisplayName => "Stat-Based Direct (ARPG)";
        
        private readonly Unity.Mathematics.Random _random;
        
        public StatBasedDirectResolver()
        {
            _random = new Unity.Mathematics.Random((uint)System.Environment.TickCount | 1u);
        }
        
        public StatBasedDirectResolver(uint seed)
        {
            _random = new Unity.Mathematics.Random(seed);
        }
        
        /// <summary>
        /// Resolve an attack using stat-based damage calculation.
        /// In-range attacks always hit (no accuracy roll).
        /// </summary>
        public CombatResult ResolveAttack(in CombatContext context)
        {
            // Step 1: Check if target is in range (assumed valid if context exists)
            // In-range = guaranteed hit for this resolver
            
            // Step 2: Roll for critical hit
            // EPIC 15.28: Headshots get +25% bonus crit chance
            float critChance = GetCritChance(in context);
            if (context.HitRegion == HitboxRegion.Head)
                critChance += 0.25f;
            bool isCrit = RollCrit(critChance);
            var hitType = isCrit ? HitType.Critical : HitType.Hit;
            
            // Step 3: Calculate damage with stat scaling
            float rawDamage = CalculateDamage(in context, hitType);
            
            // Step 4: Apply crit multiplier
            float critMultiplier = 1f;
            if (isCrit)
            {
                critMultiplier = GetCritMultiplier(in context);
                rawDamage *= critMultiplier;
            }
            
            // Step 5: Apply mitigation from target defense
            float finalDamage = ApplyMitigation(rawDamage, in context);
            
            // Create result
            CombatResult result;
            if (isCrit)
                result = CombatResult.Critical(rawDamage, finalDamage, context.WeaponData.DamageType, critMultiplier);
            else
                result = CombatResult.Hit(rawDamage, finalDamage, context.WeaponData.DamageType);

            // EPIC 15.28: Set contextual flags
            if (context.HitRegion == HitboxRegion.Head)
                result.Flags |= ResultFlags.Headshot;
            return result;
        }
        
        /// <summary>
        /// Direct resolver always hits if in range.
        /// </summary>
        public float CalculateHitChance(in CombatContext context)
        {
            return 1f;
        }
        
        /// <summary>
        /// Direct resolver: always Hit or Critical, never Miss.
        /// </summary>
        public HitType RollForHit(float hitChance, in CombatContext context)
        {
            float critChance = GetCritChance(in context);
            return RollCrit(critChance) ? HitType.Critical : HitType.Hit;
        }
        
        /// <summary>
        /// Calculate damage using stat-based formula.
        /// BaseDamage * (1 + AttackPower/100)
        /// </summary>
        public float CalculateDamage(in CombatContext context, HitType hitType)
        {
            if (hitType == HitType.Miss)
                return 0f;
            
            // Base weapon damage
            float baseDamage = context.WeaponData.BaseDamage;

            // EPIC 15.28: Apply hitbox multiplier
            baseDamage *= context.HitboxMultiplier;

            // Scale with attack power: damage * (1 + AttackPower/100)
            float attackPowerMod = 1f + (context.AttackerStats.AttackPower / 100f);

            return baseDamage * attackPowerMod;
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
            // Base crit from attacker + weapon bonus
            return context.AttackerStats.CritChance + context.WeaponData.CritChanceBonus;
        }
        
        private float GetCritMultiplier(in CombatContext context)
        {
            // Base crit mult from attacker + weapon bonus
            return context.AttackerStats.CritMultiplier + context.WeaponData.CritMultiplierBonus;
        }
        
        private bool RollCrit(float critChance)
        {
            return _random.NextFloat() < critChance;
        }
        
        /// <summary>
        /// Apply damage mitigation from defense.
        /// Formula: damage * (100 / (100 + Defense))
        /// </summary>
        private float ApplyMitigation(float damage, in CombatContext context)
        {
            float defense = context.TargetStats.Defense;
            if (defense <= 0f)
                return damage;
            
            // Diminishing returns formula
            float mitigation = 100f / (100f + defense);
            return damage * mitigation;
        }
    }
}
