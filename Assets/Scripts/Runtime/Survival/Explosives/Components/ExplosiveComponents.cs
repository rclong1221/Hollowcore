using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Survival.Explosives
{
    /// <summary>
    /// Types of explosives available.
    /// </summary>
    public enum ExplosiveType : byte
    {
        None = 0,
        MicroCharge = 1,      // Small, quick fuse, limited blast
        CuttingCharge = 2,    // Medium, shaped charge for precision
        BreachingCharge = 3   // Large, maximum destruction
    }

    /// <summary>
    /// Buffer element for player's explosive inventory.
    /// Tracks quantity of each explosive type.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    [InternalBufferCapacity(4)]
    public struct ExplosiveInventory : IBufferElementData
    {
        /// <summary>
        /// Type of explosive.
        /// </summary>
        [GhostField]
        public ExplosiveType Type;

        /// <summary>
        /// Quantity in inventory.
        /// </summary>
        [GhostField]
        public int Quantity;
    }

    /// <summary>
    /// Tracks currently selected explosive type for placement.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct SelectedExplosive : IComponentData
    {
        /// <summary>
        /// Currently selected explosive type.
        /// </summary>
        [GhostField]
        public ExplosiveType Type;
    }

    /// <summary>
    /// Core component for placed explosive entities.
    /// Tracks fuse state and ownership.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct PlacedExplosive : IComponentData
    {
        /// <summary>
        /// Type of this explosive.
        /// </summary>
        [GhostField]
        public ExplosiveType Type;

        /// <summary>
        /// Time remaining until detonation (seconds).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float FuseTimeRemaining;

        /// <summary>
        /// Total fuse time for UI display.
        /// </summary>
        public float InitialFuseTime;

        /// <summary>
        /// True when countdown has started.
        /// </summary>
        [GhostField]
        public bool IsArmed;

        /// <summary>
        /// Time since placement (for arming delay).
        /// </summary>
        public float TimeSincePlacement;

        /// <summary>
        /// Entity that placed this explosive.
        /// </summary>
        [GhostField]
        public Entity PlacerEntity;

        /// <summary>
        /// Surface normal where explosive is attached.
        /// </summary>
        public float3 AttachedNormal;
    }

    /// <summary>
    /// Stats defining explosion behavior.
    /// Configured per explosive type.
    /// </summary>
    public struct ExplosiveStats : IComponentData
    {
        /// <summary>
        /// Radius for entity damage (meters).
        /// </summary>
        public float BlastRadius;

        /// <summary>
        /// Maximum damage at explosion center.
        /// </summary>
        public float BlastDamage;

        /// <summary>
        /// Radius for voxel destruction (meters).
        /// </summary>
        public float VoxelDamageRadius;

        /// <summary>
        /// Impulse force applied to physics bodies.
        /// </summary>
        public float PhysicsForce;

        /// <summary>
        /// Damage falloff exponent (1 = linear, 2 = quadratic).
        /// </summary>
        public float FalloffExponent;
        
        /// <summary>
        /// EPIC 15.10: Shape type for voxel destruction.
        /// Spherical by default, can be Cone for shaped charges.
        /// </summary>
        public DIG.Voxel.VoxelDamageShapeType ShapeType;
        
        /// <summary>
        /// EPIC 15.10: Cone angle for shaped charges (degrees).
        /// Only used when ShapeType is Cone.
        /// </summary>
        public float ConeAngle;
        
        /// <summary>
        /// EPIC 15.10: Cone/cylinder length for shaped charges (meters).
        /// Only used when ShapeType is Cone or Cylinder.
        /// </summary>
        public float ShapeLength;

        /// <summary>
        /// Default stats for MicroCharge.
        /// </summary>
        public static ExplosiveStats MicroCharge => new()
        {
            BlastRadius = 2f,
            BlastDamage = 50f,
            VoxelDamageRadius = 1f,
            PhysicsForce = 500f,
            FalloffExponent = 2f,
            ShapeType = DIG.Voxel.VoxelDamageShapeType.Sphere,
            ConeAngle = 0f,
            ShapeLength = 0f
        };

        /// <summary>
        /// Default stats for CuttingCharge (shaped charge).
        /// Uses cone shape for directional cutting.
        /// </summary>
        public static ExplosiveStats CuttingCharge => new()
        {
            BlastRadius = 4f,
            BlastDamage = 100f,
            VoxelDamageRadius = 0.5f, // Narrow radius, uses cone instead
            PhysicsForce = 1000f,
            FalloffExponent = 1f,
            ShapeType = DIG.Voxel.VoxelDamageShapeType.Cone,
            ConeAngle = 30f,
            ShapeLength = 6f
        };

        /// <summary>
        /// Default stats for BreachingCharge.
        /// </summary>
        public static ExplosiveStats BreachingCharge => new()
        {
            BlastRadius = 6f,
            BlastDamage = 200f,
            VoxelDamageRadius = 5f,
            PhysicsForce = 2000f,
            FalloffExponent = 2f,
            ShapeType = DIG.Voxel.VoxelDamageShapeType.Sphere,
            ConeAngle = 0f,
            ShapeLength = 0f
        };

        /// <summary>
        /// Get default stats for a given explosive type.
        /// </summary>
        public static ExplosiveStats GetDefaults(ExplosiveType type) => type switch
        {
            ExplosiveType.MicroCharge => MicroCharge,
            ExplosiveType.CuttingCharge => CuttingCharge,
            ExplosiveType.BreachingCharge => BreachingCharge,
            _ => MicroCharge
        };

        /// <summary>
        /// Get default fuse time for a given explosive type.
        /// </summary>
        public static float GetDefaultFuseTime(ExplosiveType type) => type switch
        {
            ExplosiveType.MicroCharge => 3f,
            ExplosiveType.CuttingCharge => 5f,
            ExplosiveType.BreachingCharge => 5f,
            _ => 3f
        };
    }

    /// <summary>
    /// Tag component added when explosive should detonate.
    /// Triggers detonation system processing.
    /// </summary>
    public struct DetonationRequest : IComponentData { }

    /// <summary>
    /// Request to place an explosive at a location.
    /// Processed by server-authoritative spawn system.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    [InternalBufferCapacity(2)]
    public struct PlaceExplosiveRequest : IBufferElementData
    {
        /// <summary>
        /// Type of explosive to place.
        /// </summary>
        public ExplosiveType Type;

        /// <summary>
        /// World position to place at.
        /// </summary>
        public float3 Position;

        /// <summary>
        /// Surface normal for attachment orientation.
        /// </summary>
        public float3 Normal;
    }

    /// <summary>
    /// Singleton containing explosive prefab references.
    /// </summary>
    public struct ExplosivePrefabs : IComponentData
    {
        public Entity MicroChargePrefab;
        public Entity CuttingChargePrefab;
        public Entity BreachingChargePrefab;

        /// <summary>
        /// Get prefab for a given explosive type.
        /// </summary>
        public readonly Entity GetPrefab(ExplosiveType type) => type switch
        {
            ExplosiveType.MicroCharge => MicroChargePrefab,
            ExplosiveType.CuttingCharge => CuttingChargePrefab,
            ExplosiveType.BreachingCharge => BreachingChargePrefab,
            _ => Entity.Null
        };
    }

    /// <summary>
    /// Event raised when an explosion occurs.
    /// Used by presentation systems for VFX/audio.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ExplosionEvent : IComponentData
    {
        /// <summary>
        /// World position of explosion center.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float3 Position;

        /// <summary>
        /// Type of explosive that detonated.
        /// </summary>
        [GhostField]
        public ExplosiveType Type;

        /// <summary>
        /// Blast radius for VFX scaling.
        /// </summary>
        [GhostField(Quantization = 10)]
        public float BlastRadius;

        /// <summary>
        /// Network tick when explosion occurred.
        /// </summary>
        [GhostField]
        public NetworkTick Tick;
    }

    /// <summary>
    /// Client-side state for explosive visual/audio feedback.
    /// </summary>
    public struct ExplosiveVisualState : IComponentData
    {
        /// <summary>
        /// True if beeping audio is playing.
        /// </summary>
        public bool IsBeeping;

        /// <summary>
        /// Current beep interval (decreases as fuse runs down).
        /// </summary>
        public float BeepInterval;

        /// <summary>
        /// Time since last beep.
        /// </summary>
        public float TimeSinceBeep;

        /// <summary>
        /// True if warning light is on.
        /// </summary>
        public bool LightOn;
    }
}
