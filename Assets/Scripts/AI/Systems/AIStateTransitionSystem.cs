using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Transforms;
using DIG.AI.Components;
using DIG.Aggro.Components;
using DIG.Player.Abilities;
using DIG.Combat.Systems;
using Player.Components;

namespace DIG.AI.Systems
{
    /// <summary>
    /// EPIC 15.32: HFSM state machine for AI behavior.
    /// Reads aggro/health state and transitions between Idle, Patrol, Combat, ReturnHome.
    /// Guard: never transitions during active ability (AbilityExecutionState.Phase != Idle).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(CombatResolutionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct AIStateTransitionSystem : ISystem
    {
        private ComponentLookup<Health> _healthLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AIBrain>();
            _healthLookup = state.GetComponentLookup<Health>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _healthLookup.Update(ref state);
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (aiState, execState, brain, aggroState, spawnPos, transform, moveTowards, entity) in
                SystemAPI.Query<
                    RefRW<AIState>,
                    RefRO<AbilityExecutionState>,
                    RefRO<AIBrain>,
                    RefRO<AggroState>,
                    RefRO<SpawnPosition>,
                    RefRO<LocalTransform>,
                    RefRO<MoveTowardsAbility>>()
                .WithEntityAccess())
            {
                if (_healthLookup.HasComponent(entity) && _healthLookup[entity].Current <= 0f) continue;

                ref var ai = ref aiState.ValueRW;

                // Tick timers
                ai.StateTimer += deltaTime;
                ai.SubStateTimer += deltaTime;
                ai.AttackCooldownRemaining = math.max(0f, ai.AttackCooldownRemaining - deltaTime);

                // Guard: never transition during active ability
                if (execState.ValueRO.Phase != AbilityCastPhase.Idle)
                    continue;

                var prevState = ai.CurrentState;
                bool isAggroed = aggroState.ValueRO.IsAggroed;

                // ANY → COMBAT (highest priority)
                if (isAggroed && ai.CurrentState != AIBehaviorState.Combat)
                {
                    ai.CurrentState = AIBehaviorState.Combat;
                    ai.SubState = AICombatSubState.Approach;
                    ai.HasPatrolTarget = false;
                    ai.SubStateTimer = 0f;
                }
                // COMBAT → RETURN_HOME (aggro dropped)
                else if (!isAggroed && ai.CurrentState == AIBehaviorState.Combat)
                {
                    ai.CurrentState = AIBehaviorState.ReturnHome;
                }
                // RETURN_HOME → IDLE (arrived at spawn)
                else if (ai.CurrentState == AIBehaviorState.ReturnHome)
                {
                    float3 toSpawn = spawnPos.ValueRO.Position - transform.ValueRO.Position;
                    toSpawn.y = 0f;
                    if (math.lengthsq(toSpawn) < 1.5f * 1.5f)
                    {
                        ai.CurrentState = AIBehaviorState.Idle;
                    }
                }
                // IDLE → PATROL (waited long enough)
                else if (ai.CurrentState == AIBehaviorState.Idle)
                {
                    // Deterministic random threshold 5-15s
                    var rng = new Unity.Mathematics.Random(ai.RandomSeed);
                    float threshold = rng.NextFloat(5f, 15f);
                    ai.RandomSeed = rng.state;

                    if (ai.StateTimer > threshold)
                    {
                        ai.CurrentState = AIBehaviorState.Patrol;
                        ai.HasPatrolTarget = false;
                    }
                }
                // PATROL → IDLE (arrived or timeout)
                else if (ai.CurrentState == AIBehaviorState.Patrol)
                {
                    // Distance-based arrival: AIIdleBehaviorSystem clears HasPatrolTarget on arrival
                    bool arrived = ai.HasPatrolTarget == false && ai.StateTimer > 0.5f;
                    if (arrived || ai.StateTimer > 20f)
                    {
                        ai.CurrentState = AIBehaviorState.Idle;
                        ai.HasPatrolTarget = false;
                    }
                }

                // Reset state timer on state change
                if (ai.CurrentState != prevState)
                {
                    ai.StateTimer = 0f;
                }
            }
        }
    }
}
