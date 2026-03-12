using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Player.Components;
using UnityEngine;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.NetCode;

namespace Player.Systems
{
    // CRITICAL: Must run in PredictedFixedStepSimulationSystemGroup and execute after physics
    // NOTE: You cannot use [UpdateAfter] to order across different ComponentSystemGroup instances
    // (the warning seen previously occurs because PhysicsSystemGroup is not a member of
    // PredictedFixedStepSimulationSystemGroup). To guarantee ordering, ensure that physics
    // systems and this system are added to the same group (e.g. via bootstrap) or explicitly
    // place this system in the physics group. For now, keep this system in the Predicted
    // fixed-step group (used by NetCode) and rely on your bootstrap to ensure physics runs
    // before this system in that world.
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial class CharacterControllerSystem : SystemBase
    {
        // Bounded capsule blob cache to avoid recreating colliders every frame.
        // Key: (roundedHeight<<32) | roundedRadius
        // OPTIMIZATION 10.15.1: Use NativeHashMap instead of managed Dictionary for Burst access
        private const int CAPSULE_CACHE_CAPACITY = 128;
        
        // Native cache accessible from Burst jobs
        private NativeHashMap<long, BlobAssetReference<Unity.Physics.Collider>> _capsuleCache;
        
        // OPTIMIZATION 10.15.6: Persistent collections to avoid per-frame allocations
        private NativeQueue<MoveRequest> _moveQueue;
        private NativeQueue<PushRequest> _pushQueue;
        private NativeList<MoveRequest> _movesList;
        private NativeList<Entity> _entitiesList;
        private NativeList<float3> _startPosList;
        private NativeList<float3> _outPositionsList;
        private NativeList<BlobAssetReference<Unity.Physics.Collider>> _capsuleCollidersList;
        private NativeHashMap<Entity, float3> _accumMap;
        private NativeList<BlobAssetReference<Unity.Physics.Collider>> _blobsToDispose;

        // USER REQUESTED DEBUGGING - Enable via STAIRS_DEBUG scripting define symbol
#if STAIRS_DEBUG
        [BurstDiscard]
        private static void LogStairDebug(string tag, float3 pos, float3 normal, Entity hitEntity, float fraction)
        {
            UnityEngine.Debug.Log($"[STAIRS][{tag}] Hit Entity:{hitEntity.Index} | Normal:{normal} | Frac:{fraction} | Pos:{pos}");
        }
#endif

        private void DisposeDeferredBlobs()
        {
             if (_blobsToDispose.IsCreated)
             {
                 for (int i=0; i<_blobsToDispose.Length; i++)
                 {
                     if (_blobsToDispose[i].IsCreated) _blobsToDispose[i].Dispose();
                 }
                 _blobsToDispose.Clear();
             }
        }
        
        // Debug counters
        private long s_CacheHits = 0;
        private long s_CacheMisses = 0;
        private CapsuleCacheSettings s_CapsuleSettings = null;

        // Diagnostic toggle: set to true to enable internal debug logging
        // Default is false to keep logs quiet. Can be toggled at runtime.
        private static bool s_DiagnosticsEnabled = false;
        
        // Verbose toggle: logs per-body details (VERY spammy, use sparingly)
        private static bool s_VerboseLogging = false;
        
        // Track last chunk count to only log when it changes
        private static int s_LastChunkCount = -1;
        
        // Track if we've logged the world name (once per instance)
        private bool _hasLoggedWorld = false;

        // Public accessor for quick toggling in editor or tests
        public static bool DiagnosticsEnabled
        {
            get => s_DiagnosticsEnabled;
            set => s_DiagnosticsEnabled = value;
        }
        
        // Public accessor for verbose (per-body) logging
        public static bool VerboseLogging
        {
            get => s_VerboseLogging;
            set => s_VerboseLogging = value;
        }

        [BurstDiscard]
        private static void DLog(string msg)
        {
            if (s_DiagnosticsEnabled) UnityEngine.Debug.Log(msg);
        }

        [BurstDiscard]
        private static void DLogWarning(string msg)
        {
            if (s_DiagnosticsEnabled) UnityEngine.Debug.LogWarning(msg);
        }

        [BurstDiscard]
        private static void DLogError(string msg)
        {
            if (s_DiagnosticsEnabled) UnityEngine.Debug.LogError(msg);
        }

        // Runtime-checking log helpers for non-DLog usages
        [BurstDiscard]
        private static void LogIfEnabled(string msg)
        {
            if (s_DiagnosticsEnabled) UnityEngine.Debug.Log(msg);
        }

        [BurstDiscard]
        private static void LogWarningIfEnabled(string msg)
        {
            if (s_DiagnosticsEnabled) UnityEngine.Debug.LogWarning(msg);
        }

        [BurstDiscard]
        private static void LogErrorIfEnabled(string msg)
        {
            if (s_DiagnosticsEnabled) UnityEngine.Debug.LogError(msg);
        }

        private static long MakeKey(float height, float radius)
        {
            int h = (int)math.round(height * 1000f);
            int r = (int)math.round(radius * 1000f);
            return ((long)h << 32) | (uint)r;
        }

        /// <summary>
        /// OPTIMIZATION 10.15.1: Get or create a cached capsule collider.
        /// Uses NativeHashMap for Burst job access.
        /// </summary>
        public BlobAssetReference<Unity.Physics.Collider> GetOrCreateCapsuleBlob(float height, float radius)
        {
            long key = MakeKey(height, radius);
            if (_capsuleCache.TryGetValue(key, out var blob) && blob.IsCreated)
            {
                // Cache hit
                s_CacheHits++;
                return blob;
            }

            // Cache miss - create new collider
            s_CacheMisses++;

            // Use proper capsule geometry with the collision filter for character movement
            float capsuleRadius = math.max(0.01f, radius);
            var geom = new Unity.Physics.CapsuleGeometry
            {
                Vertex0 = new float3(0, capsuleRadius, 0),
                Vertex1 = new float3(0, math.max(capsuleRadius * 2f, height - capsuleRadius), 0),
                Radius = capsuleRadius
            };
            
            var filter = new CollisionFilter
            {
                BelongsTo = 1u, // Player layer
                CollidesWith = ~(1u << 6), // Collide with everything EXCEPT Trigger layer (bit 6)
                GroupIndex = 0
            };
            
            var newBlob = Unity.Physics.CapsuleCollider.Create(geom, filter);
            
            // BUGFIX: Always add to cache to prevent memory leak
            // If cache is full, evict an arbitrary entry first
            // If cache is full, evict an arbitrary entry first
            if (_capsuleCache.Count >= CAPSULE_CACHE_CAPACITY)
            {
                // Evict first entry we find
                var keys = _capsuleCache.GetKeyArray(Allocator.Temp);
                if (keys.Length > 0)
                {
                    long evictKey = keys[0];
                    if (_capsuleCache.TryGetValue(evictKey, out var evictBlob) && evictBlob.IsCreated)
                    {
                        // SAFETY: Do not dispose immediately as it might be in use by current frame's batch.
                        // Add to deferred disposal list.
                        if (!_blobsToDispose.IsCreated) _blobsToDispose = new NativeList<BlobAssetReference<Unity.Physics.Collider>>(16, Allocator.Persistent);
                        _blobsToDispose.Add(evictBlob);
                    }
                    _capsuleCache.Remove(evictKey);
                }
                keys.Dispose();
            }
            
            _capsuleCache[key] = newBlob;

            return newBlob;
        }

        /// <summary>
        /// Cache statistics: hits and misses.
        /// </summary>
        public (long hits, long misses) GetCacheStats()
        {
            return (s_CacheHits, s_CacheMisses);
        }

        /// <summary>
        /// Reset only the cache hit/miss counters.
        /// Safe to call from main thread (Editor UI / runtime).
        /// </summary>
        public void ResetCacheStats()
        {
            s_CacheHits = 0;
            s_CacheMisses = 0;
        }

        /// <summary>
        /// Clears and disposes all cached capsule blobs. Optionally prewarms afterwards.
        /// OPTIMIZATION 10.15.1: Updated for NativeHashMap.
        /// Must be called from the main thread.
        /// </summary>
        public void ClearCapsuleCache(bool prewarmAfterClear = false)
        {
            if (_capsuleCache.IsCreated)
            {
                var keys = _capsuleCache.GetKeyArray(Allocator.Temp);
                for (int i = 0; i < keys.Length; i++)
                {
                    if (_capsuleCache.TryGetValue(keys[i], out var blob) && blob.IsCreated)
                    {
                        blob.Dispose();
                    }
                }
                keys.Dispose();
                _capsuleCache.Clear();
            }

            if (prewarmAfterClear)
            {
                if (s_CapsuleSettings != null && s_CapsuleSettings.Prewarm)
                {
                    foreach (var h in s_CapsuleSettings.PrewarmHeights)
                        foreach (var r in s_CapsuleSettings.PrewarmRadii)
                            GetOrCreateCapsuleBlob(h, r);
                }
                else
                {
                    PrewarmCapsuleCache();
                }
            }
        }

        /// <summary>
        /// Clear both cache blobs and counters. Optionally prewarms cache after clearing.
        /// </summary>
        public void ResetCache(bool prewarmAfterClear = false)
        {
            ClearCapsuleCache(prewarmAfterClear);
            ResetCacheStats();
        }
        // Burst-safe collector that keeps the closest hit
        private struct ClosestHitCollector : ICollector<Unity.Physics.RaycastHit>
        {
            public bool EarlyOutOnFirstHit => false;
            public float MaxFraction { get; set; }
            public Unity.Physics.RaycastHit Hit;
            public int NumHits => (MaxFraction < float.MaxValue) ? 1 : 0;

            public ClosestHitCollector(float maxFraction)
            {
                MaxFraction = maxFraction;
                Hit = default(Unity.Physics.RaycastHit);
            }

            public bool AddHit(Unity.Physics.RaycastHit hit)
            {
                if (hit.Fraction < MaxFraction)
                {
                    MaxFraction = hit.Fraction;
                    Hit = hit;
                }
                return true;
            }
        }
        // Burst-safe collector for collider casts
        private struct ClosestColliderCastCollector : ICollector<Unity.Physics.ColliderCastHit>
        {
            public bool EarlyOutOnFirstHit => false;
            public float MaxFraction { get; set; }
            public Unity.Physics.ColliderCastHit Hit;
            public int NumHits => (MaxFraction < float.MaxValue) ? 1 : 0;

            public ClosestColliderCastCollector(float maxFraction)
            {
                MaxFraction = maxFraction;
                Hit = default(Unity.Physics.ColliderCastHit);
            }

            public bool AddHit(Unity.Physics.ColliderCastHit hit)
            {
                if (hit.Fraction < MaxFraction)
                {
                    MaxFraction = hit.Fraction;
                    Hit = hit;
                }
                return true;
            }
        }
        
        // Collector that prioritizes STEEP hits (walls) over WALKABLE hits (floor)
        private struct SteepPriorityCollector : ICollector<Unity.Physics.ColliderCastHit>
        {
            public bool EarlyOutOnFirstHit => false;
            public float MaxFraction { get; set; }
            public int NumHits { get; private set; }

            public Unity.Physics.ColliderCastHit BestHit;
            private bool _hasSteepHit;
            private float _maxSlopeCos;

            public SteepPriorityCollector(float maxFraction, float maxSlopeCos)
            {
                MaxFraction = maxFraction;
                NumHits = 0;
                BestHit = default;
                _hasSteepHit = false;
                _maxSlopeCos = maxSlopeCos;
            }

            public bool AddHit(Unity.Physics.ColliderCastHit hit)
            {
                // check fraction range
                if (hit.Fraction > MaxFraction) return true;

                bool isSteep = hit.SurfaceNormal.y < _maxSlopeCos;
                
                bool currentIsSteep = _hasSteepHit;
                
                if (isSteep)
                {
                    // Found a steep hit. 
                    if (!currentIsSteep)
                    {
                        // Upgrade from Walkable to Steep
                        BestHit = hit;
                        _hasSteepHit = true;
                        MaxFraction = hit.Fraction; // Shrink search window to this wall
                    }
                    else
                    {
                        // Both steep, pick closest
                        if (hit.Fraction < BestHit.Fraction)
                        {
                            BestHit = hit;
                            MaxFraction = hit.Fraction;
                        }
                    }
                }
                else
                {
                    // Found walkable hit (Floor/Ramp)
                    if (!currentIsSteep)
                    {
                        // Only update if we don't have a steep hit yet
                        if (NumHits == 0 || hit.Fraction < BestHit.Fraction)
                        {
                            BestHit = hit;
                            // CRITIAL FIX: Do NOT shrink MaxFraction for walkable hits!
                            // If we bind MaxFraction to the floor (0.0), we will stop searching
                            // and miss the Stair Wall at (0.001).
                            // We must keep searching deeper to find a prioritized Steep hit.
                        }
                    }
                    // If we already have a steep hit, ignore this walkable one (prefer the wall)
                }

                NumHits++;
                return true;
            }

            public Unity.Physics.ColliderCastHit GetBestHit() => BestHit;
        }

        private struct PushRequest
        {
            public Entity Target;
            public float3 Impulse;
        }
        public struct MoveRequest
        {
            public Entity Entity;
            public float3 DesiredDisp;
            public float Height;
            public float Radius;
            public float Skin;
            public float StepHeight;
            public byte PushRigidbodies;
            public byte IsSlide; // 0 = normal, 1 = slide-driven
            // resolver tuning copied from CharacterControllerSettings per-request
            public float SlideBlockedRetention;
            public float SlideOpenRetention;
            public float SlideStepExtra;
            public float SlideHitImpulseScale;
            public float MaxSlopeAngleCos;
        }
        
        /// <summary>
        /// OPTIMIZATION 10.15.1: Job now receives pre-created colliders instead of creating them.
        /// </summary>
        /// <summary>
        /// OPTIMIZATION 10.15.1: Job now receives pre-created colliders instead of creating them.
        /// </summary>
        [BurstCompile]
        private struct ResolvePhysicsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<MoveRequest> moves;
            [ReadOnly] public NativeArray<float3> startPositions;
            [ReadOnly] public PhysicsWorld physicsWorld;
            // OPTIMIZATION 10.15.1: Pre-created colliders for each move (no allocation in job)
            [ReadOnly] public NativeArray<BlobAssetReference<Unity.Physics.Collider>> capsuleColliders;
            public NativeArray<float3> outPositions;
            public NativeQueue<PushRequest>.ParallelWriter pushWriter;

            public void Execute(int index)
            {
                var mr = moves[index];
                var entStart = startPositions[index];
                var desiredDisp = mr.DesiredDisp;

                float skin = mr.Skin;
                float stepHeight = mr.StepHeight;
                float height = mr.Height;

                float3 horizontal = new float3(desiredDisp.x, 0, desiredDisp.z);
                float moveDist = math.length(horizontal);
                
                if (moveDist < 1e-5f)
                {
                    // No horizontal movement, but still apply vertical (jumping/falling)
                    outPositions[index] = entStart + new float3(0, desiredDisp.y, 0);
                    return;
                }

                float3 forward = horizontal / moveDist;

                bool blocked = false;
                float3 hitNormal = float3.zero;
                Entity hitEntity = Entity.Null;

                // ========== OVERLAP CHECK ==========
                // Before casting, check if we're already overlapping another dynamic entity (player)
                // Casts ignore bodies they start inside of, so we need this check
                // We use OverlapAabb to find nearby bodies efficiently (Broadphase)
                
                // OPTIMIZATION 10.15.1: Use pre-cached collider instead of creating new one
                var capsuleCollider = capsuleColliders[index];
                
                // ========== OVERLAP CHECK ==========
                // Use PointDistance to find touching bodies and their accurate surface normals.
                // This replaces the inaccurate Center-to-Center heuristic.
                
                float3 overlapPushDir = float3.zero;
                Entity overlapTarget = Entity.Null;
                
                var pointInput = new PointDistanceInput
                {
                    Position = entStart,
                    MaxDistance = mr.Radius + 0.04f, // Check slightly larger than radius (Touching)
                    Filter = CollisionFilter.Default
                };
                
                var distanceHits = new NativeList<DistanceHit>(8, Allocator.Temp);
                if (physicsWorld.CalculateDistance(pointInput, ref distanceHits))
                {
                    for (int k = 0; k < distanceHits.Length; k++)
                    {
                        var hit = distanceHits[k];
                        if (hit.Entity == mr.Entity || hit.Entity == Entity.Null) continue;
                        
                        // DistanceHit.SurfaceNormal is the normal on the surface of the collider we hit.
                        // This is accurate for Box Colliders (Stairs).
                        
                        // Check if we are moving INTO the wall
                        float dotToward = math.dot(forward, -hit.SurfaceNormal);
                        
                        // If we are touching (dist < eps) and moving towards it
                        if (dotToward > 0.01f)
                        {
                            overlapPushDir = hit.SurfaceNormal;
                            overlapTarget = hit.Entity;
                            blocked = true;
                            hitNormal = hit.SurfaceNormal;
                            hitEntity = overlapTarget;
                            
#if STAIRS_DEBUG
                            LogStairDebug("OVERLAP_BLOCK", hit.Position, hit.SurfaceNormal, hitEntity, hit.Distance);
#endif
                            
                            // If we found a steep wall, break immediately as this is critical for StepUp
                            if (hit.SurfaceNormal.y < mr.MaxSlopeAngleCos)
                            {
                                break;
                            }
                        }
                    }
                }
                distanceHits.Dispose();

                // OPTIMIZATION 10.15.1: Use pre-cached collider instead of creating new one
                // Declared above
                
                // Debug: Log capsule parameters and cast positions
                // UnityEngine.Debug.Log($"Entity {mr.Entity.Index} capsule casting from {entStart} to {entStart + horizontal}, moveDist={moveDist}");

                var castInput = new ColliderCastInput
                {
                    Start = entStart,
                    End = entStart + horizontal,
                    Orientation = quaternion.identity
                };
                castInput.SetCollider(capsuleCollider);

                // Custom Collector to prioritize Steep Hits (Walls/Stairs) over Walkable Hits (Floor)
                // This ensures that if we hit both Floor and Wall simultaneously, we report the Wall 
                // so the Step-Up logic triggers.
                var steepPriorityCollector = new SteepPriorityCollector(1.0f, mr.MaxSlopeAngleCos);
                physicsWorld.CollisionWorld.CastCollider(castInput, ref steepPriorityCollector);

                if (steepPriorityCollector.NumHits > 0)
                {
                    var hit = steepPriorityCollector.GetBestHit();
                    hitEntity = physicsWorld.Bodies[hit.RigidBodyIndex].Entity;
                    
                    if (hitEntity != mr.Entity && hitEntity != Entity.Null)
                    {
                        blocked = true;
                        hitNormal = hit.SurfaceNormal;
#if STAIRS_DEBUG
                        LogStairDebug("FORWARD_BLOCK", entStart + horizontal * hit.Fraction, hit.SurfaceNormal, hitEntity, hit.Fraction);
#endif
                    }
                }

                // OPTIMIZATION 10.15.1: No Dispose() needed - colliders are cached and reused

                // Try step-up
                // Only attempt step up if the obstacle is STEEP (not walkable)
                // If it's a walkable slope, we should just slide along it (handled below)
                bool isWalkableCallback = hitNormal.y >= mr.MaxSlopeAngleCos;
                
                if (blocked && !isWalkableCallback)
                {
                    // For slides, allow a slightly more aggressive step-up attempt to preserve momentum
                    float tryStepUp = stepHeight;
                    if (mr.IsSlide != 0)
                    {
                        tryStepUp = stepHeight + skin + 0.05f;
                    }

                    var elevatedStart = entStart + new float3(0, tryStepUp, 0);
                    // OPTIMIZATION 10.15.1: Reuse same cached collider for step-up cast
                    
                    var elevatedCastInput = new ColliderCastInput
                    {
                        Start = elevatedStart,
                        End = elevatedStart + horizontal,
                        Orientation = quaternion.identity
                    };
                    elevatedCastInput.SetCollider(capsuleCollider);

                    var elevatedCollector = new ClosestColliderCastCollector(1f);
                    physicsWorld.CollisionWorld.CastCollider<ClosestColliderCastCollector>(elevatedCastInput, ref elevatedCollector);
                    
                    // OPTIMIZATION 10.15.1: No Dispose() needed

                    if (elevatedCollector.NumHits > 0)
                    {
                        // there is something at elevated height -> cannot step
#if STAIRS_DEBUG
                        var hit = elevatedCollector.Hit;
                        var stepEntity = physicsWorld.Bodies[hit.RigidBodyIndex].Entity;
                        LogStairDebug("STEP_FAIL", elevatedStart + horizontal * hit.Fraction, hit.SurfaceNormal, stepEntity, hit.Fraction);
#endif
                    }
                    else
                    {
                        // SUCCESS
#if STAIRS_DEBUG
                        LogStairDebug("STEP_SUCCESS", entStart, float3.zero, Entity.Null, 0);
#endif
                        outPositions[index] = entStart + new float3(0, tryStepUp, 0) + desiredDisp;
                        return;
                    }
                }

                if (blocked)
                {
                    float3 slide = desiredDisp - math.dot(desiredDisp, hitNormal) * hitNormal;
                    // slide.y = 0; // EPIC 15.3 FIX: Removed to allow vertical momentum (jumping/falling) during horizontal contact
                    // Apply retention factor: each resolve retains a fraction of velocity
                    float retention = (mr.IsSlide != 0) ? mr.SlideBlockedRetention : 1f;
                    slide *= retention;

                    outPositions[index] = entStart + slide;
                    if (mr.PushRigidbodies != 0 && hitEntity != Entity.Null)
                    {
                        float impulseScale = (mr.IsSlide != 0) ? mr.SlideHitImpulseScale : 1.0f;
                        pushWriter.Enqueue(new PushRequest { Target = hitEntity, Impulse = slide * impulseScale });
                    }
                    return;
                }

                // Not blocked
                if (mr.IsSlide != 0)
                {
                    // sliding in open space: apply retention factor
                    float retention = mr.SlideOpenRetention;
                    outPositions[index] = entStart + new float3(horizontal.x, desiredDisp.y, horizontal.z) * retention;
                }
                else
                {
                    outPositions[index] = entStart + new float3(horizontal.x, desiredDisp.y, horizontal.z);
                }
            }
        }
        [BurstCompile]
        [WithAll(typeof(Simulate))]
        private partial struct ComputeDispJob : IJobEntity
        {
            public float dt;
            public NativeQueue<MoveRequest>.ParallelWriter moveWriter;
            [ReadOnly] public ComponentLookup<ProneStateComponent> ProneLookup;
            [ReadOnly] public ComponentLookup<FreeClimbState> ClimbLookup;
            [ReadOnly] public ComponentLookup<RideState> RideLookup;

