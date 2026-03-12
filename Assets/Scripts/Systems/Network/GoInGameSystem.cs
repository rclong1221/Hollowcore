using UnityEngine;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Burst;
using DIG.Player.Components;
using DIG.Lobby;

/// <summary>
/// This allows sending RPCs between a stand alone build and the editor for testing purposes in the event when you finish this example
/// you want to connect a server-client stand alone build to a client configured editor instance.
/// </summary>
[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
[UpdateInGroup(typeof(InitializationSystemGroup))]
[CreateAfter(typeof(RpcSystem))]
public partial struct SetRpcSystemDynamicAssemblyListSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        SystemAPI.GetSingletonRW<RpcCollection>().ValueRW.DynamicAssemblyList = true;
        state.Enabled = false;
    }
}

// RPC request from client to server for game to go "in game" and send snapshots / inputs
public struct GoInGameRequest : IRpcCommand
{
}

// EPIC 18.10: Spectator join system — managed (non-Burst) because it reads GameBootstrap.IsSpectatorMode.
// Runs BEFORE GoInGameClientSystem. If spectator mode is active, sends SpectatorJoinRequest
// and adds NetworkStreamInGame, preventing GoInGameClientSystem from firing.
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateBefore(typeof(GoInGameClientSystem))]
public partial class SpectatorGoInGameClientSystem : SystemBase
{
    private EntityQuery _pendingConnectionQuery;

    protected override void OnCreate()
    {
        RequireForUpdate<PlayerSpawner>();
        _pendingConnectionQuery = GetEntityQuery(
            ComponentType.ReadOnly<NetworkId>(),
            ComponentType.Exclude<NetworkStreamInGame>()
        );
        RequireForUpdate(_pendingConnectionQuery);
    }

    protected override void OnUpdate()
    {
        if (!GameBootstrap.IsSpectatorMode) return;

        var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        var entities = _pendingConnectionQuery.ToEntityArray(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            commandBuffer.AddComponent<NetworkStreamInGame>(entities[i]);
            var req = commandBuffer.CreateEntity();
            commandBuffer.AddComponent<DIG.Replay.SpectatorJoinRequest>(req);
            commandBuffer.AddComponent(req, new SendRpcCommandRequest { TargetConnection = entities[i] });
        }

        entities.Dispose();
        commandBuffer.Playback(EntityManager);
    }
}

// When client has a connection with network id, go in game and tell server to also go in game
[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
public partial struct GoInGameClientSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // Run only on entities with a PlayerSpawner component data
        state.RequireForUpdate<PlayerSpawner>();

        var builder = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<NetworkId>()
            .WithNone<NetworkStreamInGame>();
        state.RequireForUpdate(state.GetEntityQuery(builder));
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (id, entity) in SystemAPI.Query<RefRO<NetworkId>>().WithEntityAccess().WithNone<NetworkStreamInGame>())
        {
            commandBuffer.AddComponent<NetworkStreamInGame>(entity);
            var req = commandBuffer.CreateEntity();
            commandBuffer.AddComponent<GoInGameRequest>(req);
            commandBuffer.AddComponent(req, new SendRpcCommandRequest { TargetConnection = entity });
        }
        commandBuffer.Playback(state.EntityManager);
    }
}

// When server receives go in game request, go in game and delete request
[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct GoInGameServerSystem : ISystem
{
    private ComponentLookup<NetworkId> networkIdFromEntity;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerSpawner>();

        var builder = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<GoInGameRequest>()
            .WithAll<ReceiveRpcCommandRequest>();
        state.RequireForUpdate(state.GetEntityQuery(builder));
        networkIdFromEntity = state.GetComponentLookup<NetworkId>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Get the prefab to instantiate
        var prefab = SystemAPI.GetSingleton<PlayerSpawner>().Player;

        // Ge the name of the prefab being instantiated
        state.EntityManager.GetName(prefab, out var prefabName);
        var worldName = new FixedString32Bytes(state.WorldUnmanaged.Name);

        var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        networkIdFromEntity.Update(ref state);

        foreach (var (reqSrc, reqEntity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>().WithAll<GoInGameRequest>().WithEntityAccess())
        {
            commandBuffer.AddComponent<NetworkStreamInGame>(reqSrc.ValueRO.SourceConnection);
            // Get the NetworkId for the requesting client
            var networkId = networkIdFromEntity[reqSrc.ValueRO.SourceConnection];

            // Log information about the connection request that includes the client's assigned NetworkId and the name of the prefab spawned.
            UnityEngine.Debug.Log($"'{worldName}' setting connection '{networkId.Value}' to in game, spawning a Ghost '{prefabName}' for them!");

            // Instantiate the prefab
            var player = commandBuffer.Instantiate(prefab);
            
            // Epic 7.6.4: Add spawn grace period to prevent collision spam
            commandBuffer.AddComponent(player, CollisionGracePeriod.SpawnDefault);
            
            // Associate the instantiated prefab with the connected client's assigned NetworkId
            commandBuffer.SetComponent(player, new GhostOwner { NetworkId = networkId.Value});

            // Important: Set position so they don't all spawn at 0,0,0 and Z-fight
            // Preserve the prefab's baked rotation instead of forcing identity so
            // imported model orientation and prefab overrides remain correct.
            var prefabRotation = Unity.Mathematics.quaternion.identity;
            if (state.EntityManager.HasComponent<Unity.Transforms.LocalTransform>(prefab))
            {
                prefabRotation = state.EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(prefab).Rotation;
            }

            // EPIC 17.4: If LobbySpawnData exists, use map spawn positions
            var spawnPos = new Unity.Mathematics.float3(0, 1, networkId.Value * 2);
            var spawnRot = prefabRotation;

            if (SystemAPI.HasSingleton<LobbySpawnData>())
            {
                var spawnData = SystemAPI.GetSingletonRW<LobbySpawnData>();
                int slotIndex = spawnData.ValueRW.ClaimNextSlot();
                if (slotIndex >= 0 && slotIndex < spawnData.ValueRO.SpawnPositions.Length)
                {
                    spawnPos = spawnData.ValueRO.SpawnPositions[slotIndex];
                    if (slotIndex < spawnData.ValueRO.SpawnRotations.Length)
                        spawnRot = spawnData.ValueRO.SpawnRotations[slotIndex];
                }

                // Track spawned count for cleanup
                spawnData.ValueRW.SpawnedCount++;
            }

            var pos = new Unity.Transforms.LocalTransform
            {
                Position = spawnPos,
                Rotation = spawnRot,
                Scale = 1
            };
            commandBuffer.SetComponent(player, pos);

            // Add the player to the linked entity group so it is destroyed automatically on disconnect
            commandBuffer.AppendToBuffer(reqSrc.ValueRO.SourceConnection, new LinkedEntityGroup{Value = player});
            commandBuffer.DestroyEntity(reqEntity);
        }
        commandBuffer.Playback(state.EntityManager);

        // EPIC 17.4: Destroy LobbySpawnData after all players have spawned
        if (SystemAPI.HasSingleton<LobbySpawnData>())
        {
            var spawnData = SystemAPI.GetSingleton<LobbySpawnData>();
            if (spawnData.SpawnedCount >= spawnData.PlayerCount && spawnData.PlayerCount > 0)
            {
                var spawnEntity = SystemAPI.GetSingletonEntity<LobbySpawnData>();
                state.EntityManager.DestroyEntity(spawnEntity);
                UnityEngine.Debug.Log("[GoInGame] All players spawned — LobbySpawnData destroyed.");
            }
        }
    }
}
