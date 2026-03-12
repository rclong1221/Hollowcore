using System.Collections.Generic;
using UnityEngine;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Central registry of all music tracks and stinger definitions.
    /// Loaded from Resources/MusicDatabase by MusicBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Music/Music Database")]
    public class MusicDatabaseSO : ScriptableObject
    {
        [Tooltip("All available music tracks.")]
        public List<MusicTrackSO> Tracks = new List<MusicTrackSO>();

        [Tooltip("All stinger definitions.")]
        public List<MusicStingerDefinition> Stingers = new List<MusicStingerDefinition>();

        [Tooltip("Fallback track when no zone is active.")]
        public int DefaultTrackId;

        [Tooltip("Special 'silence' track for areas with no music (0 = true silence).")]
        public int SilenceTrackId;

        // O(1) lookup caches — built lazily on first access
        private Dictionary<int, MusicTrackSO> _trackLookup;
        private Dictionary<int, MusicStingerDefinition> _stingerLookup;

        /// <summary>Resolve TrackId to MusicTrackSO. Returns null if not found. O(1) after first call.</summary>
        public MusicTrackSO GetTrack(int trackId)
        {
            if (_trackLookup == null) RebuildTrackLookup();
            _trackLookup.TryGetValue(trackId, out var result);
            return result;
        }

        /// <summary>Resolve StingerId to MusicStingerDefinition. Returns null if not found. O(1) after first call.</summary>
        public MusicStingerDefinition GetStinger(int stingerId)
        {
            if (_stingerLookup == null) RebuildStingerLookup();
            _stingerLookup.TryGetValue(stingerId, out var result);
            return result;
        }

        /// <summary>Force rebuild of lookup caches. Call after modifying Tracks/Stingers lists at runtime.</summary>
        public void InvalidateCache()
        {
            _trackLookup = null;
            _stingerLookup = null;
        }

        private void RebuildTrackLookup()
        {
            _trackLookup = new Dictionary<int, MusicTrackSO>(Tracks.Count);
            for (int i = 0; i < Tracks.Count; i++)
            {
                if (Tracks[i] != null && !_trackLookup.ContainsKey(Tracks[i].TrackId))
                    _trackLookup[Tracks[i].TrackId] = Tracks[i];
            }
        }

        private void RebuildStingerLookup()
        {
            _stingerLookup = new Dictionary<int, MusicStingerDefinition>(Stingers.Count);
            for (int i = 0; i < Stingers.Count; i++)
            {
                if (Stingers[i] != null && !_stingerLookup.ContainsKey(Stingers[i].StingerId))
                    _stingerLookup[Stingers[i].StingerId] = Stingers[i];
            }
        }

        private void OnEnable()
        {
            InvalidateCache();
        }
    }
}
