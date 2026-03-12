using Unity.Entities;
using Unity.Collections;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 15.24 Phase 5: Burst-safe surface property database.
    /// BlobAsset indexed by SurfaceID byte for O(1) lookup from Burst jobs.
    /// Built at world initialization from SurfaceMaterial ScriptableObjects.
    /// </summary>
    public struct SurfaceDatabaseBlob
    {
        public BlobArray<SurfaceEntry> Surfaces;
    }

    /// <summary>
    /// Burst-safe surface entry with physical properties and effect IDs.
    /// </summary>
    public struct SurfaceEntry
    {
        public byte SurfaceId;
        public byte Hardness;
        public byte Density;

        public bool AllowsPenetration;
        public bool AllowsRicochet;
        public bool AllowsFootprints;
        public bool IsLiquid;

        /// <summary>
        /// SurfaceMaterialId for managed lookup (audio clips, VFX prefabs).
        /// </summary>
        public int AudioMaterialId;
    }

    /// <summary>
    /// Singleton component holding the BlobAssetReference to the surface database.
    /// Created by SurfaceDatabaseInitSystem.
    /// </summary>
    public struct SurfaceDatabaseSingleton : IComponentData
    {
        public BlobAssetReference<SurfaceDatabaseBlob> Database;
    }
}
