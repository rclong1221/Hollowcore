using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Combat.Components;
using DIG.Combat.Resources;
using DIG.Items;
using Player.Components;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Recalculates base stats from level + stat allocations + equipment.
    /// Writes to AttackStats, DefenseStats, Health.Max, and ResourcePoolBase.
    /// Runs after EquippedStatsSystem (gear aggregation) and before ResourceModifierApplySystem.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DIG.Items.Systems.EquippedStatsSystem))]
    [UpdateBefore(typeof(DIG.Combat.Resources.Systems.ResourceModifierApplySystem))]
    public partial struct LevelStatScalingSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<CharacterAttributes, PlayerProgression>()
                .WithAllRW<AttackStats>()
                .WithAllRW<DefenseStats>()
                .WithAllRW<Health>()
                .Build(ref state);
            state.RequireForUpdate<ProgressionConfigSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<ProgressionConfigSingleton>();
            ref var scaling = ref config.StatScaling.Value;

            var entities = _query.ToEntityArray(Allocator.Temp);
            var attrArray = _query.ToComponentDataArray<CharacterAttributes>(Allocator.Temp);

            var equipLookup = state.GetComponentLookup<PlayerEquippedStats>(true);
            var resourceBaseLookup = state.GetComponentLookup<ResourcePoolBase>(false);

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var attrs = attrArray[i];
                int level = math.clamp(attrs.Level, 1, scaling.StatsPerLevel.Length);
                int levelIndex = level - 1;

                ref var baseStats = ref scaling.StatsPerLevel[levelIndex];

                // Attribute contribution coefficients
                // Strength → AttackPower, Vitality → MaxHealth, Intelligence → SpellPower, Dexterity → Defense
                float strBonus = (attrs.Strength - 10) * 0.5f;   // per point above base 10
                float dexBonus = (attrs.Dexterity - 10) * 0.3f;
                float intBonus = (attrs.Intelligence - 10) * 0.5f;
                float vitBonus = (attrs.Vitality - 10) * 2.0f;

                // Equipment bonuses
                float equipDamage = 0f, equipArmor = 0f, equipHealth = 0f;
                float equipMaxMana = 0f, equipManaRegen = 0f;
                float equipMaxStamina = 0f, equipStaminaRegen = 0f;
                if (equipLookup.HasComponent(entity))
                {
                    var equip = equipLookup[entity];
                    equipDamage = equip.TotalBaseDamage;
                    equipArmor = equip.TotalArmor;
                    equipHealth = equip.TotalMaxHealthBonus;
                    equipMaxMana = equip.TotalMaxManaBonus;
                    equipManaRegen = equip.TotalManaRegenBonus;
                    equipMaxStamina = equip.TotalMaxStaminaBonus;
                    equipStaminaRegen = equip.TotalStaminaRegenBonus;
                }

                // Write AttackStats
                var attack = state.EntityManager.GetComponentData<AttackStats>(entity);
                attack.AttackPower = baseStats.AttackPower + strBonus + equipDamage;
                attack.SpellPower = baseStats.SpellPower + intBonus;
                state.EntityManager.SetComponentData(entity, attack);

                // Write DefenseStats
                var defense = state.EntityManager.GetComponentData<DefenseStats>(entity);
                defense.Defense = baseStats.Defense + dexBonus;
                defense.Armor = baseStats.Armor + equipArmor;
                state.EntityManager.SetComponentData(entity, defense);

                // Write Health.Max (preserve current as proportion)
                var health = state.EntityManager.GetComponentData<Health>(entity);
                float newMax = baseStats.MaxHealth + vitBonus + equipHealth;
                if (newMax > 0f && health.Max > 0f)
                {
                    float ratio = health.Current / health.Max;
                    health.Max = newMax;
                    health.Current = ratio * newMax;
                }
                else
                {
                    health.Max = newMax;
                    health.Current = newMax;
                }
                state.EntityManager.SetComponentData(entity, health);

                // Write ResourcePoolBase (level-scaled values for ResourceModifierApplySystem)
                if (resourceBaseLookup.HasComponent(entity))
                {
                    var rpBase = resourceBaseLookup[entity];
                    rpBase.Slot0BaseMax = baseStats.MaxStamina + equipMaxStamina;
                    rpBase.Slot0BaseRegen = baseStats.StaminaRegen + equipStaminaRegen;
                    rpBase.Slot1BaseMax = baseStats.MaxMana + equipMaxMana;
                    rpBase.Slot1BaseRegen = baseStats.ManaRegen + equipManaRegen;
                    resourceBaseLookup[entity] = rpBase;
                }
            }

            entities.Dispose();
            attrArray.Dispose();
        }
    }
}
