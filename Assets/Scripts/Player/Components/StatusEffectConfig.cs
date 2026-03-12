using Unity.Entities;

namespace Player.Components
{
    /// <summary>
    /// Configuration for status effect behavior.
    /// Loaded as singleton.
    /// </summary>
    public struct StatusEffectConfig : IComponentData
    {
        public float TickInterval; // Frequency of damage ticks (e.g. 1.0s)
        
        // Damage amounts per severity unit per tick
        public float HypoxiaDamage;
        public float RadiationDamage;
        public float BurnDamage;
        public float FrostbiteDamage;
        public float BleedDamage;
        public float ConcussionDamage; // Likely 0, mostly visual/movement

        // EPIC 15.29: Combat modifier status effects
        public float ShockDamage;       // Lightning DOT
        public float PoisonDOTDamage;   // Combat poison DOT
        // Stun/Slow/Weaken don't deal damage — handled by gameplay systems

        public static StatusEffectConfig Default => new()
        {
            TickInterval = 1.0f,
            HypoxiaDamage = 5.0f,
            RadiationDamage = 2.0f,
            BurnDamage = 5.0f,
            FrostbiteDamage = 2.0f,
            BleedDamage = 2.0f,
            ConcussionDamage = 0.0f,
            ShockDamage = 4.0f,
            PoisonDOTDamage = 3.0f
        };

        public float GetDamage(StatusEffectType type)
        {
            return type switch
            {
                StatusEffectType.Hypoxia => HypoxiaDamage,
                StatusEffectType.RadiationPoisoning => RadiationDamage,
                StatusEffectType.Burn => BurnDamage,
                StatusEffectType.Frostbite => FrostbiteDamage,
                StatusEffectType.Bleed => BleedDamage,
                StatusEffectType.Shock => ShockDamage,
                StatusEffectType.PoisonDOT => PoisonDOTDamage,
                _ => 0f
            };
        }
    }
}
