using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 16.10 Phase 3: Writes surface-derived noise modifier on NPC entities.
    /// NPCs with GroundSurfaceState emit surface-based noise that feeds into
    /// the hearing detection pipeline (HearingDetectionSystem reads SurfaceNoiseModifier).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct SurfaceStealthModifierSystem : ISystem
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
                !toggles.EnableStealthModifiers)
                return;

            var configSingleton = SystemAPI.GetSingleton<SurfaceGameplayConfigSingleton>();

            foreach (var (groundSurface, noiseMod) in
                SystemAPI.Query<RefRO<GroundSurfaceState>, RefRW<SurfaceNoiseModifier>>()
                    .WithNone<PlayerTag>())
            {
                float multiplier = 1.0f;
                if (groundSurface.ValueRO.IsGrounded)
                {
                    int idx = (int)groundSurface.ValueRO.SurfaceId;
                    ref var blob = ref configSingleton.Config.Value;
                    if (idx >= 0 && idx < blob.Modifiers.Length)
                        multiplier = blob.Modifiers[idx].NoiseMultiplier;
                }
                noiseMod.ValueRW.Multiplier = multiplier;
            }
        }
    }
}
