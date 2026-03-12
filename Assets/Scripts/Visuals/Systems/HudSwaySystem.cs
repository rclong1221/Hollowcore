using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Visuals.Components;
using Unity.NetCode;
using DIG.ProceduralMotion;

namespace Visuals.Systems
{
    /// <summary>
    /// EPIC 15.25 Phase 4: Drives diegetic HUD sway from ProceduralMotionState.SmoothedLookDelta.
    /// Replaces the stub lerp-to-zero with actual mouse-input-driven sway.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(DIG.ProceduralMotion.Systems.ProceduralMotionStateSystem))]
    public partial class HudSwaySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;

            foreach (var (hud, transform, reference, motionState) in
                     SystemAPI.Query<RefRW<DiegeticHUD>, RefRO<LocalTransform>,
                             VisorReference, RefRO<ProceduralMotionState>>()
                         .WithAll<GhostOwnerIsLocal>())
            {
                if (reference.HudRoot == null) continue;

                // Read smoothed look delta from ProceduralMotionState
                float2 lookDelta = motionState.ValueRO.SmoothedLookDelta;

                // Apply sway — HUD lags opposite to look direction
                float2 targetSway = new float2(-lookDelta.x, -lookDelta.y) * 0.5f;
                hud.ValueRW.SwayOffset = math.lerp(hud.ValueRO.SwayOffset, targetSway, dt * 8f);
            }

            // Fallback for entities without ProceduralMotionState (non-player HUDs)
            foreach (var (hud, transform, reference) in
                     SystemAPI.Query<RefRW<DiegeticHUD>, RefRO<LocalTransform>, VisorReference>()
                         .WithAll<GhostOwnerIsLocal>()
                         .WithNone<ProceduralMotionState>())
            {
                if (reference.HudRoot == null) continue;
                hud.ValueRW.SwayOffset = math.lerp(hud.ValueRO.SwayOffset, float2.zero, dt * 5f);
            }
        }
    }
}
