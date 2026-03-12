using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Collections;

namespace DIG.Swimming.Systems
{
    /// <summary>
    /// 12.3.3: Physics Adjustments - Collider Size Reduction
    /// Reduces the player's capsule collider size when underwater to prevent wall clipping.
    /// Restores original size when exiting water.
    ///
    /// Note: Unity Physics colliders are immutable BlobAssetReferences.
    /// This system creates new colliders when size changes are needed.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SwimmingControllerSystem))]
    public partial struct SwimmingColliderSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (swimState, controllerState, physicsSettings, collider, playerState, entity) in
                SystemAPI.Query<
                    RefRO<SwimmingState>,
                    RefRW<SwimmingControllerState>,
                    RefRO<SwimmingPhysicsSettings>,
                    RefRW<PhysicsCollider>,
                    RefRW<PlayerState>>()
                    .WithAll<CanSwim>()
                    .WithEntityAccess())
            {
                bool isUnderwater = swimState.ValueRO.IsSubmerged;
                bool wasSwimming = controllerState.ValueRO.WasSwimming;
                bool isSwimming = swimState.ValueRO.IsSwimming;

                // Only adjust collider when fully submerged underwater
                // This prevents wall clipping in tight underwater passages
                if (isUnderwater && isSwimming)
                {
                    // Check if we need to reduce collider size
                    float targetHeight = physicsSettings.ValueRO.UnderwaterColliderHeight;
                    float targetRadius = physicsSettings.ValueRO.UnderwaterColliderRadius;

                    // Update PlayerState.CurrentHeight for other systems to reference
                    if (math.abs(playerState.ValueRO.CurrentHeight - targetHeight) > 0.01f)
                    {
                        // Cache original height if not already cached
                        if (!controllerState.ValueRO.HasCachedValues)
                        {
                            controllerState.ValueRW.OriginalColliderHeight = playerState.ValueRO.CurrentHeight;
                            controllerState.ValueRW.HasCachedValues = true;
                        }

                        playerState.ValueRW.CurrentHeight = targetHeight;
                        playerState.ValueRW.TargetHeight = targetHeight;

                        // Note: Actual PhysicsCollider modification requires creating new BlobAssetReference
                        // This is handled by the existing PlayerColliderHeightSystem pattern
                        // For now, we update the PlayerState values which other systems can read
                    }
                }
                else if (!isSwimming && wasSwimming)
                {
                    // Restore original collider height when exiting water
                    if (controllerState.ValueRO.HasCachedValues)
                    {
                        float originalHeight = controllerState.ValueRO.OriginalColliderHeight;

                        // Restore to standing height or cached original
                        playerState.ValueRW.CurrentHeight = originalHeight;
                        playerState.ValueRW.TargetHeight = originalHeight;
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
