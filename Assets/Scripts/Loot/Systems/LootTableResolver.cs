using Unity.Collections;
using Unity.Mathematics;
using DIG.Items;
using DIG.Loot.Components;
using DIG.Loot.Definitions;

namespace DIG.Loot.Systems
{
    /// <summary>
    /// EPIC 16.6: Context for loot resolution (level, difficulty, luck, seed).
    /// </summary>
    public struct LootContext
    {
        public int Level;
        public float DifficultyMultiplier;
        public float LuckModifier;
        public uint RandomSeed;

        public static LootContext Default => new LootContext
        {
            Level = 1,
            DifficultyMultiplier = 1f,
            LuckModifier = 0f,
            RandomSeed = 1
        };
    }

    /// <summary>
    /// EPIC 16.6: Static utility that resolves a LootTableSO into concrete LootDrop results.
    /// Uses deterministic Unity.Mathematics.Random for server-authoritative rolls.
    /// </summary>
    public static class LootTableResolver
    {
        private const int MaxNestedDepth = 4;

        /// <summary>
        /// Resolve a loot table into a list of drops.
        /// </summary>
        public static void Resolve(LootTableSO table, LootContext context, ref NativeList<LootDrop> results)
        {
            if (table == null || table.Pools == null || table.Pools.Count == 0)
                return;

            // Check table-level conditions
            if (table.Conditions != null)
            {
                foreach (var condition in table.Conditions)
                {
                    if (!EvaluateCondition(condition, context))
                        return;
                }
            }

            var rng = new Random(context.RandomSeed);

            // Guaranteed rolls
            for (int r = 0; r < table.GuaranteedRolls; r++)
            {
                RollAllPools(table, context, ref rng, ref results, 0);
            }

            // Bonus rolls
            for (int r = 0; r < table.BonusRolls; r++)
            {
                if (rng.NextFloat() < table.BonusRollChance)
                {
                    RollAllPools(table, context, ref rng, ref results, 0);
                }
            }
        }

        private static void RollAllPools(LootTableSO table, LootContext context, ref Random rng,
            ref NativeList<LootDrop> results, int depth)
        {
            if (depth >= MaxNestedDepth) return;

            foreach (var pool in table.Pools)
            {
                if (pool == null || pool.Entries == null || pool.Entries.Count == 0)
                    continue;

                int dropCount = rng.NextInt(pool.MinDrops, pool.MaxDrops + 1);

                for (int d = 0; d < dropCount; d++)
                {
                    RollPool(pool, context, ref rng, ref results, depth);
                }
            }
        }

        private static void RollPool(LootPoolSO pool, LootContext context, ref Random rng,
            ref NativeList<LootDrop> results, int depth)
        {
            // Calculate total weight
            float totalWeight = 0f;
            foreach (var entry in pool.Entries)
            {
                totalWeight += entry.Weight;
            }

            if (totalWeight <= 0f) return;

            // Weighted random selection
            float roll = rng.NextFloat() * totalWeight;
            float cumulative = 0f;
            LootPoolEntry? selected = null;

            foreach (var entry in pool.Entries)
            {
                cumulative += entry.Weight;
                if (roll <= cumulative)
                {
                    selected = entry;
                    break;
                }
            }

            if (!selected.HasValue) return;

            var sel = selected.Value;

            // Apply drop chance (modified by luck)
            float adjustedChance = math.saturate(sel.DropChance + context.LuckModifier * 0.1f);
            if (rng.NextFloat() > adjustedChance)
                return;

            // Resolve the entry
            switch (sel.Type)
            {
                case LootEntryType.Item:
                    if (sel.Item != null)
                    {
                        int quantity = rng.NextInt(sel.MinQuantity, sel.MaxQuantity + 1);
                        results.Add(new LootDrop
                        {
                            ItemTypeId = sel.Item.ItemTypeId,
                            Quantity = quantity,
                            Rarity = sel.Item.Rarity,
                            Type = LootEntryType.Item
                        });
                    }
                    break;

                case LootEntryType.Resource:
                    {
                        int quantity = rng.NextInt(sel.MinQuantity, sel.MaxQuantity + 1);
                        results.Add(new LootDrop
                        {
                            Quantity = quantity,
                            Resource = sel.Resource,
                            Type = LootEntryType.Resource
                        });
                    }
                    break;

                case LootEntryType.Currency:
                    {
                        int quantity = rng.NextInt(sel.MinQuantity, sel.MaxQuantity + 1);
                        results.Add(new LootDrop
                        {
                            Quantity = quantity,
                            Currency = sel.Currency,
                            Type = LootEntryType.Currency
                        });
                    }
                    break;

                case LootEntryType.NestedTable:
                    if (sel.NestedTable != null && depth < MaxNestedDepth)
                    {
                        // Recursive resolve with incremented depth
                        var nestedContext = context;
                        nestedContext.RandomSeed = rng.NextUInt();
                        Resolve(sel.NestedTable, nestedContext, ref results);
                    }
                    break;
            }
        }

        private static bool EvaluateCondition(LootTableCondition condition, LootContext context)
        {
            return condition.Type switch
            {
                ConditionType.MinLevel => context.Level >= condition.Value,
                ConditionType.MaxLevel => context.Level <= condition.Value,
                ConditionType.MinDifficulty => context.DifficultyMultiplier >= condition.Value,
                ConditionType.MaxDifficulty => context.DifficultyMultiplier <= condition.Value,
                _ => true
            };
        }
    }
}
