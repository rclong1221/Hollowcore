using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using DIG.AI.Components;

namespace DIG.AI.Authoring
{
    /// <summary>
    /// EPIC 15.32: Authoring component that bakes EncounterProfileSO into ECS buffers.
    /// Add to boss enemy prefabs alongside AIBrainAuthoring and AbilityProfileAuthoring.
    /// Regular enemies don't need this — phases and triggers are skipped if absent.
    /// </summary>
    [AddComponentMenu("DIG/AI/Encounter Profile")]
    public class EncounterProfileAuthoring : MonoBehaviour
    {
        [Tooltip("The encounter profile ScriptableObject for this boss.")]
        public EncounterProfileSO Profile;

        class Baker : Baker<EncounterProfileAuthoring>
        {
            public override void Bake(EncounterProfileAuthoring authoring)
            {
                if (authoring.Profile == null) return;
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Encounter State
                AddComponent(entity, EncounterState.Default(
                    authoring.Profile.EnrageTimer,
                    authoring.Profile.EnrageDamageMultiplier
                ));

                // Phase Definitions
                var phaseBuffer = AddBuffer<PhaseDefinition>(entity);
                for (int i = 0; i < authoring.Profile.Phases.Count; i++)
                {
                    var phase = authoring.Profile.Phases[i];
                    phaseBuffer.Add(new PhaseDefinition
                    {
                        PhaseIndex = (byte)i,
                        HPThresholdEntry = phase.HPThresholdEntry,
                        SpeedMultiplier = phase.SpeedMultiplier,
                        DamageMultiplier = phase.DamageMultiplier,
                        GlobalCooldownOverride = phase.GlobalCooldownOverride,
                        InvulnerableDuration = phase.InvulnerableDuration,
                        TransitionAbilityId = phase.TransitionAbility != null
                            ? phase.TransitionAbility.AbilityId : (ushort)0,
                        SpawnGroupId = phase.SpawnGroupId
                    });
                }

                // Trigger Definitions
                var triggerBuffer = AddBuffer<EncounterTriggerDefinition>(entity);
                foreach (var trigger in authoring.Profile.Triggers)
                {
                    triggerBuffer.Add(new EncounterTriggerDefinition
                    {
                        ConditionType = trigger.Condition,
                        ConditionValue = trigger.ConditionValue,
                        ConditionParam = trigger.ConditionParam,
                        ConditionRange = trigger.ConditionRange,
                        ConditionPosition = trigger.ConditionPosition,
                        SubTriggerIndex0 = trigger.SubTriggerIndex0 >= 0 ? (byte)trigger.SubTriggerIndex0 : (byte)255,
                        SubTriggerIndex1 = trigger.SubTriggerIndex1 >= 0 ? (byte)trigger.SubTriggerIndex1 : (byte)255,
                        SubTriggerIndex2 = trigger.SubTriggerIndex2 >= 0 ? (byte)trigger.SubTriggerIndex2 : (byte)255,
                        ActionType = trigger.Action,
                        ActionValue = trigger.ActionValue,
                        ActionParam = trigger.ActionParam,
                        ActionPosition = trigger.ActionPosition,
                        Enabled = true,
                        FireOnce = trigger.FireOnce,
                        HasFired = false,
                        Delay = trigger.Delay,
                        DelayTimer = 0f,
                        DelayStarted = false
                    });
                }

                // Spawn Group Definitions
                var spawnBuffer = AddBuffer<SpawnGroupDefinition>(entity);
                foreach (var group in authoring.Profile.SpawnGroups)
                {
                    var prefabEntity = group.AddPrefab != null
                        ? GetEntity(group.AddPrefab, TransformUsageFlags.Dynamic)
                        : Entity.Null;

                    spawnBuffer.Add(new SpawnGroupDefinition
                    {
                        GroupId = group.GroupId,
                        PrefabEntity = prefabEntity,
                        Count = group.Count,
                        SpawnOffset = group.SpawnOffset,
                        SpawnRadius = group.SpawnRadius,
                        TetherToBoss = group.TetherToBoss
                    });
                }

                DependsOn(authoring.Profile);
            }
        }
    }
}
