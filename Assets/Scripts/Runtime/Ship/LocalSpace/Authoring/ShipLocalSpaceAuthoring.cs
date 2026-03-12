using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Ship.LocalSpace
{


    /// <summary>
    /// Authoring component for entities that use ship local space.
    /// Add to player prefab to enable automatic attach/detach.
    /// </summary>
    [AddComponentMenu("DIG/Ship/Ship Local Space Player Authoring")]
    public class ShipLocalSpacePlayerAuthoring : MonoBehaviour
    {
        [Header("Smoothing")]
        [Tooltip("Duration of misprediction smoothing in seconds")]
        [Range(0.05f, 0.5f)]
        public float SmoothingDuration = 0.1f;
    }

    /// <summary>
    /// Baker for ShipLocalSpacePlayerAuthoring.
    /// </summary>
    public class ShipLocalSpacePlayerBaker : Baker<ShipLocalSpacePlayerAuthoring>
    {
        public override void Bake(ShipLocalSpacePlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add smoothing component
            AddComponent(entity, new LocalSpaceSmoothing
            {
                SmoothingProgress = 1f,
                PositionError = float3.zero,
                RotationError = quaternion.identity,
                SmoothingDuration = authoring.SmoothingDuration
            });

            // Add InShipLocalSpace component by default (detached) ensures reliable replication
            AddComponent(entity, new InShipLocalSpace
            {
                IsAttached = false,
                ShipEntity = Entity.Null,
                LocalPosition = float3.zero,
                LocalRotation = quaternion.identity
            });
        }
    }
}
