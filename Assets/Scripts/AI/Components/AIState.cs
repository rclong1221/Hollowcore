using Unity.Entities;
using Unity.Mathematics;

namespace DIG.AI.Components
{
    /// <summary>
    /// EPIC 15.31: Runtime HFSM state managed by AIStateTransitionSystem.
    /// </summary>
    public struct AIState : IComponentData
    {
        public AIBehaviorState CurrentState;
        public AICombatSubState SubState;
        public float StateTimer;
        public float SubStateTimer;
        public float AttackCooldownRemaining;
        public float3 PatrolTarget;
        public bool HasPatrolTarget;
        public uint RandomSeed;

        public static AIState Default(uint seed) => new AIState
        {
            CurrentState = AIBehaviorState.Idle,
            SubState = AICombatSubState.Approach,
            RandomSeed = seed > 0 ? seed : 1u
        };
    }
}
