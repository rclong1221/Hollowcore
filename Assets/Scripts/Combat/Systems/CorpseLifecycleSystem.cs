using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using DIG.Combat.Components;
using DIG.AI.Components;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// EPIC 16.3: Server-side corpse lifecycle management.
    /// Drives phase transitions: Ragdoll → Settled → Fading → Destroy.
    /// Strips expensive components during Settled phase and enforces MaxCorpses cap.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class CorpseLifecycleSystem : SystemBase
    {
        private EntityQuery _corpseQuery;

        protected override void OnCreate()
        {
            _corpseQuery = GetEntityQuery(
                ComponentType.ReadWrite<CorpseState>()
            );
        }

        protected override void OnUpdate()
        {
            // Ensure CorpseConfig singleton exists
            if (!SystemAPI.HasSingleton<CorpseConfig>())
            {
                var configEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(configEntity, CorpseConfig.Default);
            }

            var config = SystemAPI.GetSingleton<CorpseConfig>();
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Phase transitions
            foreach (var (corpseState, entity) in
                     SystemAPI.Query<RefRW<CorpseState>>()
                     .WithEntityAccess())
            {
                float elapsed = currentTime - corpseState.ValueRO.PhaseStartTime;

                switch (corpseState.ValueRO.Phase)
                {
                    case CorpsePhase.Ragdoll:
                        if (elapsed >= corpseState.ValueRO.RagdollDuration)
                        {
                            corpseState.ValueRW.Phase = CorpsePhase.Settled;
                            corpseState.ValueRW.PhaseStartTime = currentTime;
                            StripSettledComponents(ref ecb, entity);
                        }
                        break;

                    case CorpsePhase.Settled:
                        if (elapsed >= corpseState.ValueRO.CorpseLifetime)
                        {
                            corpseState.ValueRW.Phase = CorpsePhase.Fading;
                            corpseState.ValueRW.PhaseStartTime = currentTime;
                            StripFadingComponents(ref ecb, entity);
                        }
                        break;

                    case CorpsePhase.Fading:
                        if (elapsed >= corpseState.ValueRO.FadeOutDuration)
                        {
                            ecb.DestroyEntity(entity);
                        }
                        break;
                }
            }

            // Distance-based corpse culling (Phase 3.3)
            if (config.DistanceCullRange > 0f)
                EnforceDistanceCull(ref ecb, config, currentTime);

            // MaxCorpses cap enforcement
            EnforceMaxCorpses(ref ecb, config, currentTime);

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        /// <summary>
        /// Strip expensive AI and combat components when ragdoll settles.
        /// Only removes non-ghost-replicated components that are safe to strip.
        /// </summary>
        private void StripSettledComponents(ref EntityCommandBuffer ecb, Entity entity)
        {
            // Strip AI components
            if (EntityManager.HasComponent<AIBrain>(entity))
                ecb.RemoveComponent<AIBrain>(entity);
            if (EntityManager.HasComponent<AIState>(entity))
                ecb.RemoveComponent<AIState>(entity);
            if (EntityManager.HasComponent<AbilityExecutionState>(entity))
                ecb.RemoveComponent<AbilityExecutionState>(entity);
            if (EntityManager.HasComponent<EnemySeparationConfig>(entity))
                ecb.RemoveComponent<EnemySeparationConfig>(entity);

            // Disable enableable components instead of removing
            if (EntityManager.HasComponent<MovementOverride>(entity))
                ecb.SetComponentEnabled<MovementOverride>(entity, false);

            // Strip combat stats
            if (EntityManager.HasComponent<AttackStats>(entity))
                ecb.RemoveComponent<AttackStats>(entity);
            if (EntityManager.HasComponent<DefenseStats>(entity))
                ecb.RemoveComponent<DefenseStats>(entity);

            // Freeze in place — zero velocity without removing (part of PhysicsBody)
            if (EntityManager.HasComponent<PhysicsVelocity>(entity))
                ecb.SetComponent(entity, new PhysicsVelocity());
        }

        /// <summary>
        /// Strip physics collider to remove corpse from broadphase entirely.
        /// </summary>
        private void StripFadingComponents(ref EntityCommandBuffer ecb, Entity entity)
        {
            if (EntityManager.HasComponent<PhysicsCollider>(entity))
                ecb.RemoveComponent<PhysicsCollider>(entity);
        }

        /// <summary>
        /// EPIC 16.3 Phase 3.3: Force Settled corpses beyond DistanceCullRange from all players to Fading.
        /// Skips bosses and corpses already fading.
        /// </summary>
        private void EnforceDistanceCull(ref EntityCommandBuffer ecb, CorpseConfig config, float currentTime)
        {
            // Gather player positions
            var playerPositions = new NativeList<float3>(4, Allocator.Temp);
            foreach (var playerTransform in
                     SystemAPI.Query<RefRO<LocalTransform>>()
                     .WithAll<global::PlayerTag>())
            {
                playerPositions.Add(playerTransform.ValueRO.Position);
            }

            if (playerPositions.Length == 0)
            {
                playerPositions.Dispose();
                return;
            }

            float cullRangeSq = config.DistanceCullRange * config.DistanceCullRange;

            foreach (var (corpseState, transform, entity) in
                     SystemAPI.Query<RefRW<CorpseState>, RefRO<LocalTransform>>()
                     .WithEntityAccess())
            {
                // Only cull Settled corpses (Ragdoll still playing, Fading already dying)
                if (corpseState.ValueRO.Phase != CorpsePhase.Settled)
                    continue;
                if (corpseState.ValueRO.IsBoss)
                    continue;

                float3 corpsePos = transform.ValueRO.Position;
                bool withinRange = false;

                for (int p = 0; p < playerPositions.Length; p++)
                {
                    if (math.distancesq(corpsePos, playerPositions[p]) <= cullRangeSq)
                    {
                        withinRange = true;
                        break;
                    }
                }

                if (!withinRange)
                {
                    corpseState.ValueRW.Phase = CorpsePhase.Fading;
                    corpseState.ValueRW.PhaseStartTime = currentTime;
                    StripFadingComponents(ref ecb, entity);
                }
            }

            playerPositions.Dispose();
        }

        /// <summary>
        /// When corpse count exceeds MaxCorpses, force oldest non-boss corpses to Fading.
        /// </summary>
        private void EnforceMaxCorpses(ref EntityCommandBuffer ecb, CorpseConfig config, float currentTime)
        {
            // Use CalculateEntityCount (NOT WithoutFiltering) to respect IEnableableComponent filter.
            // WithoutFiltering would count all entities with CorpseState including alive enemies
            // where CorpseState is disabled, causing false over-cap and immediate eviction.
            var entities = _corpseQuery.ToEntityArray(Allocator.Temp);
            var states = _corpseQuery.ToComponentDataArray<CorpseState>(Allocator.Temp);
            int corpseCount = entities.Length;

            if (corpseCount <= config.MaxCorpses)
            {
                entities.Dispose();
                states.Dispose();
                return;
            }

            // Count non-boss, non-fading corpses that can be evicted
            int evictableCount = 0;
            for (int i = 0; i < states.Length; i++)
            {
                if (!states[i].IsBoss && states[i].Phase != CorpsePhase.Fading)
                    evictableCount++;
            }

            int toEvict = corpseCount - config.MaxCorpses;
            if (toEvict <= 0 || evictableCount == 0)
            {
                entities.Dispose();
                states.Dispose();
                return;
            }

            // Build sortable array of evictable corpse indices by age (oldest first)
            var evictable = new NativeArray<CorpseEvictCandidate>(evictableCount, Allocator.Temp);
            int idx = 0;
            for (int i = 0; i < states.Length; i++)
            {
                if (!states[i].IsBoss && states[i].Phase != CorpsePhase.Fading)
                {
                    evictable[idx++] = new CorpseEvictCandidate
                    {
                        EntityIndex = i,
                        PhaseStartTime = states[i].PhaseStartTime
                    };
                }
            }

            // Sort by PhaseStartTime ascending (oldest first)
            evictable.Sort(new CorpseAgeComparer());

            // Force oldest to Fading
            int evicted = 0;
            for (int i = 0; i < evictable.Length && evicted < toEvict; i++)
            {
                var e = entities[evictable[i].EntityIndex];
                var s = states[evictable[i].EntityIndex];
                s.Phase = CorpsePhase.Fading;
                s.PhaseStartTime = currentTime;
                ecb.SetComponent(e, s);
                StripFadingComponents(ref ecb, e);
                evicted++;
            }

            evictable.Dispose();
            entities.Dispose();
            states.Dispose();
        }

        private struct CorpseEvictCandidate
        {
            public int EntityIndex;
            public float PhaseStartTime;
        }

        private struct CorpseAgeComparer : System.Collections.Generic.IComparer<CorpseEvictCandidate>
        {
            public int Compare(CorpseEvictCandidate a, CorpseEvictCandidate b)
            {
                return a.PhaseStartTime.CompareTo(b.PhaseStartTime);
            }
        }
    }
}