            void Execute(in Unity.Transforms.LocalTransform lt, in CharacterControllerSettings settings, in PhysicsVelocity vel, in PlayerState pState, in DeathState death, Entity entity)
            {
                if (death.Phase == DeathPhase.Dead || death.Phase == DeathPhase.Downed)
                {
                    // DLog is BurstDiscard, so this only shows if Burst is disabled or via some other magic, 
                    // but it confirms logic when needed.
                    // DLog($"[CC] Skipping dead/downed player {entity.Index}");
                    return;
                }

                // Skip entities that are actively prone (they're handled by ComputeDispProneJob)
                if (ProneLookup.HasComponent(entity))
                {
                    var prone = ProneLookup[entity];
                    if (prone.IsProne == 1)
                        return;
                }

                // Skip entities that are climbing (handled by FreeClimb systems)
                if (ClimbLookup.HasComponent(entity))
                {
                    var climb = ClimbLookup[entity];
                    if (climb.IsClimbing)
                        return;
                }
                
                // Skip entities that are riding (handled by RideControlSystem)
                if (RideLookup.HasComponent(entity))
                {
                    var ride = RideLookup[entity];
                    if (ride.RidePhase != RidePhaseConstants.None)
                        return;
                }

                // Skip movement when player is piloting a station (WASD controls ship, not player)
                if (pState.Mode == PlayerMode.Piloting)
                    return;

                // Use PhysicsVelocity (calculated by PlayerMovementSystem) for displacement
                // This includes Input, Gravity, Jumping, and Momentum
                float3 desiredDisp = vel.Linear * dt;

                // ALWAYS create a MoveRequest even with zero velocity
                // This allows the overlap check to run and block players from passing through each other
                // (Previously we skipped zero velocity, but that broke player-player collision)

                float height = settings.Height;
                if (pState.CurrentHeight > 0f)
                    height = pState.CurrentHeight;

                var req = new MoveRequest
                {
                    Entity = entity,
                    DesiredDisp = desiredDisp,
                    Height = height,
                    Radius = settings.Radius,
                    Skin = math.max(0.01f, settings.SkinWidth),
                    StepHeight = settings.StepHeight,
                    PushRigidbodies = settings.PushRigidbodies,
                    IsSlide = 0,
                    SlideBlockedRetention = settings.SlideBlockedRetention,
                    SlideOpenRetention = settings.SlideOpenRetention,
                    SlideStepExtra = settings.SlideStepExtra,
                    SlideHitImpulseScale = settings.SlideHitImpulseScale,
                    MaxSlopeAngleCos = math.cos(math.radians(settings.MaxSlopeAngleDeg))
                };

                moveWriter.Enqueue(req);
            }
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        private partial struct UpdatePlayerHeightJob : IJobEntity
        {
            public float dt;

            void Execute(ref PlayerState pState)
            {
                float t = math.clamp(pState.HeightTransitionSpeed * dt, 0f, 1f);
                pState.CurrentHeight = math.lerp(pState.CurrentHeight, pState.TargetHeight, t);
            }
        }
        [BurstCompile]
        [WithAll(typeof(ProneStateComponent))]
        [WithAll(typeof(Simulate))]
        private partial struct ComputeDispProneJob : IJobEntity
        {
            public float dt;
            public NativeQueue<MoveRequest>.ParallelWriter moveWriter;

