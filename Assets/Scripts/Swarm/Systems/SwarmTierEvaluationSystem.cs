using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Swarm.Components;
using DIG.Swarm.Profiling;
using Player.Components;

namespace DIG.Swarm.Systems
{
    /// <summary>
    /// EPIC 16.2 Phase 3: Evaluates swarm entities against player positions to decide
    /// tier promotion/demotion. Runs every N frames (configurable) to amortize cost.
    ///
    /// Pipeline:
    ///   1. Bulk copy particle positions (ToComponentDataArray — fast memcpy from chunks)
    ///   2. ParticleDistanceJob (Burst, parallel) — computes min distance to any player per particle
    ///   3. Main thread scan: create ECB commands for promotions (small fraction of total)
    ///   4. Aware + Combat tiers: direct foreach (hundreds/tens of entities — not worth job overhead)
    ///
    /// Promotion: Particle → Aware (AwareRange), Aware → Combat (CombatRange)
    /// Demotion: Combat → Aware (DemoteRange), Aware → Particle (AwareRange + Hysteresis)
    /// Hysteresis prevents thrashing at tier boundaries.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SwarmParticleMovementSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct SwarmTierEvaluationSystem : ISystem
    {
        private int _frameCount;
        private NativeList<float3> _playerPositions;
        private EntityQuery _particleQuery;
        private EntityQuery _combatQuery;
        private EntityQuery _awareQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SwarmConfig>();
            _playerPositions = new NativeList<float3>(8, Allocator.Persistent);
            _frameCount = 0;

            _particleQuery = SystemAPI.QueryBuilder()
                .WithAll<SwarmParticle, SwarmAnimState>()
                .WithNone<SwarmAgent>()
                .Build();

            _combatQuery = SystemAPI.QueryBuilder()
                .WithAll<SwarmCombatTag>()
                .Build();

            _awareQuery = SystemAPI.QueryBuilder()
                .WithAll<SwarmAgent>()
                .Build();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_playerPositions.IsCreated)
                _playerPositions.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SwarmConfig>();

            _frameCount++;
            int evalInterval = math.max(1, config.TierEvalFrameInterval);
            if ((_frameCount % evalInterval) != 0)
                return;

