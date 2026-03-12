using System.Collections.Generic;
using UnityEngine;

namespace DIG.Items.Definitions
{
    /// <summary>
    /// EPIC 16.6: Central item database containing all ItemEntrySO references.
    /// Place one instance in Resources/ folder for runtime loading by ItemRegistryBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Items/Item Database", order = 1)]
    public class ItemDatabaseSO : ScriptableObject
    {
        [SerializeField]
        private List<ItemEntrySO> _entries = new();

        private Dictionary<int, ItemEntrySO> _lookupTable;

        public IReadOnlyList<ItemEntrySO> Entries => _entries;

        private void OnEnable()
        {
            BuildLookupTable();
        }

        public void BuildLookupTable()
        {
            _lookupTable = new Dictionary<int, ItemEntrySO>(_entries.Count);
            foreach (var entry in _entries)
            {
                if (entry == null) continue;
                _lookupTable[entry.ItemTypeId] = entry;
            }
        }

        public ItemEntrySO GetItem(int itemTypeId)
        {
            if (_lookupTable == null) BuildLookupTable();
            _lookupTable.TryGetValue(itemTypeId, out var entry);
            return entry;
        }

        public List<ItemEntrySO> GetItemsByCategory(ItemCategory category)
        {
            var results = new List<ItemEntrySO>();
            foreach (var entry in _entries)
            {
                if (entry != null && entry.Category == category)
                    results.Add(entry);
            }
            return results;
        }

        public List<ItemEntrySO> GetItemsByRarity(ItemRarity rarity)
        {
            var results = new List<ItemEntrySO>();
            foreach (var entry in _entries)
            {
                if (entry != null && entry.Rarity == rarity)
                    results.Add(entry);
            }
            return results;
        }

        private void OnValidate()
        {
            // Check for duplicate IDs
            var seen = new HashSet<int>();
            foreach (var entry in _entries)
            {
                if (entry == null) continue;
                if (!seen.Add(entry.ItemTypeId))
                {
                    Debug.LogWarning($"[ItemDatabase] Duplicate ItemTypeId {entry.ItemTypeId} found: {entry.DisplayName}", this);
                }
                if (entry.WorldPrefab == null)
                {
                    Debug.LogWarning($"[ItemDatabase] Item '{entry.DisplayName}' (ID:{entry.ItemTypeId}) has no WorldPrefab assigned", this);
                }
            }
        }
    }
}
