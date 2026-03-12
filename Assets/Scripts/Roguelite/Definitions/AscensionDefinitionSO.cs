using System.Collections.Generic;
using UnityEngine;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.4: A single ascension tier defining forced modifiers and reward scaling.
    /// Tiers are cumulative — ascending to level 3 applies tiers 1, 2, and 3.
    /// </summary>
    [System.Serializable]
    public struct AscensionTier
    {
        [Tooltip("Ascension level (1-based). Level 0 is the default with no modifiers.")]
        [Min(1)]
        public byte Level;

        [Tooltip("Display name for this tier (e.g., 'Heat 1', 'Inferno').")]
        public string DisplayName;

        [Tooltip("Description of what this tier adds.")]
        [TextArea(2, 4)]
        public string Description;

        [Tooltip("ModifierIds forced onto every run at this ascension level.")]
        public int[] ForcedModifierIds;

        [Tooltip("Multiplier for all rewards at this tier. Applied on top of lower tiers.")]
        [Range(0.5f, 5f)]
        public float RewardMultiplier;

        [Tooltip("Additional heat budget points available at this tier for voluntary modifiers.")]
        [Min(0)]
        public int BonusHeatBudget;
    }

    /// <summary>
    /// EPIC 23.4: Designer-authored ascension/heat definition.
    /// Created via Assets > Create > DIG > Roguelite > Ascension Definition.
    /// Loaded from Resources/AscensionDefinition by ModifierBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(fileName = "AscensionDefinition", menuName = "DIG/Roguelite/Ascension Definition", order = 4)]
    public class AscensionDefinitionSO : ScriptableObject
    {
        [Tooltip("Human-readable name for this ascension path.")]
        public string DefinitionName = "Ascension";

        [Tooltip("Ordered list of ascension tiers. Level 0 (default, no modifiers) is implicit.")]
        public List<AscensionTier> Tiers = new();

        /// <summary>
        /// Validates the definition for common authoring errors. Called by editor tooling.
        /// </summary>
        public bool Validate(out string error)
        {
            var levels = new HashSet<byte>();
            foreach (var tier in Tiers)
            {
                if (!levels.Add(tier.Level))
                {
                    error = $"Duplicate ascension level: {tier.Level}";
                    return false;
                }
                if (tier.RewardMultiplier <= 0f)
                {
                    error = $"Tier '{tier.DisplayName}' (Level={tier.Level}) has non-positive RewardMultiplier.";
                    return false;
                }
            }
            error = null;
            return true;
        }
    }
}
