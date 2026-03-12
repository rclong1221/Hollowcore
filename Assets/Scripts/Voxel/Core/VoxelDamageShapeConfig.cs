using UnityEngine;

namespace DIG.Voxel
{
    /// <summary>
    /// EPIC 15.10: ScriptableObject for design-time configuration of destruction shapes.
    /// Assign to tools, weapons, or explosives to define their voxel destruction behavior.
    /// </summary>
    [CreateAssetMenu(fileName = "VoxelDamageShape", menuName = "DIG/Voxel/Damage Shape Config")]
    public class VoxelDamageShapeConfig : ScriptableObject
    {
        [Header("Shape")]
        [Tooltip("Type of destruction shape")]
        public VoxelDamageShapeType ShapeType = VoxelDamageShapeType.Point;
        
        [Tooltip("Type of damage for material resistance")]
        public VoxelDamageType DamageType = VoxelDamageType.Mining;
        
        [Tooltip("How damage falls off from center to edge")]
        public VoxelDamageFalloff Falloff = VoxelDamageFalloff.None;
        
        [Header("Shape Parameters")]
        [Tooltip("Radius for Sphere, Cylinder, Capsule. Angle (degrees) for Cone. Extent X for Box.")]
        public float Param1 = 0.5f;
        
        [Tooltip("Height for Cylinder. Length for Cone, Capsule. Extent Y for Box.")]
        public float Param2 = 1f;
        
        [Tooltip("Tip radius for Cone. Extent Z for Box.")]
        public float Param3 = 0f;
        
        [Header("Damage")]
        [Tooltip("Base damage per second (for continuous) or instant damage (for explosions)")]
        public float BaseDamage = 25f;
        
        [Tooltip("Damage multiplier at shape edge (0-1)")]
        [Range(0f, 1f)]
        public float EdgeMultiplier = 1f;
        
        [Header("Validation")]
        [Tooltip("Maximum range from source to target for this shape")]
        public float MaxRange = 3f;
        
        [Tooltip("Minimum time between destruction requests (seconds)")]
        public float Cooldown = 0f;
        
        /// <summary>
        /// Create a DestructionIntent from this config.
        /// </summary>
        public DestructionIntent CreateIntent(Unity.Entities.Entity source, Unity.Mathematics.float3 sourcePos, Unity.Mathematics.float3 targetPos, Unity.Mathematics.quaternion rotation, float damageMultiplier = 1f)
        {
            return new DestructionIntent
            {
                SourceEntity = source,
                SourcePosition = sourcePos,
                TargetPosition = targetPos,
                TargetRotation = rotation,
                ShapeType = ShapeType,
                DamageType = DamageType,
                Falloff = Falloff,
                Damage = BaseDamage * damageMultiplier,
                EdgeMultiplier = EdgeMultiplier,
                Param1 = Param1,
                Param2 = Param2,
                Param3 = Param3,
                IsValid = true
            };
        }
        
#if UNITY_EDITOR
        /// <summary>
        /// Draw shape gizmo in scene view for preview.
        /// </summary>
        public void DrawGizmo(Vector3 position, Quaternion rotation, Color color)
        {
            Gizmos.color = color;
            
            switch (ShapeType)
            {
                case VoxelDamageShapeType.Point:
                    Gizmos.DrawWireSphere(position, 0.1f);
                    break;
                    
                case VoxelDamageShapeType.Sphere:
                    Gizmos.DrawWireSphere(position, Param1);
                    break;
                    
                case VoxelDamageShapeType.Cylinder:
                    DrawWireCylinder(position, rotation, Param1, Param2);
                    break;
                    
                case VoxelDamageShapeType.Cone:
                    DrawWireCone(position, rotation, Param1, Param2, Param3);
                    break;
                    
                case VoxelDamageShapeType.Capsule:
                    DrawWireCapsule(position, rotation, Param1, Param2);
                    break;
                    
                case VoxelDamageShapeType.Box:
                    Gizmos.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
                    Gizmos.DrawWireCube(Vector3.zero, new Vector3(Param1 * 2, Param2 * 2, Param3 * 2));
                    Gizmos.matrix = Matrix4x4.identity;
                    break;
            }
        }
        
        private void DrawWireCylinder(Vector3 position, Quaternion rotation, float radius, float height)
        {
            Vector3 up = rotation * Vector3.up;
            Vector3 halfHeight = up * (height * 0.5f);
            
            // Top and bottom circles
            DrawWireCircle(position + halfHeight, rotation, radius);
            DrawWireCircle(position - halfHeight, rotation, radius);
            
            // Connecting lines
            Vector3 right = rotation * Vector3.right;
            Vector3 forward = rotation * Vector3.forward;
            Gizmos.DrawLine(position + halfHeight + right * radius, position - halfHeight + right * radius);
            Gizmos.DrawLine(position + halfHeight - right * radius, position - halfHeight - right * radius);
            Gizmos.DrawLine(position + halfHeight + forward * radius, position - halfHeight + forward * radius);
            Gizmos.DrawLine(position + halfHeight - forward * radius, position - halfHeight - forward * radius);
        }
        
        private void DrawWireCone(Vector3 position, Quaternion rotation, float angleDegrees, float length, float tipRadius)
        {
            Vector3 forward = rotation * Vector3.forward;
            Vector3 tip = position;
            Vector3 baseCenter = position + forward * length;
            float baseRadius = length * Mathf.Tan(angleDegrees * Mathf.Deg2Rad * 0.5f);
            
            // Base circle
            DrawWireCircle(baseCenter, rotation, baseRadius);
            
            // Tip (small circle if tipRadius > 0)
            if (tipRadius > 0.01f)
            {
                DrawWireCircle(tip, rotation, tipRadius);
            }
            
            // Connecting lines
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Gizmos.DrawLine(tip + right * tipRadius, baseCenter + right * baseRadius);
            Gizmos.DrawLine(tip - right * tipRadius, baseCenter - right * baseRadius);
            Gizmos.DrawLine(tip + up * tipRadius, baseCenter + up * baseRadius);
            Gizmos.DrawLine(tip - up * tipRadius, baseCenter - up * baseRadius);
        }
        
        private void DrawWireCapsule(Vector3 position, Quaternion rotation, float radius, float length)
        {
            Vector3 forward = rotation * Vector3.forward;
            Vector3 halfLength = forward * (length * 0.5f);
            
            // End spheres
            Gizmos.DrawWireSphere(position + halfLength, radius);
            Gizmos.DrawWireSphere(position - halfLength, radius);
            
            // Connecting lines
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Gizmos.DrawLine(position + halfLength + right * radius, position - halfLength + right * radius);
            Gizmos.DrawLine(position + halfLength - right * radius, position - halfLength - right * radius);
            Gizmos.DrawLine(position + halfLength + up * radius, position - halfLength + up * radius);
            Gizmos.DrawLine(position + halfLength - up * radius, position - halfLength - up * radius);
        }
        
        private void DrawWireCircle(Vector3 center, Quaternion rotation, float radius, int segments = 16)
        {
            Vector3 right = rotation * Vector3.right;
            Vector3 forward = rotation * Vector3.forward;
            
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + right * radius;
            
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 point = center + (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }
#endif
    }
}
