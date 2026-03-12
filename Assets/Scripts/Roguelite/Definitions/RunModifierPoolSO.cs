using System.Collections.Generic;
using UnityEngine;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.4: Serializable definition for a single run modifier.
    /// Authored in RunModifierPoolSO, baked into ModifierRegistryBlob at runtime.
    /// </summary>
    [System.Serializable]
    public struct RunModifierDefinition
    {
        [Tooltip("Unique identifier. Must be stable across versions.")]
        public int ModifierId;

        [Tooltip("Display name shown in modifier selection UI.")]
        public string DisplayName;

        [Tooltip("Description of what this modifier does.")]
        [TextArea(2, 4)]
        public string Description;

        [Tooltip("Icon for modifier UI.")]
        public Sprite Icon;

        [Tooltip("Whether this modifier helps, hurts, or is neutral for the player.")]
        public ModifierPolarity Polarity;

        [Tooltip("What this modifier targets (enemy stats, economy, player stats, etc.).")]
        public ModifierTarget Target;

        [Tooltip("Target-specific stat index. EnemyStat: 0=Health,1=Damage,2=SpawnRate. Economy: 0=LootQty,1=LootQual,2=XP,3=Currency.")]
        public int StatId;

        [Tooltip("The value applied per stack. Interpretation depends on IsMultiplicative.")]
        public float FloatValue;

        [Tooltip("If true, value is multiplied (1.5 = +50%). If false, value is added (+5.0).")]
        public bool IsMultiplicative;

        [Tooltip("Whether this modifier can be acquired multiple times.")]
        public bool Stackable;

        [Tooltip("Maximum number of stacks. Ignored if not Stackable.")]
        [Min(1)]
        public int MaxStacks;

        [Tooltip("Minimum ascension level required for this modifier to appear.")]
        [Min(0)]
        public int RequiredAscensionLevel;

        [Tooltip("Heat cost to voluntarily add this modifier (ascension heat budget system).")]
        [Min(0)]
        public int HeatCost;
    }

    /// <summary>
    /// EPIC 23.4: Designer-authored pool of all available run modifiers.
    /// Created via Assets > Create > DIG > Roguelite > Run Modifier Pool.
    /// Loaded from Resources/RunModifierPool by ModifierBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(fileName = "RunModifierPool", menuName = "DIG/Roguelite/Run Modifier Pool", order = 3)]
    public class RunModifierPoolSO : ScriptableObject
    {
        [Tooltip("Human-readable name for this pool.")]
        public string PoolName = "Modifier Pool";

        [Tooltip("All modifier definitions. ModifierId must be unique.")]
        public List<RunModifierDefinition> Modifiers = new();

        /// <summary>
        /// Validates the pool for common authoring errors. Called by editor tooling.
        /// </summary>
        public bool Validate(out string error)
        {
            var ids = new HashSet<int>();
            foreach (var m in Modifiers)
            {
                if (!ids.Add(m.ModifierId))
                {
                    error = $"Duplicate ModifierId: {m.ModifierId}";
                    return false;
                }
                if (m.MaxStacks < 1)
                {
                    error = $"Modifier '{m.DisplayName}' (Id={m.ModifierId}) has MaxStacks < 1.";
                    return false;
                }
            }
            error = null;
            return true;
        }
    }
}
