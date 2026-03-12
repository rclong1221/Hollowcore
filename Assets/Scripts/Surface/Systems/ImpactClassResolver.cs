using DIG.Weapons.Data;
using DIG.Weapons.Effects;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 15.24: Maps weapon categories and impact types to ImpactClass.
    /// </summary>
    public static class ImpactClassResolver
    {
        /// <summary>
        /// Resolve ImpactClass from weapon category.
        /// </summary>
        public static ImpactClass FromWeaponCategory(WeaponCategory category)
        {
            return category switch
            {
                WeaponCategory.Pistol => ImpactClass.Bullet_Light,
                WeaponCategory.SMG => ImpactClass.Bullet_Light,
                WeaponCategory.Rifle => ImpactClass.Bullet_Medium,
                WeaponCategory.LMG => ImpactClass.Bullet_Medium,
                WeaponCategory.Shotgun => ImpactClass.Bullet_Heavy,
                WeaponCategory.Sniper => ImpactClass.Bullet_Heavy,
                _ => ImpactClass.Bullet_Medium
            };
        }

        /// <summary>
        /// Resolve ImpactClass from existing ImpactType enum (backward compat).
        /// </summary>
        public static ImpactClass FromImpactType(ImpactType type)
        {
            return type switch
            {
                ImpactType.Bullet => ImpactClass.Bullet_Medium,
                ImpactType.Melee => ImpactClass.Melee_Light,
                ImpactType.Explosion => ImpactClass.Explosion_Small,
                ImpactType.Magic => ImpactClass.Explosion_Small,
                ImpactType.Laser => ImpactClass.Bullet_Light,
                _ => ImpactClass.Bullet_Medium
            };
        }

        /// <summary>
        /// Resolve ImpactClass from projectile damage (fallback heuristic).
        /// </summary>
        public static ImpactClass FromDamage(float damage)
        {
            if (damage >= 50f) return ImpactClass.Bullet_Heavy;
            if (damage >= 20f) return ImpactClass.Bullet_Medium;
            return ImpactClass.Bullet_Light;
        }
    }
}
