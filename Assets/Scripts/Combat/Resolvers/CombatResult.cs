using DIG.Targeting.Theming;

namespace DIG.Combat.Resolvers
{
    /// <summary>
    /// Result of a combat resolution.
    /// Contains all information about what happened during an attack.
    /// Blittable struct for Burst compatibility.
    /// </summary>
    public struct CombatResult
    {
        // ========== HIT OUTCOME ==========
        
        /// <summary>
        /// Whether the attack connected with the target.
        /// </summary>
        public bool DidHit;
        
        /// <summary>
        /// Type of hit that occurred.
        /// </summary>
        public HitType HitType;
        
        // ========== DAMAGE VALUES ==========
        
        /// <summary>
        /// Damage before mitigation and modifiers.
        /// </summary>
        public float RawDamage;
        
        /// <summary>
        /// Damage after all mitigation, resistance, and modifiers.
        /// </summary>
        public float FinalDamage;
        
        /// <summary>
        /// Type of damage dealt (for UI and resistance calculations).
        /// </summary>
        public DamageType DamageType;
        
        /// <summary>
        /// Critical multiplier that was applied (1.0 if no crit).
        /// </summary>
        public float CritMultiplier;
        
        // ========== TARGET STATE ==========
        
        /// <summary>
        /// Whether the target died from this attack.
        /// </summary>
        public bool TargetKilled;
        
        /// <summary>
        /// Target's health after damage was applied.
        /// </summary>
        public float TargetHealthRemaining;
        
        // ========== PROC INFORMATION ==========

        /// <summary>
        /// Number of on-hit effects that were triggered.
        /// </summary>
        public int ProcsTriggeredCount;

        /// <summary>
        /// Bitmask of proc types that triggered (for UI feedback).
        /// </summary>
        public ProcFlags ProcsTriggered;

        // ========== CONTEXTUAL FLAGS (EPIC 15.22) ==========

        /// <summary>
        /// EPIC 15.22: Contextual flags (Headshot, Backstab, Weakness, etc.)
        /// </summary>
        public ResultFlags Flags;
        
        // ========== FACTORY METHODS ==========
        
        /// <summary>
        /// Create a miss result.
        /// </summary>
        public static CombatResult Miss()
        {
            return new CombatResult
            {
                DidHit = false,
                HitType = HitType.Miss,
                RawDamage = 0f,
                FinalDamage = 0f,
                CritMultiplier = 1f
            };
        }
        
        /// <summary>
        /// Create a successful hit result.
        /// </summary>
        public static CombatResult Hit(float rawDamage, float finalDamage, DamageType damageType)
        {
            return new CombatResult
            {
                DidHit = true,
                HitType = HitType.Hit,
                RawDamage = rawDamage,
                FinalDamage = finalDamage,
                DamageType = damageType,
                CritMultiplier = 1f
            };
        }
        
        /// <summary>
        /// Create a critical hit result.
        /// </summary>
        public static CombatResult Critical(float rawDamage, float finalDamage, DamageType damageType, float critMultiplier)
        {
            return new CombatResult
            {
                DidHit = true,
                HitType = HitType.Critical,
                RawDamage = rawDamage,
                FinalDamage = finalDamage,
                DamageType = damageType,
                CritMultiplier = critMultiplier
            };
        }
        
        /// <summary>
        /// Create a graze (partial hit) result.
        /// </summary>
        public static CombatResult Graze(float rawDamage, float finalDamage, DamageType damageType)
        {
            return new CombatResult
            {
                DidHit = true,
                HitType = HitType.Graze,
                RawDamage = rawDamage,
                FinalDamage = finalDamage,
                DamageType = damageType,
                CritMultiplier = 1f
            };
        }
    }
    
    /// <summary>
    /// Flags for on-hit proc effects.
    /// Used for UI feedback and effect chaining.
    /// </summary>
    [System.Flags]
    public enum ProcFlags : uint
    {
        None = 0,
        
        // ========== DAMAGE PROCS ==========
        Bleed = 1 << 0,
        Burn = 1 << 1,
        Freeze = 1 << 2,
        Shock = 1 << 3,
        Poison = 1 << 4,
        
        // ========== UTILITY PROCS ==========
        Lifesteal = 1 << 5,
        ManaSteal = 1 << 6,
        Stun = 1 << 7,
        Slow = 1 << 8,
        Knockback = 1 << 9,
        
        // ========== SPECIAL PROCS ==========
        ChainLightning = 1 << 10,
        Explosion = 1 << 11,
        Cleave = 1 << 12,
        Pierce = 1 << 13,
        
        // ========== BUFF/DEBUFF PROCS ==========
        WeakenTarget = 1 << 14,
        BuffSelf = 1 << 15,
        Heal = 1 << 16,
        Shield = 1 << 17
    }
}
