using Unity.Mathematics;

namespace DIG.Voxel
{
    /// <summary>
    /// EPIC 15.10: Pre-defined shape configurations for common tools.
    /// Use these as reference or starting points for tool configuration.
    /// </summary>
    public static class VoxelDamageShapePresets
    {
        // ========== MINING TOOLS ==========
        
        /// <summary>Basic pickaxe - single voxel hit.</summary>
        public static DestructionIntent BasicPickaxe(Unity.Entities.Entity source, float3 sourcePos, float3 targetPos)
        {
            return new DestructionIntent
            {
                SourceEntity = source,
                SourcePosition = sourcePos,
                TargetPosition = targetPos,
                TargetRotation = quaternion.identity,
                ShapeType = VoxelDamageShapeType.Point,
                DamageType = VoxelDamageType.Mining,
                Falloff = VoxelDamageFalloff.None,
                Damage = 25f,
                EdgeMultiplier = 1f,
                Param1 = 0f,
                Param2 = 0f,
                Param3 = 0f,
                IsValid = true
            };
        }
        
        /// <summary>Hand drill - small sphere.</summary>
        public static DestructionIntent HandDrill(Unity.Entities.Entity source, float3 sourcePos, float3 targetPos, float deltaTime)
        {
            return new DestructionIntent
            {
                SourceEntity = source,
                SourcePosition = sourcePos,
                TargetPosition = targetPos,
                TargetRotation = quaternion.identity,
                ShapeType = VoxelDamageShapeType.Sphere,
                DamageType = VoxelDamageType.Mining,
                Falloff = VoxelDamageFalloff.None,
                Damage = 15f * deltaTime,
                EdgeMultiplier = 1f,
                Param1 = 0.3f, // radius
                Param2 = 0f,
                Param3 = 0f,
                IsValid = true
            };
        }
        
        /// <summary>Power drill - medium sphere, faster.</summary>
        public static DestructionIntent PowerDrill(Unity.Entities.Entity source, float3 sourcePos, float3 targetPos, float deltaTime)
        {
            return new DestructionIntent
            {
                SourceEntity = source,
                SourcePosition = sourcePos,
                TargetPosition = targetPos,
                TargetRotation = quaternion.identity,
                ShapeType = VoxelDamageShapeType.Sphere,
                DamageType = VoxelDamageType.Mining,
                Falloff = VoxelDamageFalloff.None,
                Damage = 30f * deltaTime,
                EdgeMultiplier = 1f,
                Param1 = 0.5f, // radius
                Param2 = 0f,
                Param3 = 0f,
                IsValid = true
            };
        }
        
        /// <summary>Pounder bit - vertical cylinder.</summary>
        public static DestructionIntent PounderBit(Unity.Entities.Entity source, float3 sourcePos, float3 targetPos, quaternion rotation, float deltaTime)
        {
            return new DestructionIntent
            {
                SourceEntity = source,
                SourcePosition = sourcePos,
                TargetPosition = targetPos,
                TargetRotation = rotation,
                ShapeType = VoxelDamageShapeType.Cylinder,
                DamageType = VoxelDamageType.Mining,
                Falloff = VoxelDamageFalloff.None,
                Damage = 50f * deltaTime,
                EdgeMultiplier = 1f,
                Param1 = 0.5f, // radius
                Param2 = 2f,   // height
                Param3 = 0f,
                IsValid = true
            };
        }
        
        /// <summary>Mining laser - long capsule beam.</summary>
        public static DestructionIntent MiningLaser(Unity.Entities.Entity source, float3 sourcePos, float3 targetPos, quaternion rotation, float deltaTime)
        {
            return new DestructionIntent
            {
                SourceEntity = source,
                SourcePosition = sourcePos,
                TargetPosition = targetPos,
                TargetRotation = rotation,
                ShapeType = VoxelDamageShapeType.Capsule,
                DamageType = VoxelDamageType.Laser,
                Falloff = VoxelDamageFalloff.Linear,
                Damage = 20f * deltaTime,
                EdgeMultiplier = 0.5f,
                Param1 = 0.1f, // radius
                Param2 = 10f,  // length
                Param3 = 0f,
                IsValid = true
            };
        }
        
        /// <summary>Precision cutter - thin box.</summary>
        public static DestructionIntent PrecisionCutter(Unity.Entities.Entity source, float3 sourcePos, float3 targetPos, quaternion rotation, float deltaTime)
        {
            return new DestructionIntent
            {
                SourceEntity = source,
                SourcePosition = sourcePos,
                TargetPosition = targetPos,
                TargetRotation = rotation,
                ShapeType = VoxelDamageShapeType.Box,
                DamageType = VoxelDamageType.Mining,
                Falloff = VoxelDamageFalloff.None,
                Damage = 40f * deltaTime,
                EdgeMultiplier = 1f,
                Param1 = 1f,   // extentX
                Param2 = 1f,   // extentY
                Param3 = 0.05f, // extentZ (thin)
                IsValid = true
            };
        }
        
        // ========== EXPLOSIVES ==========
        
        /// <summary>Frag grenade - medium sphere with falloff.</summary>
        public static DestructionIntent FragGrenade(Unity.Entities.Entity source, float3 sourcePos, float3 targetPos)
        {
            return new DestructionIntent
            {
                SourceEntity = source,
                SourcePosition = sourcePos,
                TargetPosition = targetPos,
                TargetRotation = quaternion.identity,
                ShapeType = VoxelDamageShapeType.Sphere,
                DamageType = VoxelDamageType.Explosive,
                Falloff = VoxelDamageFalloff.Quadratic,
                Damage = 500f, // instant
                EdgeMultiplier = 0.1f,
                Param1 = 5f, // radius
                Param2 = 0f,
                Param3 = 0f,
                IsValid = true
            };
        }
        
