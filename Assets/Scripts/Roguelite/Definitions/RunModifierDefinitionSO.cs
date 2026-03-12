using UnityEngine;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.4: Individual run modifier asset for direct references (e.g., from RewardDefinitionSO).
    /// For the full modifier pool loaded at bootstrap, see RunModifierPoolSO.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Roguelite/Run Modifier")]
    public class RunModifierDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int ModifierId;
        public string DisplayName;
        [TextArea(2, 4)] public string Description;
        public Sprite Icon;
        public ModifierPolarity Polarity;
        public ModifierTarget Target;

        [Header("Effect")]
        public int StatId;
        public float FloatValue;
        public bool IsMultiplicative;
        public int IntValue;

        [Header("Stacking")]
        public bool Stackable;
        [Min(1)] public int MaxStacks = 1;

        [Header("Ascension")]
        [Min(0)] public int RequiredAscensionLevel;
        [Min(0)] public int HeatCost;
    }
}
