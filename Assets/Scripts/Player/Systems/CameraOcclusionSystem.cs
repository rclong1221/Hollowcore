using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Player.Components;
using DIG.Player.Components;
using Unity.NetCode;

namespace Player.Systems
{
    /// <summary>
    /// Checks for objects blocking the camera's view of the player.
    /// Runs AFTER PlayerCameraControlSystem when the final camera position is known.
    /// Currently logs occlusions; will handle Object Fading in the future.
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerCameraControlSystem))]
    public unsafe partial struct CameraOcclusionSystem : ISystem
    {
        private int _logFrameCounter;
        private const float OC_RADIUS = 0.2f; // Camera Radius
        private const float MIN_DIST = 0.5f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            _logFrameCounter = 0;
        }

        public void OnUpdate(ref SystemState state)
        {
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            var deltaTime = SystemAPI.Time.DeltaTime;
            
            // Mask: Blocked by Environment, Default props, and Ship hull
            // Ignore Players, Triggers, etc.
            uint occlusionMask = CollisionLayers.Environment | CollisionLayers.Default | CollisionLayers.Ship;
            
            CollisionFilter filter = new CollisionFilter
            {
                BelongsTo = CollisionLayers.Everything,
                CollidesWith = occlusionMask,
                GroupIndex = 0
            };

            foreach (var (settings, target, viewConfig, transform, entity) in 
                SystemAPI.Query<RefRO<PlayerCameraSettings>, RefRW<CameraTarget>, RefRO<CameraViewConfig>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                // Calculate Head Position (Pivot)
                float3 pivotOffset = viewConfig.ValueRO.ActiveViewType == CameraViewType.Combat 
                    ? viewConfig.ValueRO.CombatPivotOffset 
                    : viewConfig.ValueRO.AdventurePivotOffset;
                
                float3 headPos = transform.ValueRO.Position + pivotOffset;
                
                // Calculate Ideal Camera Position based on TargetDistance (What we WANT)
                // We use the 'CurrentDistance' from settings which represents the interpolated desired distance
                // calculated by the CameraControlSystem.
                
                float desiredDist = settings.ValueRO.CurrentDistance; 
                
                // Construct ideal vector
                quaternion camRot = quaternion.Euler(math.radians(settings.ValueRO.Pitch), math.radians(settings.ValueRO.Yaw), 0f);
                float3 camDir = math.mul(camRot, new float3(0, 0, -1)); // Vector pointing BEHIND player
                
                // SphereCast Input
                float3 start = headPos;
                float3 direction = camDir;
                float distance = desiredDist;
                
                // Safety check: specific minimum distance for cast to avoid immediate self-collision
                if (distance < 0.1f) continue;

                // Mask is already set up in 'filter'

                // Let's use CastCollider (SphereCast)
                Unity.Physics.SphereGeometry sphereGeometry = new Unity.Physics.SphereGeometry { Radius = OC_RADIUS };
                
                // Create a temporary BlobAsset for the collider
                BlobAssetReference<Unity.Physics.Collider> sphereBlob = Unity.Physics.SphereCollider.Create(sphereGeometry, CollisionFilter.Default);
                
                ColliderCastInput castInput = new ColliderCastInput
                {
                    Collider = (Unity.Physics.Collider*)sphereBlob.GetUnsafePtr(), 
                    Orientation = quaternion.identity,
                    Start = start,
                    End = start + (direction * distance)
                };
                
                if (physicsWorld.CastCollider(castInput, out ColliderCastHit hit))
                {
                    // Hit something!
                    float hitDist = hit.Fraction * distance;
                    
                    // Clamp distance
                    float clampedDist = math.max(MIN_DIST, hitDist - 0.2f);
                    
                    // Apply clustered position to CameraTarget
                    // We re-calculate the position based on the clamped distance
                    // Same logic as CameraControl: Pos = Head + Dir * Dist
                    // Note: CameraControl adds offsets (like shoulder offset). 
                    // Ideally we should occlude from the Final Camera Position towards the Head?
                    // But Head->Camera is safer for preventing clipping.
                    
                    // Reconstructing exact camera pos from Head + Dir * Dist ignores Shoulder Offset...
                    // But simpler is robust. Let's place it along the center line.
                    // Or we could project the Current Target Position towards the Head?
                    
                    // Refined approach:
                    // The 'direction' vector is the central axis.
                    // If we just shorten along that axis, we lose the shoulder offset?
                    // Actually, let's just scale the vector from Head to CurrentTargetPos?
                    // No, 'desiredDist' was from settings. 
                    
                    // Let's stick to Head -> BackVector * clampedDist for now. 
                    // This creates a "center" zoom which is good for avoiding clipping (moves away from walls).
                    
                    // Add back the combat offset if needed? Usually occlusion disables offset to fit in tight spaces.
                    
                    float3 newPos = headPos + (direction * clampedDist);
                    target.ValueRW.Position = newPos;
                    
                    // Note: We do NOT update settings.CurrentDistance. 
                    // This allows the camera to naturally "spring back" when occlusion clears,
                    // because CameraControlSystem will continue writing the desired position next frame,
                    // and we will stop clamping it.
                }
                
                // CRITICAL: Dispose the temporary blob to prevent memory leaks
                sphereBlob.Dispose();
            }
            
            _logFrameCounter++;
        }
    }
}
