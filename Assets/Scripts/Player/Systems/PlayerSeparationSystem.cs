using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.NetCode;
using DIG.Player.Components;
using Player.Components;
using Player.Systems;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Physical Separation System (Epic 7.3.4)
    /// 
    /// Handles symmetric physical separation between overlapping players.
    /// This system runs BEFORE the collision response system to ensure:
    /// 1. Players are separated before stagger/knockdown logic runs
    /// 2. Separation is purely physical (no gameplay state changes)
    /// 3. Both players receive equal and opposite separation forces
    /// 
    /// Key Design:
    /// - Applies velocity-based separation impulse (not just position correction)
    /// - Uses SeparationStrength for impulse magnitude
    /// - Clamps to MaxSeparationSpeed to prevent physics explosions
    /// - Does NOT trigger stagger or state changes (7.3.5/7.3.8 responsibility)
    /// 
    /// Runs in PredictedFixedStepSimulationSystemGroup for NetCode prediction/rollback.
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(PlayerProximityCollisionSystem))]
    [UpdateBefore(typeof(global::Player.Systems.CharacterControllerSystem))]
    [BurstCompile]
    public partial struct PlayerSeparationSystem : ISystem
    {
        private ComponentLookup<PlayerCollisionState> _collisionStateLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<PhysicsVelocity> _velocityLookup;
        private ComponentLookup<CharacterControllerSettings> _ccSettingsLookup;
        private ComponentLookup<Simulate> _simulateLookup;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerCollisionSettings>();
            
            _collisionStateLookup = state.GetComponentLookup<PlayerCollisionState>(isReadOnly: true);
            _transformLookup = state.GetComponentLookup<LocalTransform>();
            _velocityLookup = state.GetComponentLookup<PhysicsVelocity>();
            _ccSettingsLookup = state.GetComponentLookup<CharacterControllerSettings>(isReadOnly: true);
            _simulateLookup = state.GetComponentLookup<Simulate>(isReadOnly: true);
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<PlayerCollisionSettings>(out var settings))
                return;
            
            if (!settings.EnableCollisionResponse)
                return;

            float deltaTime = SystemAPI.Time.DeltaTime;

            // Update lookups
            _collisionStateLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _velocityLookup.Update(ref state);
            _ccSettingsLookup.Update(ref state);
            _simulateLookup.Update(ref state);

            // NOTE: We intentionally do NOT use Unity Physics collision events here.
            // With CharacterController-based movement, Physics collision events can be unreliable.
            // Instead, we do proximity overlap checks (same reasoning as PlayerProximityCollisionSystem).

            var query = SystemAPI.QueryBuilder()
                .WithAll<PlayerTag>()
                .WithAll<LocalTransform, PhysicsVelocity, PlayerCollisionState, CharacterControllerSettings>()
                .Build();

            using var entities = query.ToEntityArray(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var velocities = query.ToComponentDataArray<PhysicsVelocity>(Allocator.Temp);
            using var collisionStates = query.ToComponentDataArray<PlayerCollisionState>(Allocator.Temp);
            using var ccSettings = query.ToComponentDataArray<CharacterControllerSettings>(Allocator.Temp);

            int count = entities.Length;
            if (count < 2)
                return;

            var deltaVelocities = new NativeArray<float3>(count, Allocator.Temp, NativeArrayOptions.ClearMemory);
            var deltaPositions = new NativeArray<float3>(count, Allocator.Temp, NativeArrayOptions.ClearMemory);

            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    var entityA = entities[i];
                    var entityB = entities[j];

                    float3 posA = transforms[i].Position;
                    float3 posB = transforms[j].Position;

                    float3 toB = posB - posA;
                    toB.y = 0;
                    float horizontalDist = math.length(toB);

                    float combinedRadius = ccSettings[i].Radius + ccSettings[j].Radius;
                    float overlap = combinedRadius - horizontalDist;
                    if (overlap <= 0.001f)
                        continue;

                    float3 direction = horizontalDist > 0.01f ? math.normalize(toB) : new float3(1, 0, 0);

                    float separationSpeed = math.min(overlap * settings.SeparationStrength, settings.MaxSeparationSpeed);
                    float3 separationVelocity = direction * separationSpeed;
                    separationVelocity.y = 0;

                    float3 positionCorrection = float3.zero;
                    if (overlap > 0.1f)
                    {
                        float correctionMag = math.min(overlap * 0.5f, settings.MaxSeparationSpeed * deltaTime);
                        positionCorrection = direction * correctionMag;
                        positionCorrection.y = 0;
                    }

                    bool simulateA = _simulateLookup.HasComponent(entityA);
                    bool simulateB = _simulateLookup.HasComponent(entityB);

                    bool knockedDownA = collisionStates[i].IsKnockedDown;
                    bool knockedDownB = collisionStates[j].IsKnockedDown;

                    // Symmetric impulses when possible. If one side is non-simulated (remote ghost on client)
                    // or knocked down, only apply to the side we can/should move.
                    if (simulateA && !knockedDownA)
                    {
                        deltaVelocities[i] -= separationVelocity;
                        deltaPositions[i] -= positionCorrection;
                    }

                    if (simulateB && !knockedDownB)
                    {
                        deltaVelocities[j] += separationVelocity;
                        deltaPositions[j] += positionCorrection;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                var entity = entities[i];
                if (!_simulateLookup.HasComponent(entity))
                    continue;

                if (collisionStates[i].IsKnockedDown)
                    continue;

                if (!deltaVelocities[i].Equals(float3.zero))
                {
                    var v = velocities[i];
                    v.Linear += deltaVelocities[i];
                    state.EntityManager.SetComponentData(entity, v);
                }

                if (!deltaPositions[i].Equals(float3.zero))
                {
                    var t = transforms[i];
                    t.Position += deltaPositions[i];
                    state.EntityManager.SetComponentData(entity, t);
                }
            }

            deltaVelocities.Dispose();
            deltaPositions.Dispose();
        }
    }
}
