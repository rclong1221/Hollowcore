using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using DIG.Player.IK;
using DIG.Player.Components; // For PlayerState
using Player.Components; // For PlayerTag

namespace DIG.Player.Systems.IK
{
    /// <summary>
    /// Calculates foot IK targets based on ground raycasts.
    /// Client-only since this is purely a visual effect.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))] 
    public partial struct FootIKSystem : ISystem
    {
        private const bool DebugEnabled = false;
        private int _frameCounter;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<NetworkStreamInGame>();
        }

        public void OnUpdate(ref SystemState state)
        {
            _frameCounter++;
            float deltaTime = SystemAPI.Time.DeltaTime;
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var collisionWorld = physicsWorld.CollisionWorld;
            bool foundAny = false;

            foreach (var (settings, ikState, transform, playerState, entity) in 
                     SystemAPI.Query<RefRO<FootIKSettings>, RefRW<FootIKState>, RefRO<LocalTransform>, RefRO<PlayerState>>()
                         .WithAll<PlayerTag>()
                         .WithEntityAccess())
            {
                foundAny = true;
                ref var stateRef = ref ikState.ValueRW;
                var set = settings.ValueRO;

                // 1. Weight Blending
                // Only IK when grounded or close to ground?
                // Opsive usually keeps IK active but blends out when jumping.
                // We use isGrounded from PlayerState.
                bool isGrounded = playerState.ValueRO.IsGrounded;
                float targetWeight = isGrounded ? set.FootIKWeight : 0f;
                // Blend out faster if airborne (to avoid sticky legs)
                float blendSpeed = isGrounded ? set.BlendSpeed : set.BlendSpeed * 2f; 
                
                stateRef.LeftFootWeight = math.lerp(stateRef.LeftFootWeight, targetWeight, deltaTime * blendSpeed);
                stateRef.RightFootWeight = math.lerp(stateRef.RightFootWeight, targetWeight, deltaTime * blendSpeed);

                // Optimization: Skip if weights are negligible
                if (stateRef.LeftFootWeight < 0.01f && stateRef.RightFootWeight < 0.01f)
                {
                    stateRef.BodyOffset = math.lerp(stateRef.BodyOffset, 0, deltaTime * set.BlendSpeed);
                    stateRef.LeftFootWeight = 0;
                    stateRef.RightFootWeight = 0;
                    continue;
                }

                // 2. Prepare Raycasts
                // Transform points are relative to Root Position + Rotation
                float3 rootPos = transform.ValueRO.Position;
                quaternion rootRot = transform.ValueRO.Rotation;

                // Ray origins: Start from knee height above the foot position offset
                // This ensures we cast through where the foot should be
                float rayStartHeight = 0.5f; // Start from about knee height
                float3 leftOrigin = rootPos + math.rotate(rootRot, set.LeftFootPosOffset) + new float3(0, rayStartHeight, 0); 
                float3 rightOrigin = rootPos + math.rotate(rootRot, set.RightFootPosOffset) + new float3(0, rayStartHeight, 0);

                float rayDist = set.FootRayLength + rayStartHeight;
                
                // Collision Filter: Only collide with ground/environment (layer 0 = Default, layer 6 = Ground typically)
                // Exclude layer 3 (Player layer) to avoid hitting self
                // BelongsTo: All layers, CollidesWith: All except player (layer 3)
                CollisionFilter filter = new CollisionFilter
                {
                    BelongsTo = ~0u,                    // Belongs to all layers
                    CollidesWith = ~(1u << 3),         // Collide with all except layer 3 (Player)
                    GroupIndex = 0
                };

                // 3. Process Feet
                bool leftHit = ProcessFoot(collisionWorld, leftOrigin, rayDist, filter, rootRot, set.FootOffset, 
                    ref stateRef.LeftFootTarget, ref stateRef.LeftFootRotation);
                    
                bool rightHit = ProcessFoot(collisionWorld, rightOrigin, rayDist, filter, rootRot, set.FootOffset, 
                    ref stateRef.RightFootTarget, ref stateRef.RightFootRotation);

                // Debug logging every 300 frames
                if (DebugEnabled && _frameCounter % 300 == 0)
                {
                    UnityEngine.Debug.Log($"[FootIK] System: Grounded={isGrounded} LWeight={stateRef.LeftFootWeight:F2} RWeight={stateRef.RightFootWeight:F2} LHit={leftHit} RHit={rightHit} BodyOffset={stateRef.BodyOffset:F3}");
                }

                // 4. Calculate Pelvis Offset (Body Lowering)
                // Find how much lower the feet are compared to the root
                // The root is at rootPos.y. The expected foot height is rootPos.y - (distance_from_hip_to_foot?)
                // Actually, simple approach: Find the lowest foot relative to the root.
                // If a foot is ABOVE reference (step up), we don't need to lower body (Anim IK raises leg).
                // If a foot is BELOW reference (step down/slope), we MUST lower body to reach it.
                
                // Opsive Logic:
                // Base foot height = rootPos.y.
                // If TargetY < rootPos.y, we need to drop.
                // But we only drop if BOTH feet are low? Or if the LOWEST foot is low?
                // If one foot is on a step (High) and one on ground (0), Body stays at 0.
                // If one foot is on ground (0) and one in a hole (-0.5), Body must drop to reach -0.5?
                // No, typically we drop the Hips so the shortest leg can reach.
                // Wait, if leg is fully extended, we drop hips.
                // Let's use the min Y.
                
                float leftY = stateRef.LeftFootTarget.y;
                float rightY = stateRef.RightFootTarget.y;
                float lowestY = math.min(leftY, rightY);
                
                // Expected 'Ground' relative to Root is usually 0 (if pivots are at feet) or offset
                // Assuming Root Pivot is at bottom of feet.
                float groundY = rootPos.y;
                
                // Calculate required drop
                float requiredDrop = 0f;
                if (lowestY < groundY)
                {
                     requiredDrop = groundY - lowestY;
                }
                
                // Clamp drop to limit
                requiredDrop = math.min(requiredDrop, set.BodyHeightAdjustment);
                
                // Smooth BodyOffset
                stateRef.BodyOffset = math.lerp(stateRef.BodyOffset, -requiredDrop, deltaTime * set.BlendSpeed);
            }
            
            // Warn if no entities found (once, at startup)
            if (DebugEnabled && !foundAny && _frameCounter == 60)
            {
                UnityEngine.Debug.LogWarning("[FootIK] FootIKSystem found no entities with FootIKSettings + FootIKState + PlayerState + PlayerTag!");
            }
        }

