using UnityEngine;

namespace DIG.Voxel.Rendering
{
    /// <summary>
    /// Configuration for voxel chunk Level of Detail (LOD) system.
    /// Controls mesh resolution and collider generation based on distance from camera.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Voxel/LOD Config")]
    public class VoxelLODConfig : ScriptableObject
    {
        [System.Serializable]
        public struct LODLevel
        {
            [Tooltip("Distance from camera at which this LOD starts")]
            public float Distance;
            
            [Tooltip("Voxel step size: 1=full, 2=half, 4=quarter resolution")]
            [Range(1, 8)]
            public int VoxelStep;
            
            [Tooltip("Generate physics collider for this LOD?")]
            public bool HasCollider;
            
            [Tooltip("Color for debug visualization")]
            public Color DebugColor;
        }

        [Header("LOD Levels")]
        [Tooltip("LOD levels ordered by distance (closest first)")]
        public LODLevel[] Levels = new LODLevel[]
        {
            new LODLevel { Distance = 32f,  VoxelStep = 1, HasCollider = true,  DebugColor = new Color(0, 1, 0, 0.3f) },    // LOD0 - Full
            new LODLevel { Distance = 64f,  VoxelStep = 2, HasCollider = true,  DebugColor = new Color(1, 1, 0, 0.3f) },    // LOD1 - Half
            new LODLevel { Distance = 128f, VoxelStep = 4, HasCollider = false, DebugColor = new Color(1, 0.5f, 0, 0.3f) }, // LOD2 - Quarter
            new LODLevel { Distance = 256f, VoxelStep = 8, HasCollider = false, DebugColor = new Color(1, 0, 0, 0.3f) },    // LOD3 - Eighth
        };

        [Header("Update Settings")]
        [Tooltip("How often to check for LOD changes (seconds)")]
        [Range(0.1f, 2f)]
        public float UpdateFrequency = 0.5f;
        
        [Tooltip("Hysteresis to prevent rapid LOD switching")]
        [Range(0f, 10f)]
        public float Hysteresis = 2f;

        [Header("Performance")]
        [Tooltip("Maximum chunks to update per frame")]
        [Range(1, 20)]
        public int MaxUpdatesPerFrame = 5;
        
        [Tooltip("Enable collider LOD (disable colliders on distant chunks)")]
        public bool EnableColliderLOD = true;

        /// <summary>
        /// Get the LOD level for a given distance.
        /// </summary>
        public int GetLODLevel(float distance)
        {
            for (int i = 0; i < Levels.Length; i++)
            {
                if (distance <= Levels[i].Distance)
                    return i;
            }
            return Levels.Length - 1;
        }

        /// <summary>
        /// Get the LOD level with hysteresis to prevent flickering.
        /// </summary>
        public int GetLODLevelWithHysteresis(float distance, int currentLOD)
        {
            int targetLOD = GetLODLevel(distance);
            
            if (targetLOD != currentLOD && Hysteresis > 0)
            {
                // Apply hysteresis
                float threshold = Levels[Mathf.Max(0, currentLOD)].Distance;
                if (currentLOD < targetLOD)
                {
                    // Moving to lower LOD - require distance + hysteresis
                    if (distance < threshold + Hysteresis)
                        return currentLOD;
                }
                else
                {
                    // Moving to higher LOD - require distance - hysteresis
                    if (distance > threshold - Hysteresis)
                        return currentLOD;
                }
            }
            
            return targetLOD;
        }

        /// <summary>
        /// Get voxel step for a given LOD level.
        /// </summary>
        public int GetVoxelStep(int lodLevel)
        {
            if (lodLevel < 0 || lodLevel >= Levels.Length)
                return 1;
            return Levels[lodLevel].VoxelStep;
        }

        /// <summary>
        /// Check if collider should be generated for a given LOD level.
        /// </summary>
        public bool ShouldHaveCollider(int lodLevel)
        {
            if (!EnableColliderLOD) return true;
            if (lodLevel < 0 || lodLevel >= Levels.Length)
                return false;
            return Levels[lodLevel].HasCollider;
        }

        private void OnValidate()
        {
            // Ensure levels are sorted by distance
            if (Levels != null && Levels.Length > 1)
            {
                for (int i = 1; i < Levels.Length; i++)
                {
                    if (Levels[i].Distance < Levels[i - 1].Distance)
                    {
                        UnityEngine.Debug.LogWarning("[VoxelLODConfig] LOD levels should be ordered by distance!");
                    }
                }
            }
        }
    }
}
