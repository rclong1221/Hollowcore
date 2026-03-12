using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Audio.Systems
{
    /// <summary>
    /// Static utility for resolving SurfaceMaterial ID from various sources with priority:
    /// 1. SurfaceMaterialId component on entity
    /// 2. Physics material name lookup via SurfaceMaterialMapping
    /// 3. Renderer material name lookup via SurfaceMaterialMapping  
    /// 4. Default material fallback
    /// 
    /// This is designed to be called from systems that need to determine surface material
    /// from raycast hits or entity lookups.
    /// </summary>
    public static class SurfaceDetectionService
    {
        private static SurfaceMaterialRegistry _cachedRegistry;
        private static SurfaceMaterialMapping _cachedMapping;
        
        /// <summary>
        /// Resolve material ID from an entity with full priority chain.
        /// Returns the DefaultMaterial ID if no match found.
        /// </summary>
        public static int ResolveMaterialId(EntityManager em, Entity hitEntity, GameObject hitGameObject = null)
        {
            EnsureCaches();
            
            // Priority 1: SurfaceMaterialId component on hit entity
            if (hitEntity != Entity.Null && em.HasComponent<SurfaceMaterialId>(hitEntity))
            {
                return em.GetComponentData<SurfaceMaterialId>(hitEntity).Id;
            }
            
            // Priority 2 & 3: Physics material or Renderer material via mapping
            if (hitGameObject != null && _cachedMapping != null)
            {
                // Try physics material
                var collider = hitGameObject.GetComponent<Collider>();
                if (collider != null && collider.sharedMaterial != null)
                {
                    var entry = _cachedMapping.FindByMaterialName(collider.sharedMaterial.name);
                    if (entry != null && entry.surfaceMaterial != null)
                    {
                        return entry.surfaceMaterial.Id;
                    }
                }
                
                // Try renderer material
                var renderer = hitGameObject.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    var entry = _cachedMapping.FindByMaterialName(renderer.sharedMaterial.name);
                    if (entry != null && entry.surfaceMaterial != null)
                    {
                        return entry.surfaceMaterial.Id;
                    }
                }
                
                // Try by tag
                var tagEntry = _cachedMapping.FindByTag(hitGameObject.tag);
                if (tagEntry != null && tagEntry.surfaceMaterial != null)
                {
                    return tagEntry.surfaceMaterial.Id;
                }
                
                // Try by layer
                var layerEntry = _cachedMapping.FindByLayer(hitGameObject.layer);
                if (layerEntry != null && layerEntry.surfaceMaterial != null)
                {
                    return layerEntry.surfaceMaterial.Id;
                }
            }
            
            // Priority 4: Default material
            if (_cachedRegistry != null && _cachedRegistry.DefaultMaterial != null)
            {
                return _cachedRegistry.DefaultMaterial.Id;
            }
            
            return 0; // Absolute fallback
        }
        
        /// <summary>
        /// Resolve material ID from a Unity Physics raycast hit (legacy fallback path).
        /// </summary>
        public static int ResolveMaterialIdFromUnityHit(RaycastHit hit)
        {
            EnsureCaches();
            
            if (hit.collider == null) return GetDefaultId();
            
            var go = hit.collider.gameObject;
            
            // Check for SurfaceMaterialAuthoring (MonoBehaviour that may hold ID)
            var authoring = go.GetComponent<SurfaceMaterialAuthoring>();
            if (authoring != null && authoring.Material != null)
            {
                return authoring.Material.Id;
            }
            
            // Fall through to mapping lookup
            return ResolveMaterialId(default, Entity.Null, go);
        }
        
        public static int GetDefaultId()
        {
            EnsureCaches();
            if (_cachedRegistry != null && _cachedRegistry.DefaultMaterial != null)
            {
                return _cachedRegistry.DefaultMaterial.Id;
            }
            return 0;
        }
        
        public static SurfaceMaterial GetMaterial(int id)
        {
            EnsureCaches();
            if (_cachedRegistry != null)
            {
                return _cachedRegistry.GetById(id);
            }
            return null;
        }
        
        private static void EnsureCaches()
        {
            if (_cachedRegistry == null)
            {
                _cachedRegistry = Resources.Load<SurfaceMaterialRegistry>("Audio/SurfaceMaterialRegistry");
            }
            if (_cachedMapping == null)
            {
                _cachedMapping = Resources.Load<SurfaceMaterialMapping>("Audio/SurfaceMaterialMapping");
            }
        }
        
        /// <summary>
        /// Clear caches (call on scene unload or domain reload).
        /// </summary>
        public static void ClearCaches()
        {
            _cachedRegistry = null;
            _cachedMapping = null;
        }
    }
}
