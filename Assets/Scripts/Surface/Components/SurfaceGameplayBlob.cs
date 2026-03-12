using Unity.Entities;
using Unity.Collections;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 16.10 Phase 2: Burst-compatible gameplay modifiers per SurfaceID.
    /// </summary>
    public struct SurfaceGameplayModifiers
    {
        public float NoiseMultiplier;
        public float SpeedMultiplier;
        public float SlipFactor;
        public float FallDamageMultiplier;
        public float DamagePerSecond;
        public byte DamageType; // cast to Player.Components.DamageType
    }

    /// <summary>
    /// EPIC 16.10 Phase 2: BlobAsset containing gameplay modifiers per SurfaceID.
    /// Created at runtime from SurfaceGameplayConfig SO.
    /// Burst-friendly — no managed lookups in hot path.
    /// </summary>
    public struct SurfaceGameplayBlob
    {
        /// <summary>
        /// Indexed by (byte)SurfaceID. 24 entries (one per SurfaceID enum value).
        /// </summary>
        public BlobArray<SurfaceGameplayModifiers> Modifiers;
    }

    /// <summary>
    /// EPIC 16.10 Phase 2: Singleton referencing the BlobAsset.
    /// Created by SurfaceGameplayConfigSystem.
    /// </summary>
    public struct SurfaceGameplayConfigSingleton : IComponentData
    {
        public BlobAssetReference<SurfaceGameplayBlob> Config;
    }
}
