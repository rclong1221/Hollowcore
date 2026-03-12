using Unity.Entities;
using UnityEngine;
using DIG.Voxel;

namespace DIG.Voxel.Authoring
{
    /// <summary>
    /// Authoring component for MeleeVoxelDamageConfig.
    /// Add this to melee weapon prefabs to enable voxel destruction.
    /// </summary>
    /// Add this to melee weapon prefabs to enable voxel destruction.
    /// </summary>
    [AddComponentMenu("DIG/Voxel/Melee Voxel Damage Authoring")]
    public class MeleeVoxelDamageAuthoring : MonoBehaviour
    {
        [Header("Damage Settings")]
        [Tooltip("Damage dealt per hit to voxels.")]
        public float VoxelDamage = 25f;

        [Tooltip("Damage type for material resistance calculations.")]
        public VoxelDamageType DamageType = VoxelDamageType.Mining;

        [Tooltip("Shape type (usually Point for precise mining, or Sphere for heavy impact).")]
        public VoxelDamageShapeType ShapeType = VoxelDamageShapeType.Point;

    }

    public class MeleeVoxelDamageBaker : Baker<MeleeVoxelDamageAuthoring>
    {
        public override void Bake(MeleeVoxelDamageAuthoring authoring)
        {
            // We need Dynamic transform usage because the system tracks position for swing detection
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new MeleeVoxelDamageConfig
            {
                VoxelDamage = authoring.VoxelDamage,
                DamageType = authoring.DamageType,
                ShapeType = authoring.ShapeType
            });

            // Add runtime state component (initialized to default)
            AddComponent(entity, new MeleeVoxelHitState
            {
                HasHitThisSwing = false,
                LastComboIndex = -1,
                CooldownRemaining = 0f
            });
        }
    }
}
