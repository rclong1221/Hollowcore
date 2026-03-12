using System.Collections.Generic;
using UnityEngine;

namespace DIG.VFX
{
    /// <summary>
    /// EPIC 16.7: ScriptableObject database containing all VFX type entries.
    /// Lives in Resources/ for runtime loading.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/VFX/VFX Type Database")]
    public class VFXTypeDatabase : ScriptableObject
    {
        [SerializeField] private List<VFXTypeEntry> _entries = new();

        private Dictionary<int, VFXTypeEntry> _lookup;

        public bool TryGetEntry(int typeId, out VFXTypeEntry entry)
        {
            EnsureLookup();
            return _lookup.TryGetValue(typeId, out entry);
        }

        public IReadOnlyList<VFXTypeEntry> AllEntries => _entries;

        private void EnsureLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<int, VFXTypeEntry>(_entries.Count);
            foreach (var e in _entries)
                _lookup[e.TypeId] = e;
        }

        private void OnEnable()
        {
            _lookup = null;
        }
    }
}
