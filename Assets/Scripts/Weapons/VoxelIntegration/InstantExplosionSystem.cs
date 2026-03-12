using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.NetCode;
using DIG.Voxel.Components;
using DIG.Voxel.Core;
using DIG.Voxel.Systems.Interaction;
using Player.Components;
using DIG.Items;
using DIG.Player.Components;

namespace DIG.Voxel.Systems.Interaction
{
    /// <summary>
    /// EPIC 15.10: Handles input for tools that explode immediately on use.
    /// Server-only to prevent client-side ghost desync issues.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [BurstCompile]
    public partial struct InstantExplosionSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var collisionWorld = physicsWorld.CollisionWorld;
            float dt = SystemAPI.Time.DeltaTime;
            
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
            var charItemLookup = SystemAPI.GetComponentLookup<DIG.Items.CharacterItem>(true);
            var playerInputLookup = SystemAPI.GetComponentLookup<PlayerInput>(true);
            var playerCameraLookup = SystemAPI.GetComponentLookup<PlayerCameraSettings>(true);

            foreach (var (config, stateData, transform, charItem, entity) in
                     SystemAPI.Query<RefRO<InstantExplosionConfig>, RefRW<InstantExplosionState>, RefRO<LocalToWorld>, RefRO<DIG.Items.CharacterItem>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                if (stateData.ValueRO.CooldownTimer > 0)
                    stateData.ValueRW.CooldownTimer -= dt;

                Entity owner = charItem.ValueRO.OwnerEntity;
                if (owner == Entity.Null || !playerInputLookup.TryGetComponent(owner, out var input))
                    continue;

                // FIX: Only allow explosion if the tool is actually equipped (held)
                if (charItem.ValueRO.State != ItemState.Equipped)
                    continue;

                if (input.Use.IsSet && stateData.ValueRO.CooldownTimer <= 0)
                {
                    float3 rayOrigin = transform.ValueRO.Position; 
                    float3 rayDir = transform.ValueRO.Forward;
                    float range = config.ValueRO.Range;

                    if (localToWorldLookup.TryGetComponent(owner, out var ownerLTW))
                    {
                        rayOrigin = ownerLTW.Position + new float3(0, 1.6f, 0);
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
                         // Create Voxel Crater
                         var craterEntity = ecb.CreateEntity();
                         ecb.AddComponent(craterEntity, new CreateCraterRequest
                         {
                             Center = hit.Position,
                             Radius = config.ValueRO.Radius,
                             Strength = 1.0f,
                             ReplaceMaterial = VoxelConstants.MATERIAL_AIR,
                             SpawnLoot = true,
                             Seed = (uint)hit.Position.x + (uint)hit.Position.z
                         });

                         // Emit explosion event for effects/chain reactions
                         var eventEntity = ecb.CreateEntity();
                         ecb.AddComponent(eventEntity, new VoxelExplosionEvent
                         {
                             Position = hit.Position,
                             BlastRadius = config.ValueRO.Radius,
                             Damage = config.ValueRO.Damage
                         });
                         
                         stateData.ValueRW.CooldownTimer = config.ValueRO.Cooldown;
                    }
                }
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
