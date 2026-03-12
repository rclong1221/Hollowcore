#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using DIG.AI.Authoring;
using DIG.AI.Components;

namespace DIG.AI.Editor
{
    /// <summary>
    /// EPIC 15.32: Editor-only dry-run simulation of an encounter.
    /// Runs through phase transitions, trigger evaluations, and ability rotations
    /// using configurable DPS/HP estimates. Outputs a timeline log with warnings
    /// for idle periods, unreachable triggers, and pacing issues.
    /// </summary>
    public class EncounterSimulator
    {
        public float SimulatedHP = 1.0f;
        public float SimulatedTime = 0f;
        public float DPSEstimate = 50f;
        public int SimulatedPlayerCount = 4;
        public float EstimatedBossHP = 10000f;
        public List<string> Warnings = new();

        // Internal state
        private int _currentPhase;
        private float _phaseTimer;
        private float _encounterTimer;
        private bool _isEnraged;
        private float[] _abilityCooldowns;
        private float _globalCooldown;
        private bool[] _triggersFired;
        private float _lastAbilityEndTime;
        private float _totalIdleTime;

        public List<SimulationEvent> Simulate(
            EncounterProfileSO encounter,
            AbilityProfileSO abilities,
            float duration = 600f)
        {
            var events = new List<SimulationEvent>();

            // Init
            SimulatedHP = 1.0f;
            SimulatedTime = 0f;
            _currentPhase = 0;
            _phaseTimer = 0f;
            _encounterTimer = 0f;
            _isEnraged = false;
            _lastAbilityEndTime = 0f;
            _totalIdleTime = 0f;
            _globalCooldown = 0f;
            Warnings.Clear();

            int abilityCount = abilities?.Abilities?.Count ?? 0;
            _abilityCooldowns = new float[abilityCount];
            _triggersFired = encounter?.Triggers != null
                ? new bool[encounter.Triggers.Count] : new bool[0];

            float dt = 0.1f; // 100ms ticks

            events.Add(new SimulationEvent
            {
                Time = 0f,
                HP = 1.0f,
                Phase = 0,
                Description = "Encounter started"
            });

            while (SimulatedTime < duration && SimulatedHP > 0)
            {
                SimulatedTime += dt;
                _encounterTimer += dt;
                _phaseTimer += dt;

                // Simulate DPS intake
                float hpLoss = (DPSEstimate * dt) / EstimatedBossHP;
                SimulatedHP -= hpLoss;
                if (SimulatedHP < 0f) SimulatedHP = 0f;

                // Tick cooldowns
                _globalCooldown = System.Math.Max(0f, _globalCooldown - dt);
                for (int i = 0; i < _abilityCooldowns.Length; i++)
                    _abilityCooldowns[i] = System.Math.Max(0f, _abilityCooldowns[i] - dt);

                // Check triggers
                if (encounter?.Triggers != null)
                    EvaluateTriggers(encounter, events);

                // Check phase transitions
                if (encounter?.Phases != null)
                    CheckPhaseTransitions(encounter, events);

                // Simulate ability selection
                if (abilities?.Abilities != null && _globalCooldown <= 0)
                    SimulateAbilitySelection(abilities, events);
            }

            if (SimulatedHP <= 0)
            {
                events.Add(new SimulationEvent
                {
                    Time = SimulatedTime,
                    HP = 0f,
                    Phase = _currentPhase,
                    Description = "Boss defeated"
                });
            }

            // Compute idle analysis
            if (_totalIdleTime > 5f)
            {
                Warnings.Add($"Boss was idle for {_totalIdleTime:F1}s total (all abilities on cooldown).");
            }

            // Report unfired triggers
            if (encounter?.Triggers != null)
            {
                for (int i = 0; i < _triggersFired.Length; i++)
                {
                    if (!_triggersFired[i] && !encounter.Triggers[i].FireOnce)
                        continue; // Repeating triggers may not have fired yet
                    if (!_triggersFired[i])
                    {
                        Warnings.Add($"Trigger [{i}] '{encounter.Triggers[i].TriggerName}' never fired.");
                    }
                }
            }

            return events;
        }

