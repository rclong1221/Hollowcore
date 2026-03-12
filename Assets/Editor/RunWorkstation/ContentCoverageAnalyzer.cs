#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using DIG.Roguelite.Zones;
using DIG.Roguelite.Rewards;

namespace DIG.Roguelite.Editor
{
    /// <summary>
    /// EPIC 23.7: Stateless analyzer. Takes RogueliteDataContext, produces ContentCoverageReport.
    /// Runs all 17 coverage checks defined in the spec.
    /// </summary>
    public static class ContentCoverageAnalyzer
    {
        public static ContentCoverageReport Analyze(RogueliteDataContext context)
        {
            context.EnsureBuilt();
            var graph = context.GetDependencyGraph();
            var report = new ContentCoverageReport();

            CheckOrphanedZoneDefinitions(context, graph, report);
            CheckOrphanedEncounterPools(context, graph, report);
            CheckOrphanedRewardDefinitions(context, graph, report);
            CheckMissingEncounterPools(context, report);
            CheckMissingSpawnDirectors(context, report);
            CheckEmptyEncounterPools(context, report);
            CheckZeroWeightEntries(context, report);
            CheckUnreachableRewards(context, report);
            CheckUnreachableModifiers(context, report);
            CheckOrphanedMetaUnlocks(context, report);
            CheckDuplicateIds(context, report);
            CheckNoBossZone(context, report);
            CheckNoShopZone(context, report);
            CheckDifficultyGaps(context, report);
            CheckEliteImpossible(context, report);
            CheckBudgetUnderflow(context, report);
            CheckEventProbabilitySum(context, report);

            // Compute score
            report.CompletenessScore = Mathf.Clamp(
                100f - report.ErrorCount * 20f - report.WarningCount * 5f, 0f, 100f);
            report.GeneratedTimestamp = EditorApplication.timeSinceStartup;

            return report;
        }

        private static void Add(ContentCoverageReport r, CoverageSeverity sev, string cat, string msg, ScriptableObject asset = null)
        {
            r.Issues.Add(new CoverageIssue { Severity = sev, Category = cat, Message = msg, Asset = asset });
            switch (sev)
            {
                case CoverageSeverity.Error: r.ErrorCount++; break;
                case CoverageSeverity.Warning: r.WarningCount++; break;
                case CoverageSeverity.Info: r.InfoCount++; break;
            }
        }

        // 1. Orphaned ZoneDefinitions — not referenced by any ZoneSequenceSO
        private static void CheckOrphanedZoneDefinitions(RogueliteDataContext ctx, SODependencyGraph graph, ContentCoverageReport r)
        {
            foreach (var z in ctx.ZoneDefinitions)
            {
                if (z == null) continue;
                if (!graph.ReferencedBy.TryGetValue(z, out var refs) || refs.Count == 0)
                    Add(r, CoverageSeverity.Warning, "Zones", $"ZoneDefinition '{z.DisplayName}' ({z.name}) is not referenced by any ZoneSequence.", z);
            }
        }

        // 2. Orphaned EncounterPools — not referenced by any ZoneDefinitionSO
        private static void CheckOrphanedEncounterPools(RogueliteDataContext ctx, SODependencyGraph graph, ContentCoverageReport r)
        {
            foreach (var p in ctx.EncounterPools)
            {
                if (p == null) continue;
                if (!graph.ReferencedBy.TryGetValue(p, out var refs) || refs.Count == 0)
                    Add(r, CoverageSeverity.Warning, "Encounters", $"EncounterPool '{p.PoolName}' ({p.name}) is not referenced by any ZoneDefinition.", p);
            }
        }

