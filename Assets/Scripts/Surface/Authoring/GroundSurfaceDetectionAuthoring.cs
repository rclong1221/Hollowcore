using UnityEngine;
using Unity.Entities;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 16.10 Phase 1: Add to any prefab that needs ground surface detection.
    /// Players, NPCs, enemies, vehicles.
    /// </summary>
    public class GroundSurfaceDetectionAuthoring : MonoBehaviour
    {
        [Tooltip("How often to raycast for surface detection (seconds). 0 = every frame.")]
        [Range(0f, 1f)]
        public float QueryInterval = 0.25f;

        [Tooltip("Add SurfaceMovementModifier for speed/friction/slip from surface.")]
        public bool AddMovementModifier = true;

        [Tooltip("Add SurfaceNoiseModifier for NPC hearing detection. Players use StealthSystem instead.")]
        public bool AddNoiseModifier = false;
    }

    public class GroundSurfaceDetectionBaker : Baker<GroundSurfaceDetectionAuthoring>
    {
        public override void Bake(GroundSurfaceDetectionAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new GroundSurfaceState
            {
                SurfaceMaterialId = -1,
                SurfaceId = SurfaceID.Default,
                IsGrounded = false,
                TimeSinceLastQuery = 0f,
                QueryInterval = authoring.QueryInterval,
                CachedHardness = 128,
                CachedDensity = 128,
                Flags = SurfaceFlags.None
            });

            if (authoring.AddMovementModifier)
            {
                AddComponent(entity, new SurfaceMovementModifier
                {
                    SpeedMultiplier = 1.0f,
                    FrictionMultiplier = 1.0f,
                    SlipFactor = 0f
                });
            }

            if (authoring.AddNoiseModifier)
            {
                AddComponent(entity, new SurfaceNoiseModifier { Multiplier = 1.0f });
            }
        }
    }
}