        private void EvaluateTriggers(EncounterProfileSO encounter, List<SimulationEvent> events)
        {
            for (int i = 0; i < encounter.Triggers.Count; i++)
            {
                var trigger = encounter.Triggers[i];
                if (trigger.FireOnce && _triggersFired[i]) continue;

                bool conditionMet = false;
                switch (trigger.Condition)
                {
                    case TriggerConditionType.HPBelow:
                        conditionMet = SimulatedHP <= trigger.ConditionValue;
                        break;
                    case TriggerConditionType.HPAbove:
                        conditionMet = SimulatedHP >= trigger.ConditionValue;
                        break;
                    case TriggerConditionType.TimerElapsed:
                        float timer = trigger.ConditionParam == 1 ? _phaseTimer : _encounterTimer;
                        conditionMet = timer >= trigger.ConditionValue;
                        break;
                    case TriggerConditionType.PhaseIs:
                        conditionMet = _currentPhase == (int)trigger.ConditionValue;
                        break;
                    case TriggerConditionType.Composite_AND:
                        conditionMet = true;
                        if (trigger.SubTriggerIndex0 >= 0 && trigger.SubTriggerIndex0 < _triggersFired.Length)
                            conditionMet &= _triggersFired[trigger.SubTriggerIndex0];
                        if (trigger.SubTriggerIndex1 >= 0 && trigger.SubTriggerIndex1 < _triggersFired.Length)
                            conditionMet &= _triggersFired[trigger.SubTriggerIndex1];
                        if (trigger.SubTriggerIndex2 >= 0 && trigger.SubTriggerIndex2 < _triggersFired.Length)
                            conditionMet &= _triggersFired[trigger.SubTriggerIndex2];
                        break;
                    case TriggerConditionType.Composite_OR:
                        conditionMet = false;
                        if (trigger.SubTriggerIndex0 >= 0 && trigger.SubTriggerIndex0 < _triggersFired.Length)
                            conditionMet |= _triggersFired[trigger.SubTriggerIndex0];
                        if (trigger.SubTriggerIndex1 >= 0 && trigger.SubTriggerIndex1 < _triggersFired.Length)
                            conditionMet |= _triggersFired[trigger.SubTriggerIndex1];
                        if (trigger.SubTriggerIndex2 >= 0 && trigger.SubTriggerIndex2 < _triggersFired.Length)
                            conditionMet |= _triggersFired[trigger.SubTriggerIndex2];
                        break;
                    // AddsDead, AddsAlive, AbilityCastCount, BossAtPosition, PlayerCountInRange
                    // cannot be accurately simulated — skip
                }

                if (conditionMet && !_triggersFired[i])
                {
                    _triggersFired[i] = true;
                    events.Add(new SimulationEvent
                    {
                        Time = SimulatedTime,
                        HP = SimulatedHP,
                        Phase = _currentPhase,
                        Description = $"Trigger [{i}] '{trigger.TriggerName}' fired → {trigger.Action}"
                    });

                    // Execute simulated action
                    switch (trigger.Action)
                    {
                        case TriggerActionType.SetEnrage:
                            _isEnraged = true;
                            events.Add(new SimulationEvent
                            {
                                Time = SimulatedTime,
                                HP = SimulatedHP,
                                Phase = _currentPhase,
                                Description = "ENRAGE activated"
                            });
                            break;
                        case TriggerActionType.TransitionPhase:
                            int targetPhase = (int)trigger.ActionValue;
                            if (targetPhase > _currentPhase)
                            {
                                _currentPhase = targetPhase;
                                _phaseTimer = 0f;
                                events.Add(new SimulationEvent
                                {
                                    Time = SimulatedTime,
                                    HP = SimulatedHP,
                                    Phase = _currentPhase,
                                    Description = $"Forced transition to Phase {_currentPhase}"
                                });
                            }
                            break;
                        case TriggerActionType.PlayCinematic:
                            events.Add(new SimulationEvent
                            {
                                Time = SimulatedTime,
                                HP = SimulatedHP,
                                Phase = _currentPhase,
                                Description = $"Cinematic triggered (Id={trigger.ActionParam}, Duration={trigger.ActionValue:F1}s)"
                            });
                            break;
                    }
                }
            }
        }

