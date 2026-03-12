using UnityEngine;
using Unity.Entities;
using Unity.Physics;
using Unity.Collections;
using DIG.Weapons.Audio;

namespace DIG.Weapons.Effects
{
    /// <summary>
    /// EPIC 14.20: Detects surface material from physics hits.
    /// Provides material information for selecting appropriate impact effects and sounds.
    /// </summary>
    public static class SurfaceMaterialDetector
    {
        /// <summary>
        /// Detect surface material from a raycast hit.
        /// Uses multiple detection methods in priority order.
        /// </summary>
        public static SurfaceMaterialType DetectSurface(UnityEngine.RaycastHit hit)
        {
            if (hit.collider == null)
                return SurfaceMaterialType.Default;

            // Priority 1: Check for SurfaceMaterialTag component
            var surfaceTag = hit.collider.GetComponent<SurfaceMaterialTag>();
            if (surfaceTag != null)
                return surfaceTag.MaterialType;

            // Priority 2: Check parent hierarchy
            surfaceTag = hit.collider.GetComponentInParent<SurfaceMaterialTag>();
            if (surfaceTag != null)
                return surfaceTag.MaterialType;

            // Priority 3: Check for terrain
            if (hit.collider is UnityEngine.TerrainCollider terrainCollider)
            {
                return DetectTerrainSurface(hit, terrainCollider.terrainData);
            }

            // Priority 4: Try to detect from material/texture name
            var renderer = hit.collider.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                return DetectFromMaterial(renderer.sharedMaterial);
            }

            // Priority 5: Check physics material
            if (hit.collider.sharedMaterial != null)
            {
                return DetectFromPhysicsMaterial(hit.collider.sharedMaterial);
            }

            return SurfaceMaterialType.Default;
        }

        /// <summary>
        /// Detect surface material from Unity Physics hit (ECS).
        /// </summary>
        public static SurfaceMaterialType DetectSurface(Unity.Physics.RaycastHit hit,
            ref PhysicsWorld physicsWorld, EntityManager entityManager)
        {
            if (hit.Entity == Entity.Null)
                return SurfaceMaterialType.Default;

            // Check for ECS SurfaceMaterial component
            if (entityManager.HasComponent<SurfaceMaterialComponent>(hit.Entity))
            {
                var material = entityManager.GetComponentData<SurfaceMaterialComponent>(hit.Entity);
                return (SurfaceMaterialType)material.MaterialType;
            }

            // Check physics material custom tag (if using custom physics materials)
            var body = physicsWorld.Bodies[hit.RigidBodyIndex];
            if (body.Collider.IsCreated)
            {
                // Could extract custom data from collider here
                // For now, return default
            }

            return SurfaceMaterialType.Default;
        }

        /// <summary>
        /// Detect surface material from terrain at hit point.
        /// </summary>
        private static SurfaceMaterialType DetectTerrainSurface(UnityEngine.RaycastHit hit, TerrainData terrainData)
        {
            if (terrainData == null || terrainData.alphamapLayers == 0)
                return SurfaceMaterialType.Dirt;

            // Get terrain position
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
                return SurfaceMaterialType.Dirt;

            Vector3 terrainPos = terrain.transform.position;
            Vector3 hitLocalPos = hit.point - terrainPos;

            // Normalize to terrain coordinates
            float normX = hitLocalPos.x / terrainData.size.x;
            float normZ = hitLocalPos.z / terrainData.size.z;

            // Clamp to valid range
            normX = Mathf.Clamp01(normX);
            normZ = Mathf.Clamp01(normZ);

            // Get alphamap coordinates
            int mapX = Mathf.RoundToInt(normX * (terrainData.alphamapWidth - 1));
            int mapZ = Mathf.RoundToInt(normZ * (terrainData.alphamapHeight - 1));

            // Get splat weights at this position
            float[,,] splatWeights = terrainData.GetAlphamaps(mapX, mapZ, 1, 1);

            // Find dominant layer
            int dominantLayer = 0;
            float maxWeight = 0f;

            for (int i = 0; i < terrainData.alphamapLayers; i++)
            {
                if (splatWeights[0, 0, i] > maxWeight)
                {
                    maxWeight = splatWeights[0, 0, i];
                    dominantLayer = i;
                }
            }

            // Map layer to surface type (assumes standard terrain layer order)
            // This should be customized based on your terrain setup
            return dominantLayer switch
            {
                0 => SurfaceMaterialType.Grass,
                1 => SurfaceMaterialType.Dirt,
                2 => SurfaceMaterialType.Sand,
                3 => SurfaceMaterialType.Concrete,
                _ => SurfaceMaterialType.Dirt
            };
        }

