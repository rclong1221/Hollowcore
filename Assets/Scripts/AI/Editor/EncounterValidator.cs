#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using DIG.AI.Authoring;
using DIG.AI.Components;

namespace DIG.AI.Editor
{
    public enum ValidationSeverity { Error, Warning, Info }

    public struct ValidationResult
    {
        public ValidationSeverity Severity;
        public string Message;
        public string Context;
    }

    /// <summary>
    /// EPIC 15.32: Automated validation checks for encounter profiles and ability configurations.
    /// Catches configuration errors before runtime: missing references, unreachable phases,
    /// cooldown gaps, trigger loops, and telegraph misconfigurations.
    /// </summary>
    public static class EncounterValidator
    {
        public static List<ValidationResult> Validate(
            EncounterProfileSO encounter,
            AbilityProfileSO abilities)
        {
            var results = new List<ValidationResult>();

            if (encounter != null)
            {
                ValidatePhases(encounter, results);
                ValidateTriggers(encounter, results);
                ValidateSpawnGroups(encounter, results);
            }

            if (abilities != null)
            {
                ValidateAbilities(abilities, results);

                if (encounter != null)
                    ValidatePhaseAbilityCoverage(encounter, abilities, results);
            }

            if (encounter == null && abilities == null)
            {
                results.Add(new ValidationResult
                {
                    Severity = ValidationSeverity.Info,
                    Message = "No profiles assigned — nothing to validate.",
                    Context = "General"
                });
            }

            results.Sort((a, b) => a.Severity.CompareTo(b.Severity));
            return results;
        }

        private static void ValidatePhases(EncounterProfileSO encounter, List<ValidationResult> results)
        {
            var phases = encounter.Phases;
            if (phases.Count == 0)
            {
                results.Add(new ValidationResult
                {
                    Severity = ValidationSeverity.Warning,
                    Message = "No phases defined — boss has no phase progression.",
                    Context = "Phases"
                });
                return;
            }

            // Check for duplicate HP thresholds
            var hpThresholds = new HashSet<float>();
            for (int i = 0; i < phases.Count; i++)
            {
                var phase = phases[i];
                if (phase.HPThresholdEntry >= 0 && !hpThresholds.Add(phase.HPThresholdEntry))
                {
                    results.Add(new ValidationResult
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = $"Duplicate HP threshold: {phase.HPThresholdEntry:P0}",
                        Context = $"Phase {i}: {phase.PhaseName}"
                    });
                }

                if (phase.SpeedMultiplier <= 0)
                {
                    results.Add(new ValidationResult
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = "Speed multiplier is 0 or negative — boss won't move.",
                        Context = $"Phase {i}: {phase.PhaseName}"
                    });
                }

