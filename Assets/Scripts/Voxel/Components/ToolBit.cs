using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Voxel
{
    /// <summary>
    /// EPIC 15.10: Tool bit type enumeration.
    /// Different bits provide different destruction characteristics.
    /// </summary>
    public enum ToolBitType : byte
    {
        None = 0,
        
        // Drill bits
        StandardBit = 1,        // Balanced point damage
        DiamondBit = 2,         // High damage to hard materials
        TungstenBit = 3,        // Long-lasting, moderate damage
        HollowCoreBit = 4,      // Fast but lower damage
        
        // Shaped bits
        ConicalBit = 10,        // Cone-shaped destruction
        FlatBit = 11,           // Shallow, wide cylinder
        
        // Specialized bits
        SamplingBit = 20,       // Low damage, high precision for ore sampling
        ExcavatorBit = 21,      // Large cylinder for tunnel boring
        LaserBit = 22           // Long-range capsule, requires power
    }
    
    /// <summary>
    /// EPIC 15.10: Tool bit component that modifies tool destruction behavior.
    /// Attached to tool entities or stored in player inventory.
    /// </summary>
    public struct ToolBit : IComponentData
    {
        /// <summary>Type of bit.</summary>
        public ToolBitType Type;
        
        /// <summary>Damage multiplier (1.0 = normal).</summary>
        public float DamageMultiplier;
        
        /// <summary>Shape type override (or preserve tool's default).</summary>
        public VoxelDamageShapeType ShapeType;
        
        /// <summary>Shape parameter 1 (e.g., radius).</summary>
        public float ShapeParam1;
        
        /// <summary>Shape parameter 2 (e.g., height/length).</summary>
        public float ShapeParam2;
        
        /// <summary>Shape parameter 3 (e.g., angle/extents).</summary>
        public float ShapeParam3;
        
        /// <summary>Damage type modifier.</summary>
        public VoxelDamageType DamageType;
        
        /// <summary>Durability remaining (0-1, 0 = broken).</summary>
        public float Durability;
        
        /// <summary>Max durability for this bit type.</summary>
        public float MaxDurability;
        
        /// <summary>Material resistance bonus (additive to tool's base).</summary>
        public float MaterialResistanceBonus;
        
        // ========== PRESET BITS ==========
        
        /// <summary>Standard balanced drill bit.</summary>
        public static ToolBit Standard => new()
        {
            Type = ToolBitType.StandardBit,
            DamageMultiplier = 1f,
            ShapeType = VoxelDamageShapeType.Point,
            ShapeParam1 = 0f,
            ShapeParam2 = 0f,
            ShapeParam3 = 0f,
            DamageType = VoxelDamageType.Mining,
            Durability = 100f,
            MaxDurability = 100f,
            MaterialResistanceBonus = 0f
        };
        
        /// <summary>Diamond bit for hard materials.</summary>
        public static ToolBit Diamond => new()
        {
            Type = ToolBitType.DiamondBit,
            DamageMultiplier = 1.5f,
            ShapeType = VoxelDamageShapeType.Point,
            ShapeParam1 = 0f,
            ShapeParam2 = 0f,
            ShapeParam3 = 0f,
            DamageType = VoxelDamageType.Mining,
            Durability = 200f,
            MaxDurability = 200f,
            MaterialResistanceBonus = 0.3f  // 30% better against resistant materials
        };
        
        /// <summary>Tungsten bit - longer lasting.</summary>
        public static ToolBit Tungsten => new()
        {
            Type = ToolBitType.TungstenBit,
            DamageMultiplier = 0.9f,
            ShapeType = VoxelDamageShapeType.Point,
            ShapeParam1 = 0f,
            ShapeParam2 = 0f,
            ShapeParam3 = 0f,
            DamageType = VoxelDamageType.Mining,
            Durability = 350f,
            MaxDurability = 350f,
            MaterialResistanceBonus = 0.1f
        };
        
        /// <summary>Hollow core - faster but weaker.</summary>
        public static ToolBit HollowCore => new()
        {
            Type = ToolBitType.HollowCoreBit,
            DamageMultiplier = 0.7f,
            ShapeType = VoxelDamageShapeType.Sphere,
            ShapeParam1 = 0.5f,  // Small sphere radius
            ShapeParam2 = 0f,
            ShapeParam3 = 0f,
            DamageType = VoxelDamageType.Mining,
            Durability = 60f,
            MaxDurability = 60f,
            MaterialResistanceBonus = -0.1f  // Weaker against hard materials
        };
        
        /// <summary>Conical bit for directional drilling.</summary>
        public static ToolBit Conical => new()
        {
            Type = ToolBitType.ConicalBit,
            DamageMultiplier = 1.2f,
            ShapeType = VoxelDamageShapeType.Cone,
            ShapeParam1 = 25f,  // Cone angle degrees
            ShapeParam2 = 2f,   // Cone length
            ShapeParam3 = 0.2f, // Tip radius
            DamageType = VoxelDamageType.Mining,
            Durability = 80f,
            MaxDurability = 80f,
            MaterialResistanceBonus = 0f
        };
        
        /// <summary>Flat bit for wide shallow excavation.</summary>
        public static ToolBit Flat => new()
        {
            Type = ToolBitType.FlatBit,
            DamageMultiplier = 0.8f,
            ShapeType = VoxelDamageShapeType.Cylinder,
            ShapeParam1 = 1.5f,  // Cylinder radius
            ShapeParam2 = 0.5f,  // Cylinder height (shallow)
            ShapeParam3 = 0f,
            DamageType = VoxelDamageType.Mining,
            Durability = 120f,
            MaxDurability = 120f,
            MaterialResistanceBonus = 0f
        };
        
        /// <summary>Sampling bit for precise ore extraction.</summary>
        public static ToolBit Sampling => new()
        {
            Type = ToolBitType.SamplingBit,
            DamageMultiplier = 0.3f,  // Very low damage
            ShapeType = VoxelDamageShapeType.Point,
            ShapeParam1 = 0f,
            ShapeParam2 = 0f,
            ShapeParam3 = 0f,
            DamageType = VoxelDamageType.Mining,
            Durability = 50f,
            MaxDurability = 50f,
            MaterialResistanceBonus = 0.5f  // High precision
        };
        
        /// <summary>Large excavator bit for tunnel boring.</summary>
        public static ToolBit Excavator => new()
        {
            Type = ToolBitType.ExcavatorBit,
            DamageMultiplier = 2f,
            ShapeType = VoxelDamageShapeType.Cylinder,
            ShapeParam1 = 3f,   // Large radius
            ShapeParam2 = 4f,   // Deep cylinder
            ShapeParam3 = 0f,
            DamageType = VoxelDamageType.Crush,
            Durability = 150f,
            MaxDurability = 150f,
            MaterialResistanceBonus = 0.2f
        };
        
        /// <summary>Laser bit for long-range precision.</summary>
        public static ToolBit Laser => new()
        {
            Type = ToolBitType.LaserBit,
            DamageMultiplier = 1.8f,
            ShapeType = VoxelDamageShapeType.Capsule,
            ShapeParam1 = 0.2f,   // Narrow radius
            ShapeParam2 = 10f,    // Long range
            ShapeParam3 = 0f,
            DamageType = VoxelDamageType.Laser,
            Durability = 100f,  // Uses power, not durability
            MaxDurability = 100f,
            MaterialResistanceBonus = 0.4f
        };
        
        /// <summary>Get preset bit by type.</summary>
        public static ToolBit GetPreset(ToolBitType type) => type switch
        {
            ToolBitType.StandardBit => Standard,
            ToolBitType.DiamondBit => Diamond,
            ToolBitType.TungstenBit => Tungsten,
            ToolBitType.HollowCoreBit => HollowCore,
            ToolBitType.ConicalBit => Conical,
            ToolBitType.FlatBit => Flat,
            ToolBitType.SamplingBit => Sampling,
            ToolBitType.ExcavatorBit => Excavator,
            ToolBitType.LaserBit => Laser,
            _ => Standard
        };
    }
    
    /// <summary>
    /// EPIC 15.10: Currently equipped bit on a tool.
    /// </summary>
    public struct EquippedToolBit : IComponentData
    {
        public ToolBit Bit;
    }
    
    /// <summary>
    /// EPIC 15.10: Buffer for storing tool bits in inventory.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ToolBitInventory : IBufferElementData
    {
        public ToolBit Bit;
        public int Quantity;
    }
}
