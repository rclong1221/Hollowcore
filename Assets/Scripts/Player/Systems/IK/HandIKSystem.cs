using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Player.IK;
using DIG.Interaction;

namespace DIG.Player.Systems.IK
{
    /// <summary>
    /// EPIC 13.17.2: Hand IK System for interaction positioning.
    ///
    /// This system calculates hand target positions based on:
    /// 1. Active interactions with InteractableIKTarget
    /// 2. Weapon/item left-hand grips (future)
    ///
    /// Burst-compiled and runs in PresentationSystemGroup.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(IKBlendSystem))]
    public partial struct HandIKSystem : ISystem
    {
        private const float DefaultInterpolationSpeed = 10f;
        private const float DefaultBlendSpeed = 5f;
        private const float DefaultWeight = 1f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Process entities with hand IK state and interact ability
            foreach (var (handState, handSettings, interactAbility, transform, entity) in
                     SystemAPI.Query<RefRW<HandIKState>, RefRO<HandIKSettings>, RefRO<InteractAbility>, RefRO<LocalTransform>>()
                     .WithEntityAccess())
            {
                ProcessHandIK(ref state, ref handState.ValueRW, handSettings.ValueRO,
                    interactAbility.ValueRO, transform.ValueRO, deltaTime);
            }

            // Process entities with hand IK state but no settings (use defaults)
            foreach (var (handState, interactAbility, transform, entity) in
                     SystemAPI.Query<RefRW<HandIKState>, RefRO<InteractAbility>, RefRO<LocalTransform>>()
                     .WithNone<HandIKSettings>()
                     .WithEntityAccess())
            {
                var defaultSettings = new HandIKSettings
                {
                    InterpolationSpeed = DefaultInterpolationSpeed,
                    BlendSpeed = DefaultBlendSpeed,
                    DefaultWeight = DefaultWeight,
                    MaxReachDistance = 2f
                };
                ProcessHandIK(ref state, ref handState.ValueRW, defaultSettings,
                    interactAbility.ValueRO, transform.ValueRO, deltaTime);
            }
        }

        [BurstCompile]
        private void ProcessHandIK(ref SystemState state, ref HandIKState handState,
            HandIKSettings settings, InteractAbility interactAbility, LocalTransform transform, float deltaTime)
        {
            // Check if interacting with something that has IK targets
            bool hasIKTarget = false;
            InteractableIKTarget ikTarget = default;
            LocalTransform targetTransform = default;

            if (interactAbility.IsInteracting && interactAbility.TargetEntity != Entity.Null)
            {
                if (SystemAPI.HasComponent<InteractableIKTarget>(interactAbility.TargetEntity))
                {
                    ikTarget = SystemAPI.GetComponent<InteractableIKTarget>(interactAbility.TargetEntity);
                    hasIKTarget = ikTarget.Goal != DIG.Interaction.HandIKGoal.None;

                    if (hasIKTarget && SystemAPI.HasComponent<LocalTransform>(interactAbility.TargetEntity))
                    {
                        targetTransform = SystemAPI.GetComponent<LocalTransform>(interactAbility.TargetEntity);
                    }
                }
            }

            // Get interpolation speed
            float interpSpeed = settings.InterpolationSpeed;
            if (hasIKTarget && ikTarget.InterpolationSpeed > 0)
            {
                interpSpeed = ikTarget.InterpolationSpeed;
            }

            // Calculate target weights and positions
            float blendSpeed = settings.BlendSpeed;

            // --- Left Hand ---
            bool wantsLeftHand = hasIKTarget &&
                                 (ikTarget.Goal == DIG.Interaction.HandIKGoal.LeftHand || ikTarget.Goal == DIG.Interaction.HandIKGoal.BothHands);

            if (wantsLeftHand)
            {
                // Calculate world position from local offset (inlined for Burst compatibility)
                float3 worldPos = targetTransform.Position + math.mul(targetTransform.Rotation, ikTarget.LeftHandPositionOffset);
                quaternion worldRot = math.mul(targetTransform.Rotation, ikTarget.LeftHandRotation);

                // Interpolate position
                handState.LeftHandTarget = math.lerp(handState.LeftHandTarget, worldPos, interpSpeed * deltaTime);
                handState.LeftHandRotation = math.slerp(handState.LeftHandRotation, worldRot, interpSpeed * deltaTime);
                handState.LeftHandTargetWeight = settings.DefaultWeight;
                handState.HasLeftTarget = true;
            }
            else
            {
                handState.LeftHandTargetWeight = 0f;
                handState.HasLeftTarget = false;
            }

            // Blend weight
            handState.LeftHandWeight = math.lerp(handState.LeftHandWeight,
                handState.LeftHandTargetWeight, blendSpeed * deltaTime);

            // Snap to zero if very small
            if (handState.LeftHandWeight < 0.01f)
            {
                handState.LeftHandWeight = 0f;
            }

            // --- Right Hand ---
            bool wantsRightHand = hasIKTarget &&
                                  (ikTarget.Goal == DIG.Interaction.HandIKGoal.RightHand || ikTarget.Goal == DIG.Interaction.HandIKGoal.BothHands);

            if (wantsRightHand)
            {
                // Calculate world position from local offset (inlined for Burst compatibility)
                float3 worldPos = targetTransform.Position + math.mul(targetTransform.Rotation, ikTarget.RightHandPositionOffset);
                quaternion worldRot = math.mul(targetTransform.Rotation, ikTarget.RightHandRotation);

                // Interpolate position
                handState.RightHandTarget = math.lerp(handState.RightHandTarget, worldPos, interpSpeed * deltaTime);
                handState.RightHandRotation = math.slerp(handState.RightHandRotation, worldRot, interpSpeed * deltaTime);
                handState.RightHandTargetWeight = settings.DefaultWeight;
                handState.HasRightTarget = true;
            }
            else
            {
                handState.RightHandTargetWeight = 0f;
                handState.HasRightTarget = false;
            }

            // Blend weight
            handState.RightHandWeight = math.lerp(handState.RightHandWeight,
                handState.RightHandTargetWeight, blendSpeed * deltaTime);

            // Snap to zero if very small
            if (handState.RightHandWeight < 0.01f)
            {
                handState.RightHandWeight = 0f;
            }
        }
    }
}
