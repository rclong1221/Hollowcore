using Unity.Entities;
using UnityEngine;
using DIG.Voxel;

namespace DIG.Voxel.Authoring
{
    public class ToolBitAuthoring : MonoBehaviour
    {
        [Header("Bit Type")]
        public ToolBitType Type = ToolBitType.StandardBit;
        public float DamageMultiplier = 1f;
        
        [Header("Shape Override")]
        public VoxelDamageShapeType ShapeType = VoxelDamageShapeType.Point;
        public float ShapeParam1 = 0f;
        public float ShapeParam2 = 0f;
        public float ShapeParam3 = 0f;
        
        [Header("Material Interaction")]
        public VoxelDamageType DamageType = VoxelDamageType.Mining;
        public float MaterialResistanceBonus = 0f;
        
        [Header("Durability")]
        public float MaxDurability = 100f;

    }

    public class ToolBitBaker : Baker<ToolBitAuthoring>
    {
        public override void Bake(ToolBitAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new ToolBit
            {
                Type = authoring.Type,
                DamageMultiplier = authoring.DamageMultiplier,
                ShapeType = authoring.ShapeType,
                ShapeParam1 = authoring.ShapeParam1,
                ShapeParam2 = authoring.ShapeParam2,
                ShapeParam3 = authoring.ShapeParam3,
                DamageType = authoring.DamageType,
                Durability = authoring.MaxDurability,
                MaxDurability = authoring.MaxDurability,
                MaterialResistanceBonus = authoring.MaterialResistanceBonus
            });
        }
    }
}
