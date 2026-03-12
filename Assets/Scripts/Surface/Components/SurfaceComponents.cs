using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 15.24: Surface type identifiers for material-aware effects.
    /// Compile-time integer IDs — no string lookups at runtime.
    /// </summary>
    public enum SurfaceID : byte
    {
        Default = 0,
        Concrete = 1,
        Metal_Thin = 2,
        Metal_Thick = 3,
        Wood = 4,
        Dirt = 5,
        Sand = 6,
        Grass = 7,
        Gravel = 8,
        Snow = 9,
        Ice = 10,
        Water = 11,
        Mud = 12,
        Glass = 13,
        Flesh = 14,
        Armor = 15,
        Fabric = 16,
        Plastic = 17,
        Stone = 18,
        Ceramic = 19,
        Foliage = 20,
        Bark = 21,
        Rubber = 22,
        Energy_Shield = 23
    }

    /// <summary>
    /// EPIC 15.24: Categorizes impact intensity by weapon/event type.
    /// Drives particle scale, decal size, audio priority, camera shake.
    /// </summary>
    public enum ImpactClass : byte
    {
        Bullet_Light = 0,    // Pistol, SMG
        Bullet_Medium = 1,   // Rifle, LMG
        Bullet_Heavy = 2,    // Shotgun, Sniper
        Melee_Light = 3,     // Knife, fist
        Melee_Heavy = 4,     // Katana, hammer
        Explosion_Small = 5, // Grenade
        Explosion_Large = 6, // Rocket, C4
        Footstep = 7,        // Walking/running
        BodyFall = 8,        // Ragdoll hitting ground
        Environmental = 9    // Falling debris, door slam
    }

    /// <summary>
    /// EPIC 15.24: Effect quality tier based on camera distance.
    /// Computed per impact, read by presentation systems.
    /// </summary>
    public enum EffectLODTier : byte
    {
        Full = 0,     // 0-15m: all effects
        Reduced = 1,  // 15-40m: 50% particles, half decals
        Minimal = 2,  // 40-60m: billboard sprite only
        Culled = 3    // 60m+: skip entirely
    }

    /// <summary>
    /// EPIC 15.24: Unified impact event data.
    /// Produced by any impact source, consumed by SurfaceImpactPresenterSystem.
    /// </summary>
    public struct SurfaceImpactData
    {
        public float3 Position;
        public float3 Normal;
        public float3 Velocity;
        public SurfaceID SurfaceId;
        public ImpactClass ImpactClass;
        public int SurfaceMaterialId;   // existing int ID for managed SurfaceMaterialRegistry lookup
        public float Intensity;         // 0-1
        public EffectLODTier LODTier;
    }

    /// <summary>
    /// EPIC 15.24: Created by WeaponFireSystem (Burst) on hitscan hits.
    /// Consumed by HitscanImpactBridgeSystem (managed) which enqueues to SurfaceImpactQueue.
    /// </summary>
    public struct EnvironmentHitRequest : IComponentData
    {
        public float3 Position;
        public float3 Normal;
        public float3 Velocity;
        public uint ServerTick;   // for deduplication during prediction
    }
}
