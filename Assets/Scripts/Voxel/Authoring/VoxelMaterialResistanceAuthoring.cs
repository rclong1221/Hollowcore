using Unity.Entities;
using UnityEngine;
using DIG.Voxel;

namespace DIG.Voxel.Authoring
{
    public class VoxelMaterialResistanceAuthoring : MonoBehaviour
    {
        [Range(0f, 1f)] public float ResistanceToMining = 0f;
        [Range(0f, 1f)] public float ResistanceToExplosive = 0f;
        [Range(0f, 1f)] public float ResistanceToHeat = 0f;
        [Range(0f, 1f)] public float ResistanceToCrush = 0f;
        [Range(0f, 1f)] public float ResistanceToLaser = 0f;

    }

    public class VoxelMaterialResistanceBaker : Baker<VoxelMaterialResistanceAuthoring>
    {
        public override void Bake(VoxelMaterialResistanceAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new VoxelMaterialResistance
            {
                ResistanceToMining = authoring.ResistanceToMining,
                ResistanceToExplosive = authoring.ResistanceToExplosive,
                ResistanceToHeat = authoring.ResistanceToHeat,
                ResistanceToCrush = authoring.ResistanceToCrush,
                ResistanceToLaser = authoring.ResistanceToLaser
            });
        }
    }
}
