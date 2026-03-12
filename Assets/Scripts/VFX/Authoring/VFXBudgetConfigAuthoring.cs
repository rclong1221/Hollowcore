using Unity.Entities;
using UnityEngine;

namespace DIG.VFX.Authoring
{
    /// <summary>
    /// EPIC 16.7: Authoring component for VFX budget configuration singleton.
    /// Place on a GameObject in a subscene to override default budget values.
    /// </summary>
    public class VFXBudgetConfigAuthoring : MonoBehaviour
    {
        [Header("Per-Category Budgets (max requests/frame)")]
        public int CombatBudget = 16;
        public int EnvironmentBudget = 24;
        public int AbilityBudget = 12;
        public int DeathBudget = 8;
        public int UIBudget = 20;
        public int AmbientBudget = 10;
        public int InteractionBudget = 8;

        [Header("Global")]
        public int GlobalMaxPerFrame = 64;

        private class Baker : Baker<VFXBudgetConfigAuthoring>
        {
            public override void Bake(VFXBudgetConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new VFXBudgetConfig
                {
                    CombatBudget = authoring.CombatBudget,
                    EnvironmentBudget = authoring.EnvironmentBudget,
                    AbilityBudget = authoring.AbilityBudget,
                    DeathBudget = authoring.DeathBudget,
                    UIBudget = authoring.UIBudget,
                    AmbientBudget = authoring.AmbientBudget,
                    InteractionBudget = authoring.InteractionBudget,
                    GlobalMaxPerFrame = authoring.GlobalMaxPerFrame
                });
            }
        }
    }
}
