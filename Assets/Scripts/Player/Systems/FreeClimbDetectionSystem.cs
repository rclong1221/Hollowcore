using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Detects climbable surfaces using physics raycasts.
    /// Replaces the entity-distance-based ClimbDetectionSystem with Invector-style multi-phase raycasting.
    /// 
    /// Algorithm (adapted from Invector vFreeClimb):
    /// 1. Cast from HandTarget position forward
    /// 2. If no hit: cast from HandTarget + input offset forward  
    /// 3. If no hit: cast diagonally back toward character
    /// 4. Validate surface angle (30-160 degrees from vertical)
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(PlayerMovementSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    // [BurstCompile]
    public partial struct FreeClimbDetectionSystem : ISystem
    {
        public bool EnableLogging;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }
        
        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            new FreeClimbDetectionJob
            {
                PhysicsWorld = physicsWorld.PhysicsWorld,
                Ecb = ecb,
                ClimbableSurfaceLookup = SystemAPI.GetComponentLookup<ClimbableSurface>(true),
                IsServer = state.WorldUnmanaged.IsServer(),
                EnableLogging = EnableLogging
            }.ScheduleParallel();
        }

        // [BurstCompile]
        private partial struct FreeClimbDetectionJob : IJobEntity
        {
            [ReadOnly] public PhysicsWorld PhysicsWorld;
            [ReadOnly] public ComponentLookup<ClimbableSurface> ClimbableSurfaceLookup;
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public bool IsServer;
            [ReadOnly] public bool EnableLogging;

            private void Execute(Entity entity, [EntityIndexInQuery] int sortKey, RefRO<LocalTransform> transform, RefRO<FreeClimbSettings> settings, RefRO<FreeClimbState> climbState)
            {
                // Skip if already climbing
                if (climbState.ValueRO.IsClimbing)
                {
                    // Remove candidate if climbing
                    Ecb.RemoveComponent<FreeClimbCandidate>(sortKey, entity);
                    return;
                }
                
                var lt = transform.ValueRO;
                var cfg = settings.ValueRO;
                
                // Calculate hand target position in world space
                float3 playerForward = math.forward(lt.Rotation);
                float3 playerUp = math.rotate(lt.Rotation, math.up());
                
                float3 handTargetWorld = lt.Position + math.rotate(lt.Rotation, cfg.HandTargetOffset);
                
                // Create collision filter for climbable surfaces
                var climbFilter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = cfg.ClimbableLayers,
                    GroupIndex = 0
                };
                
                // Phase 1: Direct raycast from hand target forward
                bool foundSurface = false;
                FreeClimbCandidate candidate = default;

                if (CastAndValidate(handTargetWorld, playerForward, cfg.DetectionDistance,
                    climbFilter, cfg.MinSurfaceAngle, cfg.MaxSurfaceAngle, entity, playerForward, out candidate, "Phase1"))
                {
                    foundSurface = true;
                }

                // Phase 2: If no hit, try from slightly below (catches ledges above)
                if (!foundSurface)
                {
                    float3 lowerOrigin = handTargetWorld - playerUp * 0.3f;
                    if (CastAndValidate(lowerOrigin, playerForward, cfg.DetectionDistance,
                        climbFilter, cfg.MinSurfaceAngle, cfg.MaxSurfaceAngle, entity, playerForward, out candidate, "Phase2"))
                    {
                        foundSurface = true;
                    }
                }

                // Phase 3: If no hit, try angled downward (catches surfaces below chest level)
                if (!foundSurface)
                {
                    float3 angledDir = math.normalize(playerForward - playerUp * 0.5f);
                    if (CastAndValidate(handTargetWorld, angledDir, cfg.DetectionDistance * 1.2f,
                        climbFilter, cfg.MinSurfaceAngle, cfg.MaxSurfaceAngle, entity, playerForward, out candidate, "Phase3"))
                    {
                        foundSurface = true;
                    }
                }

                // Phase 4: Sphere cast for nearby surfaces (catches concave geometry)
                if (!foundSurface)
                {
                    float3 sphereOrigin = handTargetWorld - playerForward * 0.3f;
                    if (SphereCastAndValidate(sphereOrigin, 0.2f, playerForward, cfg.DetectionDistance,
                        climbFilter, cfg.MinSurfaceAngle, cfg.MaxSurfaceAngle, entity, playerForward, out candidate))
                    {
                        foundSurface = true;
                    }
                }
                
                // Phase 5: Top Mount (Detect ledge behind/below when falling)
                // Only if not found yet and we might be falling past a ledge
                if (!foundSurface)
                {
                    // Cast from above and behind, downwards
                    float3 backOrigin = lt.Position - (playerForward * 0.4f) + (playerUp * 1.5f);
                    if (CastAndValidate(backOrigin, -playerUp, 2.0f, climbFilter, cfg.MinSurfaceAngle, cfg.MaxSurfaceAngle, entity, -playerForward, out candidate, "Phase5"))
                    {
                         // Verify this is actually a ledge top (surface below is null or far)
                         // And check if we are close enough to the edge
                         if (candidate.GripWorldPosition.y < lt.Position.y + 0.5f && candidate.GripWorldPosition.y > lt.Position.y - 0.5f)
                         {
                             foundSurface = true;
                             // Note: MountSystem will need to handle the 180 rotation
                         }
                    }
                }
                
                if (foundSurface)
                {
                    if (IsServer && EnableLogging) 
                    {
                        UnityEngine.Debug.Log($"[SERVER] [CLIMB_DIAG] Found Candidate! Entity={candidate.SurfaceEntity.Index} Dist={candidate.Distance:F2}");
                    }
                    Ecb.AddComponent(sortKey, entity, candidate);
                }
                else
                {
                    Ecb.RemoveComponent<FreeClimbCandidate>(sortKey, entity);
                }
            }

            /// <summary>
            /// Cast a ray and validate the hit surface angle.
            /// Excludes the player's own entity from results.
            /// EPIC 13.20: Also validates ClimbableSurface component and player facing angle.
            /// </summary>
            private bool CastAndValidate(
                float3 origin,
                float3 direction,
                float maxDistance,
                CollisionFilter filter,
                float minAngle,
                float maxAngle,
                Entity selfEntity,
                float3 playerForward,
                out FreeClimbCandidate candidate,
                string phaseName,
                float reverseProbeDist = 0.3f)
            {
                candidate = default;
                
                // Safety: Guard against NaN inputs
                if (!math.all(math.isfinite(origin)) || !math.all(math.isfinite(direction))) return false;

                var rayInput = new RaycastInput
                {
                    Start = origin,
                    End = origin + direction * maxDistance,
                    Filter = filter
                };

                // Use collector to filter out self efficiently
                var collector = new IgnoreEntityCollector(selfEntity);
                PhysicsWorld.CastRay(rayInput, ref collector);
                
                if (collector.HasHit)
                {
                    var hit = collector.ClosestHit;
                    RaycastHit? bestHit = hit; // Adaptation to existing var logic if needed or direct use
                    
                        // Calculate surface angle from vertical (0 = ceiling, 90 = wall, 180 = floor)
                        float angleFromUp = math.degrees(math.acos(math.clamp(math.dot(hit.SurfaceNormal, new float3(0, 1, 0)), -1f, 1f)));

                        if (angleFromUp >= minAngle && angleFromUp <= maxAngle)
                        {
                            // EPIC 13.20.1: Check if surface has ClimbableSurface component
                            bool hasClimbableComponent = ClimbableSurfaceLookup.HasComponent(hit.Entity);
                            
                            // EPIC 13.20.1: Validate player is facing surface (within ±60° of surface normal)
                            float3 surfaceToPlayer = -hit.SurfaceNormal;
                            float facingDot = math.dot(playerForward, surfaceToPlayer);
                            bool isFacingSurface = facingDot > 0.5f; // ~60° tolerance

                            // Require either ClimbableSurface component OR layer-based detection with facing check
                            if (hasClimbableComponent || isFacingSurface)
                            {
                                // EPIC 14.27: Thin Surface Validation (Reverse Probe)
                                // Cast from the back of the hit towards the hit position
                                float3 reverseOrigin = hit.Position + (-hit.SurfaceNormal * reverseProbeDist);
                                var reverseInput = new RaycastInput
                                {
                                    Start = reverseOrigin,
                                    End = hit.Position + (hit.SurfaceNormal * 0.1f),
                                    Filter = filter
                                };
                                
                                var reverseCollector = new IgnoreEntityCollector(Entity.Null); // Don't ignore wall!
                                PhysicsWorld.CastRay(reverseInput, ref reverseCollector);
                                
                                bool isTooThin = false;
                                if (reverseCollector.HasHit)
                                {
                                    float thickness = math.distance(hit.Position, reverseCollector.ClosestHit.Position);
                                    if (thickness < 0.15f) isTooThin = true; // Minimum thickness 15cm
                                }
                                
                                if (!isTooThin)
                                {
                                    candidate = new FreeClimbCandidate
                                    {
                                        SurfaceEntity = hit.Entity,
                                        GripWorldPosition = hit.Position,
                                        GripWorldNormal = hit.SurfaceNormal,
                                        Distance = hit.Fraction * maxDistance,
                                        SurfaceAngle = angleFromUp
                                    };
                                    return true;
                                }
                            }
                            else
                            {
                                // Log rejected hit (throttled/debug only)
                                // UnityEngine.Debug.Log($"[FreeClimbDetection] {phaseName} Rejected Hit: Entity={hit.Entity.Index} Facing={isFacingSurface} ({facingDot:F2}) HasComp={hasClimbableComponent} Angle={angleFromUp:F1}");
                            }
                        }
                        else
                        {
                            // Log bad angle
                            // UnityEngine.Debug.Log($"[FreeClimbDetection] {phaseName} Hit Entity={hit.Entity.Index} but bad angle {angleFromUp:F1} (Req: {minAngle}-{maxAngle})");
                        }
                    }
                
                return false;
            }
            
            private bool SphereCastAndValidate(
                float3 origin,
                float radius,
                float3 direction,
                float maxDistance,
                CollisionFilter filter,
                float minAngle,
                float maxAngle,
                Entity selfEntity,
                float3 playerForward,
                out FreeClimbCandidate candidate)
            {
                candidate = default;

                // Use PointDistanceInput instead of ColliderCastInput to avoid unsafe pointers
                var pointInput = new PointDistanceInput
                {
                    Position = origin + direction * (maxDistance * 0.5f),
                    MaxDistance = maxDistance,
                    Filter = filter
                };

                // Get all distance hits and filter out self
                var hits = new NativeList<DistanceHit>(8, Allocator.Temp);
                if (PhysicsWorld.CalculateDistance(pointInput, ref hits))
                {
                    // Find closest hit that isn't ourselves
                    float closestDist = float.MaxValue;
                    DistanceHit? bestHit = null;

                    for (int i = 0; i < hits.Length; i++)
                    {
                        var hit = hits[i];
                        // Skip self-collision
                        if (hit.Entity == selfEntity)
                            continue;

                        if (hit.Distance < closestDist)
                        {
                            closestDist = hit.Distance;
                            bestHit = hit;
                        }
                    }

                    if (bestHit.HasValue)
                    {
                        var hit = bestHit.Value;
                        float angleFromUp = math.degrees(math.acos(math.dot(hit.SurfaceNormal, new float3(0, 1, 0))));

                        if (angleFromUp >= minAngle && angleFromUp <= maxAngle)
                        {
                            // EPIC 13.20.1: Check if surface has ClimbableSurface component
                            bool hasClimbableComponent = ClimbableSurfaceLookup.HasComponent(hit.Entity);
                            
                            // EPIC 13.20.1: Validate player is facing surface (within ±60° of surface normal)
                            float3 surfaceToPlayer = -hit.SurfaceNormal;
                            float facingDot = math.dot(playerForward, surfaceToPlayer);
                            bool isFacingSurface = facingDot > 0.5f; // ~60° tolerance

                            // Require either ClimbableSurface component OR layer-based detection with facing check
                            if (hasClimbableComponent || isFacingSurface)
                            {
                                candidate = new FreeClimbCandidate
                                {
                                    SurfaceEntity = hit.Entity,
                                    GripWorldPosition = hit.Position,
                                    GripWorldNormal = hit.SurfaceNormal,
                                    Distance = hit.Distance,
                                    SurfaceAngle = angleFromUp
                                };
                                return true;
                            }
                        }
                    }
                }
                
                hits.Dispose();
                return false;
        }
        
        [BurstCompile]
        public struct IgnoreEntityCollector : ICollector<Unity.Physics.RaycastHit>
        {
            public Entity IgnoreEntity;
            public Unity.Physics.RaycastHit ClosestHit;
            public bool HasHit;
            public float MaxFraction { get; private set; }
            public int NumHits => HasHit ? 1 : 0;
            public bool EarlyOutOnFirstHit => false;

            public IgnoreEntityCollector(Entity ignore)
            {
                IgnoreEntity = ignore;
                ClosestHit = default;
                HasHit = false;
                MaxFraction = 1.0f;
            }

            public bool AddHit(Unity.Physics.RaycastHit hit)
            {
                if (hit.Entity == IgnoreEntity) return false;
                
                if (hit.Fraction < MaxFraction)
                {
                    MaxFraction = hit.Fraction;
                    ClosestHit = hit;
                    HasHit = true;
                    return true;
                }
                return false;
            }
        }
    }
}
}
