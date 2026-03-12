using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Roguelite.Rewards
{
    /// <summary>
    /// EPIC 23.5: On ZoneTransition start, rolls ChoiceCount rewards from pool using reward seed.
    /// Writes PendingRewardChoice buffer. Seed-deterministic.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ChoiceGenerationSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<RunState>();
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.ManagedAPI.HasSingleton<RewardRegistryManaged>())
                return;

            var registry = SystemAPI.ManagedAPI.GetSingleton<RewardRegistryManaged>();
            var pool = registry.DefaultRewardPool;
            if (pool == null || pool.Entries == null || pool.Entries.Count == 0)
                return;

            foreach (var (runState, choices, entity) in
                     SystemAPI.Query<RefRO<RunState>, DynamicBuffer<PendingRewardChoice>>()
                     .WithEntityAccess())
            {
                var run = runState.ValueRO;
                if (run.Phase != RunPhase.ZoneTransition)
                    continue;

                // Skip if this is a shop or event zone
                bool isShopOrEvent = false;
                if (SystemAPI.HasSingleton<ZoneContextSingleton>())
                {
                    var zt = SystemAPI.GetSingleton<ZoneContextSingleton>().CurrentType;
                    isShopOrEvent = zt == ZoneTransitionType.Shop || zt == ZoneTransitionType.Event;
                }
                else
                {
                    isShopOrEvent = (run.CurrentZoneIndex % 3) != 0; // 0=combat, 1=shop, 2=event
                }
                if (isShopOrEvent)
                    continue;

                // Already have choices — don't regenerate
                if (choices.Length > 0)
                    continue;

                byte zoneIndex = run.CurrentZoneIndex;
                byte ascension = run.AscensionLevel;
                uint zoneSeed = run.ZoneSeed;

                // Build valid entries (zone/ascension constraints)
                var validEntries = new List<(RewardPoolEntry entry, float weight)>();
                foreach (var e in pool.Entries)
                {
                    if (e.Reward == null) continue;
                    if (e.Reward.MinZoneIndex > 0 && zoneIndex < e.Reward.MinZoneIndex) continue;
                    if (e.Reward.MaxZoneIndex > 0 && zoneIndex > e.Reward.MaxZoneIndex) continue;
                    if (e.Reward.RequiredAscensionLevel > 0 && ascension < e.Reward.RequiredAscensionLevel) continue;
                    if (e.Reward.Rarity < e.MinRarity || (e.MaxRarity > 0 && e.Reward.Rarity > e.MaxRarity)) continue;

                    validEntries.Add((e, e.Weight));
                }

                if (validEntries.Count == 0)
                    continue;

                int choiceCount = math.min(pool.ChoiceCount, validEntries.Count);
                var rng = new Unity.Mathematics.Random(RunSeedUtility.DeriveRewardSeed(zoneSeed, 0));

                // Compute total weight once, subtract on removal
                float totalWeight = 0f;
                foreach (var (_, w) in validEntries)
                    totalWeight += w;

                for (int slot = 0; slot < choiceCount; slot++)
                {
                    float roll = rng.NextFloat() * totalWeight;
                    int pick = -1;
                    for (int i = 0; i < validEntries.Count; i++)
                    {
                        roll -= validEntries[i].weight;
                        if (roll <= 0f) { pick = i; break; }
                    }
                    if (pick < 0) pick = 0;

                    var chosen = validEntries[pick].entry.Reward;
                    choices.Add(new PendingRewardChoice
                    {
                        RewardId = chosen.RewardId,
                        Type = chosen.Type,
                        Rarity = chosen.Rarity,
                        IntValue = chosen.IntValue,
                        FloatValue = chosen.FloatValue,
                        SlotIndex = slot,
                        IsSelected = false
                    });

                    if (!pool.AllowDuplicates)
                    {
                        totalWeight -= validEntries[pick].weight;
                        validEntries.RemoveAt(pick);
                    }
                }
            }
        }
    }
}
