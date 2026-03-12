using System.Collections.Generic;
using UnityEngine;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.2: Serializable definition for a single meta-unlock entry.
    /// Authored in MetaUnlockTreeSO, baked into MetaUnlockBlob + MetaUnlockEntry buffer at runtime.
    /// </summary>
    [System.Serializable]
    public struct MetaUnlockDefinition
    {
        [Tooltip("Unique identifier. Must be stable across versions (persisted in save data).")]
        public int UnlockId;

        [Tooltip("Display name shown in the meta-progression UI.")]
        public string DisplayName;

        [Tooltip("Description of what this unlock provides.")]
        [TextArea(2, 4)]
        public string Description;

        [Tooltip("Determines how FloatValue/IntValue are interpreted.")]
        public MetaUnlockCategory Category;

        [Tooltip("Meta-currency cost to purchase this unlock.")]
        [Min(0)]
        public int Cost;

        [Tooltip("UnlockId of the prerequisite. -1 = no prerequisite.")]
        public int PrerequisiteId;

        [Tooltip("Category-specific float value (stat amount, multiplier, discount %).")]
        public float FloatValue;

        [Tooltip("Category-specific int value (item ID, ability ID, stat ID).")]
        public int IntValue;

        [Tooltip("Icon displayed in the unlock tree UI.")]
        public Sprite Icon;
    }

    /// <summary>
    /// EPIC 23.2: Designer-authored unlock tree for meta-progression.
    /// Created via Assets > Create > DIG > Roguelite > Meta Unlock Tree.
    /// Loaded from Resources/ at bootstrap. Multiple trees supported for different progression paths.
    /// </summary>
    [CreateAssetMenu(fileName = "MetaUnlockTree", menuName = "DIG/Roguelite/Meta Unlock Tree", order = 2)]
    public class MetaUnlockTreeSO : ScriptableObject
    {
        [Tooltip("Human-readable name for this unlock tree (e.g., 'Combat Upgrades', 'Economy').")]
        public string TreeName = "Unlock Tree";

        [Tooltip("All unlocks in this tree. Order doesn't matter — UnlockId is the stable key.")]
        public List<MetaUnlockDefinition> Unlocks = new();

        /// <summary>
        /// Validates the tree for common authoring errors.
        /// Called by editor tooling.
        /// </summary>
        public bool Validate(out string error)
        {
            var ids = new HashSet<int>();
            foreach (var u in Unlocks)
            {
                if (!ids.Add(u.UnlockId))
                {
                    error = $"Duplicate UnlockId: {u.UnlockId}";
                    return false;
                }
                if (u.Cost < 0)
                {
                    error = $"Unlock '{u.DisplayName}' (Id={u.UnlockId}) has negative cost.";
                    return false;
                }
                if (u.PrerequisiteId >= 0 && !ids.Contains(u.PrerequisiteId) && !HasUnlock(u.PrerequisiteId))
                {
                    error = $"Unlock '{u.DisplayName}' (Id={u.UnlockId}) references missing prerequisite {u.PrerequisiteId}.";
                    return false;
                }
            }
            error = null;
            return true;
        }

        private bool HasUnlock(int id)
        {
            foreach (var u in Unlocks)
                if (u.UnlockId == id) return true;
            return false;
        }
    }
}
