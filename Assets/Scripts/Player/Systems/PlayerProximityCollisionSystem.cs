using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using DIG.Player.Components;
using DIG.Performance;
using Player.Components;
using Player.Systems; // For PlayerMovementSystem

namespace DIG.Player.Systems
{
    /// <summary>
    /// Detects player-player proximity and triggers stagger/knockdown based on collision power.
    /// 
    /// This system runs AFTER PlayerMovementSystem (to see current velocity) and
    /// BEFORE CharacterControllerSystem (to detect before CC pushes players apart).
    /// 
    /// Uses position-based proximity detection rather than Unity Physics collision events,
    /// which don't fire because the CharacterController keeps players separated.
    /// 
    /// IMPORTANT: This system queries ALL players (including remote ghosts without Simulate)
    /// to detect proximity, but only applies collision effects to entities with Simulate tag.
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerMovementSystem))]  // MUST run after movement sets velocity!
    [UpdateBefore(typeof(global::Player.Systems.CharacterControllerSystem))]
    // Disabled BurstCompile for debugging - re-enable after fixing
    // [BurstCompile]
    public partial struct PlayerProximityCollisionSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<PhysicsVelocity> _velocityLookup;
        private ComponentLookup<PlayerState> _playerStateLookup;
        private ComponentLookup<PlayerCollisionState> _collisionStateLookup;
        private ComponentLookup<CharacterControllerSettings> _ccSettingsLookup;
        private ComponentLookup<Simulate> _simulateLookup;
        private ComponentLookup<DodgeRollState> _dodgeRollLookup;
        private ComponentLookup<DodgeDiveState> _dodgeDiveLookup;
        private BufferLookup<DIG.Player.Components.CollisionEvent> _collisionEventBufferLookup;
        private ComponentLookup<TeamId> _teamIdLookup;
        private ComponentLookup<CollisionGracePeriod> _gracePeriodLookup;
        
        // Epic 7.7.6: Temporal coherence - cache collision data across frames
        private NativeHashMap<CollisionPairKey, CachedCollisionData> _collisionCache;
        private int _framesSinceEviction;
        private const int EvictionIntervalFrames = 60; // Evict stale entries every ~1 second at 60fps
        
        // Epic 7.7.7: Track active player entities for cache cleanup on deletion
        private NativeHashSet<Entity> _previousFrameEntities;
        private NativeHashSet<Entity> _currentFrameEntities;
        
        // [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerCollisionSettings>();
            state.RequireForUpdate<NetworkTime>();
            
            _transformLookup = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
            _velocityLookup = state.GetComponentLookup<PhysicsVelocity>(isReadOnly: true);
            _playerStateLookup = state.GetComponentLookup<PlayerState>(isReadOnly: true);
            _collisionStateLookup = state.GetComponentLookup<PlayerCollisionState>();
            _ccSettingsLookup = state.GetComponentLookup<CharacterControllerSettings>(isReadOnly: true);
            _simulateLookup = state.GetComponentLookup<Simulate>(isReadOnly: true);
            _dodgeRollLookup = state.GetComponentLookup<DodgeRollState>(isReadOnly: true);
            _dodgeDiveLookup = state.GetComponentLookup<DodgeDiveState>(isReadOnly: true);
            _collisionEventBufferLookup = state.GetBufferLookup<DIG.Player.Components.CollisionEvent>();
            _teamIdLookup = state.GetComponentLookup<TeamId>(isReadOnly: true);
            _gracePeriodLookup = state.GetComponentLookup<CollisionGracePeriod>(isReadOnly: true);
            
            // Epic 7.7.6: Initialize collision cache with persistent allocator
            // Initial capacity: 32 pairs (handles up to ~8 players with frequent collisions)
            _collisionCache = new NativeHashMap<CollisionPairKey, CachedCollisionData>(32, Allocator.Persistent);
            _framesSinceEviction = 0;
            
            // Epic 7.7.7: Initialize entity tracking sets for deletion detection
            _previousFrameEntities = new NativeHashSet<Entity>(64, Allocator.Persistent);
            _currentFrameEntities = new NativeHashSet<Entity>(64, Allocator.Persistent);
        }
        
        // [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            // Epic 7.7.6: Dispose persistent collision cache
            if (_collisionCache.IsCreated)
            {
                _collisionCache.Dispose();
            }
            
            // Epic 7.7.7: Dispose entity tracking sets
            if (_previousFrameEntities.IsCreated)
            {
                _previousFrameEntities.Dispose();
            }
            if (_currentFrameEntities.IsCreated)
            {
                _currentFrameEntities.Dispose();
            }
        }
        
        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Epic 7.7.1: Profile entire proximity collision detection
            using (CollisionProfilerMarkers.ProximityCollision.Auto())
            {
            if (!SystemAPI.TryGetSingleton<PlayerCollisionSettings>(out var settings))
                return;
            
            if (!settings.EnableCollisionResponse)
                return;
            
            // Epic 7.6.3: Get game settings for friendly fire and team collision filtering
            // Default to full friendly fire enabled if singleton doesn't exist
            var gameSettings = SystemAPI.HasSingleton<CollisionGameSettings>()
                ? SystemAPI.GetSingleton<CollisionGameSettings>()
                : CollisionGameSettings.Default;
            
            // Epic 7.7.4: Get quality settings for adaptive scaling
            // Default to High quality if singleton doesn't exist (CollisionQualitySystem creates it)
            var qualitySettings = SystemAPI.HasSingleton<CollisionQualitySettings>()
                ? SystemAPI.GetSingleton<CollisionQualitySettings>()
                : CollisionQualitySettings.CreateDefault();
            
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick.TickIndexForValidTick;
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            // Epic 7.7.6: Cache maintenance - increment staleness and periodic eviction
            _framesSinceEviction++;
            if (_framesSinceEviction >= EvictionIntervalFrames)
            {
                _framesSinceEviction = 0;
                EvictStaleCacheEntries();
            }
            
            // Epic 7.7.6: Increment staleness for all cached entries at frame start
            IncrementCacheStaleness();
            
            // Epic 7.7.7: Swap entity tracking sets for deletion detection
            // Previous frame entities become "old", current frame will be populated during iteration
            _previousFrameEntities.Clear();
            var tempSwap = _previousFrameEntities;
            _previousFrameEntities = _currentFrameEntities;
            _currentFrameEntities = tempSwap;;
            
            // Update lookups
            _transformLookup.Update(ref state);
            _velocityLookup.Update(ref state);
            _playerStateLookup.Update(ref state);
            _collisionStateLookup.Update(ref state);
            _ccSettingsLookup.Update(ref state);
            _simulateLookup.Update(ref state);
            _dodgeRollLookup.Update(ref state);
            _dodgeDiveLookup.Update(ref state);
            _collisionEventBufferLookup.Update(ref state);
            _teamIdLookup.Update(ref state);
            _gracePeriodLookup.Update(ref state);
            
            // Epic 7.7.1: Profile player gathering phase
            CollisionProfilerMarkers.ProximityCollision_GatherPlayers.Begin();
            
            // Epic 7.7.2: Track memory allocation for player list
            MemoryOptimizationUtility.PlayerListAllocation.Begin();
            
            // Epic 7.7.2: Use optimized capacity hint to avoid NativeList resizing
            // Capacity includes 50% overhead to handle player count fluctuations
            int playerCapacity = MemoryOptimizationUtility.CalculatePlayerListCapacity();
            
            // Epic 7.7.2: Use TempJob allocator for per-frame collections
            // TempJob is faster than Temp and auto-disposed at job completion
            // Collect ALL players - including remote ghosts (no Simulate requirement)
            // This is essential because on the client, only the local player has Simulate,
            // but we need to detect collisions with remote players too!
            var players = new NativeList<PlayerData>(playerCapacity, Allocator.TempJob);
            
            MemoryOptimizationUtility.PlayerListAllocation.End();
            
            foreach (var (transform, velocity, playerState, collisionState, ccSettings, entity) 
                in SystemAPI.Query<RefRO<LocalTransform>, RefRO<PhysicsVelocity>, RefRO<PlayerState>, 
                    RefRO<PlayerCollisionState>, RefRO<CharacterControllerSettings>>()
                    .WithAll<PlayerTag>()  // Removed Simulate requirement - query ALL players
                    .WithEntityAccess())
            {
                // Check if this entity has Simulate (can be modified)
                bool hasSimulate = _simulateLookup.HasComponent(entity);
                
                // Epic 7.4.3: Get dodge state info for i-frame immunity
                DodgeInfo dodgeInfo = GetDodgeInfo(entity, settings);
                
                // Epic 7.6.3: Get team ID for team-based collision filtering
                TeamId teamId = _teamIdLookup.HasComponent(entity) 
                    ? _teamIdLookup[entity] 
                    : new TeamId { Value = 0 };
                
                // Epic 7.6.4: Get grace period info for spawn/teleport protection
                // Epic 7.7.4: Skip grace period checks at Low quality for performance
                GracePeriodInfo gracePeriodInfo = default;
                if (qualitySettings.ShouldCheckGracePeriods && _gracePeriodLookup.HasComponent(entity))
                {
                    var gp = _gracePeriodLookup[entity];
                    gracePeriodInfo = new GracePeriodInfo
                    {
                        HasGracePeriod = true,
                        IgnorePlayerCollision = gp.IgnorePlayerCollision,
                        IgnoreAllCollision = gp.IgnoreAllCollision
                    };
                }
                
                players.Add(new PlayerData
                {
                    Entity = entity,
                    Position = transform.ValueRO.Position,
                    Velocity = velocity.ValueRO.Linear,
                    Radius = ccSettings.ValueRO.Radius,
                    PlayerState = playerState.ValueRO,
                    CollisionState = collisionState.ValueRO,
                    CollisionCooldown = collisionState.ValueRO.CollisionCooldown,
                    HasSimulate = hasSimulate,
                    Dodge = dodgeInfo,
                    Team = teamId,
                    GracePeriod = gracePeriodInfo
                });
                
                // Epic 7.7.7: Track this entity for deletion detection
                _currentFrameEntities.Add(entity);
            }
            
            // Epic 7.7.7: Clean up cache entries for deleted entities
            CleanupDeletedEntityCacheEntries();
            
            // Epic 7.7.1: End gather phase
            CollisionProfilerMarkers.ProximityCollision_GatherPlayers.End();
            

            
            // Epic 7.7.3: Try to use spatial hash grid for O(N) collision detection
            bool useSpatialHash = false;
            SpatialHashGrid spatialGrid = default;
            
            if (SystemAPI.TryGetSingleton<SpatialHashGrid>(out spatialGrid) && spatialGrid.IsPopulated)
            {
                // Check if static grid data is available
                if (SpatialHashGridData.IsInitialized && SpatialHashGridData.CellToEntities.IsCreated)
                {
                    useSpatialHash = true;
                }
            }
            
            // Epic 7.7.1: Profile O(N²) or O(N*k) distance checks
            CollisionProfilerMarkers.ProximityCollision_DistanceChecks.Begin();
            int pairsChecked = 0;
            int collisionsDetected = 0;
            int collisionsFiltered = 0;
            int cacheHits = 0;      // Epic 7.7.6: Cache hit counter
            int cacheMisses = 0;    // Epic 7.7.6: Cache miss counter
            
            // Build a lookup from Entity to player index for spatial hash queries
            var entityToPlayerIndex = new NativeHashMap<Entity, int>(players.Length, Allocator.TempJob);
            for (int i = 0; i < players.Length; i++)
            {
                entityToPlayerIndex.TryAdd(players[i].Entity, i);
            }
            
            // Track which pairs we've already checked to avoid duplicates
            var checkedPairs = new NativeHashSet<long>(players.Length * 4, Allocator.TempJob);
            
            // Check all pairs for proximity
            for (int i = 0; i < players.Length; i++)
            {
                var playerA = players[i];
                
                // Only check collisions FOR entities with Simulate (we can modify them)
                if (!playerA.HasSimulate)
                    continue;
                
                // Skip if on cooldown
                if (playerA.CollisionCooldown > 0)
                    continue;
                
                // Skip if already staggered or knocked down
                if (playerA.CollisionState.IsStaggered || playerA.CollisionState.IsKnockedDown)
                    continue;
                
                // Epic 7.6.4: Skip if player has grace period that ignores player collision
                if (playerA.GracePeriod.HasGracePeriod && playerA.GracePeriod.IgnorePlayerCollision)
                    continue;
                
                // Epic 7.7.3: Use spatial hash to find nearby players
                if (useSpatialHash)
                {
                    // Epic 7.7.3: Profile spatial hash neighborhood query
                    CollisionProfilerMarkers.SpatialHash_NeighborhoodQuery.Begin();
                    
                    int cellIndex = spatialGrid.GetCellIndex(playerA.Position);
                    if (cellIndex < 0) 
                    {
                        CollisionProfilerMarkers.SpatialHash_NeighborhoodQuery.End();
                        continue;
                    }
                    
                    // Epic 7.7.4: At Low quality, only check same cell (no 3x3 neighborhood)
                    // This reduces neighbor lookups but may miss collisions at cell boundaries
                    var neighbors = new NeighborCells();
                    if (qualitySettings.ShouldQueryNeighborCells)
                    {
                        // Get 3x3 neighborhood cells
                        spatialGrid.GetNeighborCellsFixed(cellIndex, ref neighbors);
                    }
                    else
                    {
                        // Low quality: only check same cell
                        neighbors[0] = cellIndex;
                        neighbors.Count = 1;
                    }
                    
                    CollisionProfilerMarkers.SpatialHash_NeighborhoodQuery.End();
                    
                    // Check players in each neighboring cell
                    for (int nc = 0; nc < neighbors.Count; nc++)
                    {
                        int neighborCell = neighbors[nc];
                        
                        // Iterate entities in this cell using static grid data
                        if (SpatialHashGridData.CellToEntities.TryGetFirstValue(neighborCell, out var otherEntity, out var iterator))
                        {
                            do
                            {
                                // Skip self
                                if (otherEntity == playerA.Entity)
                                    continue;
                                
                                // Get player index
                                if (!entityToPlayerIndex.TryGetValue(otherEntity, out int j))
                                    continue;
                                
                                // Create unique pair key (smaller index first to avoid A-B and B-A duplicates)
                                int minIdx = math.min(i, j);
                                int maxIdx = math.max(i, j);
                                long pairKey = ((long)minIdx << 32) | (long)maxIdx;
                                
                                // Skip if already checked this pair
                                if (!checkedPairs.Add(pairKey))
                                    continue;
                                
                                var playerB = players[j];
                                
                                // Process collision pair (same logic as O(N²) path)
                                // Epic 7.7.4: Pass quality settings for adaptive filtering
                                // Epic 7.7.6: Pass cache hit/miss counters for metrics
                                ProcessCollisionPair(ref state, ref playerA, ref playerB, i, j, settings, gameSettings, qualitySettings,
                                    currentTick, deltaTime, ref pairsChecked, ref collisionsDetected, ref collisionsFiltered,
                                    ref cacheHits, ref cacheMisses);
                                    
                            } while (SpatialHashGridData.CellToEntities.TryGetNextValue(out otherEntity, ref iterator));
                        }
                    }
                }
                else
                {
                    // Fallback: O(N²) all-pairs check (when spatial hash not available)
                    for (int j = 0; j < players.Length; j++)
                    {
                        if (i == j) continue; // Skip self
                        
                        var playerB = players[j];
                        
                        // Epic 7.7.4: Pass quality settings for adaptive filtering
                        // Epic 7.7.6: Pass cache hit/miss counters for metrics
                        ProcessCollisionPair(ref state, ref playerA, ref playerB, i, j, settings, gameSettings, qualitySettings,
                            currentTick, deltaTime, ref pairsChecked, ref collisionsDetected, ref collisionsFiltered,
                            ref cacheHits, ref cacheMisses);
                    }
                }
            }
            
            // Dispose temporary containers
            entityToPlayerIndex.Dispose();
            checkedPairs.Dispose();
            
            // Epic 7.7.1: End distance checks
            CollisionProfilerMarkers.ProximityCollision_DistanceChecks.End();
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Log metrics periodically in development builds
            // Epic 7.7.4: Include quality level in debug output
            // Epic 7.7.6: Include cache hit rate for temporal coherence monitoring
            if ((currentTick % 300) == 0)
            {
                // Removed log to reduce spam
            }
            #endif
            
            // Epic 7.7.2: Track container disposal for profiling
            MemoryOptimizationUtility.ContainerDisposal.Begin();
            players.Dispose();
            MemoryOptimizationUtility.ContainerDisposal.End();
            } // End ProximityCollision profiler marker
        }
        
        /// <summary>
        /// Epic 7.7.3: Process a single collision pair.
        /// Extracted to support both O(N²) and spatial hash code paths.
        /// Epic 7.7.4: Now quality-aware for adaptive scaling.
        /// Epic 7.7.6: Now with temporal coherence cache support.
        /// </summary>
        private void ProcessCollisionPair(
            ref SystemState state,
            ref PlayerData playerA, ref PlayerData playerB,
            int indexA, int indexB,
            PlayerCollisionSettings settings,
            CollisionGameSettings gameSettings,
            CollisionQualitySettings qualitySettings,
            uint currentTick, float deltaTime,
            ref int pairsChecked, ref int collisionsDetected, ref int collisionsFiltered,
            ref int cacheHits, ref int cacheMisses)
        {
            // Epic 7.6.4: Skip if other player has grace period that ignores player collision
            // Epic 7.7.4: Grace period checks skipped at Low quality (handled in gathering phase)
            if (playerB.GracePeriod.HasGracePeriod && playerB.GracePeriod.IgnorePlayerCollision)
                return;
            
            // Epic 7.6.3: Check friendly fire and team collision settings
            // Epic 7.7.4: Skip team checks at Low quality for performance
            bool isSameTeam = false;
            if (qualitySettings.ShouldCheckTeams)
            {
                isSameTeam = TeamId.IsSameTeam(playerA.Team, playerB.Team);
                
                // If friendly fire is disabled, skip damaging collisions entirely
                if (!gameSettings.FriendlyFireEnabled)
                {
                    // Still allow soft push if configured
                    if (!gameSettings.SoftCollisionWhenDisabled)
                        return;
                }
                
                // If same team and team collision is disabled, skip
                if (isSameTeam && !gameSettings.TeamCollisionEnabled)
                {
                    if (!gameSettings.SoftCollisionWhenDisabled)
                        return;
                }
            }
            
            // Epic 7.7.1: Count pair checks for profiling
            pairsChecked++;
            
            // Calculate horizontal distance
            float3 toB = playerB.Position - playerA.Position;
            toB.y = 0;
            float horizontalDist = math.length(toB);
            
            // Combined radius for collision detection
            // Epic 7.7.4: Use quality-adjusted threshold at Medium/Low quality
            float combinedRadius = playerA.Radius + playerB.Radius;
            float defaultThreshold = combinedRadius + 0.3f; // Slightly larger buffer
            float collisionThreshold = qualitySettings.GetCollisionThreshold(defaultThreshold);
            
            if (horizontalDist >= collisionThreshold || horizontalDist <= 0.01f)
                return;
            
            // Epic 7.7.6: Try to use cached collision data for temporal coherence
            // This avoids redundant calculations when velocities haven't changed significantly
            float3 direction;
            float3 contactPoint;
            float3 relativeVel;
            float approachSpeed;
            bool usedCache = false;
            
            if (TryGetCachedCollision(playerA.Entity, playerB.Entity, playerA.Velocity, playerB.Velocity, 
                horizontalDist, out var cachedData))
            {
                // Use cached values - significant performance win for stable scenarios
                direction = cachedData.LastDirection;
                contactPoint = cachedData.LastContactPoint;
                relativeVel = cachedData.LastRelativeVelocity;
                approachSpeed = cachedData.LastApproachSpeed;
                usedCache = true;
                cacheHits++;  // Epic 7.7.6: Track cache hit
                
                // Epic 7.7.7: Debug visualization - green line for cache hit
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.DrawLine(
                    new UnityEngine.Vector3(playerA.Position.x, playerA.Position.y + 0.5f, playerA.Position.z),
                    new UnityEngine.Vector3(playerB.Position.x, playerB.Position.y + 0.5f, playerB.Position.z),
                    UnityEngine.Color.green, 0.05f);
                #endif
            }
            else
            {
                // Calculate fresh values
                contactPoint = (playerA.Position + playerB.Position) * 0.5f;

                // Calculate relative velocity (approach speed)
                relativeVel = playerA.Velocity - playerB.Velocity;
                direction = math.normalize(toB); // Points from A towards B
                approachSpeed = math.dot(relativeVel, direction);
                cacheMisses++;  // Epic 7.7.6: Track cache miss
                
                // Epic 7.7.7: Debug visualization - red line for cache miss
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.DrawLine(
                    new UnityEngine.Vector3(playerA.Position.x, playerA.Position.y + 0.5f, playerA.Position.z),
                    new UnityEngine.Vector3(playerB.Position.x, playerB.Position.y + 0.5f, playerB.Position.z),
                    UnityEngine.Color.red, 0.05f);
                #endif
            }
            
            // Only process if approaching (positive approach speed)
            if (approachSpeed <= 0.1f)
                return;
            
            // Calculate impact speed (combine both players' contributions)
            float speedA = math.length(new float3(playerA.Velocity.x, 0, playerA.Velocity.z));
            float speedB = math.length(new float3(playerB.Velocity.x, 0, playerB.Velocity.z));
            float impactSpeed = speedA + speedB;
            
            // Skip if impact is too weak
            if (impactSpeed < settings.StaggerThreshold)
                return;
            
            // Epic 7.7.6: Update cache with calculated values for future frames
            // Only update if we calculated fresh values (not using cache)
            if (!usedCache)
            {
                UpdateCollisionCache(playerA.Entity, playerB.Entity, relativeVel, 
                    horizontalDist, direction, contactPoint, approachSpeed);
            }
            
            // Calculate power for each player
            float powerA = CalculatePower(playerA.PlayerState, speedA, settings);
            float powerB = CalculatePower(playerB.PlayerState, speedB, settings);
            
            // Epic 7.4.3: Apply dodge power reduction
            if (playerA.Dodge.IsDodging)
                powerA *= settings.DodgeCollisionMultiplier;
            if (playerB.Dodge.IsDodging)
                powerB *= settings.DodgeCollisionMultiplier;
            
            float totalPower = powerA + powerB;
            float powerRatioA = totalPower > 0.001f ? powerA / totalPower : 0.5f;
            float powerRatioB = 1f - powerRatioA;
            
            // Epic 7.4.3: Deflect direction for dodging players
            // Epic 7.7.4: Skip deflection calculation at Medium/Low quality
            float3 directionA = -direction; // Away from B
            float3 directionB = direction;   // Away from A
            
            if (qualitySettings.ShouldCalculateDodgeDeflection)
            {
                if (playerA.Dodge.IsDodging)
                    directionA = DeflectDirection(directionA, settings.DodgeDeflectionAngle);
                if (playerB.Dodge.IsDodging)
                    directionB = DeflectDirection(directionB, settings.DodgeDeflectionAngle);
            }
            
            // Epic 7.6.3: Determine if we should only apply soft collision
            // Epic 7.7.4: Skip soft collision force calculation at Medium/Low quality
            bool softCollisionOnly = !gameSettings.FriendlyFireEnabled || 
                (isSameTeam && !gameSettings.TeamCollisionEnabled);
            
            // At Medium/Low quality, skip soft collision calculations (just use 1.0 multiplier)
            float effectiveForceMultiplier = 1f;
            if (qualitySettings.ShouldCalculateSoftCollisions && softCollisionOnly)
            {
                effectiveForceMultiplier = gameSettings.SoftCollisionForceMultiplier;
            }
            
            // Apply collision to BOTH players
            if (playerA.HasSimulate)
            {
                collisionsDetected++;
                bool immuneA = playerA.Dodge.InIFrameWindow || softCollisionOnly;
                ApplyCollision(ref state, playerA.Entity, playerB.Entity, 
                    impactSpeed * effectiveForceMultiplier, powerRatioA, directionA, contactPoint, direction, 
                    settings, currentTick, deltaTime, immuneA, playerA.Dodge.IsDodging);
            }
            
            if (playerB.HasSimulate)
            {
                bool immuneB = playerB.Dodge.InIFrameWindow || softCollisionOnly;
                ApplyCollision(ref state, playerB.Entity, playerA.Entity, 
                    impactSpeed * effectiveForceMultiplier, powerRatioB, directionB, contactPoint, -direction, 
                    settings, currentTick, deltaTime, immuneB, playerB.Dodge.IsDodging);
            }
        }
        
        private float CalculatePower(PlayerState playerState, float speed, PlayerCollisionSettings settings)
        {
            float stanceMultiplier = playerState.Stance switch
            {
                PlayerStance.Crouching => settings.StanceMultiplierCrouching,
                PlayerStance.Prone => settings.StanceMultiplierProne,
                _ => settings.StanceMultiplierStanding
            };
            
            // Epic 7.4.2: Tackling has highest power multiplier (2.0)
            float movementMultiplier = playerState.MovementState switch
            {
                PlayerMovementState.Tackling => 2.0f,
                PlayerMovementState.Sprinting => settings.MovementMultiplierSprinting,
                PlayerMovementState.Running => settings.MovementMultiplierRunning,
                PlayerMovementState.Walking => settings.MovementMultiplierWalking,
                _ => settings.MovementMultiplierIdle
            };
            
            // Apply minimum speed floor so standing players aren't instantly knocked down
            // Standing players get base "bracing" power of 1 m/s equivalent
            float effectiveSpeed = math.max(speed, 1.0f);
            
            return settings.EffectiveMass * effectiveSpeed * stanceMultiplier * movementMultiplier;
        }
        
        private void ApplyCollision(ref SystemState state, Entity entity, Entity other,
            float impactSpeed, float powerRatio, float3 knockbackDirection,
            float3 contactPoint, float3 contactNormal,
            PlayerCollisionSettings settings, uint currentTick, float deltaTime,
            bool hasIFrameImmunity = false, bool isDodging = false)
        {
            var collisionState = _collisionStateLookup.GetRefRW(entity);

            // Record basic collision metrics for downstream consumers (audio/VFX).
            // This is critical when Unity Physics collision events don't fire (CharacterController).
            byte hitDirection;
            if (hasIFrameImmunity)
            {
                hitDirection = HitDirectionType.Evaded;
            }
            else
            {
                var transform = _transformLookup[entity];
                float3 forward = math.forward(transform.Rotation);
                forward.y = 0;
                forward = math.normalizesafe(forward);

                float3 toOther = contactNormal;
                toOther.y = 0;
                toOther = math.normalizesafe(toOther);

                float facingDot = math.dot(forward, toOther);
                if (facingDot >= settings.BracedDotThreshold)
                {
                    hitDirection = HitDirectionType.Braced;
                }
                else if (facingDot <= settings.BackHitDotThreshold)
                {
                    hitDirection = HitDirectionType.Back;
                }
                else
                {
                    hitDirection = HitDirectionType.Side;
                }
            }
            collisionState.ValueRW.LastPowerRatio = powerRatio;
            collisionState.ValueRW.LastHitDirection = hitDirection;

            if (_collisionEventBufferLookup.HasBuffer(entity))
            {
                var buffer = _collisionEventBufferLookup[entity];
                if (buffer.Length < 8)
                {
                    float combinedMass = settings.EffectiveMass * 2f;
                    buffer.Add(new DIG.Player.Components.CollisionEvent
                    {
                        OtherEntity = other,
                        ContactPoint = contactPoint,
                        ContactNormal = contactNormal,
                        ImpactSpeed = impactSpeed,
                        ImpactForce = impactSpeed * combinedMass,
                        EventTick = currentTick,
                        HitDirection = hitDirection,
                        PowerRatio = powerRatio,
                        TriggeredStagger = false,
                        TriggeredKnockdown = false
                    });
                }
            }
            
            // Epic 7.4.3: If player has i-frame immunity, skip stagger/knockdown entirely
            // Still apply deflection velocity but no state change
            if (hasIFrameImmunity)
            {
                string worldName = state.World.Name;
                // UnityEngine.Debug.Log($"[ProximityCollision] [{worldName}] EVADED! Entity={entity.Index} has i-frame immunity");
                
                // Enable evading tag for audio/VFX feedback
                SystemAPI.SetComponentEnabled<Evading>(entity, true);
                
                // Still update collision tracking (for cooldown)
                collisionState.ValueRW.LastCollisionTick = currentTick;
                collisionState.ValueRW.LastCollisionEntity = other;
                collisionState.ValueRW.CollisionCooldown = settings.CollisionCooldownDuration;
                return;
            }
            
            // Determine stagger or knockdown based on power ratio
            bool triggerKnockdown = powerRatio < (1f - settings.KnockdownPowerThreshold);
            bool triggerStagger = !triggerKnockdown && (0.5f - powerRatio) > -0.2f; // Weaker or close match
            
            float knockbackSpeed = impactSpeed * (1f - powerRatio) * settings.PushForceMultiplier;
            knockbackSpeed = math.min(knockbackSpeed, settings.MaxSeparationSpeed);
            
            string logWorldName = state.World.Name;
            
            if (triggerKnockdown && !collisionState.ValueRO.IsImmuneToStagger)
            {
                float baseDuration = settings.KnockdownDuration;
                

                
                collisionState.ValueRW.KnockdownTimeRemaining = baseDuration;
                collisionState.ValueRW.KnockdownImpactSpeed = impactSpeed;
                collisionState.ValueRW.StaggerVelocity = knockbackDirection * knockbackSpeed;
                collisionState.ValueRW.LastPowerRatio = powerRatio;
                
                // Enable knockdown tag
                SystemAPI.SetComponentEnabled<KnockedDown>(entity, true);
                SystemAPI.SetComponentEnabled<Staggered>(entity, false);

                // Update latest event flags if we emitted one above
                if (_collisionEventBufferLookup.HasBuffer(entity))
                {
                    var buffer = _collisionEventBufferLookup[entity];
                    if (buffer.Length > 0)
                    {
                        var last = buffer[buffer.Length - 1];
                        last.TriggeredKnockdown = true;
                        buffer[buffer.Length - 1] = last;
                    }
                }
            }
            else if (triggerStagger && !collisionState.ValueRO.IsImmuneToStagger)
            {
                float baseDuration = math.lerp(
                    settings.MinStaggerDuration,
                    settings.MaxStaggerDuration,
                    1f - powerRatio);
                

                
                collisionState.ValueRW.StaggerTimeRemaining = baseDuration;
                collisionState.ValueRW.StaggerIntensity = math.saturate(impactSpeed / 10f);
                collisionState.ValueRW.StaggerVelocity = knockbackDirection * knockbackSpeed;
                collisionState.ValueRW.LastPowerRatio = powerRatio;
                
                // Enable stagger tag
                SystemAPI.SetComponentEnabled<Staggered>(entity, true);

                // Update latest event flags if we emitted one above
                if (_collisionEventBufferLookup.HasBuffer(entity))
                {
                    var buffer = _collisionEventBufferLookup[entity];
                    if (buffer.Length > 0)
                    {
                        var last = buffer[buffer.Length - 1];
                        last.TriggeredStagger = true;
                        buffer[buffer.Length - 1] = last;
                    }
                }
            }
            
            // Update collision tracking
            collisionState.ValueRW.LastCollisionTick = currentTick;
            collisionState.ValueRW.LastCollisionEntity = other;
            collisionState.ValueRW.CollisionCooldown = settings.CollisionCooldownDuration;
        }
        
        /// <summary>
        /// Epic 7.4.3: Gets dodge state info for i-frame immunity calculations
        /// </summary>
        private DodgeInfo GetDodgeInfo(Entity entity, PlayerCollisionSettings settings)
        {
            DodgeInfo info = default;
            
            // Check DodgeRollState
            if (_dodgeRollLookup.HasComponent(entity))
            {
                var rollState = _dodgeRollLookup[entity];
                if (rollState.IsActive == 1)
                {
                    info.IsDodging = true;
                    info.Elapsed = rollState.Elapsed;
                    info.InvulnStart = rollState.InvulnStart;
                    info.InvulnEnd = rollState.InvulnEnd;
                }
            }
            
            // Check DodgeDiveState (overrides roll if both active - dive takes priority)
            if (_dodgeDiveLookup.HasComponent(entity))
            {
                var diveState = _dodgeDiveLookup[entity];
                if (diveState.IsActive == 1)
                {
                    info.IsDodging = true;
                    info.Elapsed = diveState.Elapsed;
                    info.InvulnStart = diveState.InvulnStart;
                    info.InvulnEnd = diveState.InvulnEnd;
                }
            }
            
            // Calculate if in i-frame window using per-dodge invuln settings
            if (info.IsDodging)
            {
                info.InIFrameWindow = info.Elapsed >= info.InvulnStart && info.Elapsed <= info.InvulnEnd;
            }
            
            return info;
        }
        
        /// <summary>
        /// Epic 7.4.3: Rotates a direction vector by an angle (in degrees) tangent to collision
        /// </summary>
        private float3 DeflectDirection(float3 direction, float angleDegrees)
        {
            // Rotate around Y axis for horizontal deflection
            float angleRad = math.radians(angleDegrees);
            float cos = math.cos(angleRad);
            float sin = math.sin(angleRad);
            
            return new float3(
                direction.x * cos - direction.z * sin,
                direction.y,
                direction.x * sin + direction.z * cos
            );
        }
        
        private struct PlayerData
        {
            public Entity Entity;
            public float3 Position;
            public float3 Velocity;
            public float Radius;
            public PlayerState PlayerState;
            public PlayerCollisionState CollisionState;
            public float CollisionCooldown;
            public bool HasSimulate; // True if entity has Simulate tag (can be modified)
            public DodgeInfo Dodge;  // Epic 7.4.3: Dodge state for i-frame immunity
            public TeamId Team;      // Epic 7.6.3: Team ID for team-based collision filtering
            public GracePeriodInfo GracePeriod; // Epic 7.6.4: Grace period for spawn/teleport
        }
        
        /// <summary>
        /// Epic 7.6.4: Stores grace period information for spawn/teleport protection
        /// </summary>
        private struct GracePeriodInfo
        {
            public bool HasGracePeriod;       // True if entity has CollisionGracePeriod component
            public bool IgnorePlayerCollision; // Skip player-player collision
            public bool IgnoreAllCollision;    // Skip all collision
        }
        
        /// <summary>
        /// Epic 7.4.3: Stores dodge state information for i-frame calculations
        /// </summary>
        private struct DodgeInfo
        {
            public bool IsDodging;       // True if player is in dodge roll or dive
            public float Elapsed;        // Time elapsed in current dodge
            public float InvulnStart;    // When i-frames start (from dodge component)
            public float InvulnEnd;      // When i-frames end (from dodge component)
            public bool InIFrameWindow;  // True if currently in i-frame window
        }
        
        #region Epic 7.7.6: Temporal Coherence Cache Methods
        
        /// <summary>
        /// Epic 7.7.6: Increment staleness counter for all cached entries.
        /// Called once per frame before collision processing.
        /// </summary>
        private void IncrementCacheStaleness()
        {
            if (!_collisionCache.IsCreated || _collisionCache.Count == 0)
                return;
            
            // Get all keys and update each entry
            var keys = _collisionCache.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < keys.Length; i++)
            {
                if (_collisionCache.TryGetValue(keys[i], out var data))
                {
                    data.IncrementStaleness();
                    _collisionCache[keys[i]] = data;
                }
            }
            keys.Dispose();
        }
        
        /// <summary>
        /// Epic 7.7.6: Remove stale entries from the cache.
        /// Called periodically (every ~1 second) to prevent unbounded growth.
        /// </summary>
        private void EvictStaleCacheEntries()
        {
            if (!_collisionCache.IsCreated || _collisionCache.Count == 0)
                return;
            
            // Collect keys to remove (can't modify during iteration)
            var keysToRemove = new NativeList<CollisionPairKey>(8, Allocator.Temp);
            var keys = _collisionCache.GetKeyArray(Allocator.Temp);
            
            for (int i = 0; i < keys.Length; i++)
            {
                if (_collisionCache.TryGetValue(keys[i], out var data))
                {
                    if (data.ShouldEvict)
                    {
                        keysToRemove.Add(keys[i]);
                    }
                }
            }
            keys.Dispose();
            
            // Remove stale entries
            int evictedCount = keysToRemove.Length;
            for (int i = 0; i < keysToRemove.Length; i++)
            {
                _collisionCache.Remove(keysToRemove[i]);
            }
            keysToRemove.Dispose();
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (evictedCount > 0)
            {
                UnityEngine.Debug.Log($"[ProximityCollision] Epic 7.7.6: Evicted {evictedCount} stale cache entries. Remaining: {_collisionCache.Count}");
            }
            #endif
        }
        
        /// <summary>
        /// Epic 7.7.6: Try to get cached collision data for a pair of entities.
        /// Returns true if valid cached data exists that can be reused.
        /// </summary>
        private bool TryGetCachedCollision(Entity entityA, Entity entityB, float3 currentVelocityA, float3 currentVelocityB, 
            float currentDistance, out CachedCollisionData cachedData)
        {
            var key = CollisionPairKey.Create(entityA, entityB);
            
            if (_collisionCache.TryGetValue(key, out cachedData))
            {
                // Calculate current relative velocity for validation
                float3 currentRelativeVelocity = currentVelocityA - currentVelocityB;
                
                if (cachedData.IsValidFor(currentRelativeVelocity, currentDistance))
                {
                    // Mark as used and update cache
                    cachedData.MarkUsed();
                    _collisionCache[key] = cachedData;
                    return true;
                }
            }
            
            cachedData = default;
            return false;
        }
        
        /// <summary>
        /// Epic 7.7.6: Update the collision cache with newly calculated data.
        /// </summary>
        private void UpdateCollisionCache(Entity entityA, Entity entityB, float3 relativeVelocity, 
            float distance, float3 direction, float3 contactPoint, float approachSpeed)
        {
            var key = CollisionPairKey.Create(entityA, entityB);
            var data = CachedCollisionData.Create(relativeVelocity, distance, direction, contactPoint, approachSpeed);
            
            // Add or update the cache entry
            _collisionCache[key] = data;
        }
        
        /// <summary>
        /// Epic 7.7.7: Remove cache entries for entities that were destroyed.
        /// Compares previous frame entities to current frame entities to detect deletions.
        /// </summary>
        private void CleanupDeletedEntityCacheEntries()
        {
            if (!_collisionCache.IsCreated || _collisionCache.Count == 0)
                return;
            
            if (!_previousFrameEntities.IsCreated || _previousFrameEntities.Count == 0)
                return;
            
            // Find entities that were in previous frame but not in current frame (deleted)
            var deletedEntities = new NativeList<Entity>(8, Allocator.Temp);
            foreach (var entity in _previousFrameEntities)
            {
                if (!_currentFrameEntities.Contains(entity))
                {
                    deletedEntities.Add(entity);
                }
            }
            
            if (deletedEntities.Length == 0)
            {
                deletedEntities.Dispose();
                return;
            }
            
            // Remove cache entries containing deleted entities
            var keysToRemove = new NativeList<CollisionPairKey>(8, Allocator.Temp);
            var keys = _collisionCache.GetKeyArray(Allocator.Temp);
            
            for (int i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                for (int j = 0; j < deletedEntities.Length; j++)
                {
                    if (key.Contains(deletedEntities[j]))
                    {
                        keysToRemove.Add(key);
                        break;
                    }
                }
            }
            keys.Dispose();
            
            // Remove the entries
            int removedCount = keysToRemove.Length;
            for (int i = 0; i < keysToRemove.Length; i++)
            {
                _collisionCache.Remove(keysToRemove[i]);
            }
            keysToRemove.Dispose();
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (removedCount > 0)
            {
                UnityEngine.Debug.Log($"[ProximityCollision] Epic 7.7.7: Cleaned up {removedCount} cache entries for {deletedEntities.Length} deleted entities");
            }
            #endif
            
            deletedEntities.Dispose();
        }
        
        #endregion
    }
}
