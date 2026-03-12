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
    /// EPIC 15.31: Return home behavior after leash/aggro drop.
    /// Moves toward SpawnPosition. AIStateTransitionSystem handles transition to Idle on arrival.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AIStateTransitionSystem))]
    [UpdateBefore(typeof(CombatResolutionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct AIReturnHomeBehaviorSystem : ISystem
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
                    RefRO<AIState>,
                    RefRO<AIBrain>,
                    RefRO<SpawnPosition>,
                    RefRW<MoveTowardsAbility>,
                    RefRW<LocalTransform>,
                    RefRO<Health>>())
            {
                if (health.ValueRO.Current <= 0f) continue;
                if (aiState.ValueRO.CurrentState != AIBehaviorState.ReturnHome) continue;

                moveTowards.ValueRW.IsMoving = false; // AI moves directly, not via MoveTowardsSystem

                // Move directly toward spawn (kinematic-safe)
                float3 diff = spawnPos.ValueRO.Position - transform.ValueRO.Position;
                diff.y = 0;
                float distSq = math.lengthsq(diff);

                if (distSq > 0.01f)
                {
                    float3 dir = math.normalize(diff);
                    float speed = brain.ValueRO.ChaseSpeed;
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