            void Execute(in Unity.Transforms.LocalTransform lt, in CharacterControllerSettings settings, in ProneStateComponent prone, in PhysicsVelocity vel, in PlayerState pState, in DeathState death, Entity entity)
            {
                // Skip if dead or downed
                // Skip if dead or downed
                if (death.Phase == DeathPhase.Dead || death.Phase == DeathPhase.Downed)
                {
                    // DLog($"[CC] Skipping dead/downed player {entity.Index} (Prone)");
                    return;
                }

                if (prone.IsProne == 0) return;


                // Use PhysicsVelocity for displacement (consistent with ComputeDispJob)
                float3 desiredDisp = vel.Linear * dt;

                // Always create MoveRequest for overlap resolution

                float height = settings.Height * 0.5f;
                if (pState.CurrentHeight > 0f)
                    height = pState.CurrentHeight;

                var req = new MoveRequest
                {
                    Entity = entity,
                    DesiredDisp = desiredDisp,
                    Height = height,
                    Radius = settings.Radius,
                    Skin = math.max(0.01f, settings.SkinWidth),
                    StepHeight = settings.StepHeight,
                    PushRigidbodies = settings.PushRigidbodies,
                    IsSlide = 0,
                    SlideBlockedRetention = settings.SlideBlockedRetention,
                    SlideOpenRetention = settings.SlideOpenRetention,
                    SlideStepExtra = settings.SlideStepExtra,
                    SlideHitImpulseScale = settings.SlideHitImpulseScale,
                    MaxSlopeAngleCos = math.cos(math.radians(settings.MaxSlopeAngleDeg))
                };

                moveWriter.Enqueue(req);
            }
        }

