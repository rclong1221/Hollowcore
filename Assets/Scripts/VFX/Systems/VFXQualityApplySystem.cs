using Unity.Entities;

namespace DIG.VFX.Systems
{
    /// <summary>
    /// EPIC 16.7 Phase 7: Applies VFXQualityPreset values to VFXBudgetConfig and VFXLODConfig.
    /// Only runs when VFXQualityState.IsDirty is true.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(VFXBudgetSystem))]
    public partial struct VFXQualityApplySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VFXQualityState>();
            state.RequireForUpdate<VFXBudgetConfig>();
            state.RequireForUpdate<VFXLODConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var qualityState = SystemAPI.GetSingleton<VFXQualityState>();
            if (!qualityState.IsDirty) return;

            qualityState.IsDirty = false;
            SystemAPI.SetSingleton(qualityState);

            var (budget, lod, dynBudget) = GetPresetValues(qualityState.CurrentPreset);
            SystemAPI.SetSingleton(budget);
            SystemAPI.SetSingleton(lod);

            if (SystemAPI.HasSingleton<VFXDynamicBudget>())
                SystemAPI.SetSingleton(dynBudget);
        }

        private static (VFXBudgetConfig, VFXLODConfig, VFXDynamicBudget) GetPresetValues(VFXQualityPreset preset)
        {
            return preset switch
            {
                VFXQualityPreset.Ultra => (
                    new VFXBudgetConfig
                    {
                        CombatBudget = 32, EnvironmentBudget = 48, AbilityBudget = 24,
                        DeathBudget = 16, UIBudget = 32, AmbientBudget = 24,
                        InteractionBudget = 16, GlobalMaxPerFrame = 128
                    },
                    new VFXLODConfig { FullDistance = 25f, ReducedDistance = 60f, MinimalDistance = 120f },
                    new VFXDynamicBudget
                    {
                        Enabled = true, TargetFrameTimeMs = 16.67f,
                        MinBudgetMultiplier = 0.5f, MaxBudgetMultiplier = 1.5f,
                        CurrentMultiplier = 1f, SmoothingFrames = 30f
                    }
                ),

                VFXQualityPreset.High => (
                    VFXBudgetConfig.Default,
                    VFXLODConfig.Default,
                    VFXDynamicBudget.Default
                ),

                VFXQualityPreset.Medium => (
                    new VFXBudgetConfig
                    {
                        CombatBudget = 8, EnvironmentBudget = 12, AbilityBudget = 6,
                        DeathBudget = 4, UIBudget = 16, AmbientBudget = 4,
                        InteractionBudget = 4, GlobalMaxPerFrame = 32
                    },
                    new VFXLODConfig { FullDistance = 10f, ReducedDistance = 30f, MinimalDistance = 60f },
                    new VFXDynamicBudget
                    {
                        Enabled = true, TargetFrameTimeMs = 16.67f,
                        MinBudgetMultiplier = 0.25f, MaxBudgetMultiplier = 0.75f,
                        CurrentMultiplier = 1f, SmoothingFrames = 30f
                    }
                ),

                VFXQualityPreset.Low => (
                    new VFXBudgetConfig
                    {
                        CombatBudget = 4, EnvironmentBudget = 6, AbilityBudget = 3,
                        DeathBudget = 2, UIBudget = 12, AmbientBudget = 0,
                        InteractionBudget = 2, GlobalMaxPerFrame = 16
                    },
                    new VFXLODConfig { FullDistance = 8f, ReducedDistance = 20f, MinimalDistance = 40f },
                    new VFXDynamicBudget
                    {
                        Enabled = true, TargetFrameTimeMs = 16.67f,
                        MinBudgetMultiplier = 0.1f, MaxBudgetMultiplier = 0.5f,
                        CurrentMultiplier = 1f, SmoothingFrames = 30f
                    }
                ),

                VFXQualityPreset.Minimal => (
                    new VFXBudgetConfig
                    {
                        CombatBudget = 2, EnvironmentBudget = 2, AbilityBudget = 2,
                        DeathBudget = 2, UIBudget = 8, AmbientBudget = 0,
                        InteractionBudget = 1, GlobalMaxPerFrame = 8
                    },
                    new VFXLODConfig { FullDistance = 5f, ReducedDistance = 12f, MinimalDistance = 20f },
                    new VFXDynamicBudget
                    {
                        Enabled = true, TargetFrameTimeMs = 33.33f,
                        MinBudgetMultiplier = 0.1f, MaxBudgetMultiplier = 0.25f,
                        CurrentMultiplier = 1f, SmoothingFrames = 30f
                    }
                ),

                _ => (VFXBudgetConfig.Default, VFXLODConfig.Default, VFXDynamicBudget.Default)
            };
        }
    }
}
