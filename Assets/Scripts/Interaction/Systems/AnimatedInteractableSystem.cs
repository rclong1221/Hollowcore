using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// Handles animated interactables like doors and levers.
    /// EPIC 13.17.6-13.17.9: Enhanced with audio cycling, state reset,
    /// multi-switch groups, and animator parameter support.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct AnimatedInteractableSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // --- EPIC 13.17.7: Process Reset Requests ---
            ProcessResetRequests(ref state, ref ecb);

            // Process door animations
            foreach (var (animated, door, transform, entity) in
                     SystemAPI.Query<RefRW<AnimatedInteractable>, RefRW<DoorInteractable>, RefRW<LocalTransform>>()
                     .WithEntityAccess())
            {
                ref var animRef = ref animated.ValueRW;
                ref var doorRef = ref door.ValueRW;
                ref var transformRef = ref transform.ValueRW;

                if (animRef.IsAnimating)
                {
                    animRef.CurrentTime += deltaTime;
                    float progress = math.saturate(animRef.CurrentTime / animRef.AnimationDuration);

                    // Calculate target angle
                    float startAngle = animRef.IsOpen ? doorRef.ClosedAngle : doorRef.OpenAngle;
                    float endAngle = animRef.IsOpen ? doorRef.OpenAngle : doorRef.ClosedAngle;
                    float currentAngle = math.lerp(startAngle, endAngle, progress);

                    // Apply rotation (assuming Y-axis rotation for doors)
                    transformRef.Rotation = quaternion.Euler(0, math.radians(currentAngle), 0);

                    // Check if animation complete
                    if (progress >= 1f)
                    {
                        animRef.IsAnimating = false;
                        animRef.CurrentTime = 0f;

                        // Reset auto-close timer if door is now open
                        if (animRef.IsOpen && doorRef.AutoClose)
                        {
                            doorRef.TimeSinceOpened = 0f;
                        }
                    }
                }
                else if (animRef.IsOpen && doorRef.AutoClose)
                {
                    // Handle auto-close
                    doorRef.TimeSinceOpened += deltaTime;
                    if (doorRef.TimeSinceOpened >= doorRef.AutoCloseDelay)
                    {
                        animRef.IsOpen = false;
                        animRef.IsAnimating = true;
                        animRef.CurrentTime = 0f;
                    }
                }
            }

            // Process lever animations
            foreach (var (animated, lever, transform, entity) in
                     SystemAPI.Query<RefRW<AnimatedInteractable>, RefRO<LeverInteractable>, RefRW<LocalTransform>>()
                     .WithNone<DoorInteractable>()
                     .WithEntityAccess())
            {
                ref var animRef = ref animated.ValueRW;
                ref var transformRef = ref transform.ValueRW;

                if (animRef.IsAnimating)
                {
                    animRef.CurrentTime += deltaTime;
                    float progress = math.saturate(animRef.CurrentTime / animRef.AnimationDuration);

                    // Lever rotation (typically X-axis for forward/back lever)
                    float startAngle = animRef.IsOpen ? -30f : 30f;
                    float endAngle = animRef.IsOpen ? 30f : -30f;
                    float currentAngle = math.lerp(startAngle, endAngle, progress);

                    transformRef.Rotation = quaternion.Euler(math.radians(currentAngle), 0, 0);

                    if (progress >= 1f)
                    {
                        animRef.IsAnimating = false;
                        animRef.CurrentTime = 0f;
                    }
                }
            }

            // Process generic animated interactables (without door/lever components)
            foreach (var (animated, entity) in
                     SystemAPI.Query<RefRW<AnimatedInteractable>>()
                     .WithNone<DoorInteractable, LeverInteractable>()
                     .WithEntityAccess())
            {
                ref var animRef = ref animated.ValueRW;

                if (animRef.IsAnimating)
                {
                    animRef.CurrentTime += deltaTime;
                    if (animRef.CurrentTime >= animRef.AnimationDuration)
                    {
                        animRef.IsAnimating = false;
                        animRef.CurrentTime = 0f;
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        /// <summary>
        /// EPIC 13.17.7: Process reset requests for interactables.
        /// Resets HasInteracted, AudioClipIndex, and removes the request.
        /// </summary>
        [BurstCompile]
        private void ProcessResetRequests(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            foreach (var (animated, entity) in
                     SystemAPI.Query<RefRW<AnimatedInteractable>>()
                     .WithAll<ResetInteractableRequest>()
                     .WithEntityAccess())
            {
                ref var animRef = ref animated.ValueRW;

                // Reset all interaction state
                animRef.HasInteracted = false;
                animRef.AudioClipIndex = -1;
                animRef.IsOpen = false;
                animRef.IsAnimating = false;
                animRef.CurrentTime = 0f;
                animRef.IsActiveBoolInteractable = false;

                // Remove the request component
                ecb.RemoveComponent<ResetInteractableRequest>(entity);
            }
        }
    }

    /// <summary>
    /// EPIC 13.17.8: Handles multi-switch exclusive state logic.
    /// When an interactable in a group is activated, others in the
    /// same group are deactivated.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InteractAbilitySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct MultiSwitchGroupSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Find newly activated interactables in groups
            foreach (var (animated, interactable, entity) in
                     SystemAPI.Query<RefRO<AnimatedInteractable>, RefRO<Interactable>>()
                     .WithEntityAccess())
            {
                // Skip if not in a group or not the active one
                if (animated.ValueRO.SwitchGroupID == 0)
                    continue;

                if (!animated.ValueRO.IsActiveBoolInteractable)
                    continue;

                int groupId = animated.ValueRO.SwitchGroupID;

                // Deactivate other interactables in the same group
                DeactivateOthersInGroup(ref state, entity, groupId);
            }
        }

        [BurstCompile]
        private void DeactivateOthersInGroup(ref SystemState state, Entity activeEntity, int groupId)
        {
            foreach (var (animated, entity) in
                     SystemAPI.Query<RefRW<AnimatedInteractable>>()
                     .WithEntityAccess())
            {
                // Skip the active entity
                if (entity == activeEntity)
                    continue;

                // Skip if not in the same group
                if (animated.ValueRO.SwitchGroupID != groupId)
                    continue;

                // Skip if already inactive
                if (!animated.ValueRO.IsActiveBoolInteractable)
                    continue;

                // Deactivate this one
                ref var animRef = ref animated.ValueRW;
                animRef.IsActiveBoolInteractable = false;

                // If it's a toggle type, flip the state back
                if (animRef.ToggleBoolValue && animRef.IsOpen)
                {
                    animRef.IsOpen = false;
                    animRef.IsAnimating = true;
                    animRef.CurrentTime = 0f;
                }
            }
        }
    }
}
