using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Ship.Airlocks
{
    /// <summary>
    /// Authoring component for individual airlock doors.
    /// Use this for custom door setups with animation.
    /// NOTE: Add GhostAuthoringComponent manually for networking.
    /// </summary>
    public class AirlockDoorAuthoring : MonoBehaviour
    {
        [Header("Door Settings")]
        [Tooltip("Which side of the airlock this door is on")]
        public DoorSide DoorSide = DoorSide.Interior;

        [Tooltip("Reference to the parent airlock")]
        public AirlockAuthoring ParentAirlock;

        [Header("Animation")]
        [Tooltip("Type of animation for this door")]
        public DoorAnimationType AnimationType = DoorAnimationType.Slide;

        [Tooltip("Speed of door animation")]
        [Range(1f, 20f)]
        public float AnimationSpeed = 5f;

        [Tooltip("Direction to slide when opening (normalized)")]
        public Vector3 OpenDirection = Vector3.right;

        [Tooltip("Distance to slide when fully open")]
        [Range(0.5f, 5f)]
        public float OpenDistance = 2f;

        [Tooltip("Angle in degrees to rotate when fully open (for Rotate type)")]
        [Range(0f, 180f)]
        public float OpenAngle = 90f;

        private void OnDrawGizmosSelected()
        {
            if (AnimationType == DoorAnimationType.Slide)
            {
                // Draw slide direction and distance
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(transform.position, OpenDirection.normalized * OpenDistance);
                Gizmos.DrawWireSphere(transform.position + OpenDirection.normalized * OpenDistance, 0.1f);
            }
            else if (AnimationType == DoorAnimationType.Rotate)
            {
                // Draw rotation arc
                Gizmos.color = Color.yellow;
                Vector3 start = transform.forward;
                Vector3 end = Quaternion.AngleAxis(OpenAngle, Vector3.up) * start;
                Gizmos.DrawRay(transform.position, start * 0.5f);
                Gizmos.DrawRay(transform.position, end * 0.5f);
            }
        }
    }

    /// <summary>
    /// Baker for AirlockDoorAuthoring.
    /// </summary>
    public class AirlockDoorBaker : Baker<AirlockDoorAuthoring>
    {
        public override void Bake(AirlockDoorAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Get parent airlock entity if assigned
            // Get parent airlock entity if assigned, or auto-detect
            if (authoring.ParentAirlock == null)
            {
                authoring.ParentAirlock = authoring.GetComponentInParent<AirlockAuthoring>();
                if (authoring.ParentAirlock == null)
                {
                    UnityEngine.Debug.LogError($"[AirlockDoorBaker] Door '{authoring.name}' is missing a ParentAirlock reference and none was found in parents!");
                }
            }

            Entity airlockEntity = Entity.Null;
            if (authoring.ParentAirlock != null)
            {
                airlockEntity = GetEntity(authoring.ParentAirlock, TransformUsageFlags.Dynamic);
            }

            // Add door component
            AddComponent(entity, new AirlockDoor
            {
                DoorSide = authoring.DoorSide,
                IsOpen = false,
                IsLocked = false,
                AirlockEntity = airlockEntity
            });

            // Add animation component
            var transform = authoring.transform;
            AddComponent(entity, new AirlockDoorAnimation
            {
                AnimationType = authoring.AnimationType,
                CurrentOpenness = 0f,
                AnimationSpeed = authoring.AnimationSpeed,
                OpenDirection = (Unity.Mathematics.float3)authoring.OpenDirection.normalized,
                OpenDistance = authoring.OpenDistance,
                OpenAngle = authoring.OpenAngle,
                // Epic 3.3: Use local coordinates so doors move with the ship
                ClosedPosition = (Unity.Mathematics.float3)transform.localPosition,
                ClosedRotation = (Unity.Mathematics.quaternion)transform.localRotation
            });
        }
    }
}
