using System;
using System.Collections.Generic;
using UnityEngine;
using DIG.Roguelite;

namespace DIG.Roguelite.Rewards
{
    [Serializable]
    public class EventChoice
    {
        public string ChoiceText;
        [TextArea(2, 4)] public string OutcomeText;
        public RewardDefinitionSO Reward;            // Positive outcome (nullable)
        public RunModifierDefinitionSO Curse;        // Negative outcome (nullable)
        [Range(0f, 1f)] public float SuccessProbability = 1f;  // 0-1. 1 = guaranteed
    }

    /// <summary>
    /// EPIC 23.5: Risk-reward narrative event with choices.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Roguelite/Run Event")]
    public class RunEventDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int EventId;
        public string DisplayName;
        [TextArea(3, 6)] public string NarrativeText;
        public Sprite Illustration;

        [Header("Choices")]
        public List<EventChoice> Choices = new();

        [Header("Constraints")]
        public int MinZoneIndex;
        public int MaxZoneIndex;
        [Min(0f)] public float Weight = 1f;          // Selection weight in event pool
    }
}
