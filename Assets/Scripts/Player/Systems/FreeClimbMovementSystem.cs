using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.NetCode;
using Unity.Collections.LowLevel.Unsafe;
using Player.Components;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

namespace Player.Systems
{
    /// <summary>
    /// EPIC 14.26: Object Gravity Movement System
    /// Replaces old raycast-based grip tracking with continuous surface adhesion.
    /// 
    /// Key Concepts:
    /// - Surface Gravity: Character is pulled towards 'SurfaceNormal'
    /// - Local Move: Input is projected onto the surface plane defined by 'SurfaceNormal'
    /// - Adhesion: Sphere/Ray checks keep the player attached
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(FreeClimbMountSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    public partial struct FreeClimbMovementSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var currentTime = SystemAPI.Time.ElapsedTime;
            bool isServer = state.WorldUnmanaged.IsServer();

            state.Dependency = new FreeClimbObjectGravityJob
            {
                DeltaTime = deltaTime,
                PhysicsWorld = physicsWorld.PhysicsWorld,
                IsServer = isServer,
                CurrentTime = currentTime,
                PhysicsColliderLookup = SystemAPI.GetComponentLookup<PhysicsCollider>(true),
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true)
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        private partial struct FreeClimbObjectGravityJob : IJobEntity
        {
            [ReadOnly] public float DeltaTime;
            [ReadOnly] public PhysicsWorld PhysicsWorld;
            [ReadOnly] public bool IsServer;
            [ReadOnly] public double CurrentTime;
            [ReadOnly] public ComponentLookup<PhysicsCollider> PhysicsColliderLookup;
            [ReadOnly, NativeDisableContainerSafetyRestriction] public ComponentLookup<LocalTransform> TransformLookup;

            private void Execute(Entity entity, ref FreeClimbState climb, ref FreeClimbLocalState localState, RefRO<FreeClimbSettings> settings, RefRO<PlayerInput> input, ref LocalTransform lt, ref PhysicsVelocity vel)
            {
                if (!climb.IsClimbing) 
                    return;

                // validate surface entity
                if (climb.IsAdhered && climb.SurfaceEntity != Entity.Null)
                {
                    if (!PhysicsColliderLookup.HasComponent(climb.SurfaceEntity))
                    {
                        climb.IsAdhered = false;
                        climb.SurfaceEntity = Entity.Null;
                        // Continue to let gravity/raycasts handle finding new surface
                    }
                }

                // Stop physics velocity
                vel.Linear = float3.zero;
                vel.Angular = float3.zero;

                // Skip movement during transitions (Mounting, Vaulting, etc.)
                if (climb.IsTransitioning || climb.IsHangTransitioning || climb.IsWallJumping || climb.IsClimbingUp)
                    return;

                var cfg = settings.ValueRO;
                var playerInput = input.ValueRO;
                
                // 1. Resolve Surface & Adhesion
                float3 currentNormal = climb.SurfaceNormal;

                // Safety: Guard against NaN or Zero normals
                bool isNormalInvalid = !math.all(math.isfinite(currentNormal)) || math.lengthsq(currentNormal) < 0.001f;
                
                if (isNormalInvalid)
                {
                    // Fallback to character forward or world forward
                    float3 forward = math.forward(lt.Rotation);
                    if (!math.all(math.isfinite(forward)) || math.lengthsq(forward) < 0.001f) forward = new float3(0, 0, 1);
                    
                    currentNormal = -forward;
                    climb.SurfaceNormal = currentNormal;
                }
                
                // Safety: Guard against NaN position
                if (!math.all(math.isfinite(lt.Position)))
                {
                    // Emergency reset to prevent physics explosion
                    // This shouldn't happen but protects the simulation
                    lt.Position = new float3(0, 10, 0); 
                    return;
                }

                // 2. Resolve Surface Position (Local-Space support for moving platforms)
                if (climb.IsAdhered && TransformLookup.HasComponent(climb.SurfaceEntity))
                {
                    // Re-calculate world position from local space if surface moved
                    var surfaceLT = TransformLookup[climb.SurfaceEntity];
                    float3 worldGrip = surfaceLT.TransformPoint(climb.GripLocalPosition);
                    float3 worldNormal = math.rotate(surfaceLT.Rotation, climb.GripLocalNormal);
                    
                    // Subtle alignment: If our current position is too far from the moving surface, snap to it
                    // This handles linear movement of the platform
                    float3 targetSnap = worldGrip + (worldNormal * cfg.SurfaceOffset);
                    if (math.distance(lt.Position, targetSnap) > 0.01f)
                    {
                        lt.Position = math.lerp(lt.Position, targetSnap, DeltaTime * 20f);
                    }
                }

                // 3. Calculate Surface Space Basis
                // Right = Cross(Normal, WorldUp). If Normal is Up/Down, use Forward.
                float3 worldUp = new float3(0, 1, 0);
                float3 surfaceRight = math.cross(currentNormal, worldUp);
                
                // Handle singularity (ceiling/floor climbing)
                if (math.lengthsq(surfaceRight) < 0.01f)
                {
                    surfaceRight = math.cross(currentNormal, new float3(1, 0, 0)); // Arbitrary fallback axis
                }
                if (math.lengthsq(surfaceRight) > 0.0001f)
                    surfaceRight = math.normalize(surfaceRight);
                else
                    surfaceRight = new float3(1, 0, 0); // Absolute fallback

                // Surface Up (Forward on wall) = Cross(Right, Normal)
                float3 surfaceForward = math.cross(surfaceRight, currentNormal);
                
                // 3. Project Input to Surface Space
                float inputX = playerInput.Horizontal;
                float inputY = playerInput.Vertical;

                // Hanging restriction (reduced upward movement)
                if (climb.IsFreeHanging && inputY > 0)
                    inputY *= 0.3f;

                float3 moveDir = (surfaceRight * inputX) + (surfaceForward * inputY);
                if (math.lengthsq(moveDir) > 1f) moveDir = math.normalize(moveDir);

                // 4. Move Character
                float3 targetPos = lt.Position + (moveDir * cfg.ClimbSpeed * DeltaTime);

                // 4.1 Obstacle Check (Floors/Ceilings/Walls in movement direction)
                if (math.lengthsq(moveDir) > 0.001f)
                {
                    bool blocked = false;
                    float3 checkExtents = moveDir * 0.3f; // Reduced from 0.4 to ensure dismount triggers first
                    var obstacleFilter = new CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = cfg.ObstacleLayers | cfg.ClimbableLayers,
                        GroupIndex = 0
                    };

                    // Check Feet
                    float3 origin = lt.Position + new float3(0, 0.1f, 0);
                    var obstacleCollector = new IgnoreEntityCollector(entity);
                    var obstacleInput = new RaycastInput { Start = origin, End = origin + checkExtents, Filter = obstacleFilter };
                    PhysicsWorld.CastRay(obstacleInput, ref obstacleCollector);
                    if (obstacleCollector.HasHit) blocked = true;

                    if (!blocked)
                    {
                        // Check Waist
                        origin = lt.Position + new float3(0, 1.0f, 0);
                        obstacleCollector = new IgnoreEntityCollector(entity);
                        obstacleInput.Start = origin; obstacleInput.End = origin + checkExtents;
                        PhysicsWorld.CastRay(obstacleInput, ref obstacleCollector);
                        if (obstacleCollector.HasHit) blocked = true;
                    }

                    if (!blocked)
                    {
                        // Check Head
                        origin = lt.Position + new float3(0, 1.8f, 0);
                        obstacleCollector = new IgnoreEntityCollector(entity);
                        obstacleInput.Start = origin; obstacleInput.End = origin + checkExtents;
                        PhysicsWorld.CastRay(obstacleInput, ref obstacleCollector);
                        if (obstacleCollector.HasHit) blocked = true;
                    }

                    if (blocked)
                    {
                        SafeSetPosition(ref lt, targetPos); 
                    }
                }

                // 5. Adhesion / Surface Tracking (The "Gravity")
                // Cast from TargetPos towards -SurfaceNormal to find the wall
                // We cast slightly *into* the wall and *out* to handle curvature
                
                // Origin: TargetPos + Normal * (Radius) -> Start outside
                // Dir: -Normal
                // EPIC FIX: Use DetectionDistance to ensure we don't drop wall immediately after mounting
                float safeRadius = math.max(cfg.DetectionRadius, 0.5f); 
                float checkDist = math.max(cfg.DetectionDistance, cfg.SurfaceCheckDistance) * 1.5f; 
                float3 checkOrigin = targetPos + (currentNormal * safeRadius);
                float3 checkDir = -currentNormal;

                var filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = cfg.ClimbableLayers,
                    GroupIndex = 0
                };

                bool surfaceFound = false;
                RaycastHit hit = default;
                
                // A. Primary Adhesion Check (Ray/Sphere in gravity direction)
                // Use IgnoreEntityCollector to detect wall through player body
                var collector = new IgnoreEntityCollector(entity);
                var rayInput = new RaycastInput
                {
                    Start = checkOrigin,
                    End = checkOrigin + (checkDir * checkDist),
                    Filter = filter
                };
                
                PhysicsWorld.CastRay(rayInput, ref collector);

                if (collector.HasHit)
                {
                    hit = collector.ClosestHit;
                    surfaceFound = true;
                }

                // === C. CORNER / EDGE LOGIC (Convex & Concave) ===
                
                bool innerCornerFound = false;
                
                // Debugging Input (Verbose)
                // if (math.lengthsq(moveDir) > 0.01f) 
                //     UnityEngine.Debug.Log($"[CLIMB_TRACE] Surf={surfaceFound} Move={moveDir} Origin={checkOrigin}");

                // 1. Inner Corner (Concave)
                if (!surfaceFound && math.lengthsq(moveDir) > 0.01f)
                {
                    var innerCollector = new IgnoreEntityCollector(entity);
                    var innerInput = new RaycastInput
                    {
                        Start = checkOrigin,
                        End = checkOrigin + (moveDir * cfg.DetectionDistance),
                        Filter = filter
                    };
                    
                    // Visually Debug Inner Cast (1 second duration)
                    UnityEngine.Debug.DrawLine(innerInput.Start, innerInput.End, Color.blue, 0.1f);
                    
                    PhysicsWorld.CastRay(innerInput, ref innerCollector);
                    
                    if (innerCollector.HasHit)
                    {
                         var innerHit = innerCollector.ClosestHit;
                         bool hitWall = math.abs(math.dot(innerHit.SurfaceNormal, currentNormal)) < 0.7f;
                         
                         if (hitWall)
                         {
                             // UnityEngine.Debug.Log($"[CORNER] Inner Hit! Norm={innerHit.SurfaceNormal}");
                             hit = innerHit;
                             surfaceFound = true;
                             innerCornerFound = true;
                             
                             climb.SurfaceNormal = hit.SurfaceNormal;
                             climb.SurfaceContactPoint = hit.Position;
                             climb.SurfaceEntity = hit.Entity;
                             climb.IsAdhered = true;
                             climb.AdhesionStrength = 1.0f;
                             
                             float3 snapPos = hit.Position + (hit.SurfaceNormal * cfg.SurfaceOffset);
                             SafeSetPosition(ref lt, snapPos);
                             
                             // Safe LookRotation: Handle collinear up/normal
                             float3 cornerLook = -hit.SurfaceNormal;
                             float3 cornerUp = worldUp;
                             if (math.abs(math.dot(cornerLook, cornerUp)) > 0.99f) cornerUp = new float3(1, 0, 0); // Fallback up if looking straight up/down
                             
                             SafeSetRotation(ref lt, quaternion.LookRotation(cornerLook, cornerUp));
                         }
                    }
                }

                if (surfaceFound && !innerCornerFound)
                {
                    // Standard Adhesion
                    // ... (Standard Logic)
                    climb.SurfaceNormal = hit.SurfaceNormal;
                    climb.SurfaceContactPoint = hit.Position;
                    climb.SurfaceEntity = hit.Entity;
                    climb.IsAdhered = true;
                    climb.AdhesionStrength = 1.0f; 
                    climb.SurfaceDistance = math.distance(hit.Position, targetPos);

                    float3 snapPos = hit.Position + (hit.SurfaceNormal * cfg.SurfaceOffset);
                    SafeSetPosition(ref lt, snapPos);
                    
                    // Normal Smoothing (EPIC 14.27)
                    if (math.lengthsq(climb.SmoothedNormal) < 0.01f) climb.SmoothedNormal = climb.SurfaceNormal;
                    
                    float3 lerpedNormal = math.lerp(climb.SmoothedNormal, climb.SurfaceNormal, math.clamp(cfg.SurfaceTransitionSpeed * DeltaTime, 0, 1));
                    if (math.lengthsq(lerpedNormal) < 0.001f) lerpedNormal = climb.SurfaceNormal; // Safe guard against cancelling vectors
                    climb.SmoothedNormal = math.normalize(lerpedNormal);

                    float3 lookDir = -climb.SmoothedNormal;
                    float3 charUp = worldUp;
                    if (math.abs(math.dot(lookDir, worldUp)) > 0.9f) charUp = surfaceForward; 
                    
                    if (math.lengthsq(lookDir) > 0.001f)
                    {
                        quaternion targetRot = quaternion.LookRotation(lookDir, charUp);
                        SafeSetRotation(ref lt, math.slerp(lt.Rotation, targetRot, cfg.RotationSpeed * DeltaTime));
                    }
                }
                else if (!surfaceFound)
                {
                    // 2. Outer Corner (Convex) - "Diagonal Corner Cast"
                    // Move diagonally "out" into the void (Past Edge + Behind Wall) and look back at the corner.
                    // This hits the side wall "head on" instead of skimming it.
                    
                    bool outerCornerFound = false;
                    if (math.lengthsq(moveDir) > 0.01f)
                    {
                        // 2. Outer Corner (Convex) - "Whisker Array"
                        // Instead of a single diagonal guess, we fan out rays from the void to find where the wall went.
                        
                        // Origin: Projected position in the void (Where we would be if we walked off the edge)
                        // "Left" (Past Edge) + "Forward" (Into the turn)
                        float3 projectedPos = checkOrigin + (moveDir * 1.2f) + (-currentNormal * 0.5f);
                        
                        // Directions to check (Fan pattern)
                        float3 dirBack = -math.normalize(moveDir); // Looking back at where we came from
                        float3 dirIn = -currentNormal;             // Looking into the wall block
                        
                        // 5 Rays: 
                        // 0. Hook (45 deg) - Best for 90 deg corners
                        // 1. Deep Hook (22 deg) - Sharp corners
                        // 2. Side Hook (67 deg) - Shallow corners
                        // 3. Pure In (0 deg) - Grazing / Cylinder
                        // 4. Back (90 deg) - Fallback
                        // 5 Rays (Unrolled or FixedList for Burst)
                        FixedList128Bytes<float3> whiskerDirs = new FixedList128Bytes<float3>();
                        whiskerDirs.Add(math.normalize(dirBack + dirIn));           // Hook 45
                        whiskerDirs.Add(math.normalize(dirBack * 0.5f + dirIn));    // Deep Hook 22
                        whiskerDirs.Add(math.normalize(dirBack + dirIn * 0.5f));    // Side Hook 67
                        whiskerDirs.Add(dirIn);                                     // Pure In
                        whiskerDirs.Add(dirBack);                                   // Back
                        
                        bool whiskerFound = false;
                        CollisionFilter safetyFilter = filter; // Use same filter
                        IgnoreEntityCollector whiskerCollector = new IgnoreEntityCollector(entity);

                        for (int i = 0; i < whiskerDirs.Length; i++)
                        {
                            var wInput = new RaycastInput
                            {
                                Start = projectedPos,
                                End = projectedPos + (whiskerDirs[i] * 2.0f),
                                Filter = safetyFilter
                            };
                            
                            // Debug
                            UnityEngine.Debug.DrawLine(wInput.Start, wInput.End, Color.yellow, 0.5f);
                            
                            whiskerCollector = new IgnoreEntityCollector(entity); // Reset collector
                            PhysicsWorld.CastRay(wInput, ref whiskerCollector);
                            
                            if (whiskerCollector.HasHit)
                            {
                                var wHit = whiskerCollector.ClosestHit;
                                float dot = math.dot(wHit.SurfaceNormal, currentNormal);
                                
                                // We are looking for a DIFFERENT wall (dot < 0.9f means at least ~25 deg diff)
                                // or just any solid hit if we are truly in void? 
                                // Let's strictly require a surface deviation to act as a proper "corner turn".
                                if (dot < 0.9f)
                                {
                                    // GREEDY CORNER FIX: 
                                    // Check if the new surface is a "Floor" (pointing Up).
                                    // If so, IGNORE IT. We want the Ledge/Vault system to handle the transition to the top.
                                    // Snapping here causes us to "climb on the floor" and break the vault trigger.
                                    float upDot = math.dot(wHit.SurfaceNormal, worldUp);
                                    if (upDot > 0.7f) // ~45 degrees or flatter
                                    {
                                        // UnityEngine.Debug.Log($"[CORNER] Ignored Floor Hit! Norm={wHit.SurfaceNormal} UpDot={upDot}");
                                        continue; 
                                    }

                                    UnityEngine.Debug.Log($"[CORNER] Whisker {i} HIT! Norm={wHit.SurfaceNormal} Dot={dot}");
                                    hit = wHit;
                                    whiskerFound = true;
                                    outerCornerFound = true;
                                    surfaceFound = true;
                                    
                                    climb.SurfaceNormal = hit.SurfaceNormal;
                                    climb.SurfaceContactPoint = hit.Position;
                                    climb.SurfaceEntity = hit.Entity;
                                    climb.IsAdhered = true;
                                    climb.AdhesionStrength = 1.0f;
                                    
                                    float3 snapPos = hit.Position + (hit.SurfaceNormal * cfg.SurfaceOffset);
                                    SafeSetPosition(ref lt, snapPos);

                                    // Safe LookRotation: Handle collinear up/normal
                                    float3 cornerLook = -hit.SurfaceNormal;
                                    float3 cornerUp = worldUp;
                                    if (math.abs(math.dot(cornerLook, cornerUp)) > 0.99f) cornerUp = new float3(1, 0, 0); // Fallback up if looking straight up/down
                                    
                                    SafeSetRotation(ref lt, quaternion.LookRotation(cornerLook, cornerUp));
                                    
                                    break; // Found our surface
                                }
                            }
                        }
                        
                        if (!whiskerFound)
                        {
                            UnityEngine.Debug.Log("[CORNER] All Whiskers Missed.");
                        }
                    }
                    
                    if (!outerCornerFound)
                    {
                        // 3. Fallback: Safety Probe (Drift/Idle Lock)
                        // Checks Up/Down/Left/Right to keep us stuck if we jitter off
                        
                        var safetyCollector = new IgnoreEntityCollector(entity);
                        bool safetyHit = false;
                        float3 side = math.cross(currentNormal, new float3(0,1,0)) * 0.3f;
                        
                        // Center
                        var safeInput = new RaycastInput { Start = checkOrigin, End = checkOrigin + (checkDir * checkDist * 1.5f), Filter = filter };
                        PhysicsWorld.CastRay(safeInput, ref safetyCollector);
                        if (safetyCollector.HasHit) { hit = safetyCollector.ClosestHit; safetyHit = true; }
                        else
                        {
                            // Up
                            safetyCollector = new IgnoreEntityCollector(entity);
                            float3 probeOrigin = checkOrigin + new float3(0, 0.5f, 0);
                            safeInput.Start = probeOrigin; safeInput.End = probeOrigin + (checkDir * checkDist * 1.5f);
                            PhysicsWorld.CastRay(safeInput, ref safetyCollector);
                            if (safetyCollector.HasHit) { hit = safetyCollector.ClosestHit; safetyHit = true; }
                            else
                            {
                                // Down
                                safetyCollector = new IgnoreEntityCollector(entity);
                                probeOrigin = checkOrigin + new float3(0, -0.5f, 0);
                                safeInput.Start = probeOrigin; safeInput.End = probeOrigin + (checkDir * checkDist * 1.5f);
                                PhysicsWorld.CastRay(safeInput, ref safetyCollector);
                                if (safetyCollector.HasHit) { hit = safetyCollector.ClosestHit; safetyHit = true; }
                                else
                                {
                                    // Left
                                    safetyCollector = new IgnoreEntityCollector(entity);
                                    probeOrigin = checkOrigin + side;
                                    safeInput.Start = probeOrigin; safeInput.End = probeOrigin + (checkDir * checkDist * 1.5f);
                                    PhysicsWorld.CastRay(safeInput, ref safetyCollector);
                                    if (safetyCollector.HasHit) { hit = safetyCollector.ClosestHit; safetyHit = true; }
                                    else
                                    {
                                        // Right
                                        safetyCollector = new IgnoreEntityCollector(entity);
                                        probeOrigin = checkOrigin - side;
                                        safeInput.Start = probeOrigin; safeInput.End = probeOrigin + (checkDir * checkDist * 1.5f);
                                        PhysicsWorld.CastRay(safeInput, ref safetyCollector);
                                        if (safetyCollector.HasHit) { hit = safetyCollector.ClosestHit; safetyHit = true; }
                                    }
                                }
                            }
                        }
                        
                        if (safetyHit)
                        {
                             // UnityEngine.Debug.Log($"[CLIMB] Safety Catch! (Did Corner Logic Fail?)");
                             
                             climb.SurfaceNormal = hit.SurfaceNormal;
                             climb.SurfaceContactPoint = hit.Position;
                             climb.SurfaceEntity = hit.Entity;
                             climb.IsAdhered = true;
                             climb.AdhesionStrength = 1.0f; 
                             float3 safeLerpPos = math.lerp(lt.Position, hit.Position + (hit.SurfaceNormal * cfg.SurfaceOffset), DeltaTime * 5f);
                             SafeSetPosition(ref lt, safeLerpPos);
                        }
                        else
                        {
                             // ADHESION HYSTERESIS (EPIC 14.27)
                             if (climb.StickyFramesRemaining > 0)
                             {
                                 climb.StickyFramesRemaining--;
                                 climb.IsSticky = true;
                                 climb.IsAdhered = true;
                                 // Keep current normal/pos to prevent jitter
                             }
                             else
                             {
                                 climb.IsAdhered = false;
                                 climb.IsSticky = false;
                                 SafeSetPosition(ref lt, targetPos); 
                             }
                        }
                    }
                }

                if (surfaceFound)
                {
                    climb.StickyFramesRemaining = cfg.AdhesionHysteresisFrames;
                    climb.IsSticky = false;
                    
                    // Update Local Space Data (Moving Platform Support)
                    if (TransformLookup.HasComponent(climb.SurfaceEntity))
                    {
                        var surfaceLT = TransformLookup[climb.SurfaceEntity];
                        climb.GripLocalPosition = surfaceLT.InverseTransformPoint(hit.Position);
                        climb.GripLocalNormal = math.rotate(math.inverse(surfaceLT.Rotation), hit.SurfaceNormal);
                    }
                }

                
                // Update "Legacy" Fields for compatibility with other systems (IK, Animation)
                // Many systems read GripWorldPosition/Normal.
                climb.GripWorldPosition = climb.SurfaceContactPoint;
                climb.GripWorldNormal = climb.SurfaceNormal;

                // Debug
                // UnityEngine.Debug.DrawRay(lt.Position, moveDir, Color.green);
                // UnityEngine.Debug.DrawRay(lt.Position, climb.SurfaceNormal, Color.magenta);
            }
            
            // Safety Helpers
            private void SafeSetPosition(ref LocalTransform lt, float3 pos)
            {
                if (math.any(math.isnan(pos)) || math.any(math.isinf(pos))) return;
                lt.Position = pos;
            }
            
            private void SafeSetRotation(ref LocalTransform lt, quaternion rot)
            {
                if (math.any(math.isnan(rot.value)) || math.any(math.isinf(rot.value))) return;
                lt.Rotation = rot;
            }
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