        /// <summary>
        /// Try to detect surface type from render material name.
        /// </summary>
        private static SurfaceMaterialType DetectFromMaterial(UnityEngine.Material material)
        {
            if (material == null)
                return SurfaceMaterialType.Default;

            string matName = material.name.ToLower();

            if (matName.Contains("metal") || matName.Contains("steel") || matName.Contains("iron"))
                return SurfaceMaterialType.Metal;

            if (matName.Contains("wood") || matName.Contains("plank") || matName.Contains("timber"))
                return SurfaceMaterialType.Wood;

            if (matName.Contains("concrete") || matName.Contains("cement") || matName.Contains("stone"))
                return SurfaceMaterialType.Concrete;

            if (matName.Contains("dirt") || matName.Contains("mud") || matName.Contains("ground"))
                return SurfaceMaterialType.Dirt;

            if (matName.Contains("grass") || matName.Contains("foliage"))
                return SurfaceMaterialType.Grass;

            if (matName.Contains("sand") || matName.Contains("beach"))
                return SurfaceMaterialType.Sand;

            if (matName.Contains("water") || matName.Contains("liquid"))
                return SurfaceMaterialType.Water;

            if (matName.Contains("glass") || matName.Contains("window"))
                return SurfaceMaterialType.Glass;

            if (matName.Contains("flesh") || matName.Contains("skin") || matName.Contains("body"))
                return SurfaceMaterialType.Flesh;

            if (matName.Contains("cloth") || matName.Contains("fabric") || matName.Contains("textile"))
                return SurfaceMaterialType.Cloth;

            if (matName.Contains("plastic") || matName.Contains("rubber"))
                return SurfaceMaterialType.Plastic;

            return SurfaceMaterialType.Default;
        }

        /// <summary>
        /// Try to detect surface type from physics material name.
        /// </summary>
        private static SurfaceMaterialType DetectFromPhysicsMaterial(PhysicsMaterial material)
        {
            if (material == null)
                return SurfaceMaterialType.Default;

            string matName = material.name.ToLower();

            // Same logic as render material
            if (matName.Contains("metal")) return SurfaceMaterialType.Metal;
            if (matName.Contains("wood")) return SurfaceMaterialType.Wood;
            if (matName.Contains("concrete") || matName.Contains("stone")) return SurfaceMaterialType.Concrete;
            if (matName.Contains("dirt") || matName.Contains("ground")) return SurfaceMaterialType.Dirt;
            if (matName.Contains("grass")) return SurfaceMaterialType.Grass;
            if (matName.Contains("sand")) return SurfaceMaterialType.Sand;
            if (matName.Contains("water")) return SurfaceMaterialType.Water;
            if (matName.Contains("glass")) return SurfaceMaterialType.Glass;

            return SurfaceMaterialType.Default;
        }
    }

    /// <summary>
    /// EPIC 14.20: MonoBehaviour tag for explicit surface material assignment.
    /// Add this to GameObjects to specify their surface material.
    /// </summary>
    public class SurfaceMaterialTag : MonoBehaviour
    {
        [Tooltip("The surface material type for this object")]
        public SurfaceMaterialType MaterialType = SurfaceMaterialType.Default;
    }

    /// <summary>
    /// EPIC 14.20: ECS component for surface material.
    /// Add to entities that need surface material detection.
    /// </summary>
    public struct SurfaceMaterialComponent : IComponentData
    {
        public byte MaterialType;

        public SurfaceMaterialComponent(SurfaceMaterialType type)
        {
            MaterialType = (byte)type;
        }
    }

    /// <summary>
    /// EPIC 14.20: Authoring component for surface material in ECS.
    /// </summary>
    public class SurfaceMaterialAuthoring : MonoBehaviour
    {
        public SurfaceMaterialType MaterialType = SurfaceMaterialType.Default;

        public class Baker : Baker<SurfaceMaterialAuthoring>
        {
            public override void Bake(SurfaceMaterialAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new SurfaceMaterialComponent((SurfaceMaterialType)authoring.MaterialType));
            }
        }
    }
}
