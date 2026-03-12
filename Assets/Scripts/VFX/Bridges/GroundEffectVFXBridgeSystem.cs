using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Surface;

namespace DIG.VFX.Bridges
{
    /// <summary>
    /// EPIC 16.7 Phase 2: Drains GroundEffectQueue and creates VFXRequest entities.
    /// Routes ability ground effect VFX through the unified pipeline.
    /// Does NOT bridge decal spawning — decals remain in AbilityGroundEffectSystem.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(Systems.VFXExecutionSystem))]
    public partial class GroundEffectVFXBridgeSystem : SystemBase
    {
        private const int MaxBridgedPerFrame = 8;
        private const float ReferenceRadius = 3f;

        protected override void OnCreate()
        {
            RequireForUpdate<VFXBudgetConfig>();
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.HasSingleton<VFXBudgetConfig>()) return;

            int processed = 0;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            while (GroundEffectQueue.TryDequeue(out var request) && processed < MaxBridgedPerFrame)
            {
                var entity = ecb.CreateEntity();
                ecb.AddComponent(entity, new VFXRequest
                {
                    Position = request.Position,
                    Rotation = quaternion.identity,
                    VFXTypeId = MapEffectType(request.EffectType),
                    Category = VFXCategory.Ability,
                    Intensity = request.Intensity,
                    Scale = request.Radius / ReferenceRadius,
                    ColorTint = default,
                    Duration = request.Duration,
                    SourceEntity = Entity.Null,
                    Priority = 50
                });
                ecb.AddComponent<VFXCulled>(entity);
                ecb.SetComponentEnabled<VFXCulled>(entity, false);
                ecb.AddComponent<VFXCleanupTag>(entity);

                processed++;
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private static int MapEffectType(GroundEffectType effectType) => effectType switch
        {
            GroundEffectType.FireScorch => VFXTypeIds.AbilityFireBurst,
            GroundEffectType.IcePatch => VFXTypeIds.AbilityIceBurst,
            GroundEffectType.PoisonPuddle => VFXTypeIds.AbilityPoisonCloud,
            GroundEffectType.LightningScorch => VFXTypeIds.AbilityLightningStrike,
            GroundEffectType.HolyGlow => VFXTypeIds.AbilityHolySmite,
            GroundEffectType.ShadowPool => VFXTypeIds.AbilityShadowBlast,
            GroundEffectType.ArcaneBurn => VFXTypeIds.AbilityArcanePulse,
            _ => VFXTypeIds.AbilityFireBurst
        };
    }
}
