using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using Player.Authoring;

namespace Player.Systems
{
    /// <summary>
    /// Spawns Blitz at designated spawn points on the server.
    /// Only runs on server. Clients receive Blitz through ghost replication.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct BlitzSpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BlitzSpawner>();
            state.RequireForUpdate<BlitzSpawnRequest>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            // Get the Blitz prefab
            Entity blitzPrefab = Entity.Null;
            foreach (var spawner in SystemAPI.Query<RefRO<BlitzSpawner>>())
            {
                blitzPrefab = spawner.ValueRO.BlitzPrefab;
                break;
            }
            
            if (blitzPrefab == Entity.Null)
            {
                Debug.LogError("[BlitzSpawnSystem] BlitzSpawner has no prefab!");
                return;
            }
            
            // Spawn Blitz at each spawn request location
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
                .WithAll<BlitzSpawnRequest>()
                .WithEntityAccess())
            {
                var blitz = ecb.Instantiate(blitzPrefab);
                
                ecb.SetComponent(blitz, new LocalTransform
                {
                    Position = transform.ValueRO.Position,
                    Rotation = transform.ValueRO.Rotation,
                    Scale = 1f
                });
                
                Debug.Log($"[BlitzSpawnSystem] Spawned Blitz at {transform.ValueRO.Position}");
                
                // Remove the spawn request so we don't spawn again
                ecb.RemoveComponent<BlitzSpawnRequest>(entity);
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