        // 3. Orphaned RewardDefinitions — not in any RewardPoolSO
        private static void CheckOrphanedRewardDefinitions(RogueliteDataContext ctx, SODependencyGraph graph, ContentCoverageReport r)
        {
            foreach (var rd in ctx.RewardDefinitions)
            {
                if (rd == null) continue;
                if (!graph.ReferencedBy.TryGetValue(rd, out var refs) || refs.Count == 0)
                    Add(r, CoverageSeverity.Warning, "Rewards", $"RewardDefinition '{rd.DisplayName}' ({rd.name}) is not in any RewardPool.", rd);
            }
        }

        // 4. Missing EncounterPool on combat zones
        private static void CheckMissingEncounterPools(RogueliteDataContext ctx, ContentCoverageReport r)
        {
            foreach (var z in ctx.ZoneDefinitions)
            {
                if (z == null) continue;
                if (z.Type == ZoneType.Combat || z.Type == ZoneType.Elite || z.Type == ZoneType.Arena || z.Type == ZoneType.Boss)
                {
                    if (z.EncounterPool == null)
                        Add(r, CoverageSeverity.Error, "Zones", $"Combat zone '{z.DisplayName}' has no EncounterPool assigned.", z);
                }
            }
        }

        // 5. Missing SpawnDirector
        private static void CheckMissingSpawnDirectors(RogueliteDataContext ctx, ContentCoverageReport r)
        {
            foreach (var z in ctx.ZoneDefinitions)
            {
                if (z == null) continue;
                if (z.EncounterPool != null && z.SpawnDirectorConfig == null)
                    Add(r, CoverageSeverity.Error, "Encounters", $"Zone '{z.DisplayName}' has EncounterPool but no SpawnDirectorConfig.", z);
            }
        }

        // 6. Empty EncounterPool
        private static void CheckEmptyEncounterPools(RogueliteDataContext ctx, ContentCoverageReport r)
        {
            foreach (var p in ctx.EncounterPools)
            {
                if (p == null) continue;
                if (p.Entries == null || p.Entries.Count == 0)
                    Add(r, CoverageSeverity.Error, "Encounters", $"EncounterPool '{p.PoolName}' has zero entries.", p);
            }
        }

        // 7. Zero-weight entries
        private static void CheckZeroWeightEntries(RogueliteDataContext ctx, ContentCoverageReport r)
        {
            foreach (var p in ctx.EncounterPools)
            {
                if (p?.Entries == null) continue;
                for (int i = 0; i < p.Entries.Count; i++)
                {
                    if (p.Entries[i].Weight <= 0f)
                    {
                        string name = !string.IsNullOrEmpty(p.Entries[i].DisplayName)
                            ? p.Entries[i].DisplayName : $"Entry {i}";
                        Add(r, CoverageSeverity.Warning, "Encounters", $"Pool '{p.PoolName}' entry '{name}' has zero weight (dead entry).", p);
                    }
                }
            }
        }

        // 8. Unreachable Rewards (MinZoneIndex > any RunConfig.ZoneCount)
        private static void CheckUnreachableRewards(RogueliteDataContext ctx, ContentCoverageReport r)
        {
            int maxZones = 0;
            foreach (var c in ctx.RunConfigs)
                if (c != null && c.ZoneCount > maxZones) maxZones = c.ZoneCount;

            if (maxZones <= 0) return;

            foreach (var rd in ctx.RewardDefinitions)
            {
                if (rd == null || rd.MinZoneIndex <= 0) continue;
                if (rd.MinZoneIndex >= maxZones)
                    Add(r, CoverageSeverity.Warning, "Rewards", $"Reward '{rd.DisplayName}' requires zone {rd.MinZoneIndex}+ but max run has {maxZones} zones.", rd);
            }
        }

        // 9. Unreachable Modifiers (RequiredAscensionLevel > max tier)
        private static void CheckUnreachableModifiers(RogueliteDataContext ctx, ContentCoverageReport r)
        {
            if (ctx.AscensionDefinition == null || ctx.ModifierPool == null) return;

            byte maxTier = 0;
            foreach (var tier in ctx.AscensionDefinition.Tiers)
                if (tier.Level > maxTier) maxTier = tier.Level;

            foreach (var mod in ctx.ModifierPool.Modifiers)
            {
                if (mod.RequiredAscensionLevel > maxTier)
                    Add(r, CoverageSeverity.Warning, "Meta", $"Modifier '{mod.DisplayName}' requires ascension {mod.RequiredAscensionLevel} but max tier is {maxTier}.", ctx.ModifierPool);
            }
        }

