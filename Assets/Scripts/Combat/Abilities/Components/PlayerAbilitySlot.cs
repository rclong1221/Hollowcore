using Unity.Entities;
using Unity.NetCode;

namespace DIG.Combat.Abilities
{
    /// <summary>
    /// Per-slot runtime state for player abilities.
    /// Stored as a DynamicBuffer on the player entity (up to 6 ability slots).
    /// Cooldown and charge state are tracked per-slot for prediction/rollback.
    ///
    /// EPIC 18.19 - Phase 3
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    [InternalBufferCapacity(6)]
    public struct PlayerAbilitySlot : IBufferElementData
    {
        /// <summary>Ability ID from AbilityDef database. -1 = empty slot.</summary>
        [GhostField] public int AbilityId;

        /// <summary>Slot index (0-5).</summary>
        [GhostField] public byte SlotIndex;

        /// <summary>Remaining cooldown in seconds. 0 = ready.</summary>
        [GhostField] public float CooldownRemaining;

        /// <summary>Current charges available for charge-based abilities.</summary>
        [GhostField] public byte ChargesRemaining;

        /// <summary>Time elapsed toward next charge regeneration.</summary>
        [GhostField] public float ChargeRechargeElapsed;

        public static PlayerAbilitySlot Empty(byte slotIndex) => new PlayerAbilitySlot
        {
            AbilityId = -1,
            SlotIndex = slotIndex,
            CooldownRemaining = 0f,
            ChargesRemaining = 0,
            ChargeRechargeElapsed = 0f
        };
    }
}
