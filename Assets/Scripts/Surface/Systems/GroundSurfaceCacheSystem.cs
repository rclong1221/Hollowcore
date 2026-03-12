using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Audio.Systems;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 16.10 Phase 1: Managed companion system that resolves SurfaceMaterialId
    /// to cached properties (SurfaceId, Hardness, Density, Flags) via SurfaceMaterialRegistry.
    /// Only resolves when SurfaceMaterialId changes — not every frame.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class GroundSurfaceCacheSystem : SystemBase
    {
        private SurfaceMaterialRegistry _registry;
        private NativeHashMap<Entity, int> _previousMaterialIds;
        private int _evictionCounter;
        private const int EvictionInterval = 300; // Evict stale entries every ~5 seconds at 60fps

        protected override void OnCreate()
        {
            RequireForUpdate<GroundSurfaceState>();
            _previousMaterialIds = new NativeHashMap<Entity, int>(64, Allocator.Persistent);
            _registry = Resources.Load<SurfaceMaterialRegistry>("SurfaceMaterialRegistry");
        }

        protected override void OnDestroy()
        {
            if (_previousMaterialIds.IsCreated)
                _previousMaterialIds.Dispose();
        }

        protected override void OnUpdate()
        {
            if (_registry == null) return;

            foreach (var (surfaceState, entity) in
                SystemAPI.Query<RefRW<GroundSurfaceState>>()
                    .WithEntityAccess())
            {
                int materialId = surfaceState.ValueRO.SurfaceMaterialId;

                // Skip if unchanged
                if (_previousMaterialIds.TryGetValue(entity, out int prevId) && prevId == materialId)
                    continue;

                _previousMaterialIds[entity] = materialId;

                if (materialId < 0)
                {
                    // Airborne or unknown — reset to defaults
                    surfaceState.ValueRW.SurfaceId = SurfaceID.Default;
                    surfaceState.ValueRW.CachedHardness = 128;
                    surfaceState.ValueRW.CachedDensity = 128;
                    surfaceState.ValueRW.Flags = SurfaceFlags.None;
                    continue;
                }

                if (_registry.TryGetById(materialId, out var material))
                {
                    surfaceState.ValueRW.SurfaceId = material.SurfaceId != SurfaceID.Default
                        ? material.SurfaceId
                        : SurfaceIdResolver.FromMaterial(material);
                    surfaceState.ValueRW.CachedHardness = material.Hardness;
                    surfaceState.ValueRW.CachedDensity = material.Density;

                    SurfaceFlags flags = SurfaceFlags.None;
                    if (material.IsSlippery) flags |= SurfaceFlags.IsSlippery;
                    if (material.IsLiquid) flags |= SurfaceFlags.IsLiquid;
                    if (material.AllowsRicochet) flags |= SurfaceFlags.AllowsRicochet;
                    if (material.AllowsPenetration) flags |= SurfaceFlags.AllowsPenetration;
                    surfaceState.ValueRW.Flags = flags;
                }
            }

            // Periodic eviction of destroyed entities from the hashmap
            _evictionCounter++;
            if (_evictionCounter >= EvictionInterval)
            {
                _evictionCounter = 0;
                EvictDestroyedEntities();
            }
        }

        private void EvictDestroyedEntities()
        {
            if (_previousMaterialIds.Count == 0) return;

            var keys = _previousMaterialIds.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < keys.Length; i++)
            {
                if (!EntityManager.Exists(keys[i]))
                    _previousMaterialIds.Remove(keys[i]);
            }
            keys.Dispose();
        }
    }
}
