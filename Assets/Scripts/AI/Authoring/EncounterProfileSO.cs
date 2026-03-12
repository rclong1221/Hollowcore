using System.Collections.Generic;
using UnityEngine;
using DIG.AI.Components;

namespace DIG.AI.Authoring
{
    /// <summary>
    /// EPIC 15.32: Phase definition entry for EncounterProfileSO.
    /// </summary>
    [System.Serializable]
    public class PhaseEntry
    {
        public string PhaseName = "Phase";
        [Tooltip("-1 = trigger-only (no HP threshold)")]
        public float HPThresholdEntry = 1.0f;
        public float SpeedMultiplier = 1.0f;
        public float DamageMultiplier = 1.0f;
        public float GlobalCooldownOverride = -1f;
        public float InvulnerableDuration = 0f;
        [Tooltip("Ability to cast on phase entry")]
        public AbilityDefinitionSO TransitionAbility;
        [Tooltip("Add group to spawn on phase entry (0 = none)")]
        public byte SpawnGroupId = 0;
    }

    /// <summary>
    /// EPIC 15.32: Encounter trigger entry for EncounterProfileSO.
    /// </summary>
    [System.Serializable]
    public class TriggerEntry
    {
        public string TriggerName = "Trigger";
        public TriggerConditionType Condition;
        public float ConditionValue;
        public byte ConditionParam;
        public float ConditionRange;
        public Vector3 ConditionPosition;

        [Header("Composite (for AND/OR triggers)")]
        public int SubTriggerIndex0 = -1;
        public int SubTriggerIndex1 = -1;
        public int SubTriggerIndex2 = -1;

        public TriggerActionType Action;
        public float ActionValue;
        public byte ActionParam;
        public Vector3 ActionPosition;

        public bool FireOnce = true;
        public float Delay = 0f;
    }

    /// <summary>
    /// EPIC 15.32: Spawn group entry for EncounterProfileSO.
    /// </summary>
    [System.Serializable]
    public class SpawnGroupEntry
    {
        public byte GroupId;
        public GameObject AddPrefab;
        public byte Count = 1;
        public Vector3 SpawnOffset;
        public float SpawnRadius = 3f;
        public bool TetherToBoss = false;
    }

    /// <summary>
    /// EPIC 15.32: Boss encounter profile ScriptableObject.
    /// Defines phases, triggers, and spawn groups for multi-phase boss encounters.
    /// Only added to enemies that need phase/trigger behavior — regular enemies skip this.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEncounterProfile", menuName = "DIG/AI/Encounter Profile")]
    public class EncounterProfileSO : ScriptableObject
    {
        [Header("Encounter Settings")]
        [Tooltip("-1 = no hard enrage")]
        public float EnrageTimer = -1f;
        public float EnrageDamageMultiplier = 3f;

        [Header("Phases")]
        public List<PhaseEntry> Phases = new();

        [Header("Triggers")]
        public List<TriggerEntry> Triggers = new();

        [Header("Add Groups")]
        public List<SpawnGroupEntry> SpawnGroups = new();
    }
}
