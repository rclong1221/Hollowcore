using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.NetCode;
using Player.Components;
using DIG.Player.Components;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

namespace Player.Systems
{
    /// <summary>
    /// EPIC 15.3: Procedural Ledge Mantling
    /// Detects waist-high obstacles and automatically vaults/mantles over them.
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(FreeClimbDetectionSystem))]
    [UpdateBefore(typeof(PlayerMovementSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    public partial struct FreeClimbMantleSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

            [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var currentTime = SystemAPI.Time.ElapsedTime;
            var isServer = state.WorldUnmanaged.IsServer();

            new FreeClimbMantleJob
            {
                PhysicsWorld = physicsWorld.PhysicsWorld,
                CurrentTime = currentTime,
                IsServer = isServer,
                // Removed TransformLookup to prevent aliasing with ref LocalTransform in Execute
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct FreeClimbMantleJob : IJobEntity
        {
            [ReadOnly] public PhysicsWorld PhysicsWorld;
            [ReadOnly] public double CurrentTime;
            [ReadOnly] public bool IsServer;
            // Lookup removed

            private void Execute(
                Entity entity,
                ref FreeClimbState climb,
                ref LocalTransform lt,
                ref PhysicsVelocity vel,
                ref PhysicsGravityFactor gravity,
                RefRO<FreeClimbSettings> settings,
                RefRO<PlayerInput> input,
                RefRO<CharacterControllerSettings> charSettings)
            {
                // Skip if already climbing
                if (climb.IsClimbing) return;

                // SKIP if not moving forward
                if (input.ValueRO.Vertical <= 0.1f) return;

                // SKIP if already moving upward quickly (let the jump complete naturally)
                if (vel.Linear.y > 2.0f) return;

                var cfg = settings.ValueRO;
                float3 fwd = math.forward(lt.Rotation);
                float3 up = math.up(); // or math.mul(lt.Rotation, math.up()) if full gravity support?
                // For now assuming Y is up for mantle logic mostly

                // Raycast Settings
                float checkDist = 0.8f; // ~Arm's length
                var filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = cfg.ObstacleLayers | cfg.LedgeTopLayers, // Use generic obstacle layers
                    GroupIndex = 0
                };

                // 1. WAIST CHECK (Must Hit)
                // Height ~ 0.8m - 1.0m
                float3 waistOrigin = lt.Position + (up * 0.8f);
                var waistInput = new RaycastInput
                {
                    Start = waistOrigin,
                    End = waistOrigin + (fwd * checkDist),
                    Filter = filter
                };

                IgnoreEntityCollector collector = new IgnoreEntityCollector(entity);
                PhysicsWorld.CastRay(waistInput, ref collector);

                if (!collector.HasHit) return;
                
                var waistHit = collector.ClosestHit;
                
                // Validate Wall Angle (Must be roughly vertical)
                if (math.dot(waistHit.SurfaceNormal, up) > 0.5f) return; // Too flat (slope/floor)

                // 2. HEAD CHECK (Must NOT Hit)
                // Height ~ 1.8m
                float3 headOrigin = lt.Position + (up * 1.8f);
                var headInput = new RaycastInput
                {
                    Start = headOrigin,
                    End = headOrigin + (fwd * checkDist),
                    Filter = filter
                };
                
                collector = new IgnoreEntityCollector(entity);
                PhysicsWorld.CastRay(headInput, ref collector);

                if (collector.HasHit) return; // Wall is too tall to mantle (Climb instead)

                // 3. LEDGE TOP CHECK (Must Hit)
                // Cast DOWN from above the wall
                // Origin: Wall Hit Point + Inward + Up
                // We use Waist Hit Distance + epsilon to penetrate wall, then cast down
                
                float wallDepth = 0.3f; // Look into the wall
                float3 mantleOrigin = lt.Position + (fwd * (waistHit.Fraction * checkDist + wallDepth)) + (up * 2.0f);
                float3 mantleDir = -up;
                
                var topFilter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = cfg.LedgeTopLayers, // Specifically walkable surfaces
                    GroupIndex = 0
                };

                var topInput = new RaycastInput
                {
                    Start = mantleOrigin,
                    End = mantleOrigin + (mantleDir * 1.5f), // Check down 1.5m (to 0.5m height)
                    Filter = topFilter
                };

                collector = new IgnoreEntityCollector(entity);
                PhysicsWorld.CastRay(topInput, ref collector);

                if (!collector.HasHit) return; // No top surface found

                var topHit = collector.ClosestHit;

                // Validate Top Angle (Must be flat-ish)
                if (math.dot(topHit.SurfaceNormal, up) < cfg.MinSurfaceAngle) return; // Too steep/slope

                // Validate Dimensions
                // Ensure the surface is at a reasonable height (Waist to Chest)
                float surfaceHeight = topHit.Position.y - lt.Position.y;
                if (surfaceHeight < 0.5f || surfaceHeight > 1.6f) return;

                // START MANTLE
                StartMantle(ref climb, ref lt, ref vel, ref gravity, topHit.Position, topHit.SurfaceNormal, cfg, CurrentTime);
            }

            private void StartMantle(
                ref FreeClimbState climb,
                ref LocalTransform lt,
                ref PhysicsVelocity vel,
                ref PhysicsGravityFactor gravity,
                float3 targetPos,
                float3 targetNormal,
                FreeClimbSettings cfg,
                double time)
            {
                // Set State
                climb.IsClimbing = true; // Use Climbing state to hijack movement
                climb.IsMantling = true;
                climb.IsTransitioning = true;
                climb.TransitionStartTime = time;
                climb.TransitionProgress = 0f;

                // Disable Physics
                gravity.Value = 0f;
                vel.Linear = float3.zero;
                vel.Angular = float3.zero;

                // Setup Transition
                climb.TransitionStartPos = lt.Position;
                climb.TransitionStartRot = lt.Rotation;

                // Target Position: Top Hit Position
                // We might want to stand slightly *on* the ledge, not embedded in it.
                // Standard character ref is bottom-center? 
                // DetectionSystem logic suggests adding 0? No, usually hits are on surface.
                // So target is HitPos.
                
                climb.TransitionTargetPos = targetPos;
                
                // Target Rotation: Face the wall/ledge normal? Or keep inputs?
                // Keep facing forward (mantle direction) or align to wall normal? 
                // Aligning to wall normal (inverted) is usually safer for animation.
                
                climb.TransitionTargetRot = lt.Rotation; 
                // Optional: Snap to wall normal?
                // float3 lookDir = -targetNormal; // Normal of top surface is Up. That's useless for rotation.
                // We need the WALL normal (from waist hit). But we didn't pass it.
                // Keeping current rotation is fine for now as we are moving forward.
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