            [BurstCompile]
            [WithAll(typeof(DodgeRollState))]
            [WithAll(typeof(Simulate))]
            private partial struct ComputeDodgeDispJob : IJobEntity
            {
                public float dt;
                public NativeQueue<MoveRequest>.ParallelWriter moveWriter;

                void Execute(in Unity.Transforms.LocalTransform lt, ref DodgeRollState roll, in CharacterControllerSettings settings, in PlayerState pState, in DeathState death, Entity entity)
                {
                    // Skip if dead or downed
                    if (death.Phase == DeathPhase.Dead || death.Phase == DeathPhase.Downed)
                        return;

                    if (roll.IsActive == 0) return;
                    
                    // Apply prediction reconciliation smoothing if active
                    float effectiveElapsed = roll.Elapsed;
                    if (roll.IsReconciling == 1 && roll.ReconcileSmoothing > 0f)
                    {
                        // Smoothly blend client elapsed toward server elapsed
                        effectiveElapsed = math.lerp(roll.Elapsed, roll.ServerElapsed, roll.ReconcileSmoothing);
                    }
                    
                    float remainingTime = math.max(0.0001f, roll.Duration - effectiveElapsed);
                    float speed = (roll.DistanceRemaining / remainingTime);
                    if (speed <= 0f) return;

                    // forward is entity's forward in local space
                    var forward = math.mul(lt.Rotation, new float3(0, 0, 1));
                    float3 desiredDisp = forward * (speed * dt);
                    // clamp to remaining distance
                    float3 desiredFlat = new float3(desiredDisp.x, 0, desiredDisp.z);
                    float dispLen = math.length(desiredFlat);
                    if (dispLen > roll.DistanceRemaining)
                    {
                        desiredFlat = desiredFlat * (roll.DistanceRemaining / dispLen);
                    }

                    float height = settings.Height;
                    if (pState.CurrentHeight > 0f)
                        height = pState.CurrentHeight;

                    var req = new MoveRequest
                    {
                        Entity = entity,
                        DesiredDisp = desiredFlat,
                        Height = height,
                        Radius = settings.Radius,
                        Skin = math.max(0.01f, settings.SkinWidth),
                        StepHeight = settings.StepHeight,
                        PushRigidbodies = settings.PushRigidbodies,
                        IsSlide = 0,
                        SlideBlockedRetention = settings.SlideBlockedRetention,
                        SlideOpenRetention = settings.SlideOpenRetention,
                        SlideStepExtra = settings.SlideStepExtra,
                        SlideHitImpulseScale = settings.SlideHitImpulseScale,
                        MaxSlopeAngleCos = math.cos(math.radians(settings.MaxSlopeAngleDeg))
                    };

                    moveWriter.Enqueue(req);
                }
            }

