using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Voxel;

namespace DIG.Voxel.Authoring
{
    public class VehicleDrillAuthoring : MonoBehaviour
    {
        [Header("Drill Settings")]
        public float3 LocalOffset = new float3(0, 0, 2f);
        public float3 LocalDirection = new float3(0, 0, 1);
        public float DrillRadius = 1.5f;
        public float DrillLength = 3f;
        public float DamagePerSecond = 50f;
        
        [Header("Shape & Type")]
        public VoxelDamageShapeType ShapeType = VoxelDamageShapeType.Cylinder;
        public VoxelDamageType DamageType = VoxelDamageType.Mining;
        
        [Header("Heat Management")]
        public float MaxHeat = 100f;
        public float HeatGenerationRate = 5f;
        public float HeatDissipationRate = 10f;

    }

    public class VehicleDrillBaker : Baker<VehicleDrillAuthoring>
    {
        public override void Bake(VehicleDrillAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new VehicleDrill
            {
                VehicleEntity = entity,
                LocalOffset = authoring.LocalOffset,
                LocalDirection = authoring.LocalDirection,
                DrillRadius = authoring.DrillRadius,
                DrillLength = authoring.DrillLength,
                DamagePerSecond = authoring.DamagePerSecond,
                ShapeType = authoring.ShapeType,
                DamageType = authoring.DamageType,
                IsActive = false,
                HeatLevel = 0f,
                MaxHeat = authoring.MaxHeat,
                HeatGenerationRate = authoring.HeatGenerationRate,
                HeatDissipationRate = authoring.HeatDissipationRate
            });
            
            AddComponent(entity, new HasVehicleDrills());
        }
    }
}
