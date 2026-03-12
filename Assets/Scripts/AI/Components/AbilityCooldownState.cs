using Unity.Entities;

namespace DIG.AI.Components
{
    /// <summary>
    /// EPIC 15.32: Per-ability runtime cooldown state.
    /// One entry per ability in the AbilityDefinition buffer (parallel arrays).
    /// Manages per-ability, global, and group cooldowns plus charge-based abilities.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct AbilityCooldownState : IBufferElementData
    {
        public float CooldownRemaining;        // Per-ability cooldown
        public float GlobalCooldownRemaining;  // Shared GCD across all abilities
        public float CooldownGroupRemaining;   // Shared cooldown within a group
        public int ChargesRemaining;           // For charge-based abilities
        public int MaxCharges;                 // Charge cap (baked from definition)
        public float ChargeRegenTimer;         // Time until next charge regenerates
    }
}
