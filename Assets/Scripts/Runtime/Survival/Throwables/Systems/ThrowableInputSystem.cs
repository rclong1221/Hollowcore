using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using DIG.Shared;

namespace DIG.Survival.Throwables
{
    /// <summary>
    /// Handles throwable input - spawns thrown objects when player uses AltUse input.
    /// Runs in PredictedSimulationSystemGroup for client prediction.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ThrowableInputSystem : ISystem
    {
        private BufferLookup<ThrowableInventory> _inventoryLookup;

        // Eye height offset from player position
        private const float EyeHeightOffset = 1.7f;
        private const float DefaultThrowSpeed = 15f;
        private const float DefaultArcAngle = 15f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            _inventoryLookup = state.GetBufferLookup<ThrowableInventory>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _inventoryLookup.Update(ref state);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var viewConfigLookup = SystemAPI.GetComponentLookup<global::Player.Components.CameraViewConfig>(true);
            var socketDataLookup = SystemAPI.GetComponentLookup<SocketPositionData>(true);

            foreach (var (transform, input, selectedThrowable, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerInput>, RefRO<SelectedThrowable>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                // Check for throw input (AltUse)
                if (!input.ValueRO.AltUse.IsSet)
                    continue;

                // Get inventory
                if (!_inventoryLookup.HasBuffer(entity))
                    continue;

                var inventory = _inventoryLookup[entity];
                var throwableType = selectedThrowable.ValueRO.Type;

                // Find the selected throwable in inventory
                int inventoryIndex = -1;
                for (int i = 0; i < inventory.Length; i++)
                {
                    if (inventory[i].Type == throwableType && inventory[i].Quantity > 0)
                    {
                        inventoryIndex = i;
                        break;
                    }
                }

                // No throwables of selected type available
                if (inventoryIndex < 0)
                    continue;

                // Decrement inventory
                var item = inventory[inventoryIndex];
                item.Quantity--;
                inventory[inventoryIndex] = item;

                // Always calculate originOffset for camera position calculations
                float3 originOffset = new float3(0, EyeHeightOffset, 0); // Default fallback
                if (viewConfigLookup.HasComponent(entity))
                {
                    var viewConfig = viewConfigLookup[entity];
                    // Use the Combat Pivot Offset (which controls camera height)
                    // If adventure mode is active, use adventure pivot
                    originOffset = (viewConfig.ActiveViewType == global::Player.Components.CameraViewType.Combat)
                                   ? viewConfig.CombatPivotOffset
                                   : viewConfig.AdventurePivotOffset;
                }

                // Determine throw origin - prefer actual hand socket position if available
                float3 throwOrigin;
                bool useSocketPosition = socketDataLookup.HasComponent(entity) &&
                                         socketDataLookup[entity].IsValid;

                if (useSocketPosition)
                {
                    // Use actual hand position from animated skeleton
                    throwOrigin = socketDataLookup[entity].MainHandPosition;
                }
                else
                {
                    // Fallback: Use player position + height offset
                    throwOrigin = transform.ValueRO.Position + originOffset;
                }

                // 2. Calculate Camera Position
                // We need to approximation the camera position to know where the crosshair ray starts 
                // Pitch/Yaw rotation
                quaternion cameraRot = quaternion.Euler(math.radians(input.ValueRO.CameraPitch), math.radians(input.ValueRO.CameraYaw), 0);
                float3 lookDir = math.mul(cameraRot, math.forward());
                
                // Note: Camera distance lookup can be added via PlayerCameraSettings if needed for perfect precision.
                // For now we assume the crosshair is centered on screen.
                
                // BETTER APPROACH for Crosshair Alignment:
                // The crosshair is always "infinite forward from Camera".
                // 1. Define Focus Point far away in Look Direction from the Pivot + Offset (approx camera pos)
                // A simple approximation is sufficient: Assume Camera is at (Pivot + Offset) - (LookDir * Dist)
                
                // We actually don't need the exact camera position if we just assume a convergence distance.
                // Let's assume we want to hit what we are looking at 50m away.
                // Origin = ThrowOrigin (Pivot)
                // Target = ThrowOrigin + (LookDir * 50m) <-- This assumes camera is AT pivot.
                
                // BUT camera is offset (Shoulder cam).
                // CameraPos ~= ThrowOrigin + (Right * ShoulderOffset) - (LookDir * Distance)
                // We need to access CameraViewConfig to get these offsets specifically.
                
                float3 cameraOffset = float3.zero;
                if (viewConfigLookup.HasComponent(entity))
                {
                    var viewConfig = viewConfigLookup[entity];
                     if (viewConfig.ActiveViewType == global::Player.Components.CameraViewType.Combat)
                         cameraOffset = viewConfig.CombatCameraOffset;
                }
                
                // Transform local camera offset by rotation
                float3 worldCamOffset = math.mul(cameraRot, cameraOffset);
                
                // Approximate Camera Pos (assuming distance ~2m behind pivot)
                // This doesn't need to be perfect, just "good enough" to establishing the ray start
                float3 approxCamPos = (transform.ValueRO.Position + originOffset) + worldCamOffset - (lookDir * 2.5f);
                
                // 2. Define Target Point (What crosshair is on)
                float3 targetPoint = approxCamPos + (lookDir * 50.0f); // Converge at 50m
                
                // 3. Calculate Convergence Vector
                float3 convergenceDir = math.normalizesafe(targetPoint - throwOrigin);
                
                float3 throwDirection = convergenceDir;

                // Add upward arc relative to aim direction
                // However, if we aim with pitch, we might not need as much artificial arc.
                // Keeping it for now but applying it to the local UP of the aim vector
                float arcRad = math.radians(DefaultArcAngle);
                float3 throwVelocity = throwDirection * DefaultThrowSpeed;
                
                // Add consistent "up" arc regardless of look direction
                throwVelocity.y += math.sin(arcRad) * DefaultThrowSpeed;

                // Create spawn request
                // Add safety offset to ensure projectile spawns outside player collision
                // Smaller offset when using socket position (hand is already outside body)
                float safetyOffset = useSocketPosition ? 0.3f : 0.8f;
                float3 finalSpawnPos = throwOrigin + throwDirection * safetyOffset;
                
                ecb.AddComponent(entity, new ThrowRequest
                {
                    Type = throwableType,
                    SpawnPosition = finalSpawnPos,
                    Velocity = throwVelocity,
                    ThrowerEntity = entity
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static float3 GetThrowDirection(in PlayerInput input, in quaternion playerRotation)
        {
            // Use camera yaw/pitch from input if valid
            if (input.CameraYawValid != 0)
            {
                float yawRad = math.radians(input.CameraYaw);
                float pitchRad = math.radians(input.CameraPitch); // Use actual pitch

                // Construct direction from Pitch and Yaw (Spherical coordinates)
                // Pitch: rotation around X axis (-90 to 90)
                // Yaw: rotation around Y axis
                
                // Standard FPS camera direction conversion
                float cx = math.cos(pitchRad);
                float cy = math.cos(yawRad);
                float sx = math.sin(pitchRad);
                float sy = math.sin(yawRad);
                
                // Z-forward convention
                return math.normalizesafe(new float3(sy * cx, -sx, cy * cx));
            }

            return math.forward(playerRotation);
        }
    }

    /// <summary>
    /// Request to spawn a thrown object. Processed by ThrowableSpawnSystem.
    /// </summary>
    public struct ThrowRequest : IComponentData
    {
        public ThrowableType Type;
        public float3 SpawnPosition;
        public float3 Velocity;
        public Entity ThrowerEntity;
    }

    /// <summary>
    /// Spawns thrown objects from ThrowRequest components.
    /// Server-authoritative spawning with prefab instantiation.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ThrowableInputSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ThrowableSpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<ThrowablePrefabs>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var prefabs = SystemAPI.GetSingleton<ThrowablePrefabs>();

            foreach (var (request, entity) in
                     SystemAPI.Query<RefRO<ThrowRequest>>()
                     .WithEntityAccess())
            {
                // Get prefab for this throwable type
                Entity prefab = GetPrefabForType(prefabs, request.ValueRO.Type);
                if (prefab == Entity.Null)
                {
                    ecb.RemoveComponent<ThrowRequest>(entity);
                    continue;
                }

                // Spawn the thrown object
                var thrownEntity = ecb.Instantiate(prefab);

                // Set position
                ecb.SetComponent(thrownEntity, LocalTransform.FromPosition(request.ValueRO.SpawnPosition));

                // Set velocity via PhysicsVelocity if present, otherwise store for manual simulation
                ecb.AddComponent(thrownEntity, new ThrownObjectVelocity
                {
                    Linear = request.ValueRO.Velocity
                });

                // Set thrown object data
                ecb.SetComponent(thrownEntity, new ThrownObject
                {
                    Type = request.ValueRO.Type,
                    RemainingLifetime = GetLifetimeForType(request.ValueRO.Type),
                    InitialLifetime = GetLifetimeForType(request.ValueRO.Type),
                    Intensity = 1f,
                    ThrowerEntity = request.ValueRO.ThrowerEntity,
                    HasLanded = false
                });

                // Remove the request
                ecb.RemoveComponent<ThrowRequest>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static Entity GetPrefabForType(in ThrowablePrefabs prefabs, ThrowableType type)
        {
            return type switch
            {
                ThrowableType.Flare => prefabs.FlarePrefab,
                ThrowableType.Glowstick => prefabs.GlowstickPrefab,
                ThrowableType.SoundLure => prefabs.SoundLurePrefab,
                ThrowableType.Decoy => prefabs.DecoyPrefab,
                _ => Entity.Null
            };
        }

        private static float GetLifetimeForType(ThrowableType type)
        {
            return type switch
            {
                ThrowableType.Flare => 30f,
                ThrowableType.Glowstick => 120f, // 2 minutes
                ThrowableType.SoundLure => 20f,
                ThrowableType.Decoy => 45f,
                _ => 30f
            };
        }
    }

    /// <summary>
    /// Singleton containing prefab references for throwable types.
    /// </summary>
    public struct ThrowablePrefabs : IComponentData
    {
        public Entity FlarePrefab;
        public Entity GlowstickPrefab;
        public Entity SoundLurePrefab;
        public Entity DecoyPrefab;
    }

    /// <summary>
    /// Velocity for thrown objects that don't use Unity Physics.
    /// Allows manual arc simulation.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct ThrownObjectVelocity : IComponentData
    {
        [GhostField(Quantization = 100)]
        public float3 Linear;
    }
}