                if (phase.DamageMultiplier <= 0)
                {
                    results.Add(new ValidationResult
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = "Damage multiplier is 0 or negative — boss deals no damage.",
                        Context = $"Phase {i}: {phase.PhaseName}"
                    });
                }
            }
        }

        private static void ValidateTriggers(EncounterProfileSO encounter, List<ValidationResult> results)
        {
            var triggers = encounter.Triggers;

            for (int i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];

                // Composite trigger bounds check
                if (trigger.Condition == TriggerConditionType.Composite_AND ||
                    trigger.Condition == TriggerConditionType.Composite_OR)
                {
                    ValidateSubTriggerIndex(trigger.SubTriggerIndex0, i, triggers.Count, results);
                    ValidateSubTriggerIndex(trigger.SubTriggerIndex1, i, triggers.Count, results);
                    ValidateSubTriggerIndex(trigger.SubTriggerIndex2, i, triggers.Count, results);

                    // Self-reference check
                    if (trigger.SubTriggerIndex0 == i || trigger.SubTriggerIndex1 == i || trigger.SubTriggerIndex2 == i)
                    {
                        results.Add(new ValidationResult
                        {
                            Severity = ValidationSeverity.Error,
                            Message = "Composite trigger references itself — infinite loop.",
                            Context = $"Trigger {i}: {trigger.TriggerName}"
                        });
                    }
                }

                // Enable/Disable trigger bounds
                if (trigger.Action == TriggerActionType.EnableTrigger ||
                    trigger.Action == TriggerActionType.DisableTrigger)
                {
                    if (trigger.ActionParam >= triggers.Count)
                    {
                        results.Add(new ValidationResult
                        {
                            Severity = ValidationSeverity.Error,
                            Message = $"Action references trigger index {trigger.ActionParam} but only {triggers.Count} triggers exist.",
                            Context = $"Trigger {i}: {trigger.TriggerName}"
                        });
                    }

                    if (trigger.ActionParam == i)
                    {
                        results.Add(new ValidationResult
                        {
                            Severity = ValidationSeverity.Warning,
                            Message = "Trigger enables/disables itself.",
                            Context = $"Trigger {i}: {trigger.TriggerName}"
                        });
                    }
                }

                // Orphan spawn group
                if (trigger.Action == TriggerActionType.SpawnAddGroup)
                {
                    bool found = encounter.SpawnGroups.Any(sg => sg.GroupId == trigger.ActionParam);
                    if (!found)
                    {
                        results.Add(new ValidationResult
                        {
                            Severity = ValidationSeverity.Warning,
                            Message = $"SpawnAddGroup references group {trigger.ActionParam} which doesn't exist in SpawnGroups.",
                            Context = $"Trigger {i}: {trigger.TriggerName}"
                        });
                    }
                }

                // EPIC 17.9: Validate PlayCinematic references a valid CinematicId
                if (trigger.Action == TriggerActionType.PlayCinematic)
                {
                    if (trigger.ActionParam <= 0)
                    {
                        results.Add(new ValidationResult
                        {
                            Severity = ValidationSeverity.Warning,
                            Message = "PlayCinematic trigger has CinematicId <= 0. Verify ActionParam is a valid CinematicId.",
                            Context = $"Trigger {i}: {trigger.TriggerName}"
                        });
                    }
                    if (trigger.ActionValue <= 0)
                    {
                        results.Add(new ValidationResult
                        {
                            Severity = ValidationSeverity.Info,
                            Message = "PlayCinematic trigger has Duration <= 0. Duration will be read from CinematicDefinitionSO.",
                            Context = $"Trigger {i}: {trigger.TriggerName}"
                        });
                    }
                }
            }

            // Circular dependency detection for enable/disable chains
            DetectTriggerCycles(triggers, results);
        }

        private static void ValidateSubTriggerIndex(int index, int selfIndex, int count, List<ValidationResult> results)
        {
            if (index < 0) return; // -1 = unused
            if (index >= count)
            {
                results.Add(new ValidationResult
                {
                    Severity = ValidationSeverity.Error,
                    Message = $"Sub-trigger index {index} is out of bounds (max: {count - 1}).",
                    Context = $"Trigger {selfIndex}"
                });
            }
        }

        private static void DetectTriggerCycles(List<TriggerEntry> triggers, List<ValidationResult> results)
        {
            // Build enable/disable dependency graph and check for cycles
            for (int i = 0; i < triggers.Count; i++)
            {
                if (triggers[i].Action != TriggerActionType.EnableTrigger &&
                    triggers[i].Action != TriggerActionType.DisableTrigger) continue;

                var visited = new HashSet<int>();
                int current = triggers[i].ActionParam;
                visited.Add(i);

                int depth = 0;
                while (current >= 0 && current < triggers.Count && depth < 50)
                {
                    if (visited.Contains(current))
                    {
                        results.Add(new ValidationResult
                        {
                            Severity = ValidationSeverity.Error,
                            Message = $"Trigger chain creates circular dependency (cycle at index {current}).",
                            Context = $"Trigger {i}: {triggers[i].TriggerName}"
                        });
                        break;
                    }
                    visited.Add(current);

                    if (triggers[current].Action == TriggerActionType.EnableTrigger ||
                        triggers[current].Action == TriggerActionType.DisableTrigger)
                    {
                        current = triggers[current].ActionParam;
                    }
                    else break;

                    depth++;
                }
            }
        }

        private static void ValidateSpawnGroups(EncounterProfileSO encounter, List<ValidationResult> results)
        {
            var groups = encounter.SpawnGroups;
            var usedIds = new HashSet<byte>();

            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];

                if (!usedIds.Add(group.GroupId))
                {
                    results.Add(new ValidationResult
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = $"Duplicate spawn group ID: {group.GroupId}.",
                        Context = $"SpawnGroup {i}"
                    });
                }

                if (group.AddPrefab == null)
                {
                    results.Add(new ValidationResult
                    {
                        Severity = ValidationSeverity.Error,
                        Message = "Spawn group has no prefab assigned.",
                        Context = $"SpawnGroup {i} (ID: {group.GroupId})"
                    });
                }

                if (group.Count == 0)
                {
                    results.Add(new ValidationResult
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = "Spawn group count is 0 — nothing will spawn.",
                        Context = $"SpawnGroup {i} (ID: {group.GroupId})"
                    });
                }
            }
        }

        private static void ValidateAbilities(AbilityProfileSO profile, List<ValidationResult> results)
        {
            if (profile.Abilities == null || profile.Abilities.Count == 0)
            {
                results.Add(new ValidationResult
                {
                    Severity = ValidationSeverity.Warning,
                    Message = "Ability profile has no abilities — AI won't attack.",
                    Context = "AbilityProfile"
                });
                return;
            }

            for (int i = 0; i < profile.Abilities.Count; i++)
            {
                var ab = profile.Abilities[i];
                if (ab == null)
                {
                    results.Add(new ValidationResult
                    {
                        Severity = ValidationSeverity.Error,
                        Message = $"Ability slot [{i}] is null.",
                        Context = "AbilityProfile"
                    });
                    continue;
                }

                // Telegraph without shape
                if (ab.TelegraphDuration > 0 && ab.TelegraphShape == TelegraphShape.None)
                {
                    results.Add(new ValidationResult
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = "Telegraph duration set but TelegraphShape is None — no visual warning.",
                        Context = $"Ability: {ab.AbilityName}"
                    });
                }

                // Cooldown group without duration
                if (ab.CooldownGroupId > 0 && ab.CooldownGroupDuration <= 0)
                {
                    results.Add(new ValidationResult
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = "Cooldown group assigned but group duration is 0.",
                        Context = $"Ability: {ab.AbilityName}"
                    });
                }

                // Charges without regen
                if (ab.MaxCharges > 0 && ab.ChargeRegenTime <= 0)
                {
                    results.Add(new ValidationResult
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = "MaxCharges > 0 but no ChargeRegenTime — charges won't regenerate.",
                        Context = $"Ability: {ab.AbilityName}"
                    });
                }

                // Zero damage
                if (ab.DamageBase <= 0 && ab.TargetingMode != AbilityTargetingMode.Self)
                {
                    results.Add(new ValidationResult
                    {
                        Severity = ValidationSeverity.Info,
                        Message = "Ability has 0 base damage — intended for utility/movement only?",
                        Context = $"Ability: {ab.AbilityName}"
                    });
                }
            }

            // Check for worst-case cooldown gap
            ValidateCooldownRotation(profile, results);
        }

        private static void ValidateCooldownRotation(AbilityProfileSO profile, List<ValidationResult> results)
        {
            // Estimate worst-case: all abilities go on cooldown simultaneously
            // Find the minimum "next available time" across all abilities
            var abilities = profile.Abilities.Where(a => a != null).ToList();
            if (abilities.Count <= 1) return;

            float minCooldown = abilities.Min(a => a.Cooldown);
            float maxTotalCast = abilities.Max(a => a.CastTime + a.ActiveDuration + a.RecoveryTime);

            // If the fastest cooldown is longer than the slowest cast, there's a potential gap
            if (minCooldown > maxTotalCast * 2f)
            {
                results.Add(new ValidationResult
                {
                    Severity = ValidationSeverity.Warning,
                    Message = $"Potential cooldown gap: minimum cooldown ({minCooldown:F1}s) is much longer than cast times. Boss may idle.",
                    Context = "AbilityProfile"
                });
            }
        }

        private static void ValidatePhaseAbilityCoverage(
            EncounterProfileSO encounter, AbilityProfileSO abilities, List<ValidationResult> results)
        {
            for (int p = 0; p < encounter.Phases.Count; p++)
            {
                bool hasAbility = false;
                foreach (var ab in abilities.Abilities)
                {
                    if (ab == null) continue;
                    if (p >= ab.PhaseMin && p <= ab.PhaseMax)
                    {
                        hasAbility = true;
                        break;
                    }
                }

                if (!hasAbility)
                {
                    results.Add(new ValidationResult
                    {
                        Severity = ValidationSeverity.Error,
                        Message = $"Phase {p} has no abilities available — boss will stand idle.",
                        Context = $"Phase {p}: {encounter.Phases[p].PhaseName}"
                    });
                }
            }
        }
    }
}
#endif
