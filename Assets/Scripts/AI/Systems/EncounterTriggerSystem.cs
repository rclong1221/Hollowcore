using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using DIG.AI.Components;
using DIG.Music;
using Health = Player.Components.Health;

namespace DIG.AI.Systems
{
    /// <summary>
    /// EPIC 15.32: Evaluates encounter trigger conditions and fires actions.
    /// Supports HP, timer, add-death, position, player-count, ability-count,
    /// composite (AND/OR), and manual triggers.
    ///
    /// Not Burst-compiled due to ECB for structural changes and entity lookups.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PhaseTransitionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class EncounterTriggerSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<EncounterState>();
        }

        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (encounterState, triggersBuf, transform, entity) in
                SystemAPI.Query<
                    RefRW<EncounterState>,
                    DynamicBuffer<EncounterTriggerDefinition>,
                    RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                ref var encounter = ref encounterState.ValueRW;
                var triggers = triggersBuf;

                // Start encounter timer on first aggro
                if (!encounter.EncounterStarted)
                {
                    if (SystemAPI.HasComponent<DIG.Aggro.Components.AggroState>(entity))
                    {
                        var aggro = SystemAPI.GetComponent<DIG.Aggro.Components.AggroState>(entity);
                        if (aggro.IsAggroed)
                        {
                            encounter.EncounterStarted = true;
                            encounter.EncounterTimer = 0f;
                            encounter.PhaseTimer = 0f;
                        }
                    }
                }

                if (!encounter.EncounterStarted) continue;

                // Tick timers
                encounter.EncounterTimer += deltaTime;
                encounter.PhaseTimer += deltaTime;

                // Get current HP%
                float hpPercent = 1f;
                if (SystemAPI.HasComponent<Health>(entity))
                {
                    var health = SystemAPI.GetComponent<Health>(entity);
                    if (health.Max > 0)
                        hpPercent = health.Current / health.Max;
                }

                float3 bossPos = transform.ValueRO.Position;

                // Evaluate each trigger
                for (int i = 0; i < triggers.Length; i++)
                {
                    var trigger = triggers[i];
                    if (!trigger.Enabled) continue;
                    if (trigger.FireOnce && trigger.HasFired) continue;

                    // Handle delayed execution
                    if (trigger.DelayStarted)
                    {
                        trigger.DelayTimer -= deltaTime;
                        if (trigger.DelayTimer <= 0f)
                        {
                            ExecuteAction(ref encounter, ref trigger, entity);
                            trigger.DelayStarted = false;
                            if (trigger.FireOnce) trigger.HasFired = true;
                        }
                        triggers[i] = trigger;
                        continue;
                    }

                    // Evaluate condition
                    bool conditionMet = false;
                    switch (trigger.ConditionType)
                    {
                        case TriggerConditionType.HPBelow:
                            conditionMet = hpPercent <= trigger.ConditionValue;
                            break;
                        case TriggerConditionType.HPAbove:
                            conditionMet = hpPercent >= trigger.ConditionValue;
                            break;
                        case TriggerConditionType.TimerElapsed:
                            conditionMet = trigger.ConditionParam == 1
                                ? encounter.PhaseTimer >= trigger.ConditionValue
                                : encounter.EncounterTimer >= trigger.ConditionValue;
                            break;
                        case TriggerConditionType.AddsDead:
                            conditionMet = GetAddsDeadCount(encounter, trigger.ConditionParam)
                                >= (int)trigger.ConditionValue;
                            break;
                        case TriggerConditionType.AddsAlive:
                            conditionMet = GetAddsAliveCount(encounter, trigger.ConditionParam)
                                <= (int)trigger.ConditionValue;
                            break;
                        case TriggerConditionType.AbilityCastCount:
                            conditionMet = encounter.AbilityCastCount0 >= (int)trigger.ConditionValue;
                            break;
                        case TriggerConditionType.PhaseIs:
                            conditionMet = encounter.CurrentPhase == (byte)trigger.ConditionValue;
                            break;
                        case TriggerConditionType.BossAtPosition:
                        {
                            float3 diff = bossPos - trigger.ConditionPosition;
                            diff.y = 0f;
                            conditionMet = math.lengthsq(diff) <= trigger.ConditionRange * trigger.ConditionRange;
                            break;
                        }
                        case TriggerConditionType.Composite_AND:
                        {
                            conditionMet = true;
                            if (trigger.SubTriggerIndex0 != 255 && trigger.SubTriggerIndex0 < triggers.Length)
                                conditionMet &= triggers[trigger.SubTriggerIndex0].HasFired;
                            if (trigger.SubTriggerIndex1 != 255 && trigger.SubTriggerIndex1 < triggers.Length)
                                conditionMet &= triggers[trigger.SubTriggerIndex1].HasFired;
                            if (trigger.SubTriggerIndex2 != 255 && trigger.SubTriggerIndex2 < triggers.Length)
                                conditionMet &= triggers[trigger.SubTriggerIndex2].HasFired;
                            break;
                        }
                        case TriggerConditionType.Composite_OR:
                        {
                            conditionMet = false;
                            if (trigger.SubTriggerIndex0 != 255 && trigger.SubTriggerIndex0 < triggers.Length)
                                conditionMet |= triggers[trigger.SubTriggerIndex0].HasFired;
                            if (trigger.SubTriggerIndex1 != 255 && trigger.SubTriggerIndex1 < triggers.Length)
                                conditionMet |= triggers[trigger.SubTriggerIndex1].HasFired;
                            if (trigger.SubTriggerIndex2 != 255 && trigger.SubTriggerIndex2 < triggers.Length)
                                conditionMet |= triggers[trigger.SubTriggerIndex2].HasFired;
                            break;
                        }
                    }

                    if (conditionMet)
                    {
                        if (trigger.Delay > 0f && !trigger.DelayStarted)
                        {
                            trigger.DelayStarted = true;
                            trigger.DelayTimer = trigger.Delay;
                        }
                        else
                        {
                            ExecuteAction(ref encounter, ref trigger, entity);
                            if (trigger.FireOnce) trigger.HasFired = true;
                        }
                    }

                    triggers[i] = trigger;
                }
            }
        }

        private void ExecuteAction(ref EncounterState encounter,
            ref EncounterTriggerDefinition trigger, Entity bossEntity)
        {
            switch (trigger.ActionType)
            {
                case TriggerActionType.TransitionPhase:
                    encounter.PendingPhase = (byte)math.max((int)encounter.PendingPhase, (int)(byte)trigger.ActionValue);
                    break;

                case TriggerActionType.ForceAbility:
                    if (SystemAPI.HasComponent<AbilityExecutionState>(bossEntity))
                    {
                        var exec = SystemAPI.GetComponent<AbilityExecutionState>(bossEntity);
                        if (exec.Phase == AbilityCastPhase.Idle)
                        {
                            // Find ability by ID
                            if (SystemAPI.HasBuffer<AbilityDefinition>(bossEntity))
                            {
                                var abilities = SystemAPI.GetBuffer<AbilityDefinition>(bossEntity);
                                for (int a = 0; a < abilities.Length; a++)
                                {
                                    if (abilities[a].AbilityId == trigger.ActionParam)
                                    {
                                        exec.SelectedAbilityIndex = a;
                                        exec.Phase = abilities[a].TelegraphDuration > 0
                                            ? AbilityCastPhase.Telegraph
                                            : AbilityCastPhase.Casting;
                                        exec.PhaseTimer = 0f;
                                        exec.DamageDealt = false;
                                        SystemAPI.SetComponent(bossEntity, exec);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    break;

                case TriggerActionType.SetInvulnerable:
                    encounter.IsTransitioning = true;
                    encounter.TransitionTimer = 0f;
                    encounter.TransitionDuration = trigger.ActionValue;
                    break;

                case TriggerActionType.Teleport:
                    if (SystemAPI.HasComponent<LocalTransform>(bossEntity))
                    {
                        var t = SystemAPI.GetComponent<LocalTransform>(bossEntity);
                        t.Position = trigger.ActionPosition;
                        SystemAPI.SetComponent(bossEntity, t);
                    }
                    break;

                case TriggerActionType.SetEnrage:
                    encounter.IsEnraged = true;
                    break;

                case TriggerActionType.ResetCooldowns:
                    if (SystemAPI.HasBuffer<AbilityCooldownState>(bossEntity))
                    {
                        var cooldowns = SystemAPI.GetBuffer<AbilityCooldownState>(bossEntity);
                        for (int c = 0; c < cooldowns.Length; c++)
                        {
                            var cd = cooldowns[c];
                            cd.CooldownRemaining = 0f;
                            cd.GlobalCooldownRemaining = 0f;
                            cd.CooldownGroupRemaining = 0f;
                            cooldowns[c] = cd;
                        }
                    }
                    break;

                case TriggerActionType.EnableTrigger:
                    // Handled by caller via buffer write
                    break;

                case TriggerActionType.DisableTrigger:
                    // Handled by caller via buffer write
                    break;

                case TriggerActionType.PlayDialogue:
                    // EPIC 16.16: Create PlayDialogueTrigger transient entity for
                    // EncounterDialogueBridgeSystem to process (keeps this system Burst-compatible)
                    {
                        var dialogueEntity = EntityManager.CreateEntity();
                        EntityManager.AddComponentData(dialogueEntity, new DIG.Dialogue.PlayDialogueTrigger
                        {
                            BossEntity = bossEntity,
                            DialogueIdOrBarkId = trigger.ActionParam
                        });
                    }
                    break;

                // SpawnAddGroup, ModifyStats, PlayVFX, DestroyAdds
                // will be fully implemented when those subsystems exist.
                // For now they set the pending data for PhaseTransitionSystem/AddSpawnSystem.
                case TriggerActionType.SpawnAddGroup:
                    // AddSpawnSystem will read PendingPhase or direct spawn requests
                    break;

                // EPIC 15.33: Threat manipulation actions
                case TriggerActionType.ThreatWipeAll:
                    if (SystemAPI.HasBuffer<DIG.Aggro.Components.ThreatEntry>(bossEntity))
                    {
                        var threats = SystemAPI.GetBuffer<DIG.Aggro.Components.ThreatEntry>(bossEntity);
                        threats.Clear();
                        if (SystemAPI.HasComponent<DIG.Aggro.Components.AggroState>(bossEntity))
                        {
                            SystemAPI.SetComponent(bossEntity, DIG.Aggro.Components.AggroState.Default);
                        }
                    }
                    break;

                case TriggerActionType.ThreatMultiplyAll:
                    if (SystemAPI.HasBuffer<DIG.Aggro.Components.ThreatEntry>(bossEntity))
                    {
                        var threats = SystemAPI.GetBuffer<DIG.Aggro.Components.ThreatEntry>(bossEntity);
                        for (int t = 0; t < threats.Length; t++)
                        {
                            var entry = threats[t];
                            entry.ThreatValue *= trigger.ActionValue;
                            threats[t] = entry;
                        }
                    }
                    break;

                case TriggerActionType.ThreatFixateRandom:
                    if (SystemAPI.HasBuffer<DIG.Aggro.Components.ThreatEntry>(bossEntity) &&
                        SystemAPI.HasComponent<DIG.Aggro.Components.ThreatFixate>(bossEntity))
                    {
                        var threats = SystemAPI.GetBuffer<DIG.Aggro.Components.ThreatEntry>(bossEntity);
                        if (threats.Length > 0)
                        {
                            // Pick random entry from threat table
                            int idx = (int)(encounter.EncounterTimer * 100f) % threats.Length;
                            var fixate = new DIG.Aggro.Components.ThreatFixate
                            {
                                FixatedTarget = threats[idx].SourceEntity,
                                Duration = trigger.ActionValue,
                                Timer = trigger.ActionValue
                            };
                            SystemAPI.SetComponent(bossEntity, fixate);
                            SystemAPI.SetComponentEnabled<DIG.Aggro.Components.ThreatFixate>(bossEntity, true);
                        }
                    }
                    break;

                // EPIC 17.5: Boss music override
                case TriggerActionType.PlayMusic:
                    {
                        var musicEntity = EntityManager.CreateEntity();
                        EntityManager.AddComponentData(musicEntity, new MusicBossOverride
                        {
                            TrackId = (int)trigger.ActionValue,
                            Activate = true
                        });
                    }
                    break;

                // EPIC 17.9: Cinematic trigger
                case TriggerActionType.PlayCinematic:
                    {
                        var cinematicSystem = World.GetExistingSystemManaged<DIG.Cinematic.CinematicTriggerSystem>();
                        if (cinematicSystem != null)
                        {
                            cinematicSystem.TriggerCinematicFromEncounter(
                                trigger.ActionParam,
                                DIG.Cinematic.CinematicType.FullCinematic,
                                DIG.Cinematic.SkipPolicy.AnyoneCanSkip,
                                trigger.ActionValue);
                        }
                    }
                    break;
            }

            // Handle EnableTrigger/DisableTrigger via buffer index
            if (trigger.ActionType == TriggerActionType.EnableTrigger ||
                trigger.ActionType == TriggerActionType.DisableTrigger)
            {
                if (SystemAPI.HasBuffer<EncounterTriggerDefinition>(bossEntity))
                {
                    var allTriggers = SystemAPI.GetBuffer<EncounterTriggerDefinition>(bossEntity);
                    if (trigger.ActionParam < allTriggers.Length)
                    {
                        var target = allTriggers[trigger.ActionParam];
                        target.Enabled = trigger.ActionType == TriggerActionType.EnableTrigger;
                        allTriggers[trigger.ActionParam] = target;
                    }
                }
            }
        }

        private int GetAddsDeadCount(in EncounterState encounter, byte groupId)
        {
            return groupId switch
            {
                0 => encounter.AddTracker0Spawned - encounter.AddTracker0Alive,
                1 => encounter.AddTracker1Spawned - encounter.AddTracker1Alive,
                2 => encounter.AddTracker2Spawned - encounter.AddTracker2Alive,
                3 => encounter.AddTracker3Spawned - encounter.AddTracker3Alive,
                _ => 0
            };
        }

        private int GetAddsAliveCount(in EncounterState encounter, byte groupId)
        {
            return groupId switch
            {
                0 => encounter.AddTracker0Alive,
                1 => encounter.AddTracker1Alive,
                2 => encounter.AddTracker2Alive,
                3 => encounter.AddTracker3Alive,
                _ => 0
            };
        }
    }
}
