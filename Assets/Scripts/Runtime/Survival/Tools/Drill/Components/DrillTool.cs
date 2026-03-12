using Unity.Entities;
using Unity.NetCode;
using DIG.Voxel;

namespace DIG.Survival.Tools
{
    /// <summary>
    /// Drill tool component for mining voxels and collecting resources.
    /// Placed on drill tool entities alongside Tool, ToolDurability, ToolUsageState.
    /// EPIC 15.10: Now integrates with unified destruction system.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct DrillTool : IComponentData
    {
        /// <summary>
        /// Damage dealt to voxels per second while drilling.
        /// </summary>
        public float VoxelDamagePerSecond;

        /// <summary>
        /// Maximum range the drill can reach (meters).
        /// </summary>
        public float Range;

        /// <summary>
        /// Multiplier applied to resources collected from voxels.
        /// 1.0 = standard, 2.0 = double resources, etc.
        /// </summary>
        public float ResourceMultiplier;
        
        // ========== EPIC 15.10: Destruction Shape Configuration ==========
        
        /// <summary>
        /// Shape type for voxel destruction. Default is Sphere.
        /// </summary>
        public VoxelDamageShapeType ShapeType;
        
        /// <summary>
        /// Damage type for material resistance calculations.
        /// </summary>
        public VoxelDamageType DamageType;
        
        /// <summary>
        /// Destruction radius (for sphere shape).
        /// </summary>
        public float DestructionRadius;

        /// <summary>
        /// Creates a default drill configuration.
        /// </summary>
        public static DrillTool Default => new()
        {
            VoxelDamagePerSecond = 10f,
            Range = 3f,
            ResourceMultiplier = 1f,
            ShapeType = VoxelDamageShapeType.Sphere,
            DamageType = VoxelDamageType.Mining,
            DestructionRadius = 0.3f
        };
        
        /// <summary>
        /// Power drill configuration - larger destruction area.
        /// </summary>
        public static DrillTool PowerDrill => new()
        {
            VoxelDamagePerSecond = 30f,
            Range = 3.5f,
            ResourceMultiplier = 1.2f,
            ShapeType = VoxelDamageShapeType.Sphere,
            DamageType = VoxelDamageType.Mining,
            DestructionRadius = 0.5f
        };
    }
}
