using Unity.Entities;
using UnityEngine;
using Player.Components;
using Player.Systems;

namespace Player.Authoring
{
    /// <summary>
    /// Authoring component for player ride capability.
    /// Add to player prefab (both Client and Server) to enable mounting rideables.
    /// Uses the 'T' key (Interact) to mount/dismount.
    /// </summary>
    [AddComponentMenu("DIG/Player/Ride Authoring")]
    public class RideAuthoring : MonoBehaviour
    {
        [Header("Detection")]
        [Tooltip("Range to detect nearby rideables")]
        public float detectionRange = 3f;

        class Baker : Baker<RideAuthoring>
        {
            public override void Bake(RideAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, RideState.Default);
                
                AddComponent(entity, new RideConfig
                {
                    DetectionRange = authoring.detectionRange
                });
                
                // Add animation output component
                AddComponent(entity, new RideAnimationOutput
                {
                    IsActive = false,
                    AbilityIndex = 0,
                    AbilityIntData = 0
                });
            }
        }
    }
}

