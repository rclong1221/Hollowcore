using Unity.Entities;

namespace Player.Components
{
    /// <summary>
    /// Global configuration for damage mitigation rules.
    /// Defines default cooldowns and behaviors per damage type.
    /// Loaded as a Singleton.
    /// </summary>
    public struct DamagePolicy : IComponentData
    {
        public float DefaultPhysicalCooldown;
        public float DefaultHeatCooldown;
        public float DefaultRadiationCooldown;
        public float DefaultSuffocationCooldown;
        public float DefaultExplosionCooldown;
        public float DefaultToxicCooldown;
        
        public float GetCooldownDuration(DamageType type)
        {
            return type switch
            {
                DamageType.Physical => DefaultPhysicalCooldown,
                DamageType.Heat => DefaultHeatCooldown,
                DamageType.Radiation => DefaultRadiationCooldown,
                DamageType.Suffocation => DefaultSuffocationCooldown,
                DamageType.Explosion => DefaultExplosionCooldown,
                DamageType.Toxic => DefaultToxicCooldown,
                _ => 0f
            };
        }
    }
}