        // 10. Orphaned MetaUnlocks (prerequisite points to nonexistent UnlockId)
        private static void CheckOrphanedMetaUnlocks(RogueliteDataContext ctx, ContentCoverageReport r)
        {
            if (ctx.MetaUnlockTree == null) return;
            var ids = new HashSet<int>();
            foreach (var u in ctx.MetaUnlockTree.Unlocks)
                ids.Add(u.UnlockId);

            foreach (var u in ctx.MetaUnlockTree.Unlocks)
            {
                if (u.PrerequisiteId >= 0 && !ids.Contains(u.PrerequisiteId))
                    Add(r, CoverageSeverity.Error, "Meta", $"MetaUnlock '{u.DisplayName}' prerequisite {u.PrerequisiteId} doesn't exist.", ctx.MetaUnlockTree);
            }
        }

        // 11. Duplicate IDs
        private static void CheckDuplicateIds(RogueliteDataContext ctx, ContentCoverageReport r)
        {
            // Zone IDs
            var seen = new HashSet<int>();
            foreach (var z in ctx.ZoneDefinitions)
            {
                if (z == null) continue;
                if (!seen.Add(z.ZoneId))
                    Add(r, CoverageSeverity.Error, "Zones", $"Duplicate ZoneId {z.ZoneId} on '{z.DisplayName}'.", z);
            }

            // Reward IDs
            seen.Clear();
            foreach (var rd in ctx.RewardDefinitions)
            {
                if (rd == null) continue;
                if (!seen.Add(rd.RewardId))
                    Add(r, CoverageSeverity.Error, "Rewards", $"Duplicate RewardId {rd.RewardId} on '{rd.DisplayName}'.", rd);
            }
        }

        // 12. No Boss Zone
        private static void CheckNoBossZone(RogueliteDataContext ctx, ContentCoverageReport r)
        {
            foreach (var seq in ctx.ZoneSequences)
            {
                if (seq?.Layers == null) continue;
                bool hasBoss = false;
                foreach (var layer in seq.Layers)
                {
                    if (layer.Entries == null) continue;
                    foreach (var entry in layer.Entries)
                    {
                        if (entry.Zone != null && entry.Zone.Type == ZoneType.Boss)
                        { hasBoss = true; break; }
                    }
                    if (hasBoss) break;
                }
                if (!hasBoss)
                    Add(r, CoverageSeverity.Warning, "Zones", $"ZoneSequence '{seq.SequenceName}' has no Boss zone.", seq);
            }
        }

        // 13. No Shop Zone
        private static void CheckNoShopZone(RogueliteDataContext ctx, ContentCoverageReport r)
        {
            foreach (var seq in ctx.ZoneSequences)
            {
                if (seq?.Layers == null) continue;
                bool hasShop = false;
                foreach (var layer in seq.Layers)
                {
                    if (layer.Entries == null) continue;
                    foreach (var entry in layer.Entries)
                    {
                        if (entry.Zone != null && entry.Zone.Type == ZoneType.Shop)
                        { hasShop = true; break; }
                    }
                    if (hasShop) break;
                }
                if (!hasShop)
                    Add(r, CoverageSeverity.Info, "Economy", $"ZoneSequence '{seq.SequenceName}' has no Shop zone (economy has no spend opportunity).", seq);
            }
        }

