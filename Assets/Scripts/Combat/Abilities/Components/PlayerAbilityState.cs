using Unity.Entities;
using Unity.NetCode;

namespace DIG.Combat.Abilities
{
    /// <summary>
    /// Cast phase lifecycle for player abilities.
    /// Mirrors AI's AbilityCastPhase for consistency.
    ///
    /// EPIC 18.19 - Phase 3
    /// </summary>
    public enum AbilityCastPhase : byte
    {
        Idle = 0,        // No ability active
        Telegraph = 1,   // Ground indicator visible (for ground-target abilities)
        Casting = 2,     // Wind-up / cast bar
        Active = 3,      // Damage delivery / effect application
        Recovery = 4     // Post-ability cooldown
    }

    /// <summary>
    /// Flags for ability state modifiers.
    /// </summary>
    [System.Flags]
    public enum AbilityStateFlags : byte
    {
        None = 0,
        MovementLocked = 1 << 0,   // Cannot move during cast
        MovementSlowed = 1 << 1,   // Reduced speed during cast
        Interruptible  = 1 << 2,   // Can be interrupted by damage/CC
        IsChanneled    = 1 << 3,   // Holding input continues ability
    }

    /// <summary>
    /// Per-entity state tracking for the player ability execution pipeline.
    /// Predicted for rollback support.
    ///
    /// EPIC 18.19 - Phase 3
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PlayerAbilityState : IComponentData
    {
        /// <summary>Slot index of the currently active ability (255 = none).</summary>
        [GhostField] public byte ActiveSlotIndex;

        /// <summary>Slot index of queued ability (255 = none). Executes after current finishes.</summary>
        [GhostField] public byte QueuedSlotIndex;

        /// <summary>Current phase of the active ability.</summary>
        [GhostField] public AbilityCastPhase Phase;

        /// <summary>Time elapsed in the current phase.</summary>
        [GhostField] public float PhaseElapsed;

        /// <summary>Global cooldown remaining (shared across all abilities).</summary>
        [GhostField] public float GCDRemaining;

        /// <summary>Active flags for movement gating, interruptibility, etc.</summary>
        [GhostField] public byte Flags;

        /// <summary>Whether damage has been dealt this active phase (one-shot flag).</summary>
        [GhostField] public byte DamageDealt;

        /// <summary>Ticks delivered for channeled/multi-hit abilities.</summary>
        [GhostField] public byte TicksDelivered;

        public bool IsIdle => Phase == AbilityCastPhase.Idle;
        public bool HasActiveAbility => ActiveSlotIndex != 255;
        public bool HasQueuedAbility => QueuedSlotIndex != 255;
        public bool IsOnGCD => GCDRemaining > 0f;

        public AbilityStateFlags ActiveFlags
        {
            get => (AbilityStateFlags)Flags;
            set => Flags = (byte)value;
        }

        public static PlayerAbilityState Default => new PlayerAbilityState
        {
            ActiveSlotIndex = 255,
            QueuedSlotIndex = 255,
            Phase = AbilityCastPhase.Idle,
            PhaseElapsed = 0f,
            GCDRemaining = 0f,
            Flags = 0,
            DamageDealt = 0,
            TicksDelivered = 0
        };
    }
}
