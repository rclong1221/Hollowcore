using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using Audio.Systems;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 15.24 Phase 5: Initializes the SurfaceDatabase BlobAsset at world creation.
    /// Reads all SurfaceMaterial ScriptableObjects from SurfaceMaterialRegistry
    /// and builds a Burst-safe BlobArray indexed by SurfaceID.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class SurfaceDatabaseInitSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;

            var registry = Resources.Load<SurfaceMaterialRegistry>("SurfaceMaterialRegistry");
            if (registry == null) return;

            BuildBlobAsset(registry);
            _initialized = true;
            Enabled = false; // One-shot
        }

        private void BuildBlobAsset(SurfaceMaterialRegistry registry)
        {
            int surfaceCount = (int)SurfaceID.Energy_Shield + 1; // 24 entries

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<SurfaceDatabaseBlob>();
            var surfaces = builder.Allocate(ref root.Surfaces, surfaceCount);

            // Initialize all entries with defaults
            for (int i = 0; i < surfaceCount; i++)
            {
                surfaces[i] = new SurfaceEntry
                {
                    SurfaceId = (byte)i,
                    Hardness = 128,
                    Density = 128,
                    AllowsPenetration = false,
                    AllowsRicochet = true,
                    AllowsFootprints = false,
                    IsLiquid = false,
                    AudioMaterialId = registry.DefaultMaterial != null ? registry.DefaultMaterial.Id : 0
                };
            }

            // Populate from registered materials
            foreach (var mat in registry.Materials)
            {
                if (mat == null) continue;
                var sid = SurfaceIdResolver.FromMaterial(mat);
                int index = (int)sid;
                if (index < 0 || index >= surfaceCount) continue;

                surfaces[index] = new SurfaceEntry
                {
                    SurfaceId = (byte)index,
                    Hardness = mat.Hardness,
                    Density = mat.Density,
                    AllowsPenetration = mat.AllowsPenetration,
                    AllowsRicochet = mat.AllowsRicochet,
                    AllowsFootprints = mat.AllowFootprints,
                    IsLiquid = mat.IsLiquid,
                    AudioMaterialId = mat.Id
                };
            }

            var blobRef = builder.CreateBlobAssetReference<SurfaceDatabaseBlob>(Allocator.Persistent);

            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new SurfaceDatabaseSingleton
            {
                Database = blobRef
            });
        }

        protected override void OnDestroy()
        {
            if (SystemAPI.TryGetSingleton<SurfaceDatabaseSingleton>(out var singleton))
            {
                if (singleton.Database.IsCreated)
                    singleton.Database.Dispose();
            }
        }
    }
}
