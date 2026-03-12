using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Weapons.Effects
{
    /// <summary>
    /// Request to spawn a fire effect (muzzle flash, sound, etc.).
    /// Processed by FireEffectSpawnerSystem.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct FireEffectRequest : IComponentData
    {
        /// <summary>
        /// World position of the muzzle.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float3 Position;

        /// <summary>
        /// Fire direction.
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float3 Direction;

        /// <summary>
        /// Index into effect prefab registry.
        /// </summary>
        [GhostField]
        public int EffectPrefabIndex;

        /// <summary>
        /// Whether this request is pending.
        /// </summary>
        [GhostField]
        public bool Pending;
    }

    /// <summary>
    /// Request to spawn an impact effect (decal, particles, sound).
    /// Processed by ImpactEffectSpawnerSystem.
    /// </summary>
    public struct ImpactEffectRequest : IBufferElementData
    {
        /// <summary>
        /// World position of impact.
        /// </summary>
        public float3 Position;

        /// <summary>
        /// Surface normal at impact point.
        /// </summary>
        public float3 Normal;

        /// <summary>
        /// Surface material ID for effect selection.
        /// </summary>
        public int SurfaceMaterialId;

        /// <summary>
        /// Type of impact (bullet, melee, explosion, etc.).
        /// </summary>
        public ImpactType Type;

        /// <summary>
        /// Scale of the effect.
        /// </summary>
        public float Scale;
    }

    /// <summary>
    /// Types of impact effects.
    /// </summary>
    public enum ImpactType : byte
    {
        None = 0,
        Bullet,
        Melee,
        Explosion,
        Magic,
        Laser
    }

    /// <summary>
    /// Request to spawn a tracer/projectile visual.
    /// </summary>
    public struct TracerRequest : IBufferElementData
    {
        /// <summary>
        /// Start position (muzzle).
        /// </summary>
        public float3 Start;

        /// <summary>
        /// End position (hit point or max range).
        /// </summary>
        public float3 End;

        /// <summary>
        /// Tracer prefab index.
        /// </summary>
        public int PrefabIndex;

        /// <summary>
        /// Speed of tracer movement.
        /// </summary>
        public float Speed;
    }

    /// <summary>
    /// Configuration for weapon effects.
    /// Added to weapon prefabs via authoring.
    /// </summary>
    public struct WeaponEffectConfig : IComponentData
    {
        /// <summary>
        /// Muzzle flash prefab index.
        /// </summary>
        public int MuzzleFlashPrefabIndex;

        /// <summary>
        /// Shell eject prefab index.
        /// </summary>
        public int ShellEjectPrefabIndex;

        /// <summary>
        /// Tracer prefab index.
        /// </summary>
        public int TracerPrefabIndex;

        /// <summary>
        /// Impact effect prefab index.
        /// </summary>
        public int ImpactPrefabIndex;

        /// <summary>
        /// Probability of spawning tracer (0-1).
        /// </summary>
        public float TracerProbability;

        /// <summary>
        /// Offset from weapon origin to muzzle.
        /// </summary>
        public float3 MuzzleOffset;

        /// <summary>
        /// Offset from weapon origin to shell ejection port.
        /// </summary>
        public float3 ShellEjectOffset;

        /// <summary>
        /// Direction of shell ejection.
        /// </summary>
        public float3 ShellEjectDirection;

        /// <summary>
        /// Speed of shell ejection.
        /// </summary>
        public float ShellEjectSpeed;
    }

    /// <summary>
    /// Component for active tracer visuals.
    /// </summary>
    public struct ActiveTracer : IComponentData
    {
        public float3 StartPosition;
        public float3 EndPosition;
        public float Speed;
        public float Progress; // 0-1
        public float MaxLifetime;
        public float ElapsedTime;
    }

    /// <summary>
    /// Tag for decal entities.
    /// </summary>
    public struct DecalTag : IComponentData
    {
        public float Lifetime;
        public float ElapsedTime;
        public float FadeStartTime;
    }
}
