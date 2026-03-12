using Unity.Entities;
using Unity.Mathematics;
using DIG.Combat.Resolvers;
using DIG.Items.Definitions;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// Static utility for creating pending combat hits from weapon systems.
    /// Use this from projectile, melee, and raycast hit detection code.
    /// </summary>
    public static class CombatHitFactory
    {
        /// <summary>
        /// Creates a pending combat hit from a physics collision.
        /// </summary>
        public static Entity CreatePhysicsHit(
            EntityManager entityManager,
            Entity attacker,
            Entity target,
            Entity weapon,
            float3 hitPoint,
            float3 hitNormal,
            float hitDistance,
            WeaponStats weaponStats,
            CombatResolverType resolverType = CombatResolverType.PhysicsHitbox)
        {
            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, new PendingCombatHit
            {
                AttackerEntity = attacker,
                TargetEntity = target,
                WeaponEntity = weapon,
                HitPoint = hitPoint,
                HitNormal = hitNormal,
                HitDistance = hitDistance,
                WasPhysicsHit = true,
                ResolverType = resolverType,
                WeaponData = weaponStats
            });
            return entity;
        }
        
        /// <summary>
        /// Creates a pending combat hit from a targeting system (no physics).
        /// </summary>
        public static Entity CreateTargetedHit(
            EntityManager entityManager,
            Entity attacker,
            Entity target,
            Entity weapon,
            float3 targetPosition,
            float distance,
            WeaponStats weaponStats,
            CombatResolverType resolverType = CombatResolverType.StatBasedDirect)
        {
            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, new PendingCombatHit
            {
                AttackerEntity = attacker,
                TargetEntity = target,
                WeaponEntity = weapon,
                HitPoint = targetPosition,
                HitNormal = float3.zero,
                HitDistance = distance,
                WasPhysicsHit = false,
                ResolverType = resolverType,
                WeaponData = weaponStats
            });
            return entity;
        }
        
        /// <summary>
        /// Creates a pending combat hit from a WeaponCategoryDefinition.
        /// Extracts resolver type and damage range from the category.
        /// </summary>
        public static Entity CreateFromCategory(
            EntityManager entityManager,
            Entity attacker,
            Entity target,
            Entity weapon,
            float3 hitPoint,
            float3 hitNormal,
            float hitDistance,
            bool wasPhysicsHit,
            WeaponCategoryDefinition category,
            float damageMultiplier = 1f)
        {
            var weaponStats = new WeaponStats
            {
                BaseDamage = (category.BaseDamageRange.x + category.BaseDamageRange.y) * 0.5f * damageMultiplier,
                DamageMin = category.BaseDamageRange.x * damageMultiplier,
                DamageMax = category.BaseDamageRange.y * damageMultiplier,
                CanCrit = category.CanCrit,
                AttackSpeed = 1f / category.DefaultUseDuration
            };
            
            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, new PendingCombatHit
            {
                AttackerEntity = attacker,
                TargetEntity = target,
                WeaponEntity = weapon,
                HitPoint = hitPoint,
                HitNormal = hitNormal,
                HitDistance = hitDistance,
                WasPhysicsHit = wasPhysicsHit,
                ResolverType = category.ResolverType,
                WeaponData = weaponStats
            });
            return entity;
        }
        
        /// <summary>
        /// Creates WeaponStats from a WeaponCategoryDefinition.
        /// </summary>
        public static WeaponStats CreateWeaponStats(WeaponCategoryDefinition category, float damageMultiplier = 1f)
        {
            return new WeaponStats
            {
                BaseDamage = (category.BaseDamageRange.x + category.BaseDamageRange.y) * 0.5f * damageMultiplier,
                DamageMin = category.BaseDamageRange.x * damageMultiplier,
                DamageMax = category.BaseDamageRange.y * damageMultiplier,
                CanCrit = category.CanCrit,
                AttackSpeed = 1f / category.DefaultUseDuration
            };
        }
    }
}
