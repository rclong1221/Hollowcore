using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Items;
using DIG.Items.Systems;

namespace DIG.Combat.Resources.Systems
{
    /// <summary>
    /// EPIC 16.8 Phase 3: Applies equipment bonuses to ResourcePool max/regen values.
    /// Reads ResourcePoolBase (bake-time values) + PlayerEquippedStats (gear bonuses).
    /// Computes effective Max and RegenRate for each slot.
    /// Clamps Current to new Max if Max decreased (unequipping gear).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EquippedStatsSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct ResourceModifierApplySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResourcePool>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (pool, poolBase, equippedStats) in
                SystemAPI.Query<RefRW<ResourcePool>, RefRO<ResourcePoolBase>, RefRO<PlayerEquippedStats>>())
            {
                ref var p = ref pool.ValueRW;
                var b = poolBase.ValueRO;
                var eq = equippedStats.ValueRO;

                ApplyModifiers(ref p.Slot0, b.Slot0BaseMax, b.Slot0BaseRegen, eq);
                ApplyModifiers(ref p.Slot1, b.Slot1BaseMax, b.Slot1BaseRegen, eq);
            }
        }

        private static void ApplyModifiers(ref ResourceSlot slot, float baseMax, float baseRegen,
            PlayerEquippedStats eq)
        {
            if (slot.Type == ResourceType.None) return;

            float maxBonus = 0f;
            float regenBonus = 0f;

            switch (slot.Type)
            {
                case ResourceType.Mana:
                    maxBonus = eq.TotalMaxManaBonus;
                    regenBonus = eq.TotalManaRegenBonus;
                    break;
                case ResourceType.Energy:
                    maxBonus = eq.TotalMaxEnergyBonus;
                    regenBonus = eq.TotalEnergyRegenBonus;
                    break;
                case ResourceType.Stamina:
                    maxBonus = eq.TotalMaxStaminaBonus;
                    regenBonus = eq.TotalStaminaRegenBonus;
                    break;
            }

            float oldMax = slot.Max;
            slot.Max = baseMax + maxBonus;
            slot.RegenRate = baseRegen + regenBonus;

            // Clamp Current if Max decreased (unequipping gear)
            if (slot.Max < oldMax && slot.Current > slot.Max)
            {
                slot.Current = slot.Max;
            }
        }
    }
}
