using UnityEngine;
using System.Collections.Generic;

namespace SurfaceSystem
{
    /// <summary>
    /// Singleton manager that maps PhysicMaterial names and Textures to SurfaceDefinitions.
    /// Used by ECS systems to resolve "What surface did I just hit?".
    /// </summary>
    public class SurfaceManager : MonoBehaviour
    {
        public static SurfaceManager Instance { get; private set; }

        [Tooltip("Fallback definition if no specific surface is found.")]
        public SurfaceDefinition DefaultSurface;

        [Tooltip("All available surface definitions to register.")]
        public List<SurfaceDefinition> Surfaces = new List<SurfaceDefinition>();

        // Lookup Dictionaries
        private Dictionary<string, SurfaceDefinition> _physicMaterialMap = new Dictionary<string, SurfaceDefinition>();
        // Future: Texture/UV lookup maps similar to Opsive

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeLookups();
        }

        private void InitializeLookups()
        {
            // Build the map
            foreach (var surface in Surfaces)
            {
                if (surface == null) continue;

                foreach (var matName in surface.PhysicMaterialNames)
                {
                    if (!string.IsNullOrEmpty(matName) && !_physicMaterialMap.ContainsKey(matName))
                    {
                        _physicMaterialMap.Add(matName, surface);
                    }
                }
            }
        }

        /// <summary>
        /// Resolves the SurfaceDefinition from a RaycastHit.
        /// </summary>
        public SurfaceDefinition GetSurface(RaycastHit hit)
        {
            // 1. Check Collider Physic Material
            if (hit.collider != null && hit.collider.sharedMaterial != null)
            {
                // Note: Unity appends " (Instance)" sometimes, need to sanitize
                string matName = hit.collider.sharedMaterial.name.Replace(" (Instance)", "");
                if (_physicMaterialMap.TryGetValue(matName, out var def))
                {
                    return def;
                }
            }

            // 2. Future: Check Texture/Mesh UVs (Terrain splat maps, Mesh Textures)
            // Implementation can mirror Opsive's GetComplexSurfaceType logic later.

            return DefaultSurface;
        }

        /// <summary>
        /// Resolves surface from a PhysicMaterial name (fallback/direct lookup).
        /// </summary>
        public SurfaceDefinition GetSurface(string physicMaterialName)
        {
             if (_physicMaterialMap.TryGetValue(physicMaterialName, out var def))
            {
                return def;
            }
            return DefaultSurface;
        }
    }
}
