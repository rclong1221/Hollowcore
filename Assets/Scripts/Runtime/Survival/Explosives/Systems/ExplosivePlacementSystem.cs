using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

namespace DIG.Survival.Explosives
{
    /// <summary>
    /// Handles player input for placing explosives.
    /// Creates placement requests processed by spawn system.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ExplosivePlacementInputSystem : ISystem
    {
        private const float PlacementRange = 3f;
        private const float EyeHeightOffset = 1.7f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;

            foreach (var (transform, input, selected, inventoryBuffer, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerInput>, RefRO<SelectedExplosive>,
                                     DynamicBuffer<ExplosiveInventory>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                // Check for placement input (Use button when explosive is selected)
                if (!input.ValueRO.Use.IsSet)
                    continue;

                var selectedType = selected.ValueRO.Type;
                if (selectedType == ExplosiveType.None)
                    continue;

                // Check inventory
                int quantity = 0;
                for (int i = 0; i < inventoryBuffer.Length; i++)
                {
                    if (inventoryBuffer[i].Type == selectedType)
                    {
                        quantity = inventoryBuffer[i].Quantity;
                        break;
                    }
                }

                if (quantity <= 0)
                    continue;

                // Raycast to find placement surface
                float3 rayOrigin = transform.ValueRO.Position + new float3(0, EyeHeightOffset, 0);
                float3 rayDirection = GetLookDirection(input.ValueRO, transform.ValueRO.Rotation);

                var raycastInput = new RaycastInput
                {
                    Start = rayOrigin,
                    End = rayOrigin + rayDirection * PlacementRange,
                    Filter = new CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = ~0u,
                        GroupIndex = 0
                    }
                };

                if (!physicsWorld.CollisionWorld.CastRay(raycastInput, out var hit))
                    continue;

                // Skip if hit self
                var hitEntity = physicsWorld.Bodies[hit.RigidBodyIndex].Entity;
                if (hitEntity == entity)
                    continue;

                // Add placement request
                if (!SystemAPI.HasBuffer<PlaceExplosiveRequest>(entity))
                    continue;

                var requestBuffer = SystemAPI.GetBuffer<PlaceExplosiveRequest>(entity);
                requestBuffer.Add(new PlaceExplosiveRequest
                {
                    Type = selectedType,
                    Position = hit.Position,
                    Normal = hit.SurfaceNormal
                });
            }
        }

        private static float3 GetLookDirection(in PlayerInput input, in quaternion playerRotation)
        {
            if (input.CameraYawValid != 0)
            {
                float yawRad = math.radians(input.CameraYaw);
                return math.normalizesafe(new float3(math.sin(yawRad), 0, math.cos(yawRad)), new float3(0, 0, 1));
            }
            return math.forward(playerRotation);
        }
    }

    /// <summary>
    /// Server-authoritative system that spawns explosive entities.
    /// Processes placement requests and decrements inventory.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ExplosiveSpawnSystem : ISystem
    {
        private BufferLookup<PlaceExplosiveRequest> _requestLookup;
        private BufferLookup<ExplosiveInventory> _inventoryLookup;
        private EntityQuery _playersWithRequestsQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<ExplosivePrefabs>();

            _requestLookup = state.GetBufferLookup<PlaceExplosiveRequest>();
            _inventoryLookup = state.GetBufferLookup<ExplosiveInventory>();

            _playersWithRequestsQuery = state.GetEntityQuery(
                ComponentType.ReadWrite<PlaceExplosiveRequest>(),
                ComponentType.ReadWrite<ExplosiveInventory>()
            );
        }

        public void OnUpdate(ref SystemState state)
        {
            _requestLookup.Update(ref state);
            _inventoryLookup.Update(ref state);

            var prefabs = SystemAPI.GetSingleton<ExplosivePrefabs>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entities = _playersWithRequestsQuery.ToEntityArray(Allocator.Temp);

            foreach (var playerEntity in entities)
            {
                var requests = _requestLookup[playerEntity];
                var inventory = _inventoryLookup[playerEntity];

                if (requests.Length == 0)
                    continue;

                for (int i = requests.Length - 1; i >= 0; i--)
                {
                    var request = requests[i];

                    // Validate inventory
                    int inventoryIndex = -1;
                    for (int j = 0; j < inventory.Length; j++)
                    {
                        if (inventory[j].Type == request.Type && inventory[j].Quantity > 0)
                        {
                            inventoryIndex = j;
                            break;
                        }
                    }

                    if (inventoryIndex < 0)
                        continue;

                    // Get prefab
                    var prefab = prefabs.GetPrefab(request.Type);
                    if (prefab == Entity.Null)
                        continue;

                    // Spawn explosive
                    var explosive = ecb.Instantiate(prefab);

                    // Set position with slight offset from surface
                    float3 position = request.Position + request.Normal * 0.05f;
                    ecb.SetComponent(explosive, LocalTransform.FromPositionRotation(
                        position,
                        quaternion.LookRotationSafe(request.Normal, math.up())
                    ));

                    // Set explosive state
                    float fuseTime = ExplosiveStats.GetDefaultFuseTime(request.Type);
                    ecb.SetComponent(explosive, new PlacedExplosive
                    {
                        Type = request.Type,
                        FuseTimeRemaining = fuseTime,
                        InitialFuseTime = fuseTime,
                        IsArmed = false,
                        TimeSincePlacement = 0f,
                        PlacerEntity = playerEntity,
                        AttachedNormal = request.Normal
                    });

                    // Set stats
                    ecb.SetComponent(explosive, ExplosiveStats.GetDefaults(request.Type));

                    // Decrement inventory
                    var item = inventory[inventoryIndex];
                    item.Quantity--;
                    inventory[inventoryIndex] = item;
                }

                requests.Clear();
            }

            entities.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
