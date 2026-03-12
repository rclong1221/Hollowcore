using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// COMPREHENSIVE CLIMB DEBUGGER
    /// Uses [CLIMB_TRACE] tag for filtering.
    /// Tracks: Mount Logic, State Transitions, Adhesion.
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(FreeClimbMovementSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial class FreeClimbDebugSystem : SystemBase
    {
        private bool _wasClimbing = false;
        private int _frameCounter = 0;
        private bool debugLogging = false;

        protected override void OnCreate()
        {
             RequireForUpdate<PhysicsWorldSingleton>();
        }

        protected override void OnUpdate()
        {
            _frameCounter++;
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            
            foreach (var (climbState, settings, input, transform, entity) in 
                SystemAPI.Query<RefRO<FreeClimbState>, RefRO<FreeClimbSettings>, RefRO<PlayerInput>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
            {
                var climb = climbState.ValueRO;
                var cfg = settings.ValueRO;
                var inp = input.ValueRO;
                var lt = transform.ValueRO;

                // 1. STATE CHANGE LOGGING
                if (climb.IsClimbing != _wasClimbing)
                {
                    if (debugLogging)
                    {
                        if (climb.IsClimbing)
                            Debug.Log($"[CLIMB_TRACE] STATE: MOUNTED (Entity: {climb.SurfaceEntity.Index} | Normal: {climb.SurfaceNormal})");
                        else
                            Debug.Log($"[CLIMB_TRACE] STATE: DISMOUNTED (Reason: Unknown/Manual | Time: {SystemAPI.Time.ElapsedTime:F2})");
                    }
                    
                    _wasClimbing = climb.IsClimbing;
                }

                // 2. MOUNT ATTEMPT DIAGNOSTICS (Only when Jump is pressed and NOT climbing)
                if (inp.Jump.IsSet && !climb.IsClimbing)
                {
                     LogMountAttempt(entity, lt, cfg, physicsWorld);
                }

                // 3. PERIODIC MOVEMENT LOG (While Climbing)
                if (debugLogging && climb.IsClimbing && _frameCounter % 60 == 0)
                {
                    Debug.Log($"[CLIMB_TRACE] STATUS: Adhered={climb.IsAdhered} | Strength={climb.AdhesionStrength} | Grip={climb.GripWorldPosition} | Input={inp.Vertical:F1}");
                }
            }
        }

        private void LogMountAttempt(Entity playerEntity, LocalTransform lt, FreeClimbSettings cfg, PhysicsWorldSingleton physics)
        {
            float3 origin = lt.Position + new float3(0, 0.9f, 0); // Approx center
            float3 fwd = math.forward(lt.Rotation);
            float dist = cfg.DetectionDistance;
            
            var filter = new CollisionFilter
            {
                BelongsTo = ~0u,
                CollidesWith = cfg.ClimbableLayers,
                GroupIndex = 0
            };

            if (debugLogging) Debug.Log($"[CLIMB_TRACE] MOUNT INPUT DETECTED: Casting Ray from {origin} Fwd: {fwd} Dist: {dist}");

            if (math.any(math.isnan(origin)) || math.any(math.isinf(origin)) || 
                math.any(math.isnan(fwd)) || math.any(math.isinf(fwd)) || 
                float.IsNaN(dist) || float.IsInfinity(dist) || dist > 1000f)
            {
                return;
            }

            var collector = new DebugIgnoreEntityCollector(playerEntity);
            var input = new RaycastInput { Start = origin, End = origin + fwd * dist, Filter = filter };
            physics.PhysicsWorld.CastRay(input, ref collector);

            if (collector.HasHit)
            {
                var hit = collector.ClosestHit;
                float facingDot = math.dot(fwd, -hit.SurfaceNormal);
                float angle = math.degrees(math.acos(math.clamp(math.dot(hit.SurfaceNormal, new float3(0,1,0)), -1f, 1f)));
                bool notSelf = hit.Entity != playerEntity; // redundancies
                
                string result = "FAIL";
                if (facingDot >= 0.2f && angle >= 20f && angle <= 160f) result = "SUCCESS";

                if (debugLogging) Debug.Log($"[CLIMB_TRACE] RAY HIT: {hit.Entity.Index} | Result: {result} | FacingDot: {facingDot:F2} (Req > 0.2) | Angle: {angle:F1} (Req 20-160) | NotSelf: {notSelf}");
            }
            else
            {
                if (debugLogging) Debug.Log($"[CLIMB_TRACE] RAY MISS: No barriers detected after filtering self.");
            }
        }

        public struct DebugIgnoreEntityCollector : ICollector<Unity.Physics.RaycastHit>
        {
            public Entity IgnoreEntity;
            public Unity.Physics.RaycastHit ClosestHit;
            public bool HasHit;
            public float MaxFraction { get; private set; }
            public int NumHits => HasHit ? 1 : 0;
            public bool EarlyOutOnFirstHit => false; // Required by Interface

            public DebugIgnoreEntityCollector(Entity ignore)
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
