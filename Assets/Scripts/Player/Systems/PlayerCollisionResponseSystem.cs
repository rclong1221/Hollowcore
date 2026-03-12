using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Unity.NetCode;
using DIG.Player.Components;
using DIG.Performance;
using Player.Systems;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Three-Phase Parallel-Safe Player Collision Response System (Epic 7.3.1 + 7.3.3)
    /// 
    /// NOTE: Physical separation is now handled by PlayerSeparationSystem (7.3.4).
    /// This system focuses on gameplay response (stagger, knockdown, events).
    /// 
    /// PHASE 1 - DETECTION (Parallel): Uses Unity Physics ICollisionEventsJob to detect player-player collisions.
    ///   - Outputs collision pairs with physics data (contact point, normal, penetration)
    ///   - Computes gameplay metrics (impact speed, overlap)
    ///   - Unity schedules with optimal batch sizes (built-in parallelization)
    ///   - Output: unsorted NativeList&lt;PlayerCollisionPair&gt; (append-only, thread-safe)
    /// 
    /// PHASE 2 - AGGREGATION (Single-threaded): Aggregates collisions per-entity to find dominant collision.
    ///   - Sorts collision pairs by entity
    ///   - For each player, finds highest-impact collision (max impactSpeed * powerRatio)
    ///   - Only dominant collision triggers stagger/knockdown (prevents stagger stacking)
    ///   - Output: NativeHashMap&lt;Entity, AggregatedCollisionData&gt;
    /// 
    /// PHASE 3 - RESPONSE (Per-Entity Parallel): Uses IJobEntity with ScheduleParallel for safe parallel writes.
    ///   - Each entity processed by exactly one thread (no race conditions)
    ///   - Reads aggregated collision data for this entity
    ///   - Writes to components: PlayerCollisionState, Staggered, KnockedDown
    ///   - Uses EnabledRefRW&lt;T&gt; for tag components (no structural changes)
    ///   - Appends to CollisionEventBuffer (DynamicBuffer, per-entity = safe)
    /// 
    /// Runs in PredictedFixedStepSimulationSystemGroup for NetCode prediction/rollback.
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(CharacterControllerSystem))]
    [BurstCompile]
    public partial struct PlayerCollisionResponseSystem : ISystem
    {
        // Component lookups for Phase 2/3 processing
        private ComponentLookup<PlayerCollisionState> _collisionStateLookup;
        private ComponentLookup<PlayerState> _playerStateLookup;
        private ComponentLookup<PhysicsMass> _physicsMassLookup;
        private ComponentLookup<PhysicsVelocity> _physicsVelocityLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private BufferLookup<DIG.Player.Components.CollisionEvent> _collisionEventBufferLookup;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerCollisionSettings>();
            state.RequireForUpdate<SimulationSingleton>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            
            _collisionStateLookup = state.GetComponentLookup<PlayerCollisionState>();
            _playerStateLookup = state.GetComponentLookup<PlayerState>(isReadOnly: true);
            _physicsMassLookup = state.GetComponentLookup<PhysicsMass>(isReadOnly: true);
            _physicsVelocityLookup = state.GetComponentLookup<PhysicsVelocity>();
            _transformLookup = state.GetComponentLookup<LocalTransform>();
            _collisionEventBufferLookup = state.GetBufferLookup<DIG.Player.Components.CollisionEvent>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Epic 7.7.1: Profile entire collision response system
            using (CollisionProfilerMarkers.CollisionResponse.Auto())
            {
            // Get settings singleton
            if (!SystemAPI.TryGetSingleton<PlayerCollisionSettings>(out var settings))
                return;
            
            // Early exit if collision response disabled
            if (!settings.EnableCollisionResponse)
                return;
            
            // Get simulation singleton for collision events
            if (!SystemAPI.TryGetSingleton<SimulationSingleton>(out var simulation))
                return;
            
            // Get current network tick for event timestamping
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick.TickIndexForValidTick;
            
            // Get physics world for collision details
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            
            // Update component lookups
            _collisionStateLookup.Update(ref state);
            _playerStateLookup.Update(ref state);
            _physicsMassLookup.Update(ref state);
            _physicsVelocityLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _collisionEventBufferLookup.Update(ref state);
            
            // ========================================
            // PHASE 1: DETECTION (Parallel)
            // ========================================
            // Allocate temporary list for collision pairs (auto-disposed after job completion)
            var collisionPairs = new NativeList<PlayerCollisionPair>(64, Allocator.TempJob);
            
            // Schedule detection job - processes Unity Physics collision events in parallel
            var detectionJob = new PlayerCollisionDetectionJob
            {
                CollisionStateLookup = _collisionStateLookup,
                TransformLookup = _transformLookup,
                VelocityLookup = _physicsVelocityLookup,
                Settings = settings,
                CurrentTick = currentTick,
                PhysicsWorld = physicsWorld,
                CollisionPairs = collisionPairs.AsParallelWriter()
            };
            
            state.Dependency = detectionJob.Schedule(simulation, state.Dependency);
            
            // ========================================
            // PHASE 2: AGGREGATION (Single-threaded)
            // ========================================
            // Allocate aggregation map (entity -> aggregated data)
            var aggregatedData = new NativeHashMap<Entity, AggregatedCollisionData>(64, Allocator.TempJob);
            
            var aggregationJob = new CollisionAggregationJob
            {
                CollisionPairs = collisionPairs.AsDeferredJobArray(),
                PlayerStateLookup = _playerStateLookup,
                TransformLookup = _transformLookup,
                Settings = settings,
                AggregatedData = aggregatedData
            };
            
            state.Dependency = aggregationJob.Schedule(state.Dependency);
            
            // ========================================
            // PHASE 3: RESPONSE (Per-Entity Parallel)
            // ========================================
            var responseJob = new PlayerCollisionResponseJob
            {
                AggregatedData = aggregatedData,
                Settings = settings,
                CurrentTick = currentTick,
                DeltaTime = SystemAPI.Time.DeltaTime
            };
            
            // ScheduleParallel: Each entity processed by one thread (safe for per-entity writes)
            state.Dependency = responseJob.ScheduleParallel(state.Dependency);
            
            // Dispose temporary allocations after all jobs complete
            state.Dependency = collisionPairs.Dispose(state.Dependency);
            state.Dependency = aggregatedData.Dispose(state.Dependency);
            } // End CollisionResponse profiler marker
        }
    }
    
    // ============================================================================
    // PHASE 1: DETECTION JOB
    // ============================================================================
    
    /// <summary>
    /// Phase 1: Detects player-player collisions using Unity Physics events.
    /// Outputs PlayerCollisionPair structs with physics data for Phase 2 to consume.
    /// Uses ICollisionEventsJob for optimal Unity Physics integration.
    /// </summary>
    [BurstCompile]
    struct PlayerCollisionDetectionJob : ICollisionEventsJob
    {
        [ReadOnly] public ComponentLookup<PlayerCollisionState> CollisionStateLookup;
        [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
        [ReadOnly] public ComponentLookup<PhysicsVelocity> VelocityLookup;
        [ReadOnly] public PlayerCollisionSettings Settings;
        [ReadOnly] public uint CurrentTick;
        [ReadOnly] public PhysicsWorld PhysicsWorld;
        
        // Thread-safe parallel writer for output
        public NativeList<PlayerCollisionPair>.ParallelWriter CollisionPairs;
        
        public void Execute(Unity.Physics.CollisionEvent collisionEvent)
        {
            var entityA = collisionEvent.EntityA;
            var entityB = collisionEvent.EntityB;
            
            // Filter: Only process player-player collisions
            bool isPlayerA = CollisionStateLookup.HasComponent(entityA);
            bool isPlayerB = CollisionStateLookup.HasComponent(entityB);
            
            if (!isPlayerA || !isPlayerB)
                return;
            
            // Skip if either entity missing required components
            if (!TransformLookup.HasComponent(entityA) || !TransformLookup.HasComponent(entityB))
                return;
            if (!VelocityLookup.HasComponent(entityA) || !VelocityLookup.HasComponent(entityB))
                return;
            
            // Get transforms and velocities
            var transformA = TransformLookup[entityA];
            var transformB = TransformLookup[entityB];
            var velocityA = VelocityLookup[entityA].Linear;
            var velocityB = VelocityLookup[entityB].Linear;
            
            // Calculate collision metrics
            float3 posA = transformA.Position;
            float3 posB = transformB.Position;
            
            // Horizontal separation (ignore vertical)
            float3 toB = posB - posA;
            toB.y = 0;
            float horizontalDist = math.length(toB);
            
            // Calculate overlap
            float overlap = Settings.CombinedPlayerRadius - horizontalDist;
            
            // Calculate relative velocity (approach speed)
            float3 relativeVel = velocityA - velocityB;
            float3 direction = horizontalDist > 0.01f ? math.normalize(toB) : new float3(1, 0, 0);
            float approachSpeed = -math.dot(relativeVel, direction); // Positive when approaching
            float impactSpeed = math.max(0, approachSpeed);
            
            // Get collision details from Unity Physics
            var details = collisionEvent.CalculateDetails(ref PhysicsWorld);
            float3 contactPoint = details.EstimatedContactPointPositions.Length > 0
                ? details.EstimatedContactPointPositions[0]
                : (posA + posB) * 0.5f;
            
            // Create collision pair with all computed metrics
            var pair = new PlayerCollisionPair
            {
                EntityA = entityA,
                EntityB = entityB,
                ContactPoint = contactPoint,
                ContactNormal = collisionEvent.Normal,
                PenetrationDepth = overlap,
                ImpactSpeed = impactSpeed,
                ImpactForce = impactSpeed * Settings.EffectiveMass * 2f, // Combined mass
                Overlap = overlap,
                PositionA = posA,
                PositionB = posB,
                VelocityA = velocityA,
                VelocityB = velocityB,
                EventTick = CurrentTick
            };
            
            // Output to parallel-safe list
            CollisionPairs.AddNoResize(pair);
        }
    }
    
    // ============================================================================
    // PHASE 2: AGGREGATION JOB (Single-threaded)
    // ============================================================================
    
    /// <summary>
    /// Phase 2: Aggregates collision pairs per-entity to identify dominant collision.
    /// Epic 7.3.3: Parallel-safe architecture - aggregation phase.
    /// 
    /// - Sorts collision pairs by entity
    /// - For each player, finds highest-impact collision (max impactSpeed * powerRatio)
    /// - Only dominant collision triggers stagger/knockdown (prevents stagger stacking)
    /// - All collisions contribute to push forces (additive separation)
    /// </summary>
    [BurstCompile]
    struct CollisionAggregationJob : IJob
    {
        [ReadOnly] public NativeArray<PlayerCollisionPair> CollisionPairs;
        [ReadOnly] public ComponentLookup<PlayerState> PlayerStateLookup;
        [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
        [ReadOnly] public PlayerCollisionSettings Settings;
        
        public NativeHashMap<Entity, AggregatedCollisionData> AggregatedData;
        
        public void Execute()
        {
            // Process each collision pair and aggregate per-entity
            for (int i = 0; i < CollisionPairs.Length; i++)
            {
                var pair = CollisionPairs[i];
                
                // Calculate power for both entities
                float powerA = CalculatePower(pair.EntityA, pair.HorizontalSpeedA);
                float powerB = CalculatePower(pair.EntityB, pair.HorizontalSpeedB);
                float totalPower = powerA + powerB;
                float powerRatioA = totalPower > 0.001f ? powerA / totalPower : 0.5f;
                float powerRatioB = 1f - powerRatioA;
                
                // Calculate directional bonuses for both entities
                CalculateDirectionalBonus(pair.EntityA, pair.EntityB, pair.DirectionAtoB,
                    out byte hitDirA, out float dirMultA);
                CalculateDirectionalBonus(pair.EntityB, pair.EntityA, -pair.DirectionAtoB,
                    out byte hitDirB, out float dirMultB);
                
                // Calculate impact score for determining dominant collision
                float impactScoreA = pair.ImpactSpeed * (1f - powerRatioA); // Higher for weaker player
                float impactScoreB = pair.ImpactSpeed * (1f - powerRatioB);
                
                // Aggregate for Entity A
                AggregateForEntity(pair.EntityA, ref pair, pair.EntityB,
                    powerRatioA, hitDirA, dirMultA, impactScoreA, -pair.DirectionAtoB);
                
                // Aggregate for Entity B
                AggregateForEntity(pair.EntityB, ref pair, pair.EntityA,
                    powerRatioB, hitDirB, dirMultB, impactScoreB, pair.DirectionAtoB);
            }
            
            // Second pass: Determine stagger/knockdown based on dominant collision
            var keys = AggregatedData.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < keys.Length; i++)
            {
                var entity = keys[i];
                var data = AggregatedData[entity];
                
                // Determine if dominant collision triggers stagger or knockdown
                if (data.DominantImpactSpeed >= Settings.StaggerThreshold)
                {
                    float powerAdvantage = data.DominantPowerRatio - 0.5f;
                    
                    if (data.DominantPowerRatio < (1f - Settings.KnockdownPowerThreshold))
                    {
                        // Extreme power disadvantage = knockdown
                        data.TriggerKnockdown = true;
                    }
                    else if (powerAdvantage < 0.2f)
                    {
                        // Weaker or close match = stagger
                        data.TriggerStagger = true;
                    }
                }
                
                // Normalize cumulative push direction
                if (math.lengthsq(data.CumulativePushDirection) > 0.001f)
                {
                    data.CumulativePushDirection = math.normalize(data.CumulativePushDirection);
                }
                
                AggregatedData[entity] = data;
            }
            keys.Dispose();
        }
        
        private void AggregateForEntity(Entity entity, ref PlayerCollisionPair pair, Entity other,
            float powerRatio, byte hitDirection, float directionalMult, float impactScore, float3 knockbackDir)
        {
            AggregatedCollisionData data;
            if (!AggregatedData.TryGetValue(entity, out data))
            {
                // Initialize new aggregation
                data = new AggregatedCollisionData
                {
                    Entity = entity,
                    CollisionCount = 0,
                    DominantImpactSpeed = 0,
                    DominantPowerRatio = 0.5f,
                    MaxOverlap = 0,
                    TotalPushImpulse = 0,
                    CumulativePushDirection = float3.zero
                };
            }
            
            data.CollisionCount++;
            
            // Update dominant collision if this is more impactful
            if (impactScore > data.DominantImpactSpeed * (1f - data.DominantPowerRatio))
            {
                data.DominantOther = other;
                data.DominantImpactSpeed = pair.ImpactSpeed;
                data.DominantImpactForce = pair.ImpactForce;
                data.DominantPowerRatio = powerRatio;
                data.DominantHitDirection = hitDirection;
                data.DominantDirectionalMultiplier = directionalMult;
                data.DominantContactPoint = pair.ContactPoint;
                data.DominantContactNormal = pair.ContactNormal;
                data.KnockbackDirection = knockbackDir;
            }
            
            // Cumulative push (additive from all collisions)
            if (pair.Overlap > 0)
            {
                data.CumulativePushDirection += knockbackDir * pair.Overlap;
                data.MaxOverlap = math.max(data.MaxOverlap, pair.Overlap);
                data.TotalPushImpulse += pair.Overlap * Settings.SeparationStrength;
            }
            
            AggregatedData[entity] = data;
        }
        
        /// <summary>
        /// Calculates collision power for an entity using the utility methods.
        /// Epic 7.3.5: Power = EffectiveMass * HorizontalSpeed * StanceMultiplier * MovementMultiplier
        /// </summary>
        private float CalculatePower(Entity entity, float horizontalSpeed)
        {
            // Default to standing/idle if PlayerState not available
            PlayerStance stance = PlayerStance.Standing;
            PlayerMovementState movementState = PlayerMovementState.Idle;
            
            if (PlayerStateLookup.HasComponent(entity))
            {
                var playerState = PlayerStateLookup[entity];
                stance = playerState.Stance;
                movementState = playerState.MovementState;
            }
            
            // Use utility for calculation (Burst-compiled)
            return CollisionPowerUtility.CalculatePower(horizontalSpeed, stance, movementState, Settings);
        }
        
        private void CalculateDirectionalBonus(Entity self, Entity other, float3 directionToOther,
            out byte hitDirection, out float directionalMultiplier)
        {
            hitDirection = HitDirectionType.Side;
            directionalMultiplier = Settings.SideHitStaggerMultiplier;
            
            if (!Settings.DirectionalBonusEnabled || !TransformLookup.HasComponent(self))
                return;
            
            var transform = TransformLookup[self];
            float3 forward = math.mul(transform.Rotation, new float3(0, 0, 1));
            forward.y = 0;
            forward = math.normalizesafe(forward);
            
            float facingDot = math.dot(forward, directionToOther);
            
            if (facingDot >= Settings.BracedDotThreshold)
            {
                hitDirection = HitDirectionType.Braced;
                directionalMultiplier = Settings.BracedStaggerMultiplier;
            }
            else if (facingDot <= Settings.BackHitDotThreshold)
            {
                hitDirection = HitDirectionType.Back;
                directionalMultiplier = Settings.BackHitStaggerMultiplier;
            }
        }
    }
    
    // ============================================================================
    // PHASE 3: PER-ENTITY RESPONSE JOB (Parallel)
    // ============================================================================
    
    /// <summary>
    /// Phase 3: Applies collision response per-entity using ScheduleParallel.
    /// Epic 7.3.3: Each entity processed by exactly one thread (safe for per-entity writes).
    /// 
    /// NOTE: Physical separation handled by PlayerSeparationSystem (7.3.4).
    /// 
    /// Implements:
    /// - Asymmetric stagger via power ratio (7.3.5)
    /// - Directional bonuses (7.3.6)
    /// - Collision event buffering (7.3.7)
    /// - Stagger state management via EnabledRefRW (7.3.8)
    /// </summary>
    [BurstCompile]
    partial struct PlayerCollisionResponseJob : IJobEntity
    {
        [ReadOnly] public NativeHashMap<Entity, AggregatedCollisionData> AggregatedData;
        [ReadOnly] public PlayerCollisionSettings Settings;
        [ReadOnly] public uint CurrentTick;
        [ReadOnly] public float DeltaTime;
        
        void Execute(
            Entity entity,
            ref PlayerCollisionState collisionState,
            EnabledRefRW<Staggered> staggeredRef,
            EnabledRefRW<KnockedDown> knockedDownRef,
            DynamicBuffer<DIG.Player.Components.CollisionEvent> eventBuffer)
        {
            // Skip entities not involved in collisions this frame
            if (!AggregatedData.TryGetValue(entity, out var data))
                return;
            
            // NOTE: Physical separation now handled by PlayerSeparationSystem (7.3.4)
            
            // ========================================
            // STAGGER/KNOCKDOWN STATE (7.3.5, 7.3.8)
            // ========================================
            if (data.TriggerKnockdown && !collisionState.IsImmuneToStagger)
            {
                float baseDuration = Settings.KnockdownDuration;
                float adjustedDuration = baseDuration * data.DominantDirectionalMultiplier;
                
                float knockbackSpeed = data.DominantImpactSpeed * (1f - data.DominantPowerRatio) * Settings.PushForceMultiplier;
                knockbackSpeed = math.min(knockbackSpeed, Settings.MaxSeparationSpeed);
                
                collisionState.KnockdownTimeRemaining = adjustedDuration;
                collisionState.KnockdownImpactSpeed = data.DominantImpactSpeed;
                collisionState.StaggerVelocity = data.KnockbackDirection * knockbackSpeed;
                collisionState.LastPowerRatio = data.DominantPowerRatio;
                collisionState.LastHitDirection = data.DominantHitDirection;
                
                // Enable knockdown tag via EnabledRefRW (no structural change)
                knockedDownRef.ValueRW = true;
                staggeredRef.ValueRW = false;
            }
            else if (data.TriggerStagger && !collisionState.IsImmuneToStagger)
            {
                float baseDuration = math.lerp(
                    Settings.MinStaggerDuration,
                    Settings.MaxStaggerDuration,
                    1f - data.DominantPowerRatio);
                float adjustedDuration = baseDuration * data.DominantDirectionalMultiplier;
                
                float knockbackSpeed = data.DominantImpactSpeed * (1f - data.DominantPowerRatio) * Settings.PushForceMultiplier;
                knockbackSpeed = math.min(knockbackSpeed, Settings.MaxSeparationSpeed);
                
                collisionState.StaggerTimeRemaining = adjustedDuration;
                collisionState.StaggerIntensity = math.saturate(data.DominantImpactSpeed / 10f);
                collisionState.StaggerVelocity = data.KnockbackDirection * knockbackSpeed;
                collisionState.LastPowerRatio = data.DominantPowerRatio;
                collisionState.LastHitDirection = data.DominantHitDirection;
                
                // Enable stagger tag via EnabledRefRW (no structural change)
                staggeredRef.ValueRW = true;
            }
            
            // Update collision tracking
            collisionState.LastCollisionTick = CurrentTick;
            collisionState.LastCollisionEntity = data.DominantOther;
            collisionState.CollisionCooldown = Settings.CollisionCooldownDuration;
            
            // ========================================
            // COLLISION EVENT BUFFERING (7.3.7)
            // ========================================
            // Limit buffer size (FIFO eviction)
            if (eventBuffer.Length >= 8)
                eventBuffer.RemoveAt(0);
            
            eventBuffer.Add(new DIG.Player.Components.CollisionEvent
            {
                OtherEntity = data.DominantOther,
                ContactPoint = data.DominantContactPoint,
                ContactNormal = data.DominantContactNormal,
                ImpactForce = data.DominantImpactForce,
                ImpactSpeed = data.DominantImpactSpeed,
                EventTick = CurrentTick,
                HitDirection = data.DominantHitDirection,
                PowerRatio = data.DominantPowerRatio,
                TriggeredStagger = data.TriggerStagger,
                TriggeredKnockdown = data.TriggerKnockdown
            });
        }
    }
}
