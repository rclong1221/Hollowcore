using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.NetCode;
using DIG.Voxel.Components;
using Player.Components;
using DIG.Items;
using DIG.Player.Components;

namespace DIG.Voxel.Systems.Interaction
{
    /// <summary>
    /// EPIC 15.10: Handles input for placing explosives.
    /// Runs on Server (for spawning) and Client (for prediction/feedback).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [BurstCompile]
    public partial struct ExplosivePlacementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var collisionWorld = physicsWorld.CollisionWorld;
            float dt = SystemAPI.Time.DeltaTime;
            
            var localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
            var charItemLookup = SystemAPI.GetComponentLookup<DIG.Items.CharacterItem>(true);
            var playerInputLookup = SystemAPI.GetComponentLookup<PlayerInput>(true);
            var playerCameraLookup = SystemAPI.GetComponentLookup<PlayerCameraSettings>(true);
            
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (config, stateData, transform, charItem, entity) in
                     SystemAPI.Query<RefRO<ExplosivePlacementConfig>, RefRW<ExplosivePlacementState>, RefRO<LocalToWorld>, RefRO<DIG.Items.CharacterItem>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                // Cooldown
                if (stateData.ValueRO.CooldownTimer > 0)
                {
                    stateData.ValueRW.CooldownTimer -= dt;
                }

                // Identify Owner and Input
                Entity owner = charItem.ValueRO.OwnerEntity;
                if (owner == Entity.Null || !playerInputLookup.TryGetComponent(owner, out var input))
                    continue;

                // FIX: Only allow placement if the tool is actually equipped (held)
                if (charItem.ValueRO.State != ItemState.Equipped)
                    continue;

                // Input Check
                if (input.Use.IsSet && stateData.ValueRO.CooldownTimer <= 0)
                {
                     // Debug.Log($"[VoxelTool] Input detected on {state.WorldUnmanaged.Name}");

                    float3 rayOrigin = transform.ValueRO.Position; 
                    float3 rayDir = transform.ValueRO.Forward;
                    float range = config.ValueRO.PlacementRange;

                    // Improved Aiming: Use owner's eye/aim
                    if (localToWorldLookup.TryGetComponent(owner, out var ownerLTW))
                    {
                        rayOrigin = ownerLTW.Position + new float3(0, 1.6f, 0); // Eye Height

                        if (playerInputLookup.TryGetComponent(owner, out var pInput))
                        {
                            quaternion lookRot = quaternion.Euler(math.radians(pInput.CameraPitch), math.radians(pInput.CameraYaw), 0);
                            rayDir = math.rotate(lookRot, new float3(0, 0, 1));
                        }
                        else if (playerCameraLookup.TryGetComponent(owner, out var pCam))
                        {
                            quaternion lookRot = quaternion.Euler(math.radians(pCam.Pitch), math.radians(pCam.Yaw), 0);
                            rayDir = math.rotate(lookRot, new float3(0, 0, 1));
                        }
                    }
                    
                    var rayInput = new RaycastInput
                    {
                        Start = rayOrigin,
                        End = rayOrigin + (rayDir * range),
                        Filter = new CollisionFilter
                        {
                            BelongsTo = CollisionLayers.Everything,
                            CollidesWith = CollisionLayers.Default | CollisionLayers.Environment | CollisionLayers.Ship | (1u << 8), // Excludes Player (Layer 1)
                            GroupIndex = 0
                        }
                    };

                    if (collisionWorld.CastRay(rayInput, out var hit))
                    {
                         if (config.ValueRO.ExplosivePrefab == Entity.Null)
                         {
                             // Only warn on server. Client might not have prefab reference yet.
                             if (state.WorldUnmanaged.IsServer())
                                UnityEngine.Debug.LogWarning($"[VoxelTool] Prefab is NULL on Server! Check Authoring.");
                             continue;
                         }

                         // Server-side Spawning ONLY (Prevents ghost/local duplication issues)
                         if (state.WorldUnmanaged.IsServer())
                         {
                             UnityEngine.Debug.Log($"[VoxelTool] Spawning Explosive at {hit.Position} on Surface {hit.SurfaceNormal} (Server)");
                             
                             // Instantiate
                             var explosive = ecb.Instantiate(config.ValueRO.ExplosivePrefab);
                             
                             // Calculate Position & Rotation
                             float3 spawnPos = hit.Position;
                             quaternion spawnRot = (quaternion)UnityEngine.Quaternion.FromToRotation(UnityEngine.Vector3.up, (UnityEngine.Vector3)hit.SurfaceNormal);
                             
                             ecb.SetComponent(explosive, LocalTransform.FromPositionRotation(spawnPos, spawnRot));
                             
                             // Link to owner for remote detonation
                             ecb.AddComponent(explosive, new EntityOwner { OwnerEntity = owner });
                             
                             // Ensure it's marked as a remote explosive if it's C4
                             if (!config.ValueRO.SubsurfacePlacement) // Usually true for C4
                             {
                                 ecb.AddComponent<RemoteExplosive>(explosive);
                             }
                        }
                        else
                        {
                            // Client Side FX placeholder
                            // UnityEngine.Debug.Log($"[VoxelTool] Raycast Hit on Client (Waiting for Server Spawn)");
                        }

                         // Set Cooldown
                         stateData.ValueRW.CooldownTimer = config.ValueRO.CooldownTime;
                    }
                    else
                    {
                        // UnityEngine.Debug.Log($"[ExplosivePlacement] Raycast Missed on {state.WorldUnmanaged.Name}");
                    }
                }
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
