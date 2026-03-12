using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Voxel
{
    /// <summary>
    /// EPIC 15.10: Vehicle-mounted drill component.
    /// Defines a drill attachment point on a vehicle that can damage voxels.
    /// </summary>
    public struct VehicleDrill : IComponentData
    {
        /// <summary>Entity of the vehicle this drill is attached to.</summary>
        public Entity VehicleEntity;
        
        /// <summary>Local offset from vehicle origin to drill tip.</summary>
        public float3 LocalOffset;
        
        /// <summary>Local forward direction of drill.</summary>
        public float3 LocalDirection;
        
        /// <summary>Drill radius for cylindrical destruction.</summary>
        public float DrillRadius;
        
        /// <summary>Drill length (cylinder height).</summary>
        public float DrillLength;
        
        /// <summary>Damage per second dealt by drill.</summary>
        public float DamagePerSecond;
        
        /// <summary>Shape type for destruction (usually Cylinder or Capsule).</summary>
        public VoxelDamageShapeType ShapeType;
        
        /// <summary>Damage type for material resistance.</summary>
        public VoxelDamageType DamageType;
        
        /// <summary>Whether drill is currently active.</summary>
        public bool IsActive;
        
        /// <summary>Heat buildup (0-1, affects efficiency).</summary>
        public float HeatLevel;
        
        /// <summary>Max heat before cooldown required.</summary>
        public float MaxHeat;
        
        /// <summary>Heat generation rate per second while drilling.</summary>
        public float HeatGenerationRate;
        
        /// <summary>Heat dissipation rate per second while idle.</summary>
        public float HeatDissipationRate;
        
        /// <summary>Default small mining drill configuration.</summary>
        public static VehicleDrill SmallMiningDrill => new()
        {
            LocalOffset = new float3(0, 0, 2f),
            LocalDirection = new float3(0, 0, 1),
            DrillRadius = 1.5f,
            DrillLength = 3f,
            DamagePerSecond = 50f,
            ShapeType = VoxelDamageShapeType.Cylinder,
            DamageType = VoxelDamageType.Mining,
            IsActive = false,
            HeatLevel = 0f,
            MaxHeat = 100f,
            HeatGenerationRate = 5f,
            HeatDissipationRate = 10f
        };
        
        /// <summary>Large bore tunnel drill configuration.</summary>
        public static VehicleDrill LargeTunnelDrill => new()
        {
            LocalOffset = new float3(0, 0, 5f),
            LocalDirection = new float3(0, 0, 1),
            DrillRadius = 4f,
            DrillLength = 8f,
            DamagePerSecond = 120f,
            ShapeType = VoxelDamageShapeType.Cylinder,
            DamageType = VoxelDamageType.Mining,
            IsActive = false,
            HeatLevel = 0f,
            MaxHeat = 200f,
            HeatGenerationRate = 8f,
            HeatDissipationRate = 6f
        };
        
        /// <summary>Ship-mounted laser drill.</summary>
        public static VehicleDrill ShipLaserDrill => new()
        {
            LocalOffset = new float3(0, -1, 3f),
            LocalDirection = new float3(0, 0, 1),
            DrillRadius = 0.5f,
            DrillLength = 20f,
            DamagePerSecond = 200f,
            ShapeType = VoxelDamageShapeType.Capsule,
            DamageType = VoxelDamageType.Laser,
            IsActive = false,
            HeatLevel = 0f,
            MaxHeat = 80f,
            HeatGenerationRate = 15f,
            HeatDissipationRate = 8f
        };
    }
    
    /// <summary>
    /// EPIC 15.10: Buffer for multiple drill attachments on a single vehicle.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct VehicleDrillBuffer : IBufferElementData
    {
        public VehicleDrill Drill;
    }
    
    /// <summary>
    /// EPIC 15.10: Tag component for vehicles that have drill capability.
    /// </summary>
    public struct HasVehicleDrills : IComponentData { }
    
    /// <summary>
    /// EPIC 15.10: Input component for controlling vehicle drills.
    /// Added to player/AI controller entities.
    /// </summary>
    public struct VehicleDrillInput : IComponentData
    {
        /// <summary>Which drill indices to activate (bitmask).</summary>
        public int ActiveDrillMask;
        
        /// <summary>Target aim direction in world space.</summary>
        public float3 AimDirection;
        
        /// <summary>Whether player is requesting drill activation.</summary>
        public bool RequestActive;
    }
}
