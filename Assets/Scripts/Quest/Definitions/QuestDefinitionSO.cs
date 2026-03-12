using System;
using UnityEngine;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Serializable objective definition, inline in QuestDefinitionSO.
    /// </summary>
    [Serializable]
    public struct ObjectiveDefinition
    {
        [Tooltip("Unique within this quest")]
        public int ObjectiveId;
        public ObjectiveType Type;
        [Tooltip("Context-dependent: prefab hash (Kill), interactable ID (Interact), item type ID (Collect), zone ID (ReachZone), recipe ID (Craft)")]
        public int TargetId;
        [Min(1)]
        public int RequiredCount;
        public string Description;
        public bool IsOptional;
        public bool IsHidden;
        [Tooltip("0 = immediately available. Otherwise, ObjectiveId that must complete first.")]
        public int UnlockAfterObjectiveId;
    }

    /// <summary>
    /// EPIC 16.12: Serializable reward definition.
    /// </summary>
    [Serializable]
    public struct QuestReward
    {
        public QuestRewardType Type;
        [Tooltip("Item type ID (Item), currency amount (Currency), XP amount (Experience), recipe ID (RecipeUnlock)")]
        public int Value;
        [Tooltip("Quantity for items, ignored for others")]
        public int Quantity;
        [Tooltip("Currency type when Type == Currency")]
        public Economy.CurrencyType CurrencyType;
    }

    /// <summary>
    /// EPIC 16.12: ScriptableObject defining a single quest.
    /// Designers create these in the editor and assign them to a QuestDatabaseSO.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Quest/Quest Definition")]
    public class QuestDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int QuestId;
        public string DisplayName;
        [TextArea(2, 5)]
        public string Description;
        public QuestCategory Category;

        [Header("Objectives")]
        public ObjectiveDefinition[] Objectives = Array.Empty<ObjectiveDefinition>();

        [Header("Rewards")]
        public QuestReward[] Rewards = Array.Empty<QuestReward>();

        [Header("Prerequisites")]
        [Tooltip("Quest IDs that must be completed (TurnedIn) before this quest becomes available")]
        public int[] PrerequisiteQuestIds = Array.Empty<int>();

        [Header("Behavior")]
        public bool IsRepeatable;
        [Tooltip("0 = no time limit (in seconds)")]
        public float TimeLimit;
        [Tooltip("If true, quest completes automatically when all non-optional objectives are done. If false, player must turn in at TurnInInteractableId.")]
        public bool AutoComplete = true;
        [Tooltip("Interactable ID of NPC to turn in to. 0 = auto-complete.")]
        public int TurnInInteractableId;
    }
}
