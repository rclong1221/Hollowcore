using Unity.Entities;
using Unity.Mathematics;

namespace DIG.AI.Components
{
    /// <summary>
    /// EPIC 15.32: Lifecycle phase for ability casting.
    /// Replaces AIAttackPhase from 15.31 with full telegraph/cast/active/recovery pipeline.
    /// </summary>
    public enum AbilityCastPhase : byte
    {
        Idle = 0,        // No ability active, ready for selection
        Telegraph = 1,   // Ground indicator visible, warning players
        Casting = 2,     // Wind-up / cast bar (interruptible window)
        Active = 3,      // Damage delivery / effect application
        Recovery = 4     // Post-attack cooldown animation
    }

    /// <summary>
    /// EPIC 15.32: Per-entity ability execution state. Replaces AIAttackState.
    /// Tracks which ability is being cast and its lifecycle phase.
    /// Managed by AbilitySelectionSystem (initiation) and AbilityExecutionSystem (phases).
    /// </summary>
    public struct AbilityExecutionState : IComponentData
    {
        public AbilityCastPhase Phase;
        public float PhaseTimer;
        public int SelectedAbilityIndex;    // Index into AbilityDefinition buffer (-1 = none)
        public Entity TargetEntity;
        public float3 TargetPosition;       // Locked ground position (for AOE)
        public float3 CastDirection;        // Locked aim direction
        public bool DamageDealt;            // One-shot flag (single-target abilities)
        public int TicksDelivered;          // For channeled abilities
        public Entity TelegraphEntity;      // Spawned telegraph (if any)

        public static AbilityExecutionState Default => new AbilityExecutionState
        {
            Phase = AbilityCastPhase.Idle,
            SelectedAbilityIndex = -1,
            TargetEntity = Entity.Null,
            TelegraphEntity = Entity.Null
        };
    }
}
