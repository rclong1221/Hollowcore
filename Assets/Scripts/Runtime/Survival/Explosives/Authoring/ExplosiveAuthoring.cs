using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Survival.Explosives;
using DIG.Voxel;
using DIG.Voxel.Authoring;

namespace DIG.Survival.Explosives.Authoring
{
    public class ExplosiveAuthoring : MonoBehaviour
    {
        [Header("Identity")]
        public ExplosiveType Type = ExplosiveType.MicroCharge;
        
        [Header("Fuse Settings")]
        public float InitialFuseTime = 3f;
        public bool StartArmed = false;
        
        [Header("Blast Stats")]
        public float BlastRadius = 5f;
        public float BlastDamage = 100f;
        public float PhysicsForce = 1000f;
        public float FalloffExponent = 2f;
        
        [Header("Voxel Destruction")]
        public float VoxelDamageRadius = 3f;
        public VoxelDamageShapeType ShapeType = VoxelDamageShapeType.Sphere;
        [Tooltip("Only for Cone shape")]
        public float ConeAngle = 30f;
        [Tooltip("Only for Cone/Cylinder/Capsule")]
        public float ShapeLength = 5f;

    }

    public class ExplosiveBaker : Baker<ExplosiveAuthoring>
    {
        public override void Bake(ExplosiveAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Runtime state
            AddComponent(entity, new PlacedExplosive
            {
                Type = authoring.Type,
                InitialFuseTime = authoring.InitialFuseTime,
                FuseTimeRemaining = authoring.InitialFuseTime,
                IsArmed = authoring.StartArmed,
                TimeSincePlacement = 0f,
                PlacerEntity = Entity.Null,
                AttachedNormal = new float3(0, 1, 0)
            });
            
            // Static stats
            AddComponent(entity, new ExplosiveStats
            {
                BlastRadius = authoring.BlastRadius,
                BlastDamage = authoring.BlastDamage,
                PhysicsForce = authoring.PhysicsForce,
                FalloffExponent = authoring.FalloffExponent,
                VoxelDamageRadius = authoring.VoxelDamageRadius,
                ShapeType = authoring.ShapeType,
                ConeAngle = authoring.ConeAngle,
                ShapeLength = authoring.ShapeLength
            });
            
            // Usually explosives are also chain triggerable
            if (GetComponent<ChainTriggerableAuthoring>() == null)
            {
                AddComponent(entity, ChainTriggerable.Explosive);
            }
        }
    }
}
