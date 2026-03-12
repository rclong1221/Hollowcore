using Unity.Entities;
using Unity.NetCode;

namespace DIG.Items.Systems
{
    /// <summary>
    /// EPIC 16.6: Aggregates ItemStatBlock from all equipped items into PlayerEquippedStats.
    /// Runs on server/local only. Recomputes every frame (cheap — typically 2-3 equipped items).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class EquippedStatsSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var statLookup = GetComponentLookup<ItemStatBlock>(true);

            foreach (var (equippedBuffer, stats) in
                     SystemAPI.Query<DynamicBuffer<EquippedItemElement>, RefRW<PlayerEquippedStats>>())
            {
                // Reset to zero
                var aggregate = new PlayerEquippedStats();

                for (int i = 0; i < equippedBuffer.Length; i++)
                {
                    var equipped = equippedBuffer[i];
                    if (equipped.ItemEntity == Entity.Null) continue;

                    if (statLookup.HasComponent(equipped.ItemEntity))
                    {
                        var itemStats = statLookup[equipped.ItemEntity];
                        aggregate.TotalBaseDamage += itemStats.BaseDamage;
                        aggregate.TotalAttackSpeed += itemStats.AttackSpeed;
                        aggregate.TotalCritChance += itemStats.CritChance;
                        aggregate.TotalCritMultiplier += itemStats.CritMultiplier;
                        aggregate.TotalArmor += itemStats.Armor;
                        aggregate.TotalMaxHealthBonus += itemStats.MaxHealthBonus;
                        aggregate.TotalMovementSpeedBonus += itemStats.MovementSpeedBonus;
                        aggregate.TotalDamageResistance += itemStats.DamageResistance;

                        // Resource modifiers (EPIC 16.8)
                        aggregate.TotalMaxManaBonus += itemStats.MaxManaBonus;
                        aggregate.TotalManaRegenBonus += itemStats.ManaRegenBonus;
                        aggregate.TotalMaxEnergyBonus += itemStats.MaxEnergyBonus;
                        aggregate.TotalEnergyRegenBonus += itemStats.EnergyRegenBonus;
                        aggregate.TotalMaxStaminaBonus += itemStats.MaxStaminaBonus;
                        aggregate.TotalStaminaRegenBonus += itemStats.StaminaRegenBonus;

                        // Progression modifier (EPIC 16.14)
                        aggregate.TotalXPBonusPercent += itemStats.XPBonusPercent;
                    }
                }

                stats.ValueRW = aggregate;
            }
        }
    }
}
