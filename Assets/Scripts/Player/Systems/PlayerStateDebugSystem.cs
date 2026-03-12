using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

/// <summary>
/// Debug system to log player movement state changes for debugging network replication.
/// Logs when MovementState changes for any player entity.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
public partial class PlayerStateDebugSystem : SystemBase
{
    private EntityQuery _playerQuery;
    
    // Cache previous states to detect changes
    private NativeParallelHashMap<Entity, PlayerMovementState> _previousStates;
    
    protected override void OnCreate()
    {
        RequireForUpdate<NetworkStreamInGame>();
        _previousStates = new NativeParallelHashMap<Entity, PlayerMovementState>(16, Unity.Collections.Allocator.Persistent);
    }
    
    protected override void OnDestroy()
    {
        if (_previousStates.IsCreated)
            _previousStates.Dispose();
    }

    protected override void OnUpdate()
    {
        var worldName = World.Name;
        bool isServer = World.IsServer();
        string worldTag = isServer ? "[SERVER]" : "[CLIENT]";
        
        foreach (var (playerState, ghostOwner, entity) in 
                 SystemAPI.Query<RefRO<PlayerState>, RefRO<GhostOwner>>()
                     .WithAll<PlayerTag>()
                     .WithEntityAccess())
        {
            var currentState = playerState.ValueRO.MovementState;
            var ownerId = ghostOwner.ValueRO.NetworkId;
            
            bool hadPrevious = _previousStates.TryGetValue(entity, out var previousState);
            
            if (!hadPrevious)
            {
                // First time seeing this entity
                _previousStates[entity] = currentState;
                // Debug.Log($"{worldTag} Entity {entity.Index}:{entity.Version} (Owner:{ownerId}) - Initial state: {currentState}");
            }
            else if (currentState != previousState)
            {
                // State changed
                _previousStates[entity] = currentState;
                // Debug.Log($"{worldTag} Entity {entity.Index}:{entity.Version} (Owner:{ownerId}) - State changed: {previousState} -> {currentState}");
            }
        }
        
        // Clean up entities that no longer exist
        var toRemove = new Unity.Collections.NativeList<Entity>(Unity.Collections.Allocator.Temp);
        foreach (var kvp in _previousStates)
        {
            if (!EntityManager.Exists(kvp.Key) || !EntityManager.HasComponent<PlayerState>(kvp.Key))
            {
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var e in toRemove)
        {
            _previousStates.Remove(e);
        }
        toRemove.Dispose();
    }
}
