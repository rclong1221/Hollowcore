using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Ship.LocalSpace
{
    /// <summary>
    /// System that attaches players to ship local space when they enter.
    /// Triggered by PlayerMode change or airlock transition completion.
    /// </summary>
    // [BurstCompile] // Temporarily disabled for debug logging
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct EnterShipLocalSpaceSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<ShipRoot> _shipLookup;

        // [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _shipLookup = state.GetComponentLookup<ShipRoot>(true);
        }

        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            _shipLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // Process attach requests
            foreach (var (request, transform, entity) in
                     SystemAPI.Query<RefRO<AttachToShipRequest>, RefRO<LocalTransform>>()
                     .WithEntityAccess())
            {
                Entity shipEntity = request.ValueRO.ShipEntity;

                // Validate ship exists
                if (!_shipLookup.HasComponent(shipEntity))
                {
                    ecb.RemoveComponent<AttachToShipRequest>(entity);
                    continue;
                }

                if (!_transformLookup.HasComponent(shipEntity))
                {
                    ecb.RemoveComponent<AttachToShipRequest>(entity);
                    continue;
                }

                var shipTransform = _transformLookup[shipEntity];
                var playerTransform = transform.ValueRO;

                float3 localPosition;
                quaternion localRotation;

                if (request.ValueRO.ComputeFromWorldTransform)
                {
                    // Convert world position to ship-local position
                    // Local = inverse(ship) * world
                    float4x4 shipWorldToLocal = math.inverse(float4x4.TRS(
                        shipTransform.Position,
                        shipTransform.Rotation,
                        new float3(1, 1, 1)));

                    float4 worldPos = new float4(playerTransform.Position, 1f);
                    float4 localPos = math.mul(shipWorldToLocal, worldPos);
                    localPosition = localPos.xyz;

                    // Convert world rotation to ship-local rotation
                    localRotation = math.mul(math.inverse(shipTransform.Rotation), playerTransform.Rotation);
                }
                else
                {
                    localPosition = request.ValueRO.InitialLocalPosition;
                    localRotation = request.ValueRO.InitialLocalRotation;
                }

                // Add/Set InShipLocalSpace component using factory to ensure valid quaternion
                // Use direct EntityManager for SetComponent to ensure it persists correctly (ECB was not working)
                var localSpaceData = InShipLocalSpace.Create(shipEntity, localPosition, localRotation);
                if (SystemAPI.HasComponent<InShipLocalSpace>(entity))
                {
                    // Write directly to EntityManager - ECB SetComponent was not persisting
                    state.EntityManager.SetComponentData(entity, localSpaceData);
                    UnityEngine.Debug.Log($"[EnterShipLocalSpaceSystem] SET InShipLocalSpace via EntityManager for player {entity.Index} to ship {shipEntity.Index}. IsAttached={localSpaceData.IsAttached}");
                }
                else
                {
                    ecb.AddComponent(entity, localSpaceData);
                    UnityEngine.Debug.Log($"[EnterShipLocalSpaceSystem] ADDED InShipLocalSpace for player {entity.Index} to ship {shipEntity.Index}. IsAttached={localSpaceData.IsAttached}");
                }

                // Add smoothing state
                if (!SystemAPI.HasComponent<LocalSpaceSmoothing>(entity))
                {
                    ecb.AddComponent(entity, LocalSpaceSmoothing.Default);
                }

                // Remove the request
                ecb.RemoveComponent<AttachToShipRequest>(entity);

                // Add occupant to ship's buffer
                if (SystemAPI.HasBuffer<ShipOccupant>(shipEntity))
                {
                    var buffer = SystemAPI.GetBuffer<ShipOccupant>(shipEntity);
                    buffer.Add(new ShipOccupant
                    {
                        OccupantEntity = entity,
                        IsPilot = false
                    });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// System that detaches players from ship local space when they exit.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct ExitShipLocalSpaceSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<ShipRoot> _shipLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(false);
            _shipLookup = state.GetComponentLookup<ShipRoot>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            _shipLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // Process detach requests
            foreach (var (request, localSpace, transform, entity) in
                     SystemAPI.Query<RefRO<DetachFromShipRequest>, RefRO<InShipLocalSpace>, RefRW<LocalTransform>>()
                     .WithEntityAccess())
            {
                Entity shipEntity = localSpace.ValueRO.ShipEntity;

                if (request.ValueRO.PreserveWorldPosition)
                {
                    // Already in world position, nothing to do
                }
                else if (_transformLookup.HasComponent(shipEntity))
                {
                    // Convert local position back to world position
                    var shipTransform = _transformLookup[shipEntity];
                    
                    float4x4 shipLocalToWorld = float4x4.TRS(
                        shipTransform.Position,
                        shipTransform.Rotation,
                        new float3(1, 1, 1));

                    float4 localPos = new float4(localSpace.ValueRO.LocalPosition, 1f);
                    float4 worldPos = math.mul(shipLocalToWorld, localPos);

                    quaternion worldRotation = math.mul(shipTransform.Rotation, localSpace.ValueRO.LocalRotation);

                    ref var playerTransform = ref transform.ValueRW;
                    playerTransform.Position = worldPos.xyz;
                    playerTransform.Rotation = worldRotation;
                }

                // Disable local space instead of removing component (better for replication)
                var newLocalSpace = localSpace.ValueRO;
                newLocalSpace.IsAttached = false;
                ecb.SetComponent(entity, newLocalSpace);
                
                ecb.RemoveComponent<DetachFromShipRequest>(entity);

                // Remove from ship's occupant buffer
                if (SystemAPI.HasBuffer<ShipOccupant>(shipEntity))
                {
                    var buffer = SystemAPI.GetBuffer<ShipOccupant>(shipEntity);
                    for (int i = buffer.Length - 1; i >= 0; i--)
                    {
                        if (buffer[i].OccupantEntity == entity)
                        {
                            buffer.RemoveAt(i);
                            break;
                        }
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
