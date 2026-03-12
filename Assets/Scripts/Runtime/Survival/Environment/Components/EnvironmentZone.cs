using Unity.Entities;
using Unity.NetCode;

namespace DIG.Survival.Environment
{
    /// <summary>
    /// Defines the type of environment zone.
    /// </summary>
    public enum EnvironmentZoneType : byte
    {
        /// <summary>Normal pressurized area - no oxygen drain, no hazards.</summary>
        Pressurized = 0,

        /// <summary>Vacuum/space - oxygen drains, requires EVA suit.</summary>
        Vacuum = 1,

        /// <summary>Toxic atmosphere - oxygen drains faster, may cause damage.</summary>
        Toxic = 2,

        /// <summary>Radioactive zone - accumulates radiation exposure.</summary>
        Radioactive = 3,

        /// <summary>Extreme cold - affects temperature.</summary>
        Cold = 4,

        /// <summary>Extreme heat - affects temperature.</summary>
        Hot = 5,

        /// <summary>Underwater - oxygen drains, different movement.</summary>
        Underwater = 6
    }

    /// <summary>
    /// Component on zone trigger entities that defines environment properties.
    /// Place on trigger colliders to create hazard zones, pressurized areas, etc.
    /// </summary>
    public struct EnvironmentZone : IComponentData
    {
        /// <summary>
        /// Type of environment in this zone.
        /// </summary>
        public EnvironmentZoneType ZoneType;

        /// <summary>
        /// If true, oxygen is required in this zone (drains OxygenTank).
        /// Typically true for Vacuum, Toxic, Underwater.
        /// </summary>
        public bool OxygenRequired;

        /// <summary>
        /// Multiplier for oxygen depletion rate in this zone.
        /// 1.0 = normal, 2.0 = double drain, 0.5 = half drain.
        /// Only applies if OxygenRequired is true.
        /// </summary>
        public float OxygenDepletionMultiplier;

        /// <summary>
        /// Temperature in this zone (Celsius).
        /// Used by temperature system for hypothermia/hyperthermia.
        /// </summary>
        public float Temperature;

        /// <summary>
        /// Radiation accumulation rate per second in this zone.
        /// 0 = no radiation. Only applies if ZoneType is Radioactive.
        /// </summary>
        public float RadiationRate;

        /// <summary>
        /// Optional display name for UI.
        /// </summary>
        public Unity.Collections.FixedString64Bytes DisplayName;

        /// <summary>
        /// If true, this zone contributes to stress when unlit.
        /// </summary>
        public bool IsDark;

        /// <summary>
        /// Multiplier for stress accumulation in this zone.
        /// </summary>
        public float StressMultiplier;

        /// <summary>
        /// Creates a standard pressurized (safe) zone.
        /// </summary>
        public static EnvironmentZone Pressurized => new()
        {
            ZoneType = EnvironmentZoneType.Pressurized,
            OxygenRequired = false,
            OxygenDepletionMultiplier = 0f,
            Temperature = 20f,
            RadiationRate = 0f,
            IsDark = false,
            StressMultiplier = 1.0f
        };

        /// <summary>
        /// Creates a standard vacuum zone (space EVA).
        /// </summary>
        public static EnvironmentZone Vacuum => new()
        {
            ZoneType = EnvironmentZoneType.Vacuum,
            OxygenRequired = true,
            OxygenDepletionMultiplier = 1f,
            Temperature = -270f, // Space is cold
            RadiationRate = 0f,
            IsDark = true, // Space is dark usually (unless near sun)
            StressMultiplier = 1.5f
        };
    }

