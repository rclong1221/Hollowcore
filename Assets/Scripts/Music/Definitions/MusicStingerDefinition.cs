using UnityEngine;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Serializable definition for a one-shot musical stinger.
    /// Stored in MusicDatabaseSO.Stingers list.
    /// </summary>
    [System.Serializable]
    public class MusicStingerDefinition
    {
        /// <summary>Unique identifier referenced by MusicStingerRequest.StingerId.</summary>
        public int StingerId;

        /// <summary>Display name (e.g., "Level Up Fanfare").</summary>
        public string StingerName;

        /// <summary>One-shot audio clip.</summary>
        public AudioClip Clip;

        /// <summary>How much to duck music during stinger (default -6dB).</summary>
        public float DuckMusicDB = -6f;

        /// <summary>Duration of music duck. 0 = clip length.</summary>
        public float DuckDuration;

        /// <summary>Default priority (higher = more important).</summary>
        public byte DefaultPriority = 50;

        /// <summary>Category for editor tooling and filtering.</summary>
        public StingerCategory Category;
    }
}
