using Unity.Entities;

namespace DIG.Voxel
{
    /// <summary>
    /// Shape types for voxel destruction.
    /// Each shape has different parameters stored in VoxelDamageRequest.Param1-3.
    /// </summary>
    public enum VoxelDamageShapeType : byte
    {
        /// <summary>Single voxel destruction (basic pickaxe).</summary>
        Point = 0,
        
        /// <summary>Spherical destruction (explosions, AOE tools). Param1 = radius.</summary>
        Sphere = 1,
        
        /// <summary>Cylindrical destruction (pounder bit, vehicle drill). Param1 = radius, Param2 = height.</summary>
        Cylinder = 2,
        
        /// <summary>Conical destruction (flamethrower, spray). Param1 = angle (degrees), Param2 = length, Param3 = tipRadius.</summary>
        Cone = 3,
        
        /// <summary>Capsule destruction (laser beam, tunnel bore). Param1 = radius, Param2 = length.</summary>
        Capsule = 4,
        
        /// <summary>Box destruction (precision cutter). Param1 = extentX, Param2 = extentY, Param3 = extentZ.</summary>
        Box = 5
    }
    
    /// <summary>
    /// Damage falloff types for shaped destruction.
    /// Controls how damage decreases from center to edge.
    /// </summary>
    public enum VoxelDamageFalloff : byte
    {
        /// <summary>Uniform damage throughout shape (drills, cutters).</summary>
        None = 0,
        
        /// <summary>Damage decreases linearly from center (spray tools).</summary>
        Linear = 1,
        
        /// <summary>Damage = Base * (1 - (dist/radius)^2) (realistic explosions).</summary>
        Quadratic = 2,
        
        /// <summary>Physical falloff model (shockwaves).</summary>
        InverseSquare = 3,
        
        /// <summary>Only affects outer surface (hollow sphere).</summary>
        Shell = 4,
        
        /// <summary>Only affects center, tapers to edge (focused beams).</summary>
        Core = 5
    }
    
    /// <summary>
    /// Damage type for material interaction.
    /// Materials have different resistances to different damage types.
    /// </summary>
    public enum VoxelDamageType : byte
    {
        /// <summary>Standard mining damage (picks, drills).</summary>
        Mining = 0,
        
        /// <summary>Explosive damage (grenades, dynamite).</summary>
        Explosive = 1,
        
        /// <summary>Heat damage (flamethrower, plasma).</summary>
        Heat = 2,
        
        /// <summary>Crushing damage (hammers, heavy equipment).</summary>
        Crush = 3,
        
        /// <summary>Laser damage (mining laser, cutting beam).</summary>
        Laser = 4,
        
        /// <summary>Electric damage (shock arcs).</summary>
        Electric = 5,
        
        /// <summary>Corrosive damage (acid, dot).</summary>
        Corrosive = 6
    }
}
