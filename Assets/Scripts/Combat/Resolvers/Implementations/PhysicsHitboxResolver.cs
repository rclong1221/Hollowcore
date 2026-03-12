using Unity.Entities;
using Unity.Mathematics;
using DIG.Targeting.Theming;
using HitboxRegion = Player.Components.HitboxRegion;

namespace DIG.Combat.Resolvers
{
    /// <summary>
    /// Physics-based combat resolver for DIG action combat.
    /// Requires physics collision (raycast/trigger) to hit.
    /// No stat scaling - damage comes directly from weapon.
    /// Best for: Pure action games where player skill (aiming) determines hit.
    /// </summary>
    public class PhysicsHitboxResolver : ICombatResolver
    {
        public string ResolverID => "PhysicsHitbox";
        public string DisplayName => "Physics Hitbox (DIG)";
        
        /// <summary>
        /// Resolve an attack using physics-based hit detection.
        /// </summary>
        public CombatResult ResolveAttack(in CombatContext context)
        {
            // Step 1: Check for physics hit - if no collision, no damage
            if (!context.WasPhysicsHit)
            {
                return CombatResult.Miss();
            }
            
            // Step 2: Hit confirmed by physics, calculate damage
            var hitType = HitType.Hit;
            float damage = CalculateDamage(in context, hitType);

            // Step 3: No mitigation in pure physics mode (skill-based)
            float finalDamage = damage;

            // EPIC 15.28: Set contextual flags
            var result = CombatResult.Hit(damage, finalDamage, context.WeaponData.DamageType);
            if (context.HitRegion == HitboxRegion.Head)
                result.Flags |= ResultFlags.Headshot;
            return result;
        }
        
        /// <summary>
        /// Physics resolver always returns 1.0 if physics hit confirmed.
        /// </summary>
        public float CalculateHitChance(in CombatContext context)
        {
            return context.WasPhysicsHit ? 1f : 0f;
        }
        
        /// <summary>
        /// Physics resolver: physics hit = Hit, no physics = Miss.
        /// No graze or crit in pure physics mode.
        /// </summary>
        public HitType RollForHit(float hitChance, in CombatContext context)
        {
            return hitChance >= 1f ? HitType.Hit : HitType.Miss;
        }
        
        /// <summary>
        /// Calculate damage from weapon base damage only.
        /// No stat scaling in physics mode.
        /// </summary>
        public float CalculateDamage(in CombatContext context, HitType hitType)
        {
            if (hitType == HitType.Miss)
                return 0f;
            
            // Pure weapon damage - no stat interaction
            // EPIC 15.28: Apply hitbox multiplier
            return context.WeaponData.BaseDamage * context.HitboxMultiplier;
        }
        
        /// <summary>
        /// Apply damage to target. Placeholder for health system integration.
        /// </summary>
        public float ApplyDamage(EntityManager entityManager, Entity target, float damage, DamageType damageType)
        {
            // TODO: Integrate with health system in Phase 5
            // For now, return the damage that would be applied
            return damage;
        }
        
        /// <summary>
        /// Trigger on-hit effects. Placeholder for effect system integration.
        /// </summary>
        public void TriggerEffects(EntityManager entityManager, in CombatContext context, HitType hitType)
        {
            // TODO: Integrate with effect system in Phase 5
            // Physics mode typically has minimal procs
        }
    }
}
