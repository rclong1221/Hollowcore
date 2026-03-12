using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Interaction.Authoring
{
    /// <summary>
    /// EPIC 13.17.2: Authoring component for IK targets on interactable objects.
    /// Add this to interactables that should position the player's hands during interaction.
    /// </summary>
    public class InteractableIKTargetAuthoring : MonoBehaviour
    {
        [Header("Hand Configuration")]
        [Tooltip("Which hand(s) should reach for this target")]
        public HandIKGoal Goal = HandIKGoal.RightHand;

        [Header("Left Hand")]
        [Tooltip("Transform defining left hand position/rotation (leave empty to use offset)")]
        public Transform LeftHandTarget;

        [Tooltip("Local position offset from interactable center (used if no target transform)")]
        public Vector3 LeftHandPositionOffset = new Vector3(-0.1f, 0.5f, 0.2f);

        [Tooltip("Local rotation offset for left hand grip")]
        public Vector3 LeftHandRotationEuler = Vector3.zero;

        [Header("Right Hand")]
        [Tooltip("Transform defining right hand position/rotation (leave empty to use offset)")]
        public Transform RightHandTarget;

        [Tooltip("Local position offset from interactable center (used if no target transform)")]
        public Vector3 RightHandPositionOffset = new Vector3(0.1f, 0.5f, 0.2f);

        [Tooltip("Local rotation offset for right hand grip")]
        public Vector3 RightHandRotationEuler = Vector3.zero;

        [Header("Timing")]
        [Tooltip("Delay in seconds before IK engages after interaction starts")]
        [Range(0f, 2f)]
        public float Delay = 0f;

        [Tooltip("Duration of active IK (0 = until interaction ends)")]
        [Range(0f, 10f)]
        public float Duration = 0f;

        [Header("Speed")]
        [Tooltip("Override interpolation speed (0 = use default from settings)")]
        [Range(0f, 50f)]
        public float InterpolationSpeed = 0f;

        private void OnDrawGizmosSelected()
        {
            // Draw left hand target
            if (Goal == HandIKGoal.LeftHand || Goal == HandIKGoal.BothHands)
            {
                Gizmos.color = Color.blue;
                Vector3 leftPos = LeftHandTarget != null
                    ? LeftHandTarget.position
                    : transform.TransformPoint(LeftHandPositionOffset);
                Gizmos.DrawWireSphere(leftPos, 0.05f);
                Gizmos.DrawLine(transform.position, leftPos);

                // Draw rotation indicator
                Quaternion leftRot = LeftHandTarget != null
                    ? LeftHandTarget.rotation
                    : transform.rotation * Quaternion.Euler(LeftHandRotationEuler);
                Gizmos.DrawRay(leftPos, leftRot * Vector3.forward * 0.1f);
            }

            // Draw right hand target
            if (Goal == HandIKGoal.RightHand || Goal == HandIKGoal.BothHands)
            {
                Gizmos.color = Color.red;
                Vector3 rightPos = RightHandTarget != null
                    ? RightHandTarget.position
                    : transform.TransformPoint(RightHandPositionOffset);
                Gizmos.DrawWireSphere(rightPos, 0.05f);
                Gizmos.DrawLine(transform.position, rightPos);

                // Draw rotation indicator
                Quaternion rightRot = RightHandTarget != null
                    ? RightHandTarget.rotation
                    : transform.rotation * Quaternion.Euler(RightHandRotationEuler);
                Gizmos.DrawRay(rightPos, rightRot * Vector3.forward * 0.1f);
            }
        }

        public class Baker : Baker<InteractableIKTargetAuthoring>
        {
            public override void Bake(InteractableIKTargetAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Calculate local offsets from transforms if provided
                float3 leftOffset = authoring.LeftHandPositionOffset;
                quaternion leftRot = quaternion.Euler(math.radians(authoring.LeftHandRotationEuler));

                if (authoring.LeftHandTarget != null)
                {
                    // Convert world position to local offset
                    leftOffset = authoring.transform.InverseTransformPoint(authoring.LeftHandTarget.position);
                    // Convert world rotation to local rotation
                    leftRot = math.mul(
                        math.inverse(authoring.transform.rotation),
                        authoring.LeftHandTarget.rotation);
                }

                float3 rightOffset = authoring.RightHandPositionOffset;
                quaternion rightRot = quaternion.Euler(math.radians(authoring.RightHandRotationEuler));

                if (authoring.RightHandTarget != null)
                {
                    rightOffset = authoring.transform.InverseTransformPoint(authoring.RightHandTarget.position);
                    rightRot = math.mul(
                        math.inverse(authoring.transform.rotation),
                        authoring.RightHandTarget.rotation);
                }

                AddComponent(entity, new InteractableIKTarget
                {
                    Goal = authoring.Goal,
                    LeftHandPositionOffset = leftOffset,
                    LeftHandRotation = leftRot,
                    RightHandPositionOffset = rightOffset,
                    RightHandRotation = rightRot,
                    Delay = authoring.Delay,
                    Duration = authoring.Duration,
                    InterpolationSpeed = authoring.InterpolationSpeed
                });
            }
        }
    }
}
