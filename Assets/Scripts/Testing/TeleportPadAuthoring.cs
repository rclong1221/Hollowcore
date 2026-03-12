using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Testing
{
    /// <summary>
    /// Authoring component for teleport pads.
    /// When a player enters the trigger, they are teleported to the destination.
    /// </summary>
    public class TeleportPadAuthoring : MonoBehaviour
    {
        [Tooltip("The destination transform to teleport to")]
        public Transform Destination;

        [Tooltip("If true, preserve the player's current rotation after teleport")]
        public bool PreserveRotation = false;

        [Tooltip("Cooldown in seconds before this pad can teleport again")]
        public float Cooldown = 1.0f;

        public class Baker : Baker<TeleportPadAuthoring>
        {
            public override void Bake(TeleportPadAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                float3 destPos = float3.zero;
                quaternion destRot = quaternion.identity;

                if (authoring.Destination != null)
                {
                    destPos = authoring.Destination.position;
                    destRot = authoring.Destination.rotation;
                }

                AddComponent(entity, new TeleportPad
                {
                    Destination = destPos,
                    DestinationRotation = destRot,
                    PreserveRotation = authoring.PreserveRotation,
                    Cooldown = authoring.Cooldown,
                    LastTeleportTime = -1000f
                });
            }
        }
    }

    /// <summary>
    /// Component data for a teleport pad trigger.
    /// </summary>
    public struct TeleportPad : IComponentData
    {
        public float3 Destination;
        public quaternion DestinationRotation;
        public bool PreserveRotation;
        public float Cooldown;
        public float LastTeleportTime;
    }
}