            [BurstCompile]
            [WithAll(typeof(Player.Components.DodgeDiveState))]
            [WithAll(typeof(Simulate))]
            private partial struct ComputeDiveDiveDispJob : IJobEntity
            {
                public float dt;
                public NativeQueue<MoveRequest>.ParallelWriter moveWriter;

                void Execute(in Unity.Transforms.LocalTransform lt, ref Player.Components.DodgeDiveState dive, in CharacterControllerSettings settings, in PlayerState pState, in DeathState death, Entity entity)
                {
                    // Skip if dead or downed
                    if (death.Phase == DeathPhase.Dead || death.Phase == DeathPhase.Downed)
                        return;

                    if (dive.IsActive == 0) return;
                    
                    // Apply prediction reconciliation smoothing if active
                    float effectiveElapsed = dive.Elapsed;
                    if (dive.IsReconciling == 1 && dive.ReconcileSmoothing > 0f)
                    {
                        effectiveElapsed = math.lerp(dive.Elapsed, dive.ServerElapsed, dive.ReconcileSmoothing);
                    }
                    
                    float remainingTime = math.max(0.0001f, dive.Duration - effectiveElapsed);
                    float speed = (dive.DistanceRemaining / remainingTime);
                    if (speed <= 0f) return;

                    // Forward dive - move in entity's forward direction
                    var forward = math.mul(lt.Rotation, new float3(0, 0, 1));
                    float3 desiredDisp = forward * (speed * dt);
                    
                    // Clamp to remaining distance
                    float3 desiredFlat = new float3(desiredDisp.x, 0, desiredDisp.z);
                    float dispLen = math.length(desiredFlat);
                    if (dispLen > dive.DistanceRemaining)
                    {
                        desiredFlat = desiredFlat * (dive.DistanceRemaining / dispLen);
                    }

                    float height = settings.Height;
                    if (pState.CurrentHeight > 0f)
                        height = pState.CurrentHeight;

                    var req = new MoveRequest
                    {
                        Entity = entity,
                        DesiredDisp = desiredFlat,
                        Height = height,
                        Radius = settings.Radius,
                        Skin = math.max(0.01f, settings.SkinWidth),
                        StepHeight = settings.StepHeight,
                        PushRigidbodies = settings.PushRigidbodies,
                        IsSlide = 0,
                        SlideBlockedRetention = settings.SlideBlockedRetention,
                        SlideOpenRetention = settings.SlideOpenRetention,
                        SlideStepExtra = settings.SlideStepExtra,
                        SlideHitImpulseScale = settings.SlideHitImpulseScale
                    };

                    moveWriter.Enqueue(req);
                }
            }

            [BurstCompile]
            [WithAll(typeof(SlideState))]
            [WithAll(typeof(Simulate))]
            private partial struct ComputeSlideDispJob : IJobEntity
            {
                public float dt;
                public NativeQueue<MoveRequest>.ParallelWriter moveWriter;

                void Execute(in Unity.Transforms.LocalTransform lt, in SlideState slide, in CharacterControllerSettings settings, in PlayerState pState, in DeathState death, Entity entity)
                {
                    // Skip if dead or downed
                    if (death.Phase == DeathPhase.Dead || death.Phase == DeathPhase.Downed)
                        return;

                    if (!slide.IsSliding) return;

                    // Calculate displacement based on current slide speed and direction
                    float3 desiredDisp = slide.SlideDirection * (slide.CurrentSpeed * dt);
                    
                    // Flatten to horizontal plane
                    float3 desiredFlat = new float3(desiredDisp.x, 0, desiredDisp.z);

                    float height = settings.Height;
                    if (pState.CurrentHeight > 0f)
                        height = pState.CurrentHeight;

                    var req = new MoveRequest
                    {
                        Entity = entity,
                        DesiredDisp = desiredFlat,
                        Height = height,
                        Radius = settings.Radius,
                        Skin = math.max(0.01f, settings.SkinWidth),
                        StepHeight = settings.StepHeight,
                        PushRigidbodies = settings.PushRigidbodies,
                        IsSlide = 1, // Mark as slide for special handling
                        SlideBlockedRetention = settings.SlideBlockedRetention,
                        SlideOpenRetention = settings.SlideOpenRetention,
                        SlideStepExtra = settings.SlideStepExtra,
                        SlideHitImpulseScale = settings.SlideHitImpulseScale
                    };

                    moveWriter.Enqueue(req);
                }
            }

