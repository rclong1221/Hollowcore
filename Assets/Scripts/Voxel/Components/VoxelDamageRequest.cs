using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Voxel
{
    /// <summary>
    /// EPIC 15.10: Unified voxel damage request component.
    /// Created by DestructionMediatorSystem from DestructionIntent.
    /// Consumed by VoxelDamageValidationSystem and VoxelDamageProcessingSystem.
    /// </summary>
    public struct VoxelDamageRequest : IComponentData
    {
        /// <summary>World position of the destruction source (for validation).</summary>
        public float3 SourcePosition;
        
        /// <summary>Entity that initiated the destruction (for validation and attribution).</summary>
        public Entity SourceEntity;
        
        /// <summary>World position where destruction is centered.</summary>
        public float3 TargetPosition;
        
        /// <summary>Rotation for directional shapes (Cylinder, Cone, Capsule, Box).</summary>
        public quaternion TargetRotation;
        
        /// <summary>Shape of the destruction volume.</summary>
        public VoxelDamageShapeType ShapeType;
        
        /// <summary>Type of damage for material resistance calculations.</summary>
        public VoxelDamageType DamageType;
        
        /// <summary>How damage falls off from center to edge.</summary>
        public VoxelDamageFalloff Falloff;
        
        /// <summary>Base damage amount (DPS * deltaTime or instant damage).</summary>
        public float Damage;
        
        /// <summary>Damage multiplier at shape edge (0-1, used with falloff).</summary>
        public float EdgeMultiplier;
        
        /// <summary>Shape parameter 1. Meaning depends on ShapeType.</summary>
        public float Param1;
        
        /// <summary>Shape parameter 2. Meaning depends on ShapeType.</summary>
        public float Param2;
        
        /// <summary>Shape parameter 3. Meaning depends on ShapeType.</summary>
        public float Param3;
        
        /// <summary>Server-assigned tick when request was validated.</summary>
        public uint ValidatedTick;
        
        /// <summary>Whether this request has been validated by the server.</summary>
        public bool IsValidated;
        
        /// <summary>Whether this request has been processed.</summary>
        public bool IsProcessed;
        
        // Factory methods for common shapes
        
        /// <summary>Create a point destruction request (single voxel).</summary>
        public static VoxelDamageRequest CreatePoint(float3 sourcePos, Entity source, float3 targetPos, float damage, VoxelDamageType damageType = VoxelDamageType.Mining)
        {
            return new VoxelDamageRequest
            {
                SourcePosition = sourcePos,
                SourceEntity = source,
                TargetPosition = targetPos,
                TargetRotation = quaternion.identity,
                ShapeType = VoxelDamageShapeType.Point,
                DamageType = damageType,
                Falloff = VoxelDamageFalloff.None,
                Damage = damage,
                EdgeMultiplier = 1f,
                Param1 = 0f,
                Param2 = 0f,
                Param3 = 0f,
                ValidatedTick = 0,
                IsValidated = false,
                IsProcessed = false
            };
        }
        
        /// <summary>Create a sphere destruction request (explosions, AOE).</summary>
        public static VoxelDamageRequest CreateSphere(float3 sourcePos, Entity source, float3 targetPos, float radius, float damage, VoxelDamageFalloff falloff = VoxelDamageFalloff.Quadratic, float edgeMult = 0.1f, VoxelDamageType damageType = VoxelDamageType.Explosive)
        {
            return new VoxelDamageRequest
            {
                SourcePosition = sourcePos,
                SourceEntity = source,
                TargetPosition = targetPos,
                TargetRotation = quaternion.identity,
                ShapeType = VoxelDamageShapeType.Sphere,
                DamageType = damageType,
                Falloff = falloff,
                Damage = damage,
                EdgeMultiplier = edgeMult,
                Param1 = radius,
                Param2 = 0f,
                Param3 = 0f,
                ValidatedTick = 0,
                IsValidated = false,
                IsProcessed = false
            };
        }
        
        /// <summary>Create a cylinder destruction request (drills, vehicle bores).</summary>
        public static VoxelDamageRequest CreateCylinder(float3 sourcePos, Entity source, float3 targetPos, quaternion rotation, float radius, float height, float damage, VoxelDamageFalloff falloff = VoxelDamageFalloff.None, float edgeMult = 1f, VoxelDamageType damageType = VoxelDamageType.Mining)
        {
            return new VoxelDamageRequest
            {
                SourcePosition = sourcePos,
                SourceEntity = source,
                TargetPosition = targetPos,
                TargetRotation = rotation,
                ShapeType = VoxelDamageShapeType.Cylinder,
                DamageType = damageType,
                Falloff = falloff,
                Damage = damage,
                EdgeMultiplier = edgeMult,
                Param1 = radius,
                Param2 = height,
                Param3 = 0f,
                ValidatedTick = 0,
                IsValidated = false,
                IsProcessed = false
            };
        }
        
        /// <summary>Create a cone destruction request (flamethrower, shaped charges).</summary>
        public static VoxelDamageRequest CreateCone(float3 sourcePos, Entity source, float3 targetPos, quaternion rotation, float angleDegrees, float length, float tipRadius, float damage, VoxelDamageFalloff falloff = VoxelDamageFalloff.Linear, float edgeMult = 0.3f, VoxelDamageType damageType = VoxelDamageType.Heat)
        {
            return new VoxelDamageRequest
            {
                SourcePosition = sourcePos,
                SourceEntity = source,
                TargetPosition = targetPos,
                TargetRotation = rotation,
                ShapeType = VoxelDamageShapeType.Cone,
                DamageType = damageType,
                Falloff = falloff,
                Damage = damage,
                EdgeMultiplier = edgeMult,
                Param1 = angleDegrees,
                Param2 = length,
                Param3 = tipRadius,
                ValidatedTick = 0,
                IsValidated = false,
                IsProcessed = false
            };
        }
        
        /// <summary>Create a capsule destruction request (lasers, tunnel bores).</summary>
        public static VoxelDamageRequest CreateCapsule(float3 sourcePos, Entity source, float3 targetPos, quaternion rotation, float radius, float length, float damage, VoxelDamageFalloff falloff = VoxelDamageFalloff.Linear, float edgeMult = 0.5f, VoxelDamageType damageType = VoxelDamageType.Laser)
        {
            return new VoxelDamageRequest
            {
                SourcePosition = sourcePos,
                SourceEntity = source,
                TargetPosition = targetPos,
                TargetRotation = rotation,
                ShapeType = VoxelDamageShapeType.Capsule,
                DamageType = damageType,
                Falloff = falloff,
                Damage = damage,
                EdgeMultiplier = edgeMult,
                Param1 = radius,
                Param2 = length,
                Param3 = 0f,
                ValidatedTick = 0,
                IsValidated = false,
                IsProcessed = false
            };
        }
        
        /// <summary>Create a box destruction request (precision cutters).</summary>
        public static VoxelDamageRequest CreateBox(float3 sourcePos, Entity source, float3 targetPos, quaternion rotation, float3 extents, float damage, VoxelDamageFalloff falloff = VoxelDamageFalloff.None, float edgeMult = 1f, VoxelDamageType damageType = VoxelDamageType.Mining)
        {
            return new VoxelDamageRequest
            {
                SourcePosition = sourcePos,
                SourceEntity = source,
                TargetPosition = targetPos,
                TargetRotation = rotation,
                ShapeType = VoxelDamageShapeType.Box,
                DamageType = damageType,
                Falloff = falloff,
                Damage = damage,
                EdgeMultiplier = edgeMult,
                Param1 = extents.x,
                Param2 = extents.y,
                Param3 = extents.z,
                ValidatedTick = 0,
                IsValidated = false,
                IsProcessed = false
            };
        }
    }
}