        private bool ProcessFoot(CollisionWorld world, float3 origin, float dist, CollisionFilter filter, quaternion rootRot, float footOffset, 
            ref float3 targetPos, ref quaternion targetRot)
        {
            if (!math.all(math.isfinite(origin))) return false;
            var input = new RaycastInput
            {
                Start = origin,
                End = origin - new float3(0, dist, 0),
                Filter = filter
            };

            if (world.CastRay(input, out var hit))
            {
                // Found ground
                targetPos = hit.Position + new float3(0, footOffset, 0);
                
                // Rotate foot to match ground normal
                float3 normal = hit.SurfaceNormal;
                float3 up = math.up();
                
                // Calculate rotation that aligns foot's up vector to surface normal
                float dot = math.dot(normal, up);
                if (dot > 0.001f) // Not perpendicular/underside
                {
                    float3 axis = math.cross(up, normal);
                    float axisLength = math.length(axis);
                    if (axisLength > 0.001f)
                    {
                        quaternion alignRot = quaternion.AxisAngle(axis / axisLength, math.acos(math.clamp(dot, -1f, 1f)));
                        targetRot = math.mul(alignRot, rootRot);
                    }
                    else
                    {
                        targetRot = rootRot;
                    }
                }
                else
                {
                    targetRot = rootRot;
                }
                return true;
            }
            else
            {
                // No hit: Keep foot extended downward
                targetPos = origin - new float3(0, dist - footOffset, 0); 
                targetRot = rootRot;
                return false;
            }
        }
    }
}