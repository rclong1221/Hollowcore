using System.Collections.Generic;
using UnityEngine;

namespace Audio.Config
{
    /// <summary>
    /// Central registry mapping int ClipId to AudioClip arrays.
    /// Supports categories and clip variations (random selection per ID).
    /// EPIC 15.27 Phase 2.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioClipBank", menuName = "DIG/Audio/Clip Bank")]
    public class AudioClipBank : ScriptableObject
    {
        [System.Serializable]
        public struct ClipEntry
        {
            [Tooltip("Unique clip ID used by PlayAudioRequest.ClipId")]
            public int Id;

            [Tooltip("Human-readable name for editor display")]
            public string Name;

            [Tooltip("Category for organization")]
            public ClipCategory Category;

            [Tooltip("Clip variations — a random one is selected on play")]
            public AudioClip[] Clips;
        }

        public enum ClipCategory
        {
            Combat,
            Ambient,
            Creature,
            Ability,
            UI,
            Movement,
            Environment
        }

        [SerializeField] private ClipEntry[] _entries = System.Array.Empty<ClipEntry>();

        // Runtime lookup (built on first access)
        private Dictionary<int, ClipEntry> _lookup;

        private void BuildLookup()
        {
            _lookup = new Dictionary<int, ClipEntry>(_entries.Length);
            for (int i = 0; i < _entries.Length; i++)
            {
                if (!_lookup.ContainsKey(_entries[i].Id))
                    _lookup.Add(_entries[i].Id, _entries[i]);
                else
                    Debug.LogWarning($"[AudioClipBank] Duplicate ClipId {_entries[i].Id} in '{name}'");
            }
        }

        /// <summary>Try to get a random clip for the given ID.</summary>
        public bool TryGetClip(int clipId, out AudioClip clip)
        {
            if (_lookup == null) BuildLookup();

            clip = null;
            if (!_lookup.TryGetValue(clipId, out var entry)) return false;
            if (entry.Clips == null || entry.Clips.Length == 0) return false;

            clip = entry.Clips[Random.Range(0, entry.Clips.Length)];
            return clip != null;
        }

        /// <summary>Try to get a clip entry by ID.</summary>
        public bool TryGetEntry(int clipId, out ClipEntry entry)
        {
            if (_lookup == null) BuildLookup();
            return _lookup.TryGetValue(clipId, out entry);
        }

        /// <summary>Get all entries (for editor display).</summary>
        public ClipEntry[] GetEntries() => _entries;

        private void OnValidate()
        {
            _lookup = null; // Force rebuild on asset change
        }
    }
}
