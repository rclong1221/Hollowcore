using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Core.Input;

namespace DIG.Targeting.Systems
{
    /// <summary>
    /// Reads ParadigmSettings.ActiveTargetingMode and writes TargetData.Mode on the local player
    /// when the mode changes. Clears stale TargetEntity when switching away from entity-based modes.
    ///
    /// This system is the ECS-side counterpart to TargetingConfigurable (managed MonoBehaviour).
    /// TargetingConfigurable handles the immediate paradigm transition; this system ensures
    /// the ECS singleton and TargetData stay consistent on subsequent frames.
    ///
    /// EPIC 18.19 - Phase 2
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class TargetingModeDispatcherSystem : SystemBase
    {
        private TargetingMode _cachedMode;

        protected override void OnCreate()
        {
            RequireForUpdate<ParadigmSettings>();
            _cachedMode = TargetingMode.CameraRaycast;
        }

        protected override void OnUpdate()
        {
            var settings = SystemAPI.GetSingleton<ParadigmSettings>();
            if (!settings.IsValid) return;

            var newMode = settings.ActiveTargetingMode;

            if (newMode != _cachedMode)
                _cachedMode = newMode;

            // Write Mode EVERY frame — the player entity may not exist when the
            // paradigm first switches (ghost not spawned yet). Without per-frame
            // enforcement, TargetData.Mode stays at its default CameraRaycast(0).
            foreach (var (targetData, _) in SystemAPI.Query<RefRW<TargetData>, RefRO<GhostOwnerIsLocal>>())
            {
                if (targetData.ValueRO.Mode != newMode)
                {
                    var entityPrevMode = targetData.ValueRO.Mode;
                    targetData.ValueRW.Mode = newMode;

                    // Clear stale target when switching away from entity-based modes
                    bool wasEntityBased = entityPrevMode == TargetingMode.ClickSelect || entityPrevMode == TargetingMode.LockOn;
                    bool isEntityBased = newMode == TargetingMode.ClickSelect || newMode == TargetingMode.LockOn;
                    if (wasEntityBased && !isEntityBased)
                    {
                        targetData.ValueRW.TargetEntity = Entity.Null;
                        targetData.ValueRW.HasValidTarget = false;
                    }

                }
            }

            // Per-frame: compute AimDirection toward TargetEntity for ClickSelect mode
            if (_cachedMode == TargetingMode.ClickSelect)
            {
                foreach (var (targetData, ltw, _) in
                    SystemAPI.Query<RefRW<TargetData>, RefRO<LocalToWorld>, RefRO<GhostOwnerIsLocal>>())
                {
                    var target = targetData.ValueRO.TargetEntity;
                    if (target == Entity.Null) continue;
                    if (!SystemAPI.HasComponent<LocalToWorld>(target)) continue;

                    float3 targetPos = SystemAPI.GetComponent<LocalToWorld>(target).Position;
                    float3 playerPos = ltw.ValueRO.Position;
                    float3 toTarget = targetPos - playerPos;
                    float3 aimDir = new float3(toTarget.x, 0f, toTarget.z);

                    if (math.lengthsq(aimDir) > 0.0001f)
                        targetData.ValueRW.AimDirection = math.normalize(aimDir);
                }
            }
        }
    }
}
