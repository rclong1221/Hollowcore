using Unity.Entities;
using DIG.Combat.Components;
using Player.Components;

namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: Copies TalentPassiveStats bonuses to player's AttackStats/DefenseStats/Health.
    /// Runs AFTER LevelStatScalingSystem (which overwrites stats from level+equipment),
    /// so talent bonuses are additive on top of the base pipeline.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DIG.Progression.LevelStatScalingSystem))]
    public partial class TalentStatBridgeSystem : SystemBase
    {
        private ComponentLookup<TalentLink> _talentLinkLookup;
        private ComponentLookup<TalentPassiveStats> _passiveLookup;

        protected override void OnCreate()
        {
            RequireForUpdate<SkillTreeRegistrySingleton>();
            _talentLinkLookup = GetComponentLookup<TalentLink>(true);
            _passiveLookup = GetComponentLookup<TalentPassiveStats>(true);
        }

        protected override void OnUpdate()
        {
            _talentLinkLookup.Update(this);
            _passiveLookup.Update(this);

            foreach (var (attack, defense, health, entity) in
                SystemAPI.Query<RefRW<AttackStats>, RefRW<DefenseStats>, RefRW<Health>>()
                    .WithEntityAccess())
            {
                if (!_talentLinkLookup.HasComponent(entity)) continue;
                var link = _talentLinkLookup[entity];
                if (link.TalentChild == Entity.Null) continue;
                if (!_passiveLookup.HasComponent(link.TalentChild)) continue;

                var talent = _passiveLookup[link.TalentChild];

                // Skip if no bonuses (common case — player hasn't allocated anything)
                if (talent.BonusAttackPower == 0f && talent.BonusSpellPower == 0f &&
                    talent.BonusCritChance == 0f && talent.BonusCritDamage == 0f &&
                    talent.BonusDefense == 0f && talent.BonusArmor == 0f &&
                    talent.BonusMaxHealth == 0f)
                    continue;

                // Add talent bonuses on top of level + equipment stats
                attack.ValueRW.AttackPower += talent.BonusAttackPower;
                attack.ValueRW.SpellPower += talent.BonusSpellPower;
                attack.ValueRW.CritChance += talent.BonusCritChance;
                attack.ValueRW.CritMultiplier += talent.BonusCritDamage;

                defense.ValueRW.Defense += talent.BonusDefense;
                defense.ValueRW.Armor += talent.BonusArmor;

                // Health — maintain ratio when max changes
                if (talent.BonusMaxHealth != 0f)
                {
                    float ratio = health.ValueRO.Max > 0f
                        ? health.ValueRO.Current / health.ValueRO.Max
                        : 1f;
                    health.ValueRW.Max += talent.BonusMaxHealth;
                    health.ValueRW.Current = ratio * health.ValueRW.Max;
                }
            }
        }
    }
}
