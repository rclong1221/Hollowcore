using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Swarm.Components;
using DIG.Swarm.Profiling;
using DIG.AI.Components;
using Player.Components;

namespace DIG.Swarm.Systems
{
    /// <summary>
    /// EPIC 16.2 Phase 3: Simplified combat AI for promoted swarm entities.
    /// Chase nearest player, attack when in range. No patrol, investigate, flee, or circle-strafe.
    /// Uses existing AbilityExecutionSystem for damage delivery via PendingCombatHit.
    ///
    /// Only processes entities with SwarmCombatTag + AIBrain (Archetype=Swarm).
    /// Existing AICombatBehaviorSystem also processes these but SwarmCombatBehaviorSystem
    /// provides the simplified chase logic specific to swarm behavior.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SwarmDemotionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct SwarmCombatBehaviorSystem : ISystem
    {
        private NativeList<float3> _playerPositions;
        private NativeList<Entity> _playerEntities;
        private EntityQuery _combatQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SwarmConfig>();
            _playerPositions = new NativeList<float3>(8, Allocator.Persistent);
            _playerEntities = new NativeList<Entity>(8, Allocator.Persistent);
            _combatQuery = SystemAPI.QueryBuilder().WithAll<SwarmCombatTag>().Build();
            state.RequireForUpdate(_combatQuery);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_playerPositions.IsCreated) _playerPositions.Dispose();
            if (_playerEntities.IsCreated) _playerEntities.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using (SwarmProfilerMarkers.CombatBehavior.Auto())
            {
                var config = SystemAPI.GetSingleton<SwarmConfig>();
                float dt = SystemAPI.Time.DeltaTime;

                // Gather player positions
                _playerPositions.Clear();
                _playerEntities.Clear();
                foreach (var (transform, entity) in
                    SystemAPI.Query<RefRO<LocalTransform>>()
                    .WithAll<PlayerTag>()
                    .WithEntityAccess())
                {
                    _playerPositions.Add(transform.ValueRO.Position);
                    _playerEntities.Add(entity);
                }

                if (_playerPositions.Length == 0) return;

                foreach (var (transform, brain, aiState, combatTag) in
                    SystemAPI.Query<RefRW<LocalTransform>, RefRO<AIBrain>, RefRW<AIState>, RefRO<SwarmCombatTag>>())
                {
                    // Only process swarm archetype
                    if (brain.ValueRO.Archetype != AIBrainArchetype.Swarm)
                        continue;

                    // Find nearest player
                    float minDistSq = float.MaxValue;
                    float3 nearestPlayerPos = float3.zero;
                    Entity nearestPlayer = Entity.Null;

                    for (int p = 0; p < _playerPositions.Length; p++)
                    {
                        float distSq = math.distancesq(transform.ValueRO.Position, _playerPositions[p]);
                        if (distSq < minDistSq)
                        {
                            minDistSq = distSq;
                            nearestPlayerPos = _playerPositions[p];
                            nearestPlayer = _playerEntities[p];
                        }
                    }

                    float dist = math.sqrt(minDistSq);
                    float3 myPos = transform.ValueRO.Position;

                    // Force into combat state
                    ref var stateRef = ref aiState.ValueRW;
                    stateRef.CurrentState = AIBehaviorState.Combat;

                    // Chase or attack
                    float meleeRange = config.CombatMeleeRange;

                    if (dist > meleeRange)
                    {
                        // Chase: move toward nearest player
                        float3 dir = math.normalizesafe(nearestPlayerPos - myPos);
                        float3 movement = dir * config.CombatChaseSpeed * dt;
                        movement.y = 0f;
                        transform.ValueRW.Position += movement;

                        // Face movement direction
                        if (math.lengthsq(dir) > 0.001f)
                        {
                            quaternion targetRot = quaternion.LookRotationSafe(
                                new float3(dir.x, 0f, dir.z), math.up());
                            transform.ValueRW.Rotation = math.slerp(
                                transform.ValueRO.Rotation, targetRot, 10f * dt);
                        }

                        stateRef.SubState = AICombatSubState.Approach;
                    }
                    else
                    {
                        // In melee range — the existing AbilityExecutionSystem handles attack
                        // via AIBrain.AttackCooldown and PendingCombatHit creation.
                        // We just need to ensure the state is Attack.
                        stateRef.SubState = AICombatSubState.Attack;

                        // Face the target
                        float3 dir = math.normalizesafe(nearestPlayerPos - myPos);
                        if (math.lengthsq(dir) > 0.001f)
                        {
                            quaternion targetRot = quaternion.LookRotationSafe(
                                new float3(dir.x, 0f, dir.z), math.up());
                            transform.ValueRW.Rotation = math.slerp(
                                transform.ValueRO.Rotation, targetRot, 15f * dt);
                        }
                    }
                }
            }
        }
    }
}
