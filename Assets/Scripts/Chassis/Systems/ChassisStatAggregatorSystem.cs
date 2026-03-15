using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Chassis.Systems
{
    /// <summary>
    /// Aggregates all limb stats from ChassisState slots into a single ChassisAggregateStats
    /// component on the player entity. Runs before EquippedStatsSystem so it can feed into
    /// the framework's modifier pipeline.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial class ChassisStatAggregatorSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var limbStatLookup = GetComponentLookup<LimbStatBlock>(true);
            var limbInstanceLookup = GetComponentLookup<LimbInstance>(true);
            var chassisStateLookup = GetComponentLookup<ChassisState>(true);

            foreach (var (chassisLink, aggregateStats) in
                     SystemAPI.Query<RefRO<ChassisLink>, RefRW<ChassisAggregateStats>>())
            {
                var chassisEntity = chassisLink.ValueRO.ChassisEntity;
                if (chassisEntity == Entity.Null) continue;
                if (!chassisStateLookup.HasComponent(chassisEntity)) continue;

                var chassisState = chassisStateLookup[chassisEntity];
                var aggregate = new LimbStatBlock();

                // Sum stats from all equipped limbs
                AccumulateSlot(ref aggregate, chassisState.Head, ref limbStatLookup, ref limbInstanceLookup);
                AccumulateSlot(ref aggregate, chassisState.Torso, ref limbStatLookup, ref limbInstanceLookup);
                AccumulateSlot(ref aggregate, chassisState.LeftArm, ref limbStatLookup, ref limbInstanceLookup);
                AccumulateSlot(ref aggregate, chassisState.RightArm, ref limbStatLookup, ref limbInstanceLookup);
                AccumulateSlot(ref aggregate, chassisState.LeftLeg, ref limbStatLookup, ref limbInstanceLookup);
                AccumulateSlot(ref aggregate, chassisState.RightLeg, ref limbStatLookup, ref limbInstanceLookup);

                aggregateStats.ValueRW = new ChassisAggregateStats
                {
                    TotalDamage = aggregate.BonusDamage,
                    TotalArmor = aggregate.BonusArmor,
                    TotalMoveSpeed = aggregate.BonusMoveSpeed,
                    TotalMaxHealth = aggregate.BonusMaxHealth,
                    TotalAttackSpeed = aggregate.BonusAttackSpeed,
                    TotalStamina = aggregate.BonusStamina,
                    TotalHeatResistance = aggregate.HeatResistance,
                    TotalToxinResistance = aggregate.ToxinResistance,
                    TotalFallDamageReduction = aggregate.FallDamageReduction,
                    EquippedLimbCount = chassisState.EquippedCount
                };
            }
        }

        private static void AccumulateSlot(
            ref LimbStatBlock aggregate, Entity limbEntity,
            ref ComponentLookup<LimbStatBlock> statLookup,
            ref ComponentLookup<LimbInstance> instanceLookup)
        {
            if (limbEntity == Entity.Null) return;
            if (!statLookup.HasComponent(limbEntity)) return;

            var stats = statLookup[limbEntity];

            // Check for district affinity bonus
            float multiplier = 1f;
            if (instanceLookup.HasComponent(limbEntity))
            {
                var instance = instanceLookup[limbEntity];
                // District affinity check would go here when current district tracking is implemented
                // For now, base stats only (EPIC 1.5 adds memory bonuses)
                _ = instance;
            }

            aggregate.BonusDamage += stats.BonusDamage * multiplier;
            aggregate.BonusArmor += stats.BonusArmor * multiplier;
            aggregate.BonusMoveSpeed += stats.BonusMoveSpeed * multiplier;
            aggregate.BonusMaxHealth += stats.BonusMaxHealth * multiplier;
            aggregate.BonusAttackSpeed += stats.BonusAttackSpeed * multiplier;
            aggregate.BonusStamina += stats.BonusStamina * multiplier;
            aggregate.HeatResistance += stats.HeatResistance * multiplier;
            aggregate.ToxinResistance += stats.ToxinResistance * multiplier;
            aggregate.FallDamageReduction += stats.FallDamageReduction * multiplier;
        }
    }

    /// <summary>
    /// Aggregated stats from all equipped limbs. Lives on the player entity.
    /// Read by EquippedStatsSystem to feed into the modifier pipeline.
    /// </summary>
    public struct ChassisAggregateStats : IComponentData
    {
        public float TotalDamage;
        public float TotalArmor;
        public float TotalMoveSpeed;
        public float TotalMaxHealth;
        public float TotalAttackSpeed;
        public float TotalStamina;
        public float TotalHeatResistance;
        public float TotalToxinResistance;
        public float TotalFallDamageReduction;
        public int EquippedLimbCount;
    }
}
