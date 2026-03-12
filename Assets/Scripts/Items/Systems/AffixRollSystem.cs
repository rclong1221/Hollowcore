using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Items.Definitions;

namespace DIG.Items.Systems
{
    /// <summary>
    /// EPIC 16.6: Static utility for rolling affixes on newly spawned equipment items.
    /// Called by LootSpawnSystem when spawning equipment-type loot.
    /// </summary>
    public static class AffixRollSystem
    {
        /// <summary>
        /// Roll affixes for an item and write the results to the entity's ItemAffix buffer and ItemStatBlock.
        /// </summary>
        public static void RollAffixes(
            AffixPoolSO pool,
            ItemRarity rarity,
            uint randomSeed,
            ref DynamicBuffer<ItemAffix> affixBuffer,
            ref ItemStatBlock statBlock)
        {
            if (pool == null) return;

            var rng = new Random(randomSeed);

            // Roll implicits (always present)
            foreach (var implicitDef in pool.Implicits)
            {
                if (implicitDef == null) continue;
                var affix = RollSingleAffix(implicitDef, ref rng);
                affixBuffer.Add(affix);
                ApplyAffixToStats(implicitDef, affix.Value, ref statBlock);
            }

            // Roll prefixes
            int maxPrefixes = pool.GetMaxPrefixes(rarity);
            int prefixCount = rng.NextInt(0, maxPrefixes + 1);
            RollFromList(pool.Prefixes, prefixCount, rarity, ref rng, ref affixBuffer, ref statBlock);

            // Roll suffixes
            int maxSuffixes = pool.GetMaxSuffixes(rarity);
            int suffixCount = rng.NextInt(0, maxSuffixes + 1);
            RollFromList(pool.Suffixes, suffixCount, rarity, ref rng, ref affixBuffer, ref statBlock);
        }

        private static void RollFromList(
            System.Collections.Generic.List<AffixDefinitionSO> candidates,
            int count,
            ItemRarity rarity,
            ref Random rng,
            ref DynamicBuffer<ItemAffix> affixBuffer,
            ref ItemStatBlock statBlock)
        {
            if (candidates == null || candidates.Count == 0 || count <= 0)
                return;

            // Build weight table for eligible affixes
            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                var def = candidates[i];
                if (def == null) continue;
                if (rarity < def.MinRarity) continue;
                if (!IsCategoryValid(def)) continue;
                totalWeight += def.Weight;
            }

            if (totalWeight <= 0f) return;

            var usedIds = new NativeHashSet<int>(count, Allocator.Temp);

            for (int r = 0; r < count; r++)
            {
                float roll = rng.NextFloat() * totalWeight;
                float cumulative = 0f;
                AffixDefinitionSO selected = null;

                for (int i = 0; i < candidates.Count; i++)
                {
                    var def = candidates[i];
                    if (def == null || rarity < def.MinRarity || !IsCategoryValid(def))
                        continue;

                    cumulative += def.Weight;
                    if (roll <= cumulative)
                    {
                        if (!usedIds.Contains(def.AffixId))
                        {
                            selected = def;
                        }
                        break;
                    }
                }

                if (selected == null) continue;

                usedIds.Add(selected.AffixId);
                var affix = RollSingleAffix(selected, ref rng);
                affixBuffer.Add(affix);
                ApplyAffixToStats(selected, affix.Value, ref statBlock);
            }

            usedIds.Dispose();
        }

        private static ItemAffix RollSingleAffix(AffixDefinitionSO def, ref Random rng)
        {
            float value = rng.NextFloat(def.MinValue, def.MaxValue);
            int tier = rng.NextInt(def.MinTier, def.MaxTier + 1);

            return new ItemAffix
            {
                AffixId = def.AffixId,
                Slot = def.Slot,
                Value = value,
                Tier = tier
            };
        }

        private static void ApplyAffixToStats(AffixDefinitionSO def, float rolledValue, ref ItemStatBlock statBlock)
        {
            if (def.Modifiers == null) return;

            foreach (var mod in def.Modifiers)
            {
                float delta = rolledValue * mod.Multiplier;

                switch (mod.Stat)
                {
                    case StatType.BaseDamage:         statBlock.BaseDamage += delta; break;
                    case StatType.AttackSpeed:        statBlock.AttackSpeed += delta; break;
                    case StatType.CritChance:         statBlock.CritChance += delta; break;
                    case StatType.CritMultiplier:     statBlock.CritMultiplier += delta; break;
                    case StatType.Armor:              statBlock.Armor += delta; break;
                    case StatType.MaxHealthBonus:      statBlock.MaxHealthBonus += delta; break;
                    case StatType.MovementSpeedBonus:  statBlock.MovementSpeedBonus += delta; break;
                    case StatType.DamageResistance:    statBlock.DamageResistance += delta; break;
                }
            }
        }

        private static bool IsCategoryValid(AffixDefinitionSO def)
        {
            // Empty ValidCategories = all categories valid
            return def.ValidCategories == null || def.ValidCategories.Length == 0;
        }
    }
}