        protected override void OnCreate()
        {
            base.OnCreate();
            Enabled = true;
            
            // OPTIMIZATION 10.15.1: Initialize NativeHashMap for Burst-compatible collider cache
            _capsuleCache = new NativeHashMap<long, BlobAssetReference<Unity.Physics.Collider>>(CAPSULE_CACHE_CAPACITY, Allocator.Persistent);
            
            // OPTIMIZATION 10.15.6: Initialize persistent collections
            _moveQueue = new NativeQueue<MoveRequest>(Allocator.Persistent);
            _pushQueue = new NativeQueue<PushRequest>(Allocator.Persistent);
            _movesList = new NativeList<MoveRequest>(128, Allocator.Persistent);
            _entitiesList = new NativeList<Entity>(128, Allocator.Persistent);
            _startPosList = new NativeList<float3>(128, Allocator.Persistent);
            _outPositionsList = new NativeList<float3>(128, Allocator.Persistent);
            _capsuleCollidersList = new NativeList<BlobAssetReference<Unity.Physics.Collider>>(128, Allocator.Persistent);
            _accumMap = new NativeHashMap<Entity, float3>(16, Allocator.Persistent);
            _blobsToDispose = new NativeList<BlobAssetReference<Unity.Physics.Collider>>(16, Allocator.Persistent);
            
            // Load settings asset from Resources if present
            s_CapsuleSettings = Resources.Load<CapsuleCacheSettings>("CapsuleCacheSettings");
            if (s_CapsuleSettings != null)
            {
                if (s_CapsuleSettings.Prewarm)
                {
                    foreach (var h in s_CapsuleSettings.PrewarmHeights)
                    {
                        foreach (var r in s_CapsuleSettings.PrewarmRadii)
                        {
                            GetOrCreateCapsuleBlob(h, r);
                        }
                    }
                }
            }
            else
            {
                // Pre-warm a small set of common capsule sizes for player characters
                PrewarmCapsuleCache();
            }
        }

        /// <summary>
        /// OPTIMIZATION 10.15.1: Capacity is now fixed at compile time.
        /// This method is kept for API compatibility but does nothing.
        /// </summary>
        public void SetCapsuleCacheCapacity(int capacity)
        {
            // Capacity is now fixed at CAPSULE_CACHE_CAPACITY (64)
            // This method is kept for backwards compatibility
        }

        /// <summary>
        /// Pre-warm the capsule cache with a small set of common sizes (height, radius).
        /// </summary>
        public void PrewarmCapsuleCache()
        {
            var heights = new float[] { 1.6f, 1.8f, 2.0f };
            var radii = new float[] { 0.25f, 0.35f };
            foreach (var h in heights)
            {
                foreach (var r in radii)
                {
                    GetOrCreateCapsuleBlob(h, r);
                }
            }
        }

