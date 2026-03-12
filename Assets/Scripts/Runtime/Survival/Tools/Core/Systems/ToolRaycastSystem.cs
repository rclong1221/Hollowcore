using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

namespace DIG.Survival.Tools
{
    /// <summary>
    /// Performs raycasts for the active tool and updates ToolUsageState with hit info.
    /// Also handles Use input to set IsInUse state.
    /// </summary>
    /// <remarks>
    /// Runs in PredictedSimulationSystemGroup for client prediction.
    /// Raycasts use Unity Physics CollisionWorld for consistent results.
    /// Updates run after ToolSwitchingSystem but before tool-specific usage systems.
    /// Uses player transform rotation and CameraYaw from input for look direction.
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ToolSwitchingSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ToolRaycastSystem : ISystem
    {
        private ComponentLookup<ToolUsageState> _toolUsageStateLookup;
        private ComponentLookup<Tool> _toolLookup;
        private ComponentLookup<DrillTool> _drillToolLookup;
        private ComponentLookup<WelderTool> _welderToolLookup;
        private ComponentLookup<SprayerTool> _sprayerToolLookup;

        // Eye height offset from player position
        private const float EyeHeightOffset = 1.7f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PhysicsWorldSingleton>();

            _toolUsageStateLookup = state.GetComponentLookup<ToolUsageState>();
            _toolLookup = state.GetComponentLookup<Tool>(true);
            _drillToolLookup = state.GetComponentLookup<DrillTool>(true);
            _welderToolLookup = state.GetComponentLookup<WelderTool>(true);
            _sprayerToolLookup = state.GetComponentLookup<SprayerTool>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _toolUsageStateLookup.Update(ref state);
            _toolLookup.Update(ref state);
            _drillToolLookup.Update(ref state);
            _welderToolLookup.Update(ref state);
            _sprayerToolLookup.Update(ref state);

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            var deltaTime = SystemAPI.Time.DeltaTime;

            // Sequential execution due to raycast requirements
            foreach (var (transform, activeTool, input, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<ActiveTool>, RefRO<PlayerInput>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                // Skip if no tool equipped
                var toolEntity = activeTool.ValueRO.ToolEntity;
                if (toolEntity == Entity.Null)
                    continue;

                // Skip if tool doesn't have usage state
                if (!_toolUsageStateLookup.HasComponent(toolEntity))
                    continue;

                var usageState = _toolUsageStateLookup[toolEntity];

                // Update IsInUse based on Use input
                usageState.IsInUse = input.ValueRO.Use.IsSet;

                // Update use timer
                if (usageState.IsInUse)
                {
                    usageState.UseTimer += deltaTime;
                }
                else
                {
                    usageState.UseTimer = 0f;
                }

                // Get tool range
                float range = GetToolRange(toolEntity);

                // Calculate raycast origin (eye position)
                float3 rayOrigin = transform.ValueRO.Position + new float3(0, EyeHeightOffset, 0);

                // Calculate raycast direction from camera yaw or player forward
                float3 rayDirection = GetLookDirection(input.ValueRO, transform.ValueRO.Rotation);

                // Perform raycast
                var raycastInput = new RaycastInput
                {
                    Start = rayOrigin,
                    End = rayOrigin + rayDirection * range,
                    Filter = new CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = ~0u,
                        GroupIndex = 0
                    }
                };

                var hits = new NativeList<Unity.Physics.RaycastHit>(Allocator.Temp);
                bool hasHit = physicsWorld.CollisionWorld.CastRay(raycastInput, ref hits);

                // Find closest hit (excluding self)
                float closestFraction = float.MaxValue;
                float3 closestPoint = float3.zero;
                float3 closestNormal = float3.zero;
                Entity closestEntity = Entity.Null;
                bool foundValidHit = false;

                if (hasHit)
                {
                    for (int i = 0; i < hits.Length; i++)
                    {
                        var hit = hits[i];
                        var hitEntity = physicsWorld.Bodies[hit.RigidBodyIndex].Entity;

                        // Skip self
                        if (hitEntity == entity)
                            continue;

                        if (hit.Fraction < closestFraction)
                        {
                            closestFraction = hit.Fraction;
                            closestPoint = hit.Position;
                            closestNormal = hit.SurfaceNormal;
                            closestEntity = hitEntity;
                            foundValidHit = true;
                        }
                    }
                }

                hits.Dispose();

                // Update usage state with raycast results
                usageState.HasTarget = foundValidHit;
                usageState.TargetPoint = closestPoint;
                usageState.TargetNormal = closestNormal;
                usageState.TargetEntity = closestEntity;

                _toolUsageStateLookup[toolEntity] = usageState;
            }
        }

        private float GetToolRange(Entity toolEntity)
        {
            // Check each tool type for its range
            if (_drillToolLookup.HasComponent(toolEntity))
                return _drillToolLookup[toolEntity].Range;

            if (_welderToolLookup.HasComponent(toolEntity))
                return _welderToolLookup[toolEntity].Range;

            if (_sprayerToolLookup.HasComponent(toolEntity))
                return _sprayerToolLookup[toolEntity].Range;

            // Default range for tools without specific range (flashlight, geiger)
            return 10f;
        }

        private static float3 GetLookDirection(in PlayerInput input, in quaternion playerRotation)
        {
            // Use camera yaw from input if valid, otherwise use player forward
            if (input.CameraYawValid != 0)
            {
                // Convert yaw to direction (horizontal only for now)
                float yawRad = math.radians(input.CameraYaw);
                return math.normalizesafe(new float3(math.sin(yawRad), 0, math.cos(yawRad)), new float3(0, 0, 1));
            }

            // Fall back to player's forward direction
            return math.forward(playerRotation);
        }
    }
}
