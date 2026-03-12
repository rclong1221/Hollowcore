using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: Burst-compiled system that sums passive bonuses from all talent allocations.
    /// Reads TalentAllocation buffer, looks up each node in SkillTreeRegistryBlob,
    /// accumulates SkillPassiveBonus values, writes TalentPassiveStats.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TalentRespecSystem))]
    public partial struct TalentPassiveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SkillTreeRegistrySingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var registry = SystemAPI.GetSingleton<SkillTreeRegistrySingleton>();
            ref var blob = ref registry.Registry.Value;

            foreach (var (allocations, passiveStats) in
                SystemAPI.Query<DynamicBuffer<TalentAllocation>, RefRW<TalentPassiveStats>>()
                    .WithAll<TalentChildTag>())
            {
                var stats = new TalentPassiveStats();

                for (int a = 0; a < allocations.Length; a++)
                {
                    var alloc = allocations[a];
                    if (!TryGetBonus(ref blob, alloc.TreeId, alloc.NodeId, out var bonus)) continue;
                    if (bonus.BonusType == SkillBonusType.None) continue;

                    AccumulateBonus(ref stats, bonus);
                }

                passiveStats.ValueRW = stats;
            }
        }

        private static bool TryGetBonus(ref SkillTreeRegistryBlob blob,
            ushort treeId, ushort nodeId, out SkillPassiveBonus bonus)
        {
            bonus = default;
            for (int t = 0; t < blob.Trees.Length; t++)
            {
                if (blob.Trees[t].TreeId != treeId) continue;
                for (int n = 0; n < blob.Trees[t].Nodes.Length; n++)
                {
                    if (blob.Trees[t].Nodes[n].NodeId != nodeId) continue;
                    bonus = blob.Trees[t].Nodes[n].PassiveBonus;
                    return true;
                }
                return false;
            }
            return false;
        }

        private static void AccumulateBonus(ref TalentPassiveStats stats, SkillPassiveBonus bonus)
        {
            switch (bonus.BonusType)
            {
                case SkillBonusType.MaxHealth:        stats.BonusMaxHealth += bonus.Value; break;
                case SkillBonusType.AttackPower:       stats.BonusAttackPower += bonus.Value; break;
                case SkillBonusType.SpellPower:        stats.BonusSpellPower += bonus.Value; break;
                case SkillBonusType.Defense:           stats.BonusDefense += bonus.Value; break;
                case SkillBonusType.Armor:             stats.BonusArmor += bonus.Value; break;
                case SkillBonusType.CritChance:        stats.BonusCritChance += bonus.Value; break;
                case SkillBonusType.CritDamage:        stats.BonusCritDamage += bonus.Value; break;
                case SkillBonusType.MovementSpeed:     stats.BonusMovementSpeed += bonus.Value; break;
                case SkillBonusType.CooldownReduction: stats.BonusCooldownReduction += bonus.Value; break;
                case SkillBonusType.ResourceRegen:     stats.BonusResourceRegen += bonus.Value; break;
                case SkillBonusType.DamagePercent:     stats.BonusDamagePercent += bonus.Value; break;
                case SkillBonusType.HealingPercent:    stats.BonusHealingPercent += bonus.Value; break;
            }
        }
    }
}
