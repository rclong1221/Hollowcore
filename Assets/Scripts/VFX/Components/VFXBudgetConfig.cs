using Unity.Entities;

namespace DIG.VFX
{
    /// <summary>
    /// EPIC 16.7: Per-category VFX budget caps singleton.
    /// Authored via VFXBudgetConfigAuthoring or created with defaults by bootstrap.
    /// </summary>
    public struct VFXBudgetConfig : IComponentData
    {
        public int CombatBudget;
        public int EnvironmentBudget;
        public int AbilityBudget;
        public int DeathBudget;
        public int UIBudget;
        public int AmbientBudget;
        public int InteractionBudget;
        public int GlobalMaxPerFrame;

        public int GetBudget(VFXCategory category) => category switch
        {
            VFXCategory.Combat => CombatBudget,
            VFXCategory.Environment => EnvironmentBudget,
            VFXCategory.Ability => AbilityBudget,
            VFXCategory.Death => DeathBudget,
            VFXCategory.UI => UIBudget,
            VFXCategory.Ambient => AmbientBudget,
            VFXCategory.Interaction => InteractionBudget,
            _ => 8
        };

        public static VFXBudgetConfig Default => new()
        {
            CombatBudget = 16,
            EnvironmentBudget = 24,
            AbilityBudget = 12,
            DeathBudget = 8,
            UIBudget = 20,
            AmbientBudget = 10,
            InteractionBudget = 8,
            GlobalMaxPerFrame = 64
        };
    }

    /// <summary>
    /// EPIC 16.7 Phase 3: Runtime budget adjustment based on frame performance.
    /// Disabled by default (static budgets only).
    /// </summary>
    public struct VFXDynamicBudget : IComponentData
    {
        public bool Enabled;
        public float TargetFrameTimeMs;
        public float MinBudgetMultiplier;
        public float MaxBudgetMultiplier;
        public float CurrentMultiplier;
        public float SmoothingFrames;

        public static VFXDynamicBudget Default => new()
        {
            Enabled = false,
            TargetFrameTimeMs = 16.67f,
            MinBudgetMultiplier = 0.25f,
            MaxBudgetMultiplier = 1.5f,
            CurrentMultiplier = 1.0f,
            SmoothingFrames = 30f
        };
    }
}
