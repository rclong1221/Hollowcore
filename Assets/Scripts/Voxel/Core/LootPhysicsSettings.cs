using UnityEngine;

namespace DIG.Voxel.Core
{
    /// <summary>
    /// Centralized settings for loot physics behavior.
    /// Task 10.17.13: Configurable loot physics parameters.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Voxel/Loot Physics Settings")]
    public class LootPhysicsSettings : ScriptableObject
    {
        [Header("Scatter Force (Task 10.17.11)")]
        [Tooltip("Base force magnitude for loot scatter explosion")]
        [Range(1f, 30f)]
        public float ScatterForce = 8f;
        
        [Tooltip("Upward bias added to scatter direction (0 = horizontal, 1 = 45°)")]
        [Range(0f, 2f)]
        public float UpwardBias = 1.5f;
        
        [Tooltip("Random variation in scatter force (±percentage)")]
        [Range(0f, 0.5f)]
        public float ForceVariation = 0.3f;
        
        [Tooltip("Horizontal spread radius for spawn position offset")]
        [Range(0f, 0.5f)]
        public float SpawnSpread = 0.25f;
        
        [Header("Physics Properties (Task 10.17.12)")]
        [Tooltip("Base mass for loot items in kg")]
        [Range(0.5f, 10f)]
        public float BaseMass = 3f;
        
        [Tooltip("Linear drag - higher = stops faster")]
        [Range(0f, 3f)]
        public float Drag = 0.8f;
        
        [Tooltip("Angular drag - higher = stops rotating faster")]
        [Range(0f, 3f)]
        public float AngularDrag = 0.6f;
        
        [Header("Lifetime (Task 10.17.15)")]
        [Tooltip("Time in seconds before loot auto-destroys")]
        [Range(10f, 300f)]
        public float Lifetime = 60f;
        
        [Tooltip("Fade duration before destruction")]
        [Range(0f, 5f)]
        public float FadeDuration = 2f;
        
        [Header("Collision (Task 10.17.14)")]
        [Tooltip("Physics material for loot bouncing")]
        public PhysicsMaterial LootPhysicsMaterial;
        
        [Tooltip("Layer mask for terrain collision")]
        public LayerMask TerrainLayer;
        
        [Header("Debug")]
        [Tooltip("Enable verbose logging for loot spawning and physics")]
        public bool EnableDebugLogs = false;
        
        /// <summary>
        /// Calculate scatter velocity for a loot item.
        /// </summary>
        public Vector3 CalculateScatterVelocity(Vector3 minePoint, Vector3 spawnPoint, System.Random rng)
        {
            // Radial direction from mine point
            Vector3 radialDir = (spawnPoint - minePoint).normalized;
            if (radialDir.sqrMagnitude < 0.001f)
            {
                // Fallback: random horizontal direction
                float angle = (float)(rng.NextDouble() * 2 * System.Math.PI);
                radialDir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            }
            
            // Add upward bias
            Vector3 scatterDir = (radialDir + Vector3.up * UpwardBias).normalized;
            
            // Apply force with variation
            float variation = 1f + ((float)rng.NextDouble() * 2f - 1f) * ForceVariation;
            float force = ScatterForce * variation;
            
            return scatterDir * force;
        }
        
        /// <summary>
        /// Calculate randomized spawn position offset.
        /// </summary>
        public Vector3 CalculateSpawnOffset(System.Random rng)
        {
            return new Vector3(
                ((float)rng.NextDouble() * 2f - 1f) * SpawnSpread,
                (float)rng.NextDouble() * 0.3f + 0.1f, // Always slightly above
                ((float)rng.NextDouble() * 2f - 1f) * SpawnSpread
            );
        }
        
        /// <summary>
        /// Configure a Rigidbody with these physics settings.
        /// </summary>
        public void ConfigureRigidbody(Rigidbody rb, float massMultiplier = 1f)
        {
            // CRITICAL: Ensure physics is enabled
            rb.useGravity = true;
            rb.isKinematic = false;
            
            rb.mass = BaseMass * massMultiplier;
            rb.linearDamping = Drag;
            rb.angularDamping = AngularDrag;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            
            // Apply physics material if collider exists
            if (LootPhysicsMaterial != null)
            {
                var collider = rb.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.material = LootPhysicsMaterial;
                }
            }
        }
        
        /// <summary>
        /// Default settings for when no asset is configured.
        /// </summary>
        public static LootPhysicsSettings CreateDefault()
        {
            var settings = CreateInstance<LootPhysicsSettings>();
            settings.ScatterForce = 12f;
            settings.UpwardBias = 1.5f;
            settings.ForceVariation = 0.3f;
            settings.SpawnSpread = 0.25f;
            settings.BaseMass = 3f;
            settings.Drag = 0.8f;
            settings.AngularDrag = 0.6f;
            settings.Lifetime = 60f;
            settings.Lifetime = 60f;
            settings.FadeDuration = 2f;
            settings.EnableDebugLogs = false;
            return settings;
        }
    }
}