        protected override void OnDestroy()
        {
            // SAFETY: Force all scheduled jobs to complete before disposing their containers
            Dependency.Complete();

            // OPTIMIZATION 10.15.1: Dispose NativeHashMap and all cached blobs
            if (_capsuleCache.IsCreated)
            {
                var keys = _capsuleCache.GetKeyArray(Allocator.Temp);
                for (int i = 0; i < keys.Length; i++)
                {
                    if (_capsuleCache.TryGetValue(keys[i], out var blob) && blob.IsCreated)
                    {
                        blob.Dispose();
                    }
                }
                keys.Dispose();
                _capsuleCache.Dispose();
            }

            // OPTIMIZATION 10.15.6: Dispose persistent collections
            if (_moveQueue.IsCreated) _moveQueue.Dispose();
            if (_pushQueue.IsCreated) _pushQueue.Dispose();
            if (_movesList.IsCreated) _movesList.Dispose();
            if (_entitiesList.IsCreated) _entitiesList.Dispose();
            if (_startPosList.IsCreated) _startPosList.Dispose();
            if (_outPositionsList.IsCreated) _outPositionsList.Dispose();
            if (_capsuleCollidersList.IsCreated) _capsuleCollidersList.Dispose();
            if (_accumMap.IsCreated) _accumMap.Dispose();
            DisposeDeferredBlobs();
            if (_blobsToDispose.IsCreated) _blobsToDispose.Dispose();

            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;
            
            // Debug: Log which world we're running in (once per world to avoid spam)
            string worldName = World.Name;
            if (s_DiagnosticsEnabled && !_hasLoggedWorld)
            {
                DLog($"CharacterControllerSystem running in world: {worldName}");
                _hasLoggedWorld = true;
            }

            // OPTIMIZATION 10.15.5: Gate ALL debug diagnostics - these were running every frame!
            if (s_DiagnosticsEnabled)
            {
                // Debug: Check if system is running and finding entities
                var query = SystemAPI.QueryBuilder()
                    .WithAll<LocalTransform, CharacterControllerSettings, PhysicsVelocity, PlayerState>()
                    .Build();
                int entityCount = query.CalculateEntityCount();
                if (entityCount == 0)
                {
                    DLogWarning("CharacterControllerSystem: No matching entities found!");
                }
                else
                {
                    // Only log entity processing once at startup or when VerboseLogging is enabled
                    if (s_VerboseLogging)
                    {
                        DLog($"CharacterControllerSystem: Processing {entityCount} entities");
                    }
                     
                    // Full entity diagnostic: only in VerboseLogging mode
                    if (s_VerboseLogging)
                    {
                        int colliderCount = 0;
                        int massCount = 0;
                        int simulateCount = 0;
                        var queryWithEntities = SystemAPI.QueryBuilder()
                            .WithAll<LocalTransform, PhysicsCollider>()
                            .Build();
                        var entities = queryWithEntities.ToEntityArray(Allocator.Temp);
                        colliderCount = entities.Length;
                        
                        foreach (var entity in entities)
                        {
                            bool hasMass = EntityManager.HasComponent<PhysicsMass>(entity);
                            bool hasSimulate = EntityManager.HasComponent<Simulate>(entity);
                            bool hasLocalToWorld = EntityManager.HasComponent<Unity.Transforms.LocalToWorld>(entity);
                            bool hasPhysicsWorldIndex = EntityManager.HasComponent<PhysicsWorldIndex>(entity);
                            bool isDisabled = EntityManager.HasComponent<Unity.Entities.Disabled>(entity);
                            
                            if (hasSimulate) simulateCount++;
                            if (hasMass) massCount++;
                            
                            var collider = EntityManager.GetComponentData<PhysicsCollider>(entity);
                            bool isColliderValid = collider.IsValid;
                            
                            // Get CollisionFilter details
                            var filter = collider.Value.Value.GetCollisionFilter();
                            
                            // Get PhysicsWorldIndex if present (it's a shared component)
                            uint worldIndex = hasPhysicsWorldIndex ? EntityManager.GetSharedComponent<PhysicsWorldIndex>(entity).Value : 0;
                            
                            // Get mass details
                            string massDetails = hasMass ? $"InverseMass={EntityManager.GetComponentData<PhysicsMass>(entity).InverseMass:G}" : "NO_MASS";
                            
                            DLogWarning($"FULL DIAGNOSTIC Entity({entity.Index}:{entity.Version}):\n" +
                                $"  PhysicsCollider Valid={isColliderValid}\n" +
                                $"  PhysicsMass: {massDetails}\n" +
                                $"  LocalToWorld={hasLocalToWorld}\n" +
                                $"  Simulate={hasSimulate}\n" +
                                $"  PhysicsWorldIndex={worldIndex} (has component={hasPhysicsWorldIndex})\n" +
                                $"  Disabled={isDisabled}\n" +
                                $"  CollisionFilter: BelongsTo=0x{filter.BelongsTo:X8}, CollidesWith=0x{filter.CollidesWith:X8}, GroupIndex={filter.GroupIndex}");
                        }
                        
                        entities.Dispose();
                        DLog($"CharacterControllerSystem: {colliderCount} entities have PhysicsCollider, {massCount} have PhysicsMass, {simulateCount} have Simulate");
                    }
                }
            }

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            
            // OPTIMIZATION 10.15.5: Gate physics body debug logging
            // Task 10.17.4: Enhanced terrain collision debugging (throttled)
            if (s_DiagnosticsEnabled)
            {
                // Only log PhysicsWorld summary every 60 frames to reduce spam
                bool isLogFrame = (UnityEngine.Time.frameCount % 60 == 0);
                
                if (isLogFrame)
                {
                    DLog($"[CC] PhysicsWorld: Total={physicsWorld.NumBodies}, Static={physicsWorld.NumStaticBodies}, Dynamic={physicsWorld.NumDynamicBodies}");
                }
                
                // Count chunk entities with valid PhysicsCollider (terrain debugging)
                int chunkColliderCount = 0;
                int chunkColliderValidCount = 0;
                int chunkWithPWICount = 0;  // PhysicsWorldIndex count
                var chunkQuery = EntityManager.CreateEntityQuery(
                    typeof(DIG.Voxel.Components.ChunkPosition),
                    typeof(PhysicsCollider)
                );
                if (!chunkQuery.IsEmpty)
                {
                    var chunkEntities = chunkQuery.ToEntityArray(Allocator.Temp);
                    chunkColliderCount = chunkEntities.Length;
                    foreach (var chunkEnt in chunkEntities)
                    {
                        var collider = EntityManager.GetComponentData<PhysicsCollider>(chunkEnt);
                        if (collider.IsValid)
                        {
                            chunkColliderValidCount++;
                            
                            // Task 10.17.1: Check if chunk has PhysicsWorldIndex
                            if (EntityManager.HasComponent<PhysicsWorldIndex>(chunkEnt))
                            {
                                chunkWithPWICount++;
                            }
                        }
                    }
                    chunkEntities.Dispose();
                }
                chunkQuery.Dispose();
                
                // Only log terrain stats when chunk count changes (reduces spam significantly)
                if (chunkColliderValidCount != s_LastChunkCount)
                {
                    DLog($"[CC] Terrain: {chunkColliderValidCount}/{chunkColliderCount} valid colliders, {chunkWithPWICount} with PhysicsWorldIndex | StaticBodies={physicsWorld.NumStaticBodies}");
                    s_LastChunkCount = chunkColliderValidCount;
                }
                
                // Debug: Log physics body details (VERY verbose - only when explicitly enabled)
                if (s_VerboseLogging)
                {
                    for (int i = 0; i < physicsWorld.NumBodies; i++)
                    {
                        var body = physicsWorld.Bodies[i];
                        var hasCollider = EntityManager.HasComponent<PhysicsCollider>(body.Entity);
                        var hasMass = EntityManager.HasComponent<PhysicsMass>(body.Entity);
                        var hasVelocity = EntityManager.HasComponent<PhysicsVelocity>(body.Entity);
                        DLog($"  Body[{i}]: Entity({body.Entity.Index}:{body.Entity.Version}), HasCollider={hasCollider}, HasMass={hasMass}, HasVelocity={hasVelocity}");
                    }
                }
            }

            // OPTIMIZATION: Complete dependencies before main thread access to persistent lists
            // This prevents "InvalidOperationException: The previously scheduled job... writes to the NativeList"
            Dependency.Complete();

            // Cleanup deferred blobs
            DisposeDeferredBlobs();

            // Prepare queues: use persistent queues from OnCreate
            // Ensure queues are empty (should be from previous frame drain, but safer to check/clear if possible)
            // NativeQueue doesn't have Clear(), but we drain it fully below. 
            // If logic is correct, it starts empty.
            
            var moveWriter = _moveQueue.AsParallelWriter();
            // _pushQueue is used as parallel writer in job, then read on main thread
            
            // OPTIMIZATION 10.15.2: Chain all jobs instead of calling Complete() after each one
            // ... (rest of job scheduling remains similar, but using persistent queues)
            
            // First update interpolated player heights
            var updateHeights = new UpdatePlayerHeightJob { dt = dt };
            var updateHandle = updateHeights.ScheduleParallel(Dependency);
            
            var computeProneJob = new ComputeDispProneJob
            {
                dt = dt,
                moveWriter = moveWriter
            };
            var proneHandle = computeProneJob.ScheduleParallel(updateHandle);

            var computeJob = new ComputeDispJob
            {
                dt = dt,
                moveWriter = moveWriter,
                ProneLookup = GetComponentLookup<ProneStateComponent>(true),
                ClimbLookup = GetComponentLookup<FreeClimbState>(true),
                RideLookup = GetComponentLookup<RideState>(true)
            };
            var dispHandle = computeJob.ScheduleParallel(proneHandle);

            var computeDodgeJob = new ComputeDodgeDispJob
            {
                dt = dt,
                moveWriter = moveWriter
            };
            var dodgeHandle = computeDodgeJob.ScheduleParallel(dispHandle);

            var computeDiveDiveJob = new ComputeDiveDiveDispJob
            {
                dt = dt,
                moveWriter = moveWriter
            };
            var diveHandle = computeDiveDiveJob.ScheduleParallel(dodgeHandle);

            var computeSlideJob = new ComputeSlideDispJob
            {
                dt = dt,
                moveWriter = moveWriter
            };
            var slideHandle = computeSlideJob.ScheduleParallel(diveHandle);
            
            // OPTIMIZATION 10.15.2: Single Complete() call for the entire chain
            slideHandle.Complete();

            // Drain moveQueue into persistent lists for job processing
            int moveCount = _moveQueue.Count;
            
            // Debug: Log move count
            LogIfEnabled($"[CC] moveCount={moveCount}, numDynamicBodies={physicsWorld.NumDynamicBodies}");

            // Resize persistent lists
            _movesList.Clear();
            _movesList.ResizeUninitialized(moveCount);
            
            _entitiesList.Clear();
            _entitiesList.ResizeUninitialized(moveCount);
            
            _startPosList.Clear();
            _startPosList.ResizeUninitialized(moveCount);

            // Dequeue into lists
            for (int i = 0; i < moveCount; i++)
            {
                MoveRequest mr = _moveQueue.Dequeue();
                _movesList[i] = mr;
                _entitiesList[i] = mr.Entity;
                if (EntityManager.HasComponent<Unity.Transforms.LocalTransform>(mr.Entity))
                {
                    _startPosList[i] = EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(mr.Entity).Position;
                }
                else
                {
                    _startPosList[i] = float3.zero;
                }
            }

            // OPTIMIZATION 10.15.1: Pre-create cached colliders for each move BEFORE job runs
            // Done BEFORE Overlap Job so we can chain dependencies
            _capsuleCollidersList.Clear();
            _capsuleCollidersList.ResizeUninitialized(moveCount);
            
            for (int i = 0; i < moveCount; i++)
            {
                var mr = _movesList[i];
                float capsuleRadius = math.max(0.01f, mr.Radius - mr.Skin);
                float capsuleHeight = mr.Height;
                // Get or create collider from cache (main thread only)
                _capsuleCollidersList[i] = GetOrCreateCapsuleBlob(capsuleHeight, capsuleRadius);
            }

            JobHandle dependency = Dependency;

            // OPTIMIZATION 10.15.4: Moved main thread overlap check to Burst Job
            // Pre-flight check: detect player-player overlaps and block movement toward each other
            int numDynamicBodies = physicsWorld.NumDynamicBodies;
            if (numDynamicBodies >= 2 && moveCount >= 2)
            {
                 var overlapJob = new PreventionOverlapJob
                 {
                     movesList = _movesList.AsArray(),
                     startPositions = _startPosList.AsArray()
                 };
                 // Schedule as single threaded job (N^2 but small N)
                 dependency = overlapJob.Schedule(dependency);
            }

            // Job: resolve physics for moves in parallel, write out new positions and push requests
            _outPositionsList.Clear();
            _outPositionsList.ResizeUninitialized(moveCount);

            // Build a conservative capsule collider that covers the tallest/widest player in this batch (for potential broadphase usage, though unused in ResolveJob direct list?)
            // This loop was purely for 'maxHeight'/'maxRadius' calculation which seems unused by ResolvePhysicsJob directly 
            // (it uses per-move colliders now). Removed.


            // Job: resolve physics for moves in parallel (per-move radial probe raycasts)
            var resolveJob = new ResolvePhysicsJob
            {
                moves = _movesList.AsArray(),
                startPositions = _startPosList.AsArray(),
                physicsWorld = physicsWorld,
                capsuleColliders = _capsuleCollidersList.AsArray(), // OPTIMIZATION 10.15.1: Pass cached colliders
                outPositions = _outPositionsList.AsArray(),
                pushWriter = _pushQueue.AsParallelWriter()
            };

            var resolveHandle = resolveJob.Schedule(moveCount, 8, dependency);
            resolveHandle.Complete();

            // Apply results
            for (int i = 0; i < moveCount; i++)
            {
                var ent = _entitiesList[i];
                if (!EntityManager.HasComponent<Unity.Transforms.LocalTransform>(ent))
                    continue;
                var lt = EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(ent);
                lt.Position = _outPositionsList[i];
                EntityManager.SetComponentData(ent, lt);
            }

            // OPTIMIZATION 10.15.6: No need to Dispose persistent lists here - they are reused

            // Accumulate pushes per-target, scale by inverse mass and apply smoothing
            _accumMap.Clear();
            
            while (_pushQueue.TryDequeue(out PushRequest pr))
            {
                if (_accumMap.TryGetValue(pr.Target, out var cur))
                {
                    cur += pr.Impulse;
                    _accumMap[pr.Target] = cur;
                }
                else
                {
                    _accumMap.TryAdd(pr.Target, pr.Impulse);
                }
            }

            var keys = _accumMap.GetKeyArray(Allocator.Temp);
            float smoothing = 0.6f;
            for (int k = 0; k < keys.Length; k++)
            {
                var target = keys[k];
                if (!EntityManager.Exists(target))
                    continue;
                if (!EntityManager.HasComponent<PhysicsVelocity>(target))
                    continue;

                var totalImpulse = _accumMap[target];
                float invMass = 1.0f;
                if (EntityManager.HasComponent<PhysicsMass>(target))
                {
                    var pm = EntityManager.GetComponentData<PhysicsMass>(target);
                    invMass = pm.InverseMass;
                }

                var vel = EntityManager.GetComponentData<PhysicsVelocity>(target);
                var delta = totalImpulse * invMass;
                vel.Linear = math.lerp(vel.Linear, vel.Linear + delta, smoothing);
                EntityManager.SetComponentData(target, vel);
            }

            keys.Dispose();
            // _accumMap is persistent, cleared at start/before use
            // _moveQueue and _pushQueue are emptied here
        }
    }

