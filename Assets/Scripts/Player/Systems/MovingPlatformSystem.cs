using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Handles moving platform attachment, position updates, and momentum inheritance.
    /// <para>
    /// <b>Architecture:</b>
    /// - Runs after CharacterControllerSystem (needs ground detection results)
    /// - Burst-compiled with proper job dependency chaining
    /// - Platform velocity calculated from position deltas
    /// - Player position updated relative to platform each frame
    /// - Uses enableable component pattern for OnMovingPlatform
    /// </para>
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(CharacterControllerSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct MovingPlatformSystem : ISystem
    {
        private ComponentLookup<MovingPlatform> _platformLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkStreamInGame>();
            _platformLookup = state.GetComponentLookup<MovingPlatform>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            
            _platformLookup.Update(ref state);
            _transformLookup.Update(ref state);
            
            // Step 1: Update platform velocities
            state.Dependency = new UpdatePlatformVelocityJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel(state.Dependency);
            
            // Step 2: Check for platform attachment (when landing on MovingPlatform)
            // Note: Must get PhysicsWorld for raycasting
            if (SystemAPI.HasSingleton<PhysicsWorldSingleton>())
            {
                var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
                state.Dependency = new CheckPlatformAttachmentJob
                {
                    PlatformLookup = _platformLookup,
                    TransformLookup = _transformLookup,
                    PhysicsWorld = physicsWorld
                }.Schedule(state.Dependency);
            }
            
            // Step 3: Update player position on platform
            state.Dependency = new UpdatePlayerOnPlatformJob
            {
                PlatformLookup = _platformLookup,
                TransformLookup = _transformLookup,
                DeltaTime = deltaTime
            }.ScheduleParallel(state.Dependency);
            
            // Step 4: Handle platform disconnection
            state.Dependency = new HandlePlatformDisconnectJob
            {
                PlatformLookup = _platformLookup
            }.ScheduleParallel(state.Dependency);
            
            // Step 5: Decay platform momentum
            state.Dependency = new DecayPlatformMomentumJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel(state.Dependency);
        }
        
        /// <summary>
        /// Updates MovingPlatform velocity from position delta.
        /// </summary>
        [BurstCompile]
        private partial struct UpdatePlatformVelocityJob : IJobEntity
        {
            public float DeltaTime;
            
            public void Execute(ref MovingPlatform platform, in LocalTransform transform)
            {
                if (DeltaTime <= 0) return;
                
                // Calculate linear velocity
                float3 positionDelta = transform.Position - platform.LastPosition;
                platform.Velocity = positionDelta / DeltaTime;
                
                // Calculate angular velocity (simplified - just track yaw for now)
                quaternion rotationDelta = math.mul(transform.Rotation, math.inverse(platform.LastRotation));
                float3 eulerDelta = math.Euler(rotationDelta);
                platform.AngularVelocity = eulerDelta / DeltaTime;
                
                // Store current state for next frame
                platform.LastPosition = transform.Position;
                platform.LastRotation = transform.Rotation;
            }
        }
        
        /// <summary>
        /// Checks if player should attach to a moving platform.
        /// Uses raycast to detect ground platform entity.
        /// NOTE: Does NOT write to LocalTransform - only reads player position for raycast.
        /// </summary>
        [BurstCompile]
        [WithAll(typeof(Simulate), typeof(PlayerTag))]
        [WithDisabled(typeof(OnMovingPlatform))] // Only process when not already on platform
        private partial struct CheckPlatformAttachmentJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<MovingPlatform> PlatformLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public PhysicsWorld PhysicsWorld;
            
            public void Execute(
                Entity entity,
                in LocalTransform transform, // READ ONLY - no aliasing with TransformLookup
                ref OnMovingPlatform onPlatform,
                EnabledRefRW<OnMovingPlatform> onPlatformEnabled,
                in PlayerState playerState,
                in CharacterControllerSettings ccSettings)
            {
                // Only check when grounded
                if (!playerState.IsGrounded) return;
                
                // Raycast down to find ground entity
                float rayLength = ccSettings.Height * 0.5f + 0.1f;
                RaycastInput rayInput = new RaycastInput
                {
                    Start = transform.Position,
                    End = transform.Position - new float3(0, rayLength, 0),
                    Filter = new CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = ~0u,
                        GroupIndex = 0
                    }
                };
                
                if (!PhysicsWorld.CastRay(rayInput, out Unity.Physics.RaycastHit hit))
                    return;
                
                Entity groundEntity = hit.Entity;
                if (groundEntity == Entity.Null) return;
                
                // Check if ground is a moving platform
                if (!PlatformLookup.HasComponent(groundEntity)) return;
                
                // Get platform transform (using lookup for OTHER entity, not self)
                if (!TransformLookup.HasComponent(groundEntity)) return;
                LocalTransform platformTransform = TransformLookup[groundEntity];
                
                // Calculate local position relative to platform
                float3 localPos = math.mul(
                    math.inverse(platformTransform.Rotation),
                    transform.Position - platformTransform.Position
                );
                
                // Calculate local rotation
                quaternion localRot = math.mul(
                    math.inverse(platformTransform.Rotation),
                    transform.Rotation
                );
                
                // Set OnMovingPlatform data and enable it
                onPlatform.PlatformEntity = groundEntity;
                onPlatform.LocalPosition = localPos;
                onPlatform.LocalRotation = localRot;
                onPlatform.TimeOnPlatform = 0;
                onPlatformEnabled.ValueRW = true;
            }
        }
        
        /// <summary>
        /// Updates player position based on platform movement.
        /// </summary>
        [BurstCompile]
        [WithAll(typeof(Simulate), typeof(PlayerTag), typeof(OnMovingPlatform))] // Only when enabled
        private partial struct UpdatePlayerOnPlatformJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<MovingPlatform> PlatformLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            public float DeltaTime;
            
            public void Execute(
                ref LocalTransform transform,
                ref OnMovingPlatform onPlatform,
                in MovingPlatformSettings settings,
                in PlayerState playerState)
            {
                // Validate platform still exists
                if (!PlatformLookup.HasComponent(onPlatform.PlatformEntity)) return;
                if (!TransformLookup.HasComponent(onPlatform.PlatformEntity)) return;
                
                LocalTransform platformTransform = TransformLookup[onPlatform.PlatformEntity];
                MovingPlatform platform = PlatformLookup[onPlatform.PlatformEntity];
                
                // Calculate world position from local position on platform
                float3 worldPos = platformTransform.Position + 
                    math.mul(platformTransform.Rotation, onPlatform.LocalPosition);
                
                // Apply platform movement to player
                transform.Position = worldPos;
                
                // Optionally rotate with platform
                if (settings.RotateWithPlatform == 1)
                {
                    transform.Rotation = math.mul(platformTransform.Rotation, onPlatform.LocalRotation);
                }
                
                // Update time on platform
                onPlatform.TimeOnPlatform += DeltaTime;
            }
        }
        
        /// <summary>
        /// Handles disconnect from platform (jumping off, falling off).
        /// </summary>
        [BurstCompile]
        [WithAll(typeof(Simulate), typeof(PlayerTag), typeof(OnMovingPlatform))] // Only when enabled
        private partial struct HandlePlatformDisconnectJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<MovingPlatform> PlatformLookup;
            
            public void Execute(
                Entity entity,
                ref PhysicsVelocity velocity,
                ref PlatformMomentum momentum,
                ref OnMovingPlatform onPlatform,
                EnabledRefRW<OnMovingPlatform> onPlatformEnabled,
                in PlayerState playerState,
                in MovingPlatformSettings settings)
            {
                // Check if we should disconnect (not grounded or ground changed)
                bool shouldDisconnect = !playerState.IsGrounded;
                
                // Also disconnect if platform no longer exists
                if (!PlatformLookup.HasComponent(onPlatform.PlatformEntity))
                {
                    shouldDisconnect = true;
                }
                
                if (shouldDisconnect)
                {
                    // Get platform velocity for momentum inheritance
                    if (PlatformLookup.HasComponent(onPlatform.PlatformEntity))
                    {
                        MovingPlatform platform = PlatformLookup[onPlatform.PlatformEntity];
                        
                        // Only inherit if velocity is significant
                        if (math.length(platform.Velocity) > settings.MinVelocityForMomentum)
                        {
                            // Add platform velocity to player velocity
                            velocity.Linear += platform.Velocity;
                            
                            // Also store as momentum for gradual decay
                            momentum.InheritedVelocity = platform.Velocity;
                            momentum.DecayTimer = 0;
                            momentum.DecayDuration = settings.MomentumDecayDuration;
                        }
                    }
                    
                    // Disable OnMovingPlatform component
                    onPlatformEnabled.ValueRW = false;
                }
            }
        }
        
        /// <summary>
        /// Decays inherited platform momentum over time.
        /// </summary>
        [BurstCompile]
        [WithAll(typeof(Simulate), typeof(PlayerTag))]
        private partial struct DecayPlatformMomentumJob : IJobEntity
        {
            public float DeltaTime;
            
            public void Execute(ref PlatformMomentum momentum, ref PhysicsVelocity velocity)
            {
                if (momentum.IsActive == 0) return;
                
                // Update decay timer
                momentum.DecayTimer += DeltaTime;
                
                // Clear momentum when fully decayed
                if (momentum.DecayTimer >= momentum.DecayDuration)
                {
                    momentum.InheritedVelocity = float3.zero;
                }
            }
        }
    }
}
