using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Ship.LocalSpace
{
    /// <summary>
    /// System that handles automatic attach/detach based on PlayerMode changes.
    /// Monitors PlayerState and issues attach/detach requests accordingly.
    /// NOTE: Requires a ship with ShipRoot component in the scene.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(EnterShipLocalSpaceSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct PlayerModeLocalSpaceSystem : ISystem
    {
        private ComponentLookup<ShipRoot> _shipLookup;
        private ComponentLookup<InShipLocalSpace> _localSpaceLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            _shipLookup = state.GetComponentLookup<ShipRoot>(true);
            _localSpaceLookup = state.GetComponentLookup<InShipLocalSpace>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _shipLookup.Update(ref state);
            _localSpaceLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // Find the first ship (MVP: assume single ship)
            Entity shipEntity = Entity.Null;
            foreach (var (shipRoot, entity) in
                     SystemAPI.Query<RefRO<ShipRoot>>()
                     .WithEntityAccess())
            {
                shipEntity = entity;
                break;
            }

            // Process players whose mode changed
            foreach (var (playerState, entity) in
                     SystemAPI.Query<RefRO<PlayerState>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                PlayerMode mode = playerState.ValueRO.Mode;
                bool hasLocalSpace = _localSpaceLookup.HasComponent(entity);
                bool hasAttachRequest = SystemAPI.HasComponent<AttachToShipRequest>(entity);
                bool hasDetachRequest = SystemAPI.HasComponent<DetachFromShipRequest>(entity);
                
                // Check if player is ACTUALLY attached (not just has the component)
                bool isCurrentlyAttached = false;
                if (hasLocalSpace)
                {
                    isCurrentlyAttached = _localSpaceLookup[entity].IsAttached;
                }

                // Should be attached when InShip or Piloting
                bool shouldBeAttached = (mode == PlayerMode.InShip || mode == PlayerMode.Piloting);

                if (shouldBeAttached && !isCurrentlyAttached && !hasAttachRequest && shipEntity != Entity.Null)
                {
                    // Need to attach
                    ecb.AddComponent(entity, new AttachToShipRequest
                    {
                        ShipEntity = shipEntity,
                        ComputeFromWorldTransform = true
                    });
                }
                else if (!shouldBeAttached && isCurrentlyAttached && !hasDetachRequest)
                {
                    // Need to detach (EVA, dead, etc.) - only if ACTUALLY attached
                    ecb.AddComponent(entity, new DetachFromShipRequest
                    {
                        PreserveWorldPosition = true
                    });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// System that handles edge cases for ship local space.
    /// Cleans up when ships despawn, players disconnect, etc.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ExitShipLocalSpaceSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct ShipLocalSpaceCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // Check for entities attached to ships that no longer exist
            foreach (var (localSpace, entity) in
                     SystemAPI.Query<RefRO<InShipLocalSpace>>()
                     .WithEntityAccess())
            {
                Entity shipEntity = localSpace.ValueRO.ShipEntity;

                // Only check if player is actually attached to a valid ship
                // Skip if ShipEntity is null (player not attached) or IsAttached is false
                if (shipEntity == Entity.Null || !localSpace.ValueRO.IsAttached)
                    continue;

                // Check if ship still exists
                if (!state.EntityManager.Exists(shipEntity))
                {
                    // Ship despawned - detach player
                    ecb.AddComponent(entity, new DetachFromShipRequest
                    {
                        PreserveWorldPosition = true
                    });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