    [BurstCompile]
    public struct PreventionOverlapJob : IJob
    {
        public NativeArray<CharacterControllerSystem.MoveRequest> movesList;
        [ReadOnly] public NativeArray<float3> startPositions;
        
        public void Execute()
        {
            int count = movesList.Length;
            for (int i = 0; i < count; i++)
            {
                float3 posI = startPositions[i];
                float radiusI = movesList[i].Radius;
                float3 dispI = movesList[i].DesiredDisp;
                
                for (int j = i + 1; j < count; j++)
                {
                    float3 posJ = startPositions[j];
                    float radiusJ = movesList[j].Radius;
                    float3 dispJ = movesList[j].DesiredDisp;
                    
                    // Calculate horizontal distance
                    float3 toJ = posJ - posI;
                    toJ.y = 0;
                    float horizontalDist = math.length(toJ);
                    float minSeparation = radiusI + radiusJ + 0.05f; // Sum of radii + small buffer
                    
                    if (horizontalDist < minSeparation)
                    {
                        // Players are overlapping! Check if they're moving toward each other
                        float3 toJDir = math.normalizesafe(toJ);
                        float dotI = math.dot(math.normalizesafe(new float3(dispI.x, 0, dispI.z)), toJDir);
                        float dotJ = math.dot(math.normalizesafe(new float3(dispJ.x, 0, dispJ.z)), -toJDir);
                        
                        // If player I is moving toward player J, cancel that component
                        if (dotI > 0.1f)
                        {
                            float3 towardComponent = math.dot(dispI, toJDir) * toJDir;
                            var modifiedMove = movesList[i];
                            modifiedMove.DesiredDisp = dispI - towardComponent;
                            movesList[i] = modifiedMove;
                            // Update local var for next comparison? No, strictly we should updates loops.
                            // But for N=small, updating list is fine. "dispI" is local copy though.
                            // Does updating movesList[i] affect next 'j'? No, 'dispI' is reused?
                            // Issue: We used 'dispI' which is stale now.
                            // Fix: Update dispI.
                            dispI = modifiedMove.DesiredDisp;
                        }
                        
                        // If player J is moving toward player I, cancel that component
                        if (dotJ > 0.1f)
                        {
                            float3 towardComponent = math.dot(dispJ, -toJDir) * (-toJDir);
                            var modifiedMove = movesList[j];
                            modifiedMove.DesiredDisp = dispJ - towardComponent;
                            movesList[j] = modifiedMove;
                            // No need to update dispJ relevant to 'i' loop unless we re-read.
                        }
                    }
                }
            }
        }
    }
}