        // 14. Difficulty Gap (non-monotonic curve)
        private static void CheckDifficultyGaps(RogueliteDataContext ctx, ContentCoverageReport r)
        {
            foreach (var cfg in ctx.RunConfigs)
            {
                if (cfg == null || cfg.ZoneCount < 2) continue;
                for (int z = 1; z < cfg.ZoneCount; z++)
                {
                    float prev = cfg.GetDifficultyAtZone(z - 1);
                    float curr = cfg.GetDifficultyAtZone(z);
                    if (curr < prev - 0.01f)
                        Add(r, CoverageSeverity.Warning, "Zones", $"RunConfig '{cfg.ConfigName}': difficulty drops from zone {z - 1} ({prev:F2}) to zone {z} ({curr:F2}).", cfg);
                }
            }
        }

        // 15. Elite Impossible — precompute max difficulty per config to avoid O(pools×zones×configs×zoneCount)
        private static void CheckEliteImpossible(RogueliteDataContext ctx, ContentCoverageReport r)
        {
            // Precompute: max difficulty value across all zones for each RunConfig
            float globalMaxDiff = 0f;
            foreach (var cfg in ctx.RunConfigs)
            {
                if (cfg == null) continue;
                for (int i = 0; i < cfg.ZoneCount; i++)
                {
                    float d = cfg.GetDifficultyAtZone(i);
                    if (d > globalMaxDiff) globalMaxDiff = d;
                }
            }

            foreach (var p in ctx.EncounterPools)
            {
                if (p?.Entries == null) continue;
                bool hasEliteCapable = false;
                foreach (var e in p.Entries)
                    if (e.CanBeElite) { hasEliteCapable = true; break; }

                if (!hasEliteCapable) continue;

                // Find the first zone using this pool and check
                foreach (var z in ctx.ZoneDefinitions)
                {
                    if (z == null || z.EncounterPool != p || z.SpawnDirectorConfig == null) continue;
                    float maxReachable = globalMaxDiff * z.DifficultyMultiplier;
                    if (maxReachable < z.SpawnDirectorConfig.EliteMinDifficulty)
                        Add(r, CoverageSeverity.Info, "Encounters", $"Pool '{p.PoolName}' has elite entries but EliteMinDifficulty ({z.SpawnDirectorConfig.EliteMinDifficulty:F1}) is unreachable (max difficulty {maxReachable:F1}).", p);
                    break;
                }
            }
        }

        // 16. Budget Underflow
        private static void CheckBudgetUnderflow(RogueliteDataContext ctx, ContentCoverageReport r)
        {
            foreach (var z in ctx.ZoneDefinitions)
            {
                if (z == null || z.EncounterPool == null || z.SpawnDirectorConfig == null) continue;
                if (z.EncounterPool.Entries == null || z.EncounterPool.Entries.Count == 0) continue;

                int cheapest = int.MaxValue;
                foreach (var e in z.EncounterPool.Entries)
                    if (e.SpawnCost < cheapest) cheapest = e.SpawnCost;

                if (z.SpawnDirectorConfig.InitialBudget < cheapest && z.SpawnDirectorConfig.CreditsPerSecond <= 0f)
                    Add(r, CoverageSeverity.Warning, "Encounters", $"Zone '{z.DisplayName}': InitialBudget ({z.SpawnDirectorConfig.InitialBudget}) < cheapest entry ({cheapest}). Nothing can spawn.", z);
            }
        }

        // 17. Event Probability Sum
        private static void CheckEventProbabilitySum(RogueliteDataContext ctx, ContentCoverageReport r)
        {
            foreach (var evt in ctx.RunEvents)
            {
                if (evt?.Choices == null || evt.Choices.Count == 0) continue;
                bool hasGuaranteed = false;
                foreach (var choice in evt.Choices)
                {
                    if (choice.SuccessProbability >= 1f)
                    { hasGuaranteed = true; break; }
                }
                if (!hasGuaranteed)
                    Add(r, CoverageSeverity.Warning, "Economy", $"RunEvent '{evt.DisplayName}' has no guaranteed-success choice (all < 100%).", evt);
            }
        }
    }
}
#endif
