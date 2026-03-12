using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Player.Components;

namespace Player.Authoring
{
    /// <summary>
    /// Authoring component for rideable entities (mounts, vehicles).
    /// Add to Blitz prefab or any mount.
    /// </summary>
    [AddComponentMenu("DIG/Player/Rideable Authoring")]
    public class RideableAuthoring : MonoBehaviour
    {
        [Header("Rideable Settings")]
        [Tooltip("Can this mount be ridden?")]
        public bool canBeRidden = true;
        
        [Tooltip("Interaction radius for mount detection")]
        public float interactionRadius = 2f;
        
        [Header("Mount Positions")]
        [Tooltip("Offset from mount origin for left-side mount")]
        public Vector3 mountOffsetLeft = new Vector3(-1f, 0f, 0f);
        
        [Tooltip("Offset from mount origin for right-side mount")]
        public Vector3 mountOffsetRight = new Vector3(1f, 0f, 0f);
        
        [Tooltip("Final seat position when mounted")]
        public Vector3 seatOffset = new Vector3(0f, 1.5f, 0f);

        [Header("Transform References (Optional)")]
        [Tooltip("Transform where player sits (optional, uses seatOffset if null)")]
        public Transform seatTransform;
        
        [Tooltip("Transform for left mount position (optional)")]
        public Transform mountLeftTransform;
        
        [Tooltip("Transform for right mount position (optional)")]
        public Transform mountRightTransform;

        [Header("Movement Settings")]
        [Tooltip("Walk/trot speed when ridden")]
        public float walkSpeed = 5f;
        
        [Tooltip("Run/gallop speed when sprinting (Shift held)")]
        public float runSpeed = 12f;
        
        [Tooltip("Turn speed in degrees per second")]
        public float turnSpeed = 120f;

        class Baker : Baker<RideableAuthoring>
        {
            public override void Bake(RideableAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, new RideableState
                {
                    HasRider = false,
                    RiderEntity = Entity.Null,
                    CanBeRidden = authoring.canBeRidden,
                    MountOffsetLeft = authoring.mountOffsetLeft,
                    MountOffsetRight = authoring.mountOffsetRight,
                    SeatOffset = authoring.seatOffset,
                    InteractionRadius = authoring.interactionRadius
                });
                
                // Add movement input component (receives input from rider)
                AddComponent(entity, new MountMovementInput
                {
                    ForwardInput = 0f,
                    HorizontalInput = 0f,
                    SprintInput = 0,
                    LookDirection = float3.zero
                });
                
                // Add movement config with walk/run speeds
                AddComponent(entity, new MountMovementConfig
                {
                    WalkSpeed = authoring.walkSpeed,
                    RunSpeed = authoring.runSpeed,
                    TurnSpeed = authoring.turnSpeed
                });
            }
        }
    }
}

