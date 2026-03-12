using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Ship.LocalSpace
{
    /// <summary>
    /// System that tracks ship transform changes for delta calculation.
    /// Stores previous transform each tick for inertial correction.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct ShipTransformTrackingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Store current transform as previous for next tick's delta calculation
            // This runs at START of tick so we capture "before movement" state
            foreach (var (transform, prevTransform, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRW<ShipPreviousTransform>>()
                     .WithAll<ShipRoot>()
                     .WithEntityAccess())
            {
                ref var prev = ref prevTransform.ValueRW;

                // Don't update on first frame - just mark as initialized
                if (prev.IsFirstFrame)
                {
                    prev.PreviousPosition = transform.ValueRO.Position;
                    prev.PreviousRotation = transform.ValueRO.Rotation;
                    prev.IsFirstFrame = false;
                }
                // Previous transform is already set from end of last tick
            }
        }
    }

    /// <summary>
    /// System that applies ship movement delta to all occupants.
    /// Runs after ship movement but before player movement.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(Unity.Transforms.TransformSystemGroup))]
    [UpdateBefore(typeof(global::Player.Systems.CharacterControllerSystem))] // Fix 3.3.A: Prevent clipping
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct ShipInertialCorrectionSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<ShipPreviousTransform> _prevTransformLookup;
        private ComponentLookup<ShipKinematics> _kinematicsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _prevTransformLookup = state.GetComponentLookup<ShipPreviousTransform>(true);
            _kinematicsLookup = state.GetComponentLookup<ShipKinematics>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            _prevTransformLookup.Update(ref state);
            _kinematicsLookup.Update(ref state);

            // Apply inertial correction to Simulated entities (Server + Local Pilot).
            // Remote passengers (Interpolated) should relying on the replicated World Position from the server.
            // Running this on interpolated ghosts causes 'double movement' or 'propelling' due to timing mismatches.
            foreach (var (localSpace, transform, entity) in
                     SystemAPI.Query<RefRW<InShipLocalSpace>, RefRW<LocalTransform>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                if (!localSpace.ValueRO.IsAttached)
                    continue;

                Entity shipEntity = localSpace.ValueRO.ShipEntity;

                // Validate ship exists
                if (!_transformLookup.HasComponent(shipEntity))
                {
                    continue;
                }

                var shipTransform = _transformLookup[shipEntity];

                // Validate ship rotation is valid
                quaternion shipRotation = shipTransform.Rotation;
                if (!IsValidQuaternion(shipRotation))
                    shipRotation = quaternion.identity;

                // Validate local rotation is valid
                quaternion localRotation = localSpace.ValueRO.LocalRotation;
                if (!IsValidQuaternion(localRotation))
                    localRotation = quaternion.identity;

                // Get ship kinematics to check if moving
                bool shipIsMoving = false;
                if (_kinematicsLookup.HasComponent(shipEntity))
                {
                    var kinematics = _kinematicsLookup[shipEntity];
                    shipIsMoving = kinematics.IsMoving ||
                                   math.lengthsq(kinematics.LinearVelocity) > 0.001f ||
                                   math.lengthsq(kinematics.AngularVelocity) > 0.001f;
                }
                
                // Compute world position from ship transform and local position
                float4x4 shipLocalToWorld = float4x4.TRS(
                    shipTransform.Position,
                    shipRotation,
                    new float3(1, 1, 1));

                float4 localPos = new float4(localSpace.ValueRO.LocalPosition, 1f);
                float4 worldPos = math.mul(shipLocalToWorld, localPos);

                quaternion worldRotation = math.normalize(math.mul(shipRotation, localRotation));

                // Final validation
                if (!IsValidQuaternion(worldRotation))
                    worldRotation = quaternion.identity;

                // Update world transform
                ref var playerTransform = ref transform.ValueRW;
                playerTransform.Position = worldPos.xyz;
                playerTransform.Rotation = worldRotation;
            }
        }

        private static bool IsValidQuaternion(quaternion q)
        {
            float lengthSq = math.lengthsq(q.value);
            return math.isfinite(lengthSq) && lengthSq > 0.001f;
        }
    }

    /// <summary>
    /// System that captures local position after player movement.
    /// Converts world-space movement back to ship-local for persistence.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Unity.Transforms.TransformSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct ShipLocalSpaceCaptureSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);

            // Capture local position after movement for all entities in local space
            // NOTE: We only capture for Simulated entities (Server + Local Pilot).
            // Remote passengers (interpolated) should rely on the replicated LocalPosition from server.
            // If they capture locally, they introduce drift which causes them to fall out of the ship.
            foreach (var (localSpace, transform, entity) in
                     SystemAPI.Query<RefRW<InShipLocalSpace>, RefRO<LocalTransform>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                if (!localSpace.ValueRO.IsAttached)
                    continue;

                Entity shipEntity = localSpace.ValueRO.ShipEntity;

                // Validate ship exists
                if (!_transformLookup.HasComponent(shipEntity))
                    continue;

                var shipTransform = _transformLookup[shipEntity];

                // Validate ship rotation
                quaternion shipRotation = shipTransform.Rotation;
                if (!IsValidQuaternion(shipRotation))
                    continue; // Skip if ship rotation is invalid

                // Convert world position back to ship-local
                float4x4 shipWorldToLocal = math.inverse(float4x4.TRS(
                    shipTransform.Position,
                    shipRotation,
                    new float3(1, 1, 1)));

                float4 worldPos = new float4(transform.ValueRO.Position, 1f);
                float4 localPos = math.mul(shipWorldToLocal, worldPos);

                // Validate player rotation
                quaternion playerRotation = transform.ValueRO.Rotation;
                if (!IsValidQuaternion(playerRotation))
                    playerRotation = quaternion.identity;

                quaternion localRotation = math.normalize(math.mul(math.inverse(shipRotation), playerRotation));

                // Final validation
                if (!IsValidQuaternion(localRotation))
                    localRotation = quaternion.identity;

                ref var local = ref localSpace.ValueRW;
                local.LocalPosition = localPos.xyz;
                local.LocalRotation = localRotation;
            }
        }

        private static bool IsValidQuaternion(quaternion q)
        {
            float lengthSq = math.lengthsq(q.value);
            return math.isfinite(lengthSq) && lengthSq > 0.001f;
        }
    }

    /// <summary>
    /// System that stores ship transform at end of tick for next tick's delta.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct ShipTransformStoreSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Store current transform as previous for next tick
            foreach (var (transform, prevTransform, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRW<ShipPreviousTransform>>()
                     .WithAll<ShipRoot>()
                     .WithEntityAccess())
            {
                ref var prev = ref prevTransform.ValueRW;
                prev.PreviousPosition = transform.ValueRO.Position;
                prev.PreviousRotation = transform.ValueRO.Rotation;
            }
        }
    }
}
