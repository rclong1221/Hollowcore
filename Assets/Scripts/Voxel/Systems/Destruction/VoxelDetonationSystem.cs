using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Voxel.Components;
using DIG.Voxel.Core;
using DIG.Voxel.Systems.Interaction;

namespace DIG.Voxel.Systems.Destruction
{
    /// <summary>
    /// EPIC 15.10: Processes Detonation Requests and triggers actual voxel/physics explosions.
    /// This is the bridge between Chain Reactions and the Voxel Explosion system.
    /// Only runs on server - ghosts are automatically despawned on clients when server destroys them.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(VoxelExplosionSystem))]
    [UpdateBefore(typeof(VoxelExplosionNetworkSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    // [BurstCompile] // Disabled for debugging
    public partial struct VoxelDetonationSystem : ISystem
    {
        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            // Process all entities that have a Detonation Request
            // (Barrels, C4, Dynamite, Grenades after countdown or remote trigger)
            foreach (var (request, transform, entity) in
                     SystemAPI.Query<RefRO<VoxelDetonationRequest>, RefRO<LocalTransform>>()
                     .WithEntityAccess())
            {
                float3 position = transform.ValueRO.Position;
                UnityEngine.Debug.Log($"[GRENADE] VoxelDetonationSystem: Processing detonation for Entity {entity.Index} at {position}");
                
                // 1. Create the Voxel Crater Request
                // (Using VoxelDamageConfig if it exists on the entity, otherwise default)
                float radius = 4f;
                float damage = 100f;
                
                if (SystemAPI.HasComponent<VoxelDamageRequest>(entity))
                {
                    var config = SystemAPI.GetComponent<VoxelDamageRequest>(entity);
                    radius = config.Param1;
                    damage = config.Damage;
                }
                
                UnityEngine.Debug.Log($"[GRENADE] Creating crater: Pos={position}, Radius={radius}, Damage={damage}");

                // Create Voxel Crater
                var craterEntity = ecb.CreateEntity();
                ecb.AddComponent(craterEntity, new CreateCraterRequest
                {
                    Center = position,
                    Radius = radius,
                    Strength = 1.0f,
                    ReplaceMaterial = VoxelConstants.MATERIAL_AIR,
                    SpawnLoot = true,
                    FromRpc = false,
                    Seed = (uint)position.x + (uint)position.z // Deterministic-ish seed
                });

                // 2. Emit event for visuals/sound/chain reactions
                // This is consumed by VoxelExplosionNetworkSystem for broadcast
                // and by ChainReactionSystem for triggering nearby items.
                var eventEntity = ecb.CreateEntity();
                ecb.AddComponent(eventEntity, new VoxelExplosionEvent
                {
                    Position = position,
                    BlastRadius = radius,
                    Damage = damage
                });

                // 3. Destroy the source explosive entity
                ecb.DestroyEntity(entity);
                UnityEngine.Debug.Log($"[GRENADE] Detonation complete. Entity {entity.Index} destroyed.");
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
