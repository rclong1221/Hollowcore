using Unity.Entities;
using Unity.Mathematics;
using DIG.Targeting.Theming;
using HitboxRegion = Player.Components.HitboxRegion;

namespace DIG.Combat.Resolvers
{
    /// <summary>
    /// Hybrid combat resolver combining physics hit detection with stat-based damage.
    /// Requires physics collision to hit, then applies full stat calculations.
    /// Best for: Games wanting both aiming skill and stat progression depth.
    /// </summary>
    public class HybridResolver : ICombatResolver
    {
        public string ResolverID => "Hybrid";
        public string DisplayName => "Hybrid (Physics + Stats)";
        
        private Unity.Mathematics.Random _random;
        
        public HybridResolver()
        {
            _random = new Unity.Mathematics.Random((uint)System.Environment.TickCount | 1u);
        }
        
        public HybridResolver(uint seed)
        {
            _random = new Unity.Mathematics.Random(seed);
        }
        
        /// <summary>
        /// Resolve an attack requiring physics hit, then applying stat damage.
        /// </summary>
        public CombatResult ResolveAttack(in CombatContext context)
        {
            // Step 1: REQUIRE physics hit (skill-based aiming)
            if (!context.WasPhysicsHit)
            {
                return CombatResult.Miss();
            }
            
            // Step 2: Physics hit confirmed - now apply stat-based damage
            
            // Step 3: Roll for crit
            // EPIC 15.28: Headshots get +25% bonus crit chance
            float critChance = GetCritChance(in context);
            if (context.HitRegion == HitboxRegion.Head)
                critChance += 0.25f;
            bool isCrit = RollCrit(critChance);
            var hitType = isCrit ? HitType.Critical : HitType.Hit;
            
            // Step 4: Calculate damage with full stat scaling
            float rawDamage = CalculateDamage(in context, hitType);
            
            // Step 5: Apply crit multiplier
            float critMultiplier = 1f;
            if (isCrit)
            {
                critMultiplier = GetCritMultiplier(in context);
                rawDamage *= critMultiplier;
            }
            
            // Step 6: Apply distance falloff (optional - rewards closer combat)
            rawDamage = ApplyDistanceFalloff(rawDamage, context.HitDistance);
            
            // Step 7: Apply mitigation
            float finalDamage = ApplyMitigation(rawDamage, in context);
            
            // Step 8: Apply elemental resistance
            finalDamage = ApplyResistance(finalDamage, context.WeaponData.DamageType, in context);
            
            // Create result
            CombatResult result;
            if (isCrit)
                result = CombatResult.Critical(rawDamage, finalDamage, context.WeaponData.DamageType, critMultiplier);
            else
                result = CombatResult.Hit(rawDamage, finalDamage, context.WeaponData.DamageType);

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
            float eleResistance = context.TargetStats.GetResistance(context.WeaponData.DamageType);
            if (eleResistance > 0.25f)
                result.Flags |= ResultFlags.Resistance;
            if (eleResistance < -0.1f)
                result.Flags |= ResultFlags.Weakness;

            return result;
        }
        
        /// <summary>
        /// Hybrid resolver: 1.0 if physics hit, 0.0 otherwise.
        /// </summary>
        public float CalculateHitChance(in CombatContext context)
        {
            return context.WasPhysicsHit ? 1f : 0f;
        }
        
        /// <summary>
        /// Physics determines hit, stats determine crit.
        /// </summary>
        public HitType RollForHit(float hitChance, in CombatContext context)
        {
            if (hitChance < 1f)
                return HitType.Miss;
            
            float critChance = GetCritChance(in context);
            return RollCrit(critChance) ? HitType.Critical : HitType.Hit;
        }
        
        /// <summary>
        /// Calculate damage using full stat formula.
        /// </summary>
        public float CalculateDamage(in CombatContext context, HitType hitType)
        {
            if (hitType == HitType.Miss)
                return 0f;
            
            // Random roll within weapon damage range (fall back to BaseDamage if range not set)
            float weaponDamage;
            if (context.WeaponData.DamageMin > 0f || context.WeaponData.DamageMax > 0f)
                weaponDamage = _random.NextFloat(context.WeaponData.DamageMin, context.WeaponData.DamageMax);
            else
                weaponDamage = context.WeaponData.BaseDamage;

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
        /// Apply distance falloff for ranged weapons.
        /// Closer = more damage (rewards aggressive play).
        /// </summary>
        private float ApplyDistanceFalloff(float damage, float distance)
        {
            // No falloff within 5 units
            if (distance <= 5f)
                return damage;
            
            // Gradual falloff from 5-30 units (down to 70% damage)
            float falloffStart = 5f;
            float falloffEnd = 30f;
            float minMultiplier = 0.7f;
            
            if (distance >= falloffEnd)
                return damage * minMultiplier;
            
            float falloffProgress = (distance - falloffStart) / (falloffEnd - falloffStart);
            float multiplier = math.lerp(1f, minMultiplier, falloffProgress);
            
            return damage * multiplier;
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
