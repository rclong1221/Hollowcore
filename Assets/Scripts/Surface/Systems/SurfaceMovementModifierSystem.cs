using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 16.10 Phase 4: Reads GroundSurfaceState + SurfaceGameplayBlob,
    /// writes SurfaceMovementModifier (speed, friction, slip).
    /// Smooths values to prevent jarring changes at surface boundaries.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(GroundSurfaceQuerySystem))]
    [UpdateBefore(typeof(PlayerMovementSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct SurfaceMovementModifierSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SurfaceGameplayConfigSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // EPIC 16.10 Phase 8: Respect feature toggles
            if (SystemAPI.TryGetSingleton<SurfaceGameplayToggles>(out var toggles) &&
                !toggles.EnableMovementModifiers)
                return;

            var configSingleton = SystemAPI.GetSingleton<SurfaceGameplayConfigSingleton>();
            float dt = SystemAPI.Time.DeltaTime;
            const float smoothRate = 8.0f;

            foreach (var (groundSurface, moveMod) in
                SystemAPI.Query<RefRO<GroundSurfaceState>, RefRW<SurfaceMovementModifier>>())
            {
                float targetSpeed = 1.0f;
                float targetFriction = 1.0f;
                float targetSlip = 0f;

                if (groundSurface.ValueRO.IsGrounded)
                {
                    int idx = (int)groundSurface.ValueRO.SurfaceId;
                    ref var blob = ref configSingleton.Config.Value;
                    if (idx >= 0 && idx < blob.Modifiers.Length)
                    {
                        targetSpeed = blob.Modifiers[idx].SpeedMultiplier;
                        targetSlip = blob.Modifiers[idx].SlipFactor;
                    }

                    // Friction derived from hardness (Burst-friendly, no managed lookup)
                    targetFriction = math.lerp(0.5f, 1.5f, groundSurface.ValueRO.CachedHardness / 255.0f);
                }

                // Smooth transitions at surface boundaries
                float lerpT = math.saturate(smoothRate * dt);
                moveMod.ValueRW.SpeedMultiplier = math.lerp(moveMod.ValueRO.SpeedMultiplier, targetSpeed, lerpT);
                moveMod.ValueRW.FrictionMultiplier = math.lerp(moveMod.ValueRO.FrictionMultiplier, targetFriction, lerpT);
                moveMod.ValueRW.SlipFactor = math.lerp(moveMod.ValueRO.SlipFactor, targetSlip, lerpT);
            }
        }
    }
}
