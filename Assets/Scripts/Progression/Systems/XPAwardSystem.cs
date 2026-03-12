using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Combat.Components;
using DIG.Items;
using Player.Components;
using DIG.Party;
using DIG.PvP;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Reads KillCredited on player entities, computes XP with
    /// diminishing returns + gear bonus + rested bonus, writes to PlayerProgression.
    /// Removes KillCredited after processing (it's ephemeral, added via ECB).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class XPAwardSystem : SystemBase
    {
        private EntityQuery _killQuery;

        protected override void OnCreate()
        {
            _killQuery = GetEntityQuery(
                ComponentType.ReadOnly<KillCredited>(),
                ComponentType.ReadWrite<PlayerProgression>(),
                ComponentType.ReadOnly<CharacterAttributes>());
            RequireForUpdate<ProgressionConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<ProgressionConfigSingleton>();
            ref var curve = ref config.Curve.Value;

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var attrLookup = GetComponentLookup<CharacterAttributes>(true);
            var equipLookup = GetComponentLookup<PlayerEquippedStats>(true);
            var partyXPModLookup = GetComponentLookup<PartyXPModifier>(true);
            var partyKillLookup = GetComponentLookup<PartyKillTag>(true);
            var pvpKillMarkerLookup = GetComponentLookup<PvPKillMarker>(false);

            // EPIC 17.10: Get PvP kill XP multiplier if config exists
            float pvpKillXPMultiplier = 0.5f;
            bool hasPvPConfig = SystemAPI.HasSingleton<PvPConfigSingleton>();
            if (hasPvPConfig)
                pvpKillXPMultiplier = SystemAPI.GetSingleton<PvPConfigSingleton>().Config.Value.PvPKillXPMultiplier;

            var entities = _killQuery.ToEntityArray(Allocator.Temp);
            var kills = _killQuery.ToComponentDataArray<KillCredited>(Allocator.Temp);
            var progressions = _killQuery.ToComponentDataArray<PlayerProgression>(Allocator.Temp);
            var attributes = _killQuery.ToComponentDataArray<CharacterAttributes>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var kill = kills[i];
                var prog = progressions[i];
                var playerAttrs = attributes[i];

                // Get enemy level from victim entity
                int enemyLevel = 1;
                if (kill.Victim != Entity.Null && attrLookup.HasComponent(kill.Victim))
                    enemyLevel = math.max(1, attrLookup[kill.Victim].Level);

                int playerLevel = math.max(1, playerAttrs.Level);

                // Don't award XP if at max level
                if (playerLevel >= curve.MaxLevel)
                {
                    ecb.RemoveComponent<KillCredited>(entities[i]);
                    continue;
                }

                // Base kill XP formula
                float rawXP = curve.BaseKillXP * math.pow(curve.KillXPPerEnemyLevel, enemyLevel - 1);

                // Diminishing returns for outleveled enemies
                int levelDelta = playerLevel - enemyLevel;
                if (levelDelta > curve.DiminishStartDelta)
                {
                    float diminish = math.pow(curve.DiminishFactorPerLevel, levelDelta - curve.DiminishStartDelta);
                    diminish = math.max(diminish, curve.DiminishFloor);
                    rawXP *= diminish;
                }

                // Rested XP bonus
                float restedBonus = 0f;
                if (prog.RestedXP > 0f)
                {
                    restedBonus = rawXP * curve.RestedXPMultiplier;
                    float actualRested = math.min(restedBonus, prog.RestedXP);
                    prog.RestedXP -= actualRested;
                    restedBonus = actualRested;
                }

                // Equipment XP bonus
                float equipBonusPercent = 0f;
                if (equipLookup.HasComponent(entities[i]))
                    equipBonusPercent = equipLookup[entities[i]].TotalXPBonusPercent;

                float finalXP = (rawXP + restedBonus) * (1f + equipBonusPercent);

                // EPIC 17.2: Party XP sharing modifier
                if (partyXPModLookup.HasComponent(entities[i]))
                    finalXP *= partyXPModLookup[entities[i]].XPMultiplier;

                // EPIC 17.10: PvP kill XP multiplier
                if (pvpKillMarkerLookup.HasComponent(entities[i]) &&
                    pvpKillMarkerLookup.IsComponentEnabled(entities[i]))
                {
                    finalXP *= pvpKillXPMultiplier;
                    pvpKillMarkerLookup.SetComponentEnabled(entities[i], false);
                }

                int xpAwarded = (int)math.max(1, math.round(finalXP));

                // Write progression
                prog.CurrentXP += xpAwarded;
                prog.TotalXPEarned += xpAwarded;
                EntityManager.SetComponentData(entities[i], prog);

                // Enqueue visual event
                LevelUpVisualQueue.EnqueueXPGain(xpAwarded, XPSourceType.Kill);

                // Remove ephemeral KillCredited
                ecb.RemoveComponent<KillCredited>(entities[i]);

                // EPIC 17.2: Remove ephemeral party components
                if (partyXPModLookup.HasComponent(entities[i]))
                    ecb.RemoveComponent<PartyXPModifier>(entities[i]);
                if (partyKillLookup.HasComponent(entities[i]))
                    ecb.RemoveComponent<PartyKillTag>(entities[i]);
            }

            entities.Dispose();
            kills.Dispose();
            progressions.Dispose();
            attributes.Dispose();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