            using (SwarmProfilerMarkers.TierEvaluation.Auto())
            {
                // Gather player positions (1-4 players, negligible cost)
                _playerPositions.Clear();
                foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
                {
                    _playerPositions.Add(transform.ValueRO.Position);
                }

                if (_playerPositions.Length == 0)
                    return;

                var ecb = new EntityCommandBuffer(Allocator.Temp);

                float awareRangeSq = config.AwareRange * config.AwareRange;
                float combatRangeSq = config.CombatRange * config.CombatRange;
                float demoteRangeSq = config.DemoteRange * config.DemoteRange;
                float awareDemoteRangeSq = (config.AwareRange + config.AwareHysteresis) *
                                           (config.AwareRange + config.AwareHysteresis);

                // O(1) counts via chunk metadata
                int currentCombatCount = _combatQuery.CalculateEntityCount();
                int currentAwareCount = _awareQuery.CalculateEntityCount();

                // --- Phase 1: Evaluate pure particles for promotion (Burst, parallel) ---
                int particleCount = _particleQuery.CalculateEntityCount();
                if (particleCount > 0)
                {
                    var particles = _particleQuery.ToComponentDataArray<SwarmParticle>(Allocator.TempJob);
                    var entities = _particleQuery.ToEntityArray(Allocator.TempJob);

                    // Copy player positions to NativeArray for Burst job
                    var playerPosArray = new NativeArray<float3>(_playerPositions.Length, Allocator.TempJob);
                    _playerPositions.AsArray().CopyTo(playerPosArray);

                    var minDists = new NativeArray<float>(particleCount, Allocator.TempJob);
                    var nearestIdx = new NativeArray<int>(particleCount, Allocator.TempJob);

                    new ParticleDistanceJob
                    {
                        Particles = particles,
                        PlayerPositions = playerPosArray,
                        MinDistSq = minDists,
                        NearestPlayerIdx = nearestIdx,
                    }.Schedule(particleCount, 64).Complete();

                    playerPosArray.Dispose();

                    // Main thread: create ECB for promotions within aware range
                    for (int i = 0; i < particleCount; i++)
                    {
                        if (minDists[i] < awareRangeSq && currentAwareCount < config.MaxAwareEntities)
                        {
                            int pIdx = nearestIdx[i];
                            ecb.AddComponent(entities[i], new SwarmAgent
                            {
                                FlowTarget = _playerPositions[pIdx],
                                AgentTimer = 0f,
                                SourceParticleID = particles[i].ParticleID
                            });
                            ecb.AddComponent(entities[i], new SwarmGroupID { GroupIndex = 0 });
                            currentAwareCount++;
                        }
                    }

                    particles.Dispose();
                    entities.Dispose();
                    minDists.Dispose();
                    nearestIdx.Dispose();
                }

                // --- Phase 2: Evaluate aware agents (foreach — capped at MaxAwareEntities) ---
                foreach (var (particle, agent, animState, entity) in
                    SystemAPI.Query<RefRO<SwarmParticle>, RefRW<SwarmAgent>, RefRO<SwarmAnimState>>()
                    .WithEntityAccess())
                {
                    float minDistSq = float.MaxValue;
                    float3 nearestPlayer = float3.zero;

                    for (int p = 0; p < _playerPositions.Length; p++)
                    {
                        float distSq = math.distancesq(particle.ValueRO.Position, _playerPositions[p]);
                        if (distSq < minDistSq)
                        {
                            minDistSq = distSq;
                            nearestPlayer = _playerPositions[p];
                        }
                    }

                    agent.ValueRW.FlowTarget = nearestPlayer;
                    agent.ValueRW.AgentTimer += SystemAPI.Time.DeltaTime * config.TierEvalFrameInterval;

                    if (minDistSq < combatRangeSq && currentCombatCount < config.MaxCombatEntities)
                    {
                        var promotionEvent = ecb.CreateEntity();
                        ecb.AddComponent(promotionEvent, new SwarmPromotionEvent
                        {
                            ParticleID = particle.ValueRO.ParticleID,
                            Position = particle.ValueRO.Position,
                            Velocity = particle.ValueRO.Velocity,
                            AnimClipIndex = animState.ValueRO.AnimClipIndex,
                            AnimTime = animState.ValueRO.AnimTime,
                            SourceEntity = entity
                        });
                        currentCombatCount++;
                    }
                    else if (minDistSq > awareDemoteRangeSq)
                    {
                        ecb.RemoveComponent<SwarmAgent>(entity);
                        ecb.RemoveComponent<SwarmGroupID>(entity);
                    }
                }

                // --- Phase 3: Evaluate combat entities for demotion (max ~20, foreach) ---
                foreach (var (combatTag, transform, entity) in
                    SystemAPI.Query<RefRO<SwarmCombatTag>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
                {
                    float minDistSq = float.MaxValue;

                    for (int p = 0; p < _playerPositions.Length; p++)
                    {
                        float distSq = math.distancesq(transform.ValueRO.Position, _playerPositions[p]);
                        if (distSq < minDistSq)
                            minDistSq = distSq;
                    }

                    float lifetime = (float)SystemAPI.Time.ElapsedTime - combatTag.ValueRO.PromotionTime;
                    if (lifetime < 2f)
                        continue;

                    if (minDistSq > demoteRangeSq)
                    {
                        var demotionEvent = ecb.CreateEntity();
                        ecb.AddComponent(demotionEvent, new SwarmDemotionEvent
                        {
                            CombatEntity = entity,
                            Position = transform.ValueRO.Position,
                            Velocity = float3.zero
                        });
                    }
                }

                ecb.Playback(state.EntityManager);
                ecb.Dispose();
            }
        }

        /// <summary>
        /// Burst-compiled parallel distance computation for particle-tier entities.
        /// Finds the nearest player and squared distance for each particle.
        /// </summary>
        [BurstCompile]
        struct ParticleDistanceJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SwarmParticle> Particles;
            [ReadOnly] public NativeArray<float3> PlayerPositions;
            [WriteOnly] public NativeArray<float> MinDistSq;
            [WriteOnly] public NativeArray<int> NearestPlayerIdx;

            public void Execute(int i)
            {
                float3 pos = Particles[i].Position;
                float bestDistSq = float.MaxValue;
                int bestIdx = 0;

                for (int p = 0; p < PlayerPositions.Length; p++)
                {
                    float distSq = math.distancesq(pos, PlayerPositions[p]);
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestIdx = p;
                    }
                }

                MinDistSq[i] = bestDistSq;
                NearestPlayerIdx[i] = bestIdx;
            }
        }
    }
}
