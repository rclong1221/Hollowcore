using Unity.Entities;
using Unity.NetCode;
using DIG.Targeting.Theming;

namespace DIG.Weapons
{
    /// <summary>
    /// EPIC 15.29: Type of on-hit modifier effect.
    /// </summary>
    public enum ModifierType : byte
    {
        None = 0,

        // Status DOTs (create StatusEffectRequest on target)
        Bleed,          // Physical DOT
        Burn,           // Fire DOT
        Freeze,         // Slow + Ice damage
        Shock,          // Lightning stun/chain
        Poison,         // Poison DOT

        // On-hit utility
        Lifesteal,      // Heal attacker % of damage dealt
        Stun,           // Brief stun (no damage)
        Slow,           // Movement speed reduction
        Weaken,         // Reduce target defense
        Knockback,      // Push target from hit point

        // AOE effects (create entities via ECB)
        Explosion,      // Bonus damage + radius at hit point
        Chain,          // Jump damage to nearby enemies
        Cleave,         // Hit additional targets in arc

        // Passive (always active, no proc roll)
        BonusDamage,    // Add flat bonus damage of Element type per hit
    }

    /// <summary>
    /// EPIC 15.29: Source of a weapon modifier, for selective removal.
    /// </summary>
    public enum ModifierSource : byte
    {
        Innate = 0,     // Baked into weapon prefab (permanent)
        Enchantment,    // Applied by rune/enchantment system (removable)
        Ammo,           // From equipped ammo/arrow type (swapped on reload)
    }

    /// <summary>
    /// EPIC 15.29: A single modifier effect on a weapon.
    /// Weapons can have unlimited modifiers (buffer grows dynamically).
    /// Each modifier triggers independently on hit based on its Chance.
    /// </summary>
    [InternalBufferCapacity(4)]
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct WeaponModifier : IBufferElementData
    {
        /// <summary>What kind of effect this modifier applies.</summary>
        [GhostField]
        public ModifierType Type;

        /// <summary>Where this modifier came from (for selective removal).</summary>
        [GhostField]
        public ModifierSource Source;

        /// <summary>Element for this modifier's damage (e.g., Fire for Burn DOT).</summary>
        [GhostField]
        public DamageType Element;

        /// <summary>Flat bonus damage (BonusDamage type) or explosion center damage.</summary>
        [GhostField]
        public float BonusDamage;

        /// <summary>Proc probability per hit (0-1, 1.0 = guaranteed).</summary>
        [GhostField]
        public float Chance;

        /// <summary>Duration in seconds (DOTs, debuffs, stun).</summary>
        [GhostField]
        public float Duration;

        /// <summary>DPS for DOTs, % for lifesteal, speed multiplier for Slow.</summary>
        [GhostField]
        public float Intensity;

        /// <summary>AOE radius for Explosion/Chain/Cleave (0 = single target).</summary>
        [GhostField]
        public float Radius;

        /// <summary>Knockback force.</summary>
        [GhostField]
        public float Force;
    }
}
