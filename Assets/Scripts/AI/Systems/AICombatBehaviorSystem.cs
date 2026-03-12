using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Transforms;
using DIG.AI.Components;
using DIG.Targeting;
using DIG.Player.Abilities;
using DIG.Combat.Systems;
using Player.Components;

namespace DIG.AI.Systems
{
    /// <summary>
    /// EPIC 15.32: Combat behavior — chase and face target.
    /// Attack initiation moved to AbilitySelectionSystem; this system only handles movement.
    /// Checks AbilityExecutionState + MovementOverride to pause movement during casts.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AIStateTransitionSystem))]
    [UpdateBefore(typeof(CombatResolutionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct AICombatBehaviorSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            state.RequireForUpdate<AIBrain>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (aiState, execState, brain, targetData, transform, moveTowards, health, entity) in
                SystemAPI.Query<
                    RefRW<AIState>,
                    RefRO<AbilityExecutionState>,
                    RefRO<AIBrain>,
                    RefRO<TargetData>,
                    RefRW<LocalTransform>,
                    RefRW<MoveTowardsAbility>,
                    RefRO<Health>>()
                .WithEntityAccess())
            {
                if (health.ValueRO.Current <= 0f) continue;

                ref var ai = ref aiState.ValueRW;
                if (ai.CurrentState != AIBehaviorState.Combat) continue;

                var target = targetData.ValueRO.TargetEntity;
                if (target == Entity.Null) continue;
                if (!_transformLookup.HasComponent(target)) continue;

                float3 selfPos = transform.ValueRO.Position;
                float3 targetPos = _transformLookup[target].Position;
                float3 toTarget = targetPos - selfPos;
                toTarget.y = 0f;
                float distance = math.length(toTarget);
                float3 dirToTarget = distance > 0.01f ? toTarget / distance : new float3(0, 0, 1);

                // Always face target — slerp rotation
                var targetRot = quaternion.LookRotation(dirToTarget, math.up());
                transform.ValueRW.Rotation = math.slerp(
                    transform.ValueRO.Rotation, targetRot, 10f * deltaTime);

                // If ability in progress, stop movement and skip sub-state selection
                if (execState.ValueRO.Phase != AbilityCastPhase.Idle)
                {
                    moveTowards.ValueRW.IsMoving = false;
                    continue;
                }

                // Also check MovementOverride enableable (set by AbilityExecutionSystem)
                if (SystemAPI.IsComponentEnabled<MovementOverride>(entity))
                {
                    moveTowards.ValueRW.IsMoving = false;
                    continue;
                }

                float meleeRange = brain.ValueRO.MeleeRange;

                if (distance > meleeRange)
                {
                    // Approach — move directly toward target (kinematic-safe)
                    ai.SubState = AICombatSubState.Approach;
                    float speed = brain.ValueRO.ChaseSpeed;
                    float3 move = dirToTarget * speed * deltaTime;
                    transform.ValueRW.Position += new float3(move.x, 0f, move.z);
                    moveTowards.ValueRW.IsMoving = false;
                }
                else
                {
                    // In range — strafe laterally around target while waiting for ability
                    if (ai.SubState != AICombatSubState.CircleStrafe)
                    {
                        ai.SubState = AICombatSubState.CircleStrafe;
                        ai.SubStateTimer = 0f;
                        // Pick initial strafe direction via RNG
                        var rng = new Random(ai.RandomSeed > 0 ? ai.RandomSeed : 1u);
                        ai.RandomSeed = rng.NextUInt(1u, uint.MaxValue);
                        // SubStateTimer sign encodes direction: positive=right, negative=left
                        ai.SubStateTimer = rng.NextBool() ? 0.01f : -0.01f;
                    }

                    ai.SubStateTimer += math.sign(ai.SubStateTimer) * deltaTime;

                    // Change direction every 1-3 seconds
                    float absStrafeTime = math.abs(ai.SubStateTimer);
                    if (absStrafeTime > 1.5f)
                    {
                        var rng = new Random(ai.RandomSeed > 0 ? ai.RandomSeed : 1u);
                        ai.RandomSeed = rng.NextUInt(1u, uint.MaxValue);
                        float newDir = rng.NextBool() ? 1f : -1f;
                        ai.SubStateTimer = newDir * 0.01f;
                    }

                    // Strafe perpendicular to target direction at half chase speed
                    float3 strafeDir = math.cross(math.up(), dirToTarget);
                    strafeDir = math.sign(ai.SubStateTimer) * strafeDir;
                    float strafeSpeed = brain.ValueRO.ChaseSpeed * 0.4f;
                    float3 strafeMove = strafeDir * strafeSpeed * deltaTime;
                    transform.ValueRW.Position += new float3(strafeMove.x, 0f, strafeMove.z);
                    moveTowards.ValueRW.IsMoving = false;
                }
            }
        }
    }
}
