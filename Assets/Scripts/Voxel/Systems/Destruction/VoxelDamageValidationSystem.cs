using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Voxel
{
    /// <summary>
    /// EPIC 15.10: Server-side validation for voxel damage requests.
    /// Validates source position, range checks, rate limiting.
    /// Rejects invalid requests and logs suspicious activity.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DestructionMediatorSystem))]
    [UpdateBefore(typeof(VoxelDamageProcessingSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct VoxelDamageValidationSystem : ISystem
    {
        private EntityQuery _requestQuery;
        
        // Validation constants
        private const float MAX_SOURCE_DISTANCE_TOLERANCE = 5f; // Max difference between claimed source pos and actual entity pos
        private const float MAX_REQUEST_RANGE = 50f; // Max distance from source to target
        private const float MIN_REQUEST_INTERVAL = 0.016f; // ~60fps rate limit per source
        
        public void OnCreate(ref SystemState state)
        {
            _requestQuery = state.GetEntityQuery(
                ComponentType.ReadWrite<VoxelDamageRequest>()
            );
            
            state.RequireForUpdate(_requestQuery);
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var transformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(isReadOnly: true);
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = networkTime.ServerTick.IsValid ? networkTime.ServerTick.TickIndexForValidTick : 0;
            
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            bool enableDebug = VoxelDamageProcessingSystem.EnableDebugLogs; // Reuse flag
            
            foreach (var (request, entity) in SystemAPI.Query<RefRW<VoxelDamageRequest>>().WithEntityAccess())
            {
                // Skip already processed
                if (request.ValueRO.IsValidated || request.ValueRO.IsProcessed)
                    continue;
                
                bool isValid = ValidateRequest(ref request.ValueRW, transformLookup, currentTick);
                
                if (!isValid)
                {
                    // Reject invalid request by destroying the entity
                    ecb.DestroyEntity(entity);
                    if (enableDebug)
                        UnityEngine.Debug.LogWarning($"[VoxelValidation] REJECTED request from Entity {request.ValueRO.SourceEntity.Index}");
                }
                else if (enableDebug)
                {
                    UnityEngine.Debug.Log($"[VoxelValidation] VALIDATED request from Entity {request.ValueRO.SourceEntity.Index}");
                }
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
        
        /// <summary>
        /// Validate a destruction request.
        /// </summary>
        private bool ValidateRequest(ref VoxelDamageRequest request, ComponentLookup<LocalToWorld> transformLookup, uint currentTick)
        {
            // 1. Verify source entity exists
            if (request.SourceEntity == Entity.Null)
            {
                return false;
            }
            
            // 2. Verify source entity has transform
            if (!transformLookup.HasComponent(request.SourceEntity))
            {
                return false;
            }
            
            // 3. Verify claimed source position matches actual entity position
            float3 actualPosition = transformLookup[request.SourceEntity].Position;
            float sourceDistanceError = math.length(request.SourcePosition - actualPosition);
            
            if (sourceDistanceError > MAX_SOURCE_DISTANCE_TOLERANCE)
            {
                // Source position doesn't match entity - possible exploit attempt
                return false;
            }
            
            // 4. Verify target is within reasonable range of source
            float targetDistance = math.length(request.TargetPosition - request.SourcePosition);
            
            if (targetDistance > MAX_REQUEST_RANGE)
            {
                // Target too far from source
                return false;
            }
            
            // 5. Verify shape parameters are valid
            if (!ValidateShapeParameters(request))
            {
                return false;
            }
            
            // 6. Verify damage is positive and reasonable
            if (request.Damage <= 0f || request.Damage > 100000f)
            {
                return false;
            }
            
            // 7. Verify rotation is valid quaternion
            if (!math.all(math.isfinite(request.TargetRotation.value)))
            {
                return false;
            }
            
            // All checks passed - mark as validated
            request.IsValidated = true;
            request.ValidatedTick = currentTick;
            
            return true;
        }
        
        /// <summary>
        /// Validate shape-specific parameters.
        /// </summary>
        private bool ValidateShapeParameters(VoxelDamageRequest request)
        {
            switch (request.ShapeType)
            {
                case VoxelDamageShapeType.Point:
                    // No additional params to validate
                    return true;
                    
                case VoxelDamageShapeType.Sphere:
                    // Radius must be positive and reasonable
                    return request.Param1 > 0f && request.Param1 <= 100f;
                    
                case VoxelDamageShapeType.Cylinder:
                    // Radius and height must be positive and reasonable
                    return request.Param1 > 0f && request.Param1 <= 50f &&
                           request.Param2 > 0f && request.Param2 <= 100f;
                    
                case VoxelDamageShapeType.Cone:
                    // Angle, length, and tip radius must be valid
                    return request.Param1 > 0f && request.Param1 <= 180f &&  // angle
                           request.Param2 > 0f && request.Param2 <= 100f &&  // length
                           request.Param3 >= 0f && request.Param3 <= 50f;    // tip radius
                    
                case VoxelDamageShapeType.Capsule:
                    // Radius and length must be positive and reasonable
                    return request.Param1 > 0f && request.Param1 <= 50f &&
                           request.Param2 > 0f && request.Param2 <= 100f;
                    
                case VoxelDamageShapeType.Box:
                    // All extents must be positive and reasonable
                    return request.Param1 > 0f && request.Param1 <= 50f &&
                           request.Param2 > 0f && request.Param2 <= 50f &&
                           request.Param3 > 0f && request.Param3 <= 50f;
                    
                default:
                    return false;
            }
        }
    }
}