    /// <summary>
    /// Component on entities (players) tracking which environment zone they're in.
    /// Updated by EnvironmentZoneDetectionSystem.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct CurrentEnvironmentZone : IComponentData
    {
        /// <summary>
        /// Entity reference to the zone the entity is currently in.
        /// Entity.Null if not in any defined zone (defaults to Pressurized).
        /// </summary>
        [GhostField]
        public Entity ZoneEntity;

        /// <summary>
        /// Cached zone type for quick access without entity lookup.
        /// </summary>
        [GhostField]
        public EnvironmentZoneType ZoneType;

        /// <summary>
        /// Cached: Does this zone require oxygen?
        /// </summary>
        [GhostField]
        public bool OxygenRequired;

        /// <summary>
        /// Cached: Oxygen depletion multiplier in current zone.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float OxygenDepletionMultiplier;

        /// <summary>
        /// Cached: Radiation rate in current zone.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float RadiationRate;

        /// <summary>
        /// Cached: Is this zone dark?
        /// </summary>
        [GhostField]
        public bool IsDark;

        /// <summary>
        /// Cached: Stress Multiplier.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float StressMultiplier;

        /// <summary>
        /// Cached: Temperature in current zone.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float Temperature;

        /// <summary>
        /// Default state: in a pressurized zone with no hazards.
        /// </summary>
        public static CurrentEnvironmentZone Default => new()
        {
            ZoneEntity = Entity.Null,
            ZoneType = EnvironmentZoneType.Pressurized,
            OxygenRequired = false,
            OxygenDepletionMultiplier = 0f,
            RadiationRate = 0f,
            IsDark = false,
            StressMultiplier = 1.0f,
            Temperature = 20f
        };
    }

    /// <summary>
    /// Tag component indicating this entity can be affected by environment zones.
    /// Add to players, NPCs that need to react to hazards.
    /// </summary>
    public struct EnvironmentSensitive : IComponentData { }

    /// <summary>
    /// Defines the shape type for environment zone bounds.
    /// </summary>
    public enum ZoneShapeType : byte
    {
        Box = 0,
        Sphere = 1,
        Capsule = 2
    }

    /// <summary>
    /// Stores zone bounds for AABB/distance checks without using physics colliders.
    /// This avoids all physics simulation issues with trigger volumes.
    /// </summary>
    public struct ZoneBounds : IComponentData
    {
        /// <summary>Shape of the zone (Box, Sphere, or Capsule)</summary>
        public ZoneShapeType Shape;
        
        /// <summary>World position of the zone center</summary>
        public Unity.Mathematics.float3 Center;
        
        /// <summary>Half extents for Box shape (x, y, z)</summary>
        public Unity.Mathematics.float3 HalfExtents;
        
        /// <summary>Radius for Sphere/Capsule shapes</summary>
        public float Radius;
        
        /// <summary>Half height for Capsule shape</summary>
        public float HalfHeight;

        /// <summary>
        /// Checks if a point is inside this zone.
        /// </summary>
        public bool ContainsPoint(Unity.Mathematics.float3 point)
        {
            switch (Shape)
            {
                case ZoneShapeType.Box:
                    var localPoint = point - Center;
                    return Unity.Mathematics.math.abs(localPoint.x) <= HalfExtents.x &&
                           Unity.Mathematics.math.abs(localPoint.y) <= HalfExtents.y &&
                           Unity.Mathematics.math.abs(localPoint.z) <= HalfExtents.z;
                
                case ZoneShapeType.Sphere:
                    return Unity.Mathematics.math.distancesq(point, Center) <= Radius * Radius;
                
                case ZoneShapeType.Capsule:
                    // Capsule: check distance to line segment
                    var bottom = Center - new Unity.Mathematics.float3(0, HalfHeight, 0);
                    var top = Center + new Unity.Mathematics.float3(0, HalfHeight, 0);
                    var closestPoint = ClosestPointOnLineSegment(bottom, top, point);
                    return Unity.Mathematics.math.distancesq(point, closestPoint) <= Radius * Radius;
                
                default:
                    return false;
            }
        }

        private static Unity.Mathematics.float3 ClosestPointOnLineSegment(
            Unity.Mathematics.float3 a, Unity.Mathematics.float3 b, Unity.Mathematics.float3 p)
        {
            var ab = b - a;
            var t = Unity.Mathematics.math.dot(p - a, ab) / Unity.Mathematics.math.dot(ab, ab);
            t = Unity.Mathematics.math.clamp(t, 0f, 1f);
            return a + t * ab;
        }
    }
}
