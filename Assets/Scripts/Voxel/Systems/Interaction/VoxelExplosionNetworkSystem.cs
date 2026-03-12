using Unity.Entities;
using Unity.NetCode;
using DIG.Voxel.Core;
using Unity.Collections;
using Unity.Mathematics;

namespace DIG.Voxel.Systems.Interaction
{
    /// <summary>
    /// Handles network replication of voxel explosions.
    /// Broadcasts CreateCraterRequests from Server to Clients.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateBefore(typeof(VoxelExplosionSystem))]
    public partial class VoxelExplosionNetworkSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<NetworkTime>(); // Ensure NetCode is running
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // SERVER: Broadcast new requests
            if (World.IsServer())
            {
                foreach (var (request, entity) in SystemAPI.Query<RefRO<CreateCraterRequest>>().WithEntityAccess())
                {
                    // Only broadcast if it's an original request (not from RPC)
                    if (!request.ValueRO.FromRpc)
                    {
                        var rpcEntity = ecb.CreateEntity();
                        ecb.AddComponent(rpcEntity, new ExplosionBroadcastRpc
                        {
                            Center = request.ValueRO.Center,
                            Radius = request.ValueRO.Radius,
                            Seed = request.ValueRO.Seed // Sync Seed
                        });
                        // TargetConnection = Entity.Null broadcasts to all clients
                        ecb.AddComponent(rpcEntity, new SendRpcCommandRequest { TargetConnection = Entity.Null });
                    }
                }
            }

            // CLIENT: Receive RPCs and create local visual requests
            foreach (var (rpc, entity) in SystemAPI.Query<RefRO<ExplosionBroadcastRpc>>().WithEntityAccess())
            {
                // Create local request for visualization
                var reqEntity = ecb.CreateEntity();
                ecb.AddComponent(reqEntity, new CreateCraterRequest
                {
                    Center = rpc.ValueRO.Center,
                    Radius = rpc.ValueRO.Radius,
                    Strength = 1.0f, // Default strength
                    ReplaceMaterial = 0, // AIR
                    SpawnLoot = false, // Visuals only
                    FromRpc = true,
                    Seed = rpc.ValueRO.Seed // Use synced seed
                });
                
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
