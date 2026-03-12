using Unity.Entities;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Tracks cooldown timestamps for damage types to prevent high-frequency spam 
    /// from continuous sources (e.g. standing in fire trigger).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct DamageCooldown : IComponentData
    {
        // Timestamps when the next damage of this type is allowed.
        // Derived from NetworkTime.ElapsedTime.
        
        [GhostField(Quantization = 100)]
        public float NextPhysicalTime;

        [GhostField(Quantization = 100)]
        public float NextHeatTime;

        [GhostField(Quantization = 100)]
        public float NextRadiationTime;

        [GhostField(Quantization = 100)]
        public float NextSuffocationTime;

        [GhostField(Quantization = 100)]
        public float NextExplosionTime;

        [GhostField(Quantization = 100)]
        public float NextToxicTime;
        
        /// <summary>
        /// Global cooldown for "Any" damage (prevents shotgunning all types at once if desired).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float NextGlobalTime;
        
        public bool IsCooldownActive(DamageType type, float currentTime)
        {
            if (currentTime < NextGlobalTime) return true;
            
            return type switch
            {
                DamageType.Physical => currentTime < NextPhysicalTime,
                DamageType.Heat => currentTime < NextHeatTime,
                DamageType.Radiation => currentTime < NextRadiationTime,
                DamageType.Suffocation => currentTime < NextSuffocationTime,
                DamageType.Explosion => currentTime < NextExplosionTime,
                DamageType.Toxic => currentTime < NextToxicTime,
                _ => false
            };
        }
        
        public void SetCooldown(DamageType type, float currentTime, float duration)
        {
            float expiry = currentTime + duration;
            switch (type)
            {
                case DamageType.Physical: NextPhysicalTime = expiry; break;
                case DamageType.Heat: NextHeatTime = expiry; break;
                case DamageType.Radiation: NextRadiationTime = expiry; break;
                case DamageType.Suffocation: NextSuffocationTime = expiry; break;
                case DamageType.Explosion: NextExplosionTime = expiry; break;
                case DamageType.Toxic: NextToxicTime = expiry; break;
            }
        }
    }
}
