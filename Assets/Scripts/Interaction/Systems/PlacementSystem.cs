using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 16.1 Phase 6: Manages spatial placement of objects in the world.
    ///
    /// Each frame when the player is in placement mode:
    /// 1. Raycast from eye position using camera pitch/yaw
    /// 2. Grid snap the hit position (if GridSnap > 0)
    /// 3. Validate the position (surface angle, overlap, etc.)
    /// 4. Write preview position/rotation/validity to PlacementState
    /// 5. On confirm (Use input): server spawns real entity via ECB
    /// 6. On cancel (AltUse input): exit placement mode
    ///
    /// Follows the ExplosivePlacementSystem pattern for eye-level raycast
    /// and server-authoritative spawning.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(InteractAbilitySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PlacementSystem : ISystem
    {
        private const float EyeHeight = 1.6f;

        // Collision filter: hits Default (0), Environment (2), Ship (7) — excludes Players, Creatures, Projectiles
        private static readonly CollisionFilter PlacementRaycastFilter = new CollisionFilter
        {
            BelongsTo = ~0u,
            CollidesWith = (1u << 0) | (1u << 2) | (1u << 7),
            GroupIndex = 0
        };

        private ComponentLookup<PlaceableConfig> _placeableConfigLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlacementState>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            _placeableConfigLookup = state.GetComponentLookup<PlaceableConfig>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _placeableConfigLookup.Update(ref state);

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            bool isServer = state.WorldUnmanaged.IsServer();

            foreach (var (placementState, input, transform, entity) in
                     SystemAPI.Query<RefRW<PlacementState>, RefRO<PlayerInput>, RefRO<LocalTransform>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                // Clear single-frame flags
                placementState.ValueRW.ConfirmPlacement = false;
                placementState.ValueRW.CancelPlacement = false;

                if (!placementState.ValueRO.IsPlacing)
                    continue;

                // Read config from the placeable source entity
                Entity sourceEntity = placementState.ValueRO.PlaceableSource;
                if (sourceEntity == Entity.Null || !_placeableConfigLookup.HasComponent(sourceEntity))
                {
                    // Source entity lost — exit placement mode
                    placementState.ValueRW.IsPlacing = false;
                    continue;
                }

                var config = _placeableConfigLookup[sourceEntity];

                // --- Raycast from eye ---
                float3 eyePos = transform.ValueRO.Position + new float3(0, EyeHeight, 0);

                var pInput = input.ValueRO;
                quaternion lookRot = quaternion.Euler(
                    math.radians(pInput.CameraPitch),
                    math.radians(pInput.CameraYaw), 0);
                float3 rayDir = math.rotate(lookRot, new float3(0, 0, 1));

                var rayInput = new RaycastInput
                {
                    Start = eyePos,
                    End = eyePos + rayDir * config.MaxPlacementRange,
                    Filter = PlacementRaycastFilter
                };

                bool hasHit = physicsWorld.CastRay(rayInput, out var hit);

                if (hasHit)
                {
                    float3 hitPos = hit.Position;
                    float3 hitNormal = hit.SurfaceNormal;

                    // --- Grid snap ---
                    if (config.GridSnap > 0)
                    {
                        hitPos = math.floor(hitPos / config.GridSnap + 0.5f) * config.GridSnap;
                    }

                    // --- Calculate rotation from surface normal ---
                    // Default: place upright on surface. Use surface normal as up.
                    quaternion previewRot = quaternion.identity;
                    float3 surfaceUp = math.normalizesafe(hitNormal, math.up());
                    float3 surfaceForward = math.normalizesafe(
                        math.cross(math.right(), surfaceUp), math.forward());
                    if (math.lengthsq(surfaceForward) > 0.001f)
                    {
                        previewRot = quaternion.LookRotationSafe(surfaceForward, surfaceUp);
                    }

                    // --- Validate ---
                    bool isValid = true;

                    switch (config.Validation)
                    {
                        case PlacementValidation.FlatSurface:
                        case PlacementValidation.Foundation:
                        {
                            // Surface angle check
                            float angleRad = math.acos(math.clamp(
                                math.dot(hitNormal, math.up()), -1f, 1f));
                            float maxAngleRad = math.radians(config.MaxSurfaceAngle);
                            if (angleRad > maxAngleRad)
                                isValid = false;
                            break;
                        }

                        case PlacementValidation.NoOverlap:
                        {
                            // Surface angle check first
                            float angleRad = math.acos(math.clamp(
                                math.dot(hitNormal, math.up()), -1f, 1f));
                            float maxAngleRad = math.radians(config.MaxSurfaceAngle);
                            if (angleRad > maxAngleRad)
                            {
                                isValid = false;
                                break;
                            }

                            // Physics overlap check using PointDistanceInput
                            float checkRadius = config.OverlapCheckRadius > 0
                                ? config.OverlapCheckRadius : 0.5f;

                            var distanceInput = new PointDistanceInput
                            {
                                Position = hitPos,
                                MaxDistance = checkRadius,
                                Filter = PlacementRaycastFilter
                            };

                            var distanceHits = new NativeList<DistanceHit>(Allocator.Temp);
                            if (physicsWorld.CalculateDistance(distanceInput, ref distanceHits))
                            {
                                // If any geometry overlaps (other than the surface we hit), invalid
                                // Allow the surface body itself
                                int overlapCount = 0;
                                for (int i = 0; i < distanceHits.Length; i++)
                                {
                                    if (distanceHits[i].RigidBodyIndex != hit.RigidBodyIndex)
                                        overlapCount++;
                                }
                                if (overlapCount > 0)
                                    isValid = false;
                            }
                            distanceHits.Dispose();
                            break;
                        }

                        case PlacementValidation.None:
                        case PlacementValidation.Custom:
                        default:
                            // No validation or delegated to game-specific system
                            break;
                    }

                    // Write state
                    placementState.ValueRW.PreviewPosition = hitPos;
                    placementState.ValueRW.PreviewRotation = previewRot;
                    placementState.ValueRW.IsValid = isValid;
                    placementState.ValueRW.SurfaceNormal = hitNormal;
                }
                else
                {
                    // No surface hit — show preview at max range, invalid
                    placementState.ValueRW.PreviewPosition = eyePos + rayDir * config.MaxPlacementRange;
                    placementState.ValueRW.PreviewRotation = quaternion.identity;
                    placementState.ValueRW.IsValid = false;
                    placementState.ValueRW.SurfaceNormal = math.up();
                }

                // --- Handle confirm ---
                if (pInput.Use.IsSet && placementState.ValueRO.IsValid)
                {
                    placementState.ValueRW.ConfirmPlacement = true;

                    // Server spawns the real entity
                    if (isServer && config.PlaceablePrefab != Entity.Null)
                    {
                        var spawned = ecb.Instantiate(config.PlaceablePrefab);
                        ecb.SetComponent(spawned, LocalTransform.FromPositionRotation(
                            placementState.ValueRO.PreviewPosition,
                            placementState.ValueRO.PreviewRotation));
                    }

                    // Exit placement mode
                    placementState.ValueRW.IsPlacing = false;
                    placementState.ValueRW.PlaceableSource = Entity.Null;
                }

                // --- Handle cancel ---
                if (pInput.AltUse.IsSet)
                {
                    placementState.ValueRW.CancelPlacement = true;
                    placementState.ValueRW.IsPlacing = false;
                    placementState.ValueRW.PlaceableSource = Entity.Null;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
