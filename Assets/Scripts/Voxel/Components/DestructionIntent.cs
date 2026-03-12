using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Voxel
{
    /// <summary>
    /// EPIC 15.10: Raw destruction intent from a source.
    /// This is the intermediate format between destruction sources and the VoxelDamageRequest.
    /// DestructionMediatorSystem converts this to VoxelDamageRequest after applying modifiers.
    /// </summary>
    public struct DestructionIntent
    {
        /// <summary>Entity initiating the destruction.</summary>
        public Entity SourceEntity;
        
        /// <summary>World position of the source (for validation).</summary>
        public float3 SourcePosition;
        
        /// <summary>Target position for destruction center.</summary>
        public float3 TargetPosition;
        
        /// <summary>Rotation for directional shapes.</summary>
        public quaternion TargetRotation;
        
        /// <summary>Shape of destruction.</summary>
        public VoxelDamageShapeType ShapeType;
        
        /// <summary>Type of damage.</summary>
        public VoxelDamageType DamageType;
        
        /// <summary>Falloff type.</summary>
        public VoxelDamageFalloff Falloff;
        
        /// <summary>Base damage amount.</summary>
        public float Damage;
        
        /// <summary>Edge damage multiplier.</summary>
        public float EdgeMultiplier;
        
        /// <summary>Shape param 1 (radius, angle, etc.).</summary>
        public float Param1;
        
        /// <summary>Shape param 2 (height, length, etc.).</summary>
        public float Param2;
        
        /// <summary>Shape param 3 (tip radius, extent Z, etc.).</summary>
        public float Param3;
        
        /// <summary>Whether this intent is valid and should be processed.</summary>
        public bool IsValid;
        
        /// <summary>Create a simple point destruction intent.</summary>
        public static DestructionIntent CreatePoint(Entity source, float3 sourcePos, float3 targetPos, float damage, VoxelDamageType damageType = VoxelDamageType.Mining)
        {
            return new DestructionIntent
            {
                SourceEntity = source,
                SourcePosition = sourcePos,
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
                IsValid = true
            };
        }
        
        /// <summary>Create an invalid/empty intent.</summary>
        public static DestructionIntent Invalid => new DestructionIntent { IsValid = false };
    }
}
