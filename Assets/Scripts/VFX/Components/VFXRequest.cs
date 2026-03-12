using Unity.Entities;
using Unity.Mathematics;

namespace DIG.VFX
{
    /// <summary>
    /// EPIC 16.7: Transient VFX request component.
    /// Created by any system (Burst or managed), consumed by VFXExecutionSystem.
    /// Each request lives on a standalone entity destroyed after processing.
    /// </summary>
    public struct VFXRequest : IComponentData
    {
        /// <summary>World-space position to spawn the VFX.</summary>
        public float3 Position;

        /// <summary>World-space rotation for directional VFX (impacts, trails).</summary>
        public quaternion Rotation;

        /// <summary>Integer ID mapping to VFXTypeDatabase entry. Resolved to prefab at execution.</summary>
        public int VFXTypeId;

        /// <summary>Budget category for throttling.</summary>
        public VFXCategory Category;

        /// <summary>Intensity scalar [0-1]. Affects particle count, emission rate, scale.</summary>
        public float Intensity;

        /// <summary>Uniform scale multiplier applied to the spawned VFX. Default 1.0.</summary>
        public float Scale;

        /// <summary>
        /// Color tint applied to the VFX via particle start color modulation.
        /// Default (0,0,0,0) means "use prefab default" — no tint override.
        /// </summary>
        public float4 ColorTint;

        /// <summary>
        /// Duration override in seconds. 0 = use prefab default.
        /// </summary>
        public float Duration;

        /// <summary>
        /// Source entity that caused this VFX (attacker, caster, etc.).
        /// Entity.Null if no source.
        /// </summary>
        public Entity SourceEntity;

        /// <summary>
        /// Priority within category. Higher survives budget culling.
        /// Default 0. Boss effects: 100+. Ambient: -10.
        /// </summary>
        public int Priority;
    }

    /// <summary>
    /// EPIC 16.7: Tag enabled on VFX request entities that have been budget-culled or LOD-culled.
    /// VFXExecutionSystem skips entities with this enabled. VFXCleanupSystem destroys them.
    /// </summary>
    public struct VFXCulled : IComponentData, IEnableableComponent { }

    /// <summary>
    /// EPIC 16.7: Transient component added by VFXLODSystem with the resolved LOD tier.
    /// Read by VFXExecutionSystem for prefab variant selection.
    /// </summary>
    public struct VFXResolvedLOD : IComponentData
    {
        public DIG.Surface.EffectLODTier Tier;
        public float DistanceToCamera;
    }

    /// <summary>
    /// EPIC 16.7: Cleanup component ensuring VFXRequest entities are destroyed
    /// even if all VFX systems are removed from the world.
    /// </summary>
    public struct VFXCleanupTag : ICleanupComponentData { }
}
