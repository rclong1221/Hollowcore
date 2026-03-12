using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Surface;

namespace DIG.VFX.Bridges
{
    /// <summary>
    /// EPIC 16.7 Phase 2: Drains SurfaceImpactQueue and creates VFXRequest entities.
    /// Routes surface impact VFX through the unified pipeline while preserving
    /// existing audio/decal paths in SurfaceImpactPresenterSystem.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(Systems.VFXExecutionSystem))]
    public partial class SurfaceImpactVFXBridgeSystem : SystemBase
    {
        private const int MaxBridgedPerFrame = 32;

        protected override void OnCreate()
        {
            RequireForUpdate<VFXBudgetConfig>();
        }

        protected override void OnUpdate()
        {
            // Check bridge activation flag
            if (!SystemAPI.HasSingleton<VFXBudgetConfig>()) return;

            int processed = 0;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            while (SurfaceImpactQueue.TryDequeue(out var impact) && processed < MaxBridgedPerFrame)
            {
                // Map ImpactClass to VFXCategory
                var category = MapCategory(impact.ImpactClass);

                // Compute rotation from surface normal
                var rotation = impact.Normal.Equals(float3.zero)
                    ? quaternion.identity
                    : quaternion.LookRotation(math.normalizesafe(impact.Normal), new float3(0, 1, 0));

                var entity = ecb.CreateEntity();
                ecb.AddComponent(entity, new VFXRequest
                {
                    Position = impact.Position,
                    Rotation = rotation,
                    VFXTypeId = MapSurfaceToVFXType(impact.SurfaceId, impact.ImpactClass),
                    Category = category,
                    Intensity = impact.Intensity,
                    Scale = 1f,
                    ColorTint = default,
                    Duration = 0f,
                    SourceEntity = Entity.Null,
                    Priority = MapPriority(impact.ImpactClass)
                });
                ecb.AddComponent<VFXCulled>(entity);
                ecb.SetComponentEnabled<VFXCulled>(entity, false);
                ecb.AddComponent<VFXCleanupTag>(entity);

                processed++;
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private static VFXCategory MapCategory(ImpactClass impactClass) => impactClass switch
        {
            ImpactClass.Bullet_Light or ImpactClass.Bullet_Medium or ImpactClass.Bullet_Heavy => VFXCategory.Combat,
            ImpactClass.Melee_Light or ImpactClass.Melee_Heavy => VFXCategory.Combat,
            ImpactClass.Explosion_Small or ImpactClass.Explosion_Large => VFXCategory.Combat,
            ImpactClass.Footstep => VFXCategory.Environment,
            ImpactClass.BodyFall => VFXCategory.Death,
            ImpactClass.Environmental => VFXCategory.Environment,
            _ => VFXCategory.Environment
        };

        private static int MapPriority(ImpactClass impactClass) => impactClass switch
        {
            ImpactClass.Explosion_Large => 60,
            ImpactClass.Explosion_Small => 40,
            ImpactClass.Bullet_Heavy => 20,
            ImpactClass.Melee_Heavy => 20,
            ImpactClass.Bullet_Medium => 10,
            ImpactClass.Bullet_Light => 5,
            ImpactClass.Melee_Light => 5,
            ImpactClass.BodyFall => 15,
            ImpactClass.Footstep => -10,
            ImpactClass.Environmental => 0,
            _ => 0
        };

        private static int MapSurfaceToVFXType(SurfaceID surfaceId, ImpactClass impactClass)
        {
            // Map surface + impact class to well-known VFXTypeIds
            // These IDs correspond to entries in VFXTypeDatabase
            if (impactClass == ImpactClass.Footstep)
            {
                return surfaceId == SurfaceID.Water
                    ? VFXTypeIds.FootstepWater
                    : VFXTypeIds.FootstepDust;
            }

            return surfaceId switch
            {
                SurfaceID.Metal_Thin or SurfaceID.Metal_Thick => VFXTypeIds.BulletImpactMetal,
                SurfaceID.Water => VFXTypeIds.BulletImpactWater,
                SurfaceID.Flesh or SurfaceID.Armor => VFXTypeIds.BulletImpactFlesh,
                SurfaceID.Dirt or SurfaceID.Sand or SurfaceID.Mud or SurfaceID.Grass => VFXTypeIds.BulletImpactDirt,
                _ => VFXTypeIds.BulletImpactDefault
            };
        }
    }
}