        /// <summary>Dynamite - large sphere.</summary>
        public static DestructionIntent Dynamite(Unity.Entities.Entity source, float3 sourcePos, float3 targetPos)
        {
            return new DestructionIntent
            {
                SourceEntity = source,
                SourcePosition = sourcePos,
                TargetPosition = targetPos,
                TargetRotation = quaternion.identity,
                ShapeType = VoxelDamageShapeType.Sphere,
                DamageType = VoxelDamageType.Explosive,
                Falloff = VoxelDamageFalloff.Quadratic,
                Damage = 1000f,
                EdgeMultiplier = 0.05f,
                Param1 = 8f, // radius
                Param2 = 0f,
                Param3 = 0f,
                IsValid = true
            };
        }
        
        /// <summary>Shaped charge - directional cone.</summary>
        public static DestructionIntent ShapedCharge(Unity.Entities.Entity source, float3 sourcePos, float3 targetPos, quaternion rotation)
        {
            return new DestructionIntent
            {
                SourceEntity = source,
                SourcePosition = sourcePos,
                TargetPosition = targetPos,
                TargetRotation = rotation,
                ShapeType = VoxelDamageShapeType.Cone,
                DamageType = VoxelDamageType.Explosive,
                Falloff = VoxelDamageFalloff.Linear,
                Damage = 800f,
                EdgeMultiplier = 0.2f,
                Param1 = 30f, // angle degrees
                Param2 = 6f,  // length
                Param3 = 0.2f, // tip radius
                IsValid = true
            };
        }
        
        // ========== VEHICLE DRILLS ==========
        
        /// <summary>Small drill vehicle bore.</summary>
        public static DestructionIntent SmallVehicleDrill(Unity.Entities.Entity source, float3 sourcePos, float3 targetPos, quaternion rotation, float deltaTime)
        {
            return new DestructionIntent
            {
                SourceEntity = source,
                SourcePosition = sourcePos,
                TargetPosition = targetPos,
                TargetRotation = rotation,
                ShapeType = VoxelDamageShapeType.Cylinder,
                DamageType = VoxelDamageType.Crush,
                Falloff = VoxelDamageFalloff.Linear,
                Damage = 100f * deltaTime,
                EdgeMultiplier = 0.8f,
                Param1 = 1f,  // radius
                Param2 = 2f,  // height
                Param3 = 0f,
                IsValid = true
            };
        }
        
        /// <summary>Large drill vehicle bore.</summary>
        public static DestructionIntent LargeVehicleDrill(Unity.Entities.Entity source, float3 sourcePos, float3 targetPos, quaternion rotation, float deltaTime)
        {
            return new DestructionIntent
            {
                SourceEntity = source,
                SourcePosition = sourcePos,
                TargetPosition = targetPos,
                TargetRotation = rotation,
                ShapeType = VoxelDamageShapeType.Cylinder,
                DamageType = VoxelDamageType.Crush,
                Falloff = VoxelDamageFalloff.Linear,
                Damage = 200f * deltaTime,
                EdgeMultiplier = 0.8f,
                Param1 = 1.5f, // radius
                Param2 = 3f,   // height
                Param3 = 0f,
                IsValid = true
            };
        }
        
        /// <summary>Tunnel bore - capsule for long tunnels.</summary>
        public static DestructionIntent TunnelBore(Unity.Entities.Entity source, float3 sourcePos, float3 targetPos, quaternion rotation, float deltaTime)
        {
            return new DestructionIntent
            {
                SourceEntity = source,
                SourcePosition = sourcePos,
                TargetPosition = targetPos,
                TargetRotation = rotation,
                ShapeType = VoxelDamageShapeType.Capsule,
                DamageType = VoxelDamageType.Crush,
                Falloff = VoxelDamageFalloff.Shell,
                Damage = 150f * deltaTime,
                EdgeMultiplier = 1f,
                Param1 = 2f, // radius
                Param2 = 5f, // length
                Param3 = 0f,
                IsValid = true
            };
        }
        
        // ========== HEAT WEAPONS ==========
        
        /// <summary>Flamethrower - cone of fire.</summary>
        public static DestructionIntent Flamethrower(Unity.Entities.Entity source, float3 sourcePos, float3 targetPos, quaternion rotation, float deltaTime)
        {
            return new DestructionIntent
            {
                SourceEntity = source,
                SourcePosition = sourcePos,
                TargetPosition = targetPos,
                TargetRotation = rotation,
                ShapeType = VoxelDamageShapeType.Cone,
                DamageType = VoxelDamageType.Heat,
                Falloff = VoxelDamageFalloff.Linear,
                Damage = 30f * deltaTime,
                EdgeMultiplier = 0.3f,
                Param1 = 25f, // angle degrees
                Param2 = 6f,  // length
                Param3 = 0.1f, // tip radius
                IsValid = true
            };
        }
        
        /// <summary>Terraformer - large sphere for landscaping.</summary>
        public static DestructionIntent Terraformer(Unity.Entities.Entity source, float3 sourcePos, float3 targetPos, float deltaTime)
        {
            return new DestructionIntent
            {
                SourceEntity = source,
                SourcePosition = sourcePos,
                TargetPosition = targetPos,
                TargetRotation = quaternion.identity,
                ShapeType = VoxelDamageShapeType.Sphere,
                DamageType = VoxelDamageType.Mining,
                Falloff = VoxelDamageFalloff.None,
                Damage = 80f * deltaTime,
                EdgeMultiplier = 1f,
                Param1 = 3f, // radius
                Param2 = 0f,
                Param3 = 0f,
                IsValid = true
            };
        }
    }
}