        private void CheckPhaseTransitions(EncounterProfileSO encounter, List<SimulationEvent> events)
        {
            int targetPhase = _currentPhase;

            for (int i = 0; i < encounter.Phases.Count; i++)
            {
                var phase = encounter.Phases[i];
                if (phase.HPThresholdEntry < 0) continue;
                if (SimulatedHP <= phase.HPThresholdEntry && i > targetPhase)
                    targetPhase = i;
            }

            if (targetPhase > _currentPhase)
            {
                _currentPhase = targetPhase;
                _phaseTimer = 0f;

                string phaseName = _currentPhase < encounter.Phases.Count
                    ? encounter.Phases[_currentPhase].PhaseName
                    : $"Phase {_currentPhase}";

                events.Add(new SimulationEvent
                {
                    Time = SimulatedTime,
                    HP = SimulatedHP,
                    Phase = _currentPhase,
                    Description = $"Phase transition → {phaseName} (HP: {SimulatedHP:P0})"
                });
            }

            // Enrage timer
            if (encounter.EnrageTimer > 0 && _encounterTimer >= encounter.EnrageTimer && !_isEnraged)
            {
                _isEnraged = true;
                events.Add(new SimulationEvent
                {
                    Time = SimulatedTime,
                    HP = SimulatedHP,
                    Phase = _currentPhase,
                    Description = $"Hard enrage at {encounter.EnrageTimer:F0}s"
                });
            }
        }

        private void SimulateAbilitySelection(AbilityProfileSO abilities, List<SimulationEvent> events)
        {
            int selectedIndex = -1;

            for (int i = 0; i < abilities.Abilities.Count; i++)
            {
                var ab = abilities.Abilities[i];
                if (ab == null) continue;
                if (_abilityCooldowns[i] > 0) continue;
                if (_currentPhase < ab.PhaseMin || _currentPhase > ab.PhaseMax) continue;
                if (SimulatedHP < ab.HPThresholdMin || SimulatedHP > ab.HPThresholdMax) continue;

                selectedIndex = i;
                break; // Priority mode — first valid wins
            }

            if (selectedIndex >= 0)
            {
                var ab = abilities.Abilities[selectedIndex];
                float castDuration = ab.CastTime + ab.ActiveDuration + ab.RecoveryTime;

                // Track idle time
                float idleGap = SimulatedTime - _lastAbilityEndTime;
                if (idleGap > 1.0f && _lastAbilityEndTime > 0)
                {
                    _totalIdleTime += idleGap;
                    Warnings.Add($"Boss idle for {idleGap:F1}s at {FormatTime(SimulatedTime - idleGap)}");
                }

                events.Add(new SimulationEvent
                {
                    Time = SimulatedTime,
                    HP = SimulatedHP,
                    Phase = _currentPhase,
                    Description = $"Cast: {ab.AbilityName} (dmg:{ab.DamageBase:F0}, cast:{castDuration:F1}s)"
                });

                _abilityCooldowns[selectedIndex] = ab.Cooldown;
                _globalCooldown = ab.GlobalCooldown;
                _lastAbilityEndTime = SimulatedTime + castDuration;

                // Apply cooldown group
                if (ab.CooldownGroupId > 0)
                {
                    for (int j = 0; j < abilities.Abilities.Count; j++)
                    {
                        var other = abilities.Abilities[j];
                        if (other != null && other.CooldownGroupId == ab.CooldownGroupId)
                        {
                            _abilityCooldowns[j] = System.Math.Max(
                                _abilityCooldowns[j], ab.CooldownGroupDuration);
                        }
                    }
                }
            }
        }

        private static string FormatTime(float seconds)
        {
            int min = (int)(seconds / 60f);
            int sec = (int)(seconds % 60f);
            return $"{min}:{sec:D2}";
        }
    }

    public struct SimulationEvent
    {
        public float Time;
        public float HP;
        public int Phase;
        public string Description;
    }
}
#endif
