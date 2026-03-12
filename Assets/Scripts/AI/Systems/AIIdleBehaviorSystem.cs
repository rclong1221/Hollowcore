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
    /// EPIC 15.31: Idle and patrol behavior.
    /// Idle: stand still. Patrol: wander within PatrolRadius of SpawnPosition.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AIStateTransitionSystem))]
    [UpdateBefore(typeof(CombatResolutionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct AIIdleBehaviorSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AIBrain>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (aiState, brain, spawnPos, moveTowards, transform, health) in
                SystemAPI.Query<
                    RefRW<AIState>,
                    RefRO<AIBrain>,
                    RefRO<SpawnPosition>,
                    RefRW<MoveTowardsAbility>,
                    RefRW<LocalTransform>,
                    RefRO<Health>>())
            {
                if (health.ValueRO.Current <= 0f) continue;

                ref var ai = ref aiState.ValueRW;
                moveTowards.ValueRW.IsMoving = false; // AI moves directly, not via MoveTowardsSystem

                if (ai.CurrentState == AIBehaviorState.Idle)
                {
                    // Stand still — no movement
                }
                else if (ai.CurrentState == AIBehaviorState.Patrol)
                {
                    if (!ai.HasPatrolTarget)
                    {
                        // Pick random point within PatrolRadius of spawn
                        var rng = new Unity.Mathematics.Random(ai.RandomSeed);
                        float angle = rng.NextFloat(0f, math.PI * 2f);
                        float dist = rng.NextFloat(1f, brain.ValueRO.PatrolRadius);
                        ai.RandomSeed = rng.state;

                        float3 spawn = spawnPos.ValueRO.Position;
                        ai.PatrolTarget = spawn + new float3(
                            math.cos(angle) * dist,
                            0f,
                            math.sin(angle) * dist
                        );
                        ai.HasPatrolTarget = true;
                    }

                    if (ai.HasPatrolTarget)
                    {
                        // Distance-based arrival check + direct movement
                        float3 diff = ai.PatrolTarget - transform.ValueRO.Position;
                        diff.y = 0;
                        float distSq = math.lengthsq(diff);

                        if (distSq < 0.5f * 0.5f)
                        {
                            // Arrived
                            ai.HasPatrolTarget = false;
                        }
                        else
                        {
                            // Move directly toward patrol target (kinematic-safe)
                            float3 dir = math.normalize(diff);
                            float speed = brain.ValueRO.PatrolSpeed;
                            float3 move = dir * speed * deltaTime;
                            transform.ValueRW.Position += new float3(move.x, 0f, move.z);

                            // Face movement direction
                            var targetRot = quaternion.LookRotation(dir, math.up());
                            transform.ValueRW.Rotation = math.slerp(
                                transform.ValueRO.Rotation, targetRot, 10f * deltaTime);
                        }
                    }
                }
            }
        }
    }
}
