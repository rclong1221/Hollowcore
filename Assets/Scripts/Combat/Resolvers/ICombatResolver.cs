using Unity.Entities;
using Unity.Mathematics;
using DIG.Targeting.Theming;

namespace DIG.Combat.Resolvers
{
    /// <summary>
    /// Interface for combat resolution implementations.
    /// Supports different combat styles: physics-based (DIG) and stat-based (ARPG).
    /// </summary>
    public interface ICombatResolver
    {
        /// <summary>
        /// Unique identifier for this resolver type.
        /// </summary>
        string ResolverID { get; }
        
        /// <summary>
        /// Human-readable name for UI/debug purposes.
        /// </summary>
        string DisplayName { get; }
        
        /// <summary>
        /// Main entry point for combat resolution.
        /// Takes a combat context and returns the result of the attack.
        /// </summary>
        /// <param name="context">All information about the attack</param>
        /// <returns>The result of the combat resolution</returns>
        CombatResult ResolveAttack(in CombatContext context);
        
        /// <summary>
        /// Calculate hit chance for stat-based systems.
        /// Returns 0-1 probability of hit landing.
        /// Physics resolvers may return 1.0 if physics hit is confirmed.
        /// </summary>
        /// <param name="context">Combat context with attacker/target stats</param>
        /// <returns>Hit probability from 0.0 to 1.0</returns>
        float CalculateHitChance(in CombatContext context);
        
        /// <summary>
        /// Perform a hit roll using the calculated hit chance.
        /// </summary>
        /// <param name="hitChance">Probability from CalculateHitChance</param>
        /// <param name="context">Combat context for additional factors</param>
        /// <returns>The type of hit that occurred</returns>
        HitType RollForHit(float hitChance, in CombatContext context);
        
        /// <summary>
        /// Calculate damage based on hit type and stats.
        /// </summary>
        /// <param name="context">Combat context with all stat information</param>
        /// <param name="hitType">Type of hit (affects damage multipliers)</param>
        /// <returns>Calculated damage value</returns>
        float CalculateDamage(in CombatContext context, HitType hitType);
        
        /// <summary>
        /// Apply damage to the target entity.
        /// Implementations may interface with health systems.
        /// </summary>
        /// <param name="entityManager">ECS EntityManager for component access</param>
        /// <param name="target">Target entity to damage</param>
        /// <param name="damage">Amount of damage to apply</param>
        /// <param name="damageType">Type of damage for resistance calculations</param>
        /// <returns>Actual damage applied after all modifiers</returns>
        float ApplyDamage(EntityManager entityManager, Entity target, float damage, DamageType damageType);
        
        /// <summary>
        /// Trigger on-hit effects (procs, status effects, etc.).
        /// </summary>
        /// <param name="entityManager">ECS EntityManager for component access</param>
        /// <param name="context">Combat context</param>
        /// <param name="hitType">Type of hit for conditional procs</param>
        void TriggerEffects(EntityManager entityManager, in CombatContext context, HitType hitType);
    }
}
