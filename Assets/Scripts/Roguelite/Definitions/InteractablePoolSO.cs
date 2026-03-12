using System;
using System.Collections.Generic;
using UnityEngine;

namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Weighted pool of interactable types for zone placement.
    /// InteractableDirectorSystem picks from this pool; the game's
    /// IInteractableHandler does the actual spawning.
    /// </summary>
    [CreateAssetMenu(fileName = "InteractablePool", menuName = "DIG/Roguelite/Interactable Pool", order = 14)]
    public class InteractablePoolSO : ScriptableObject
    {
        public string PoolName;
        public List<InteractablePoolEntry> Entries = new();
    }

    [Serializable]
    public struct InteractablePoolEntry
    {
        [Tooltip("Identifier passed to IInteractableHandler. Game maps this to prefabs.")]
        public int InteractableTypeId;

        public string DisplayName;

        public float Weight;

        [Tooltip("Base run-currency cost. 0 = free. Negative = gives currency.")]
        public int BaseCost;

        [Tooltip("Cost scales with difficulty. Final = BaseCost * (1 + difficulty * CostScale).")]
        public float CostScale;

        [Tooltip("Maximum of this type per zone. 0 = unlimited.")]
        public int MaxPerZone;

        [Tooltip("Minimum effective difficulty for this to appear. 0 = always.")]
        public float MinDifficulty;
    }
}
