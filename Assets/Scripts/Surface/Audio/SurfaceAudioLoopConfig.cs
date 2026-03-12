using UnityEngine;
using DIG.Surface;

namespace DIG.Surface.Audio
{
    /// <summary>
    /// EPIC 15.24 Phase 8: Configuration for continuous surface audio loops.
    /// Maps SurfaceID to a looping audio clip, speed threshold, and volume curve.
    /// Create via: Assets > Create > DIG/Surface/Audio Loop Config
    /// </summary>
    [CreateAssetMenu(fileName = "SurfaceAudioLoopConfig", menuName = "DIG/Surface/Audio Loop Config")]
    public class SurfaceAudioLoopConfig : ScriptableObject
    {
        [System.Serializable]
        public struct SurfaceLoopEntry
        {
            [Tooltip("Surface type this loop applies to.")]
            public SurfaceID Surface;

            [Tooltip("Looping audio clip (e.g. ice crackle, gravel crunch, water wading).")]
            public AudioClip LoopClip;

            [Tooltip("Minimum player speed to start the loop.")]
            [Range(0.1f, 10f)]
            public float SpeedThreshold;

            [Tooltip("Volume at maximum speed (lerps from 0 to this value).")]
            [Range(0f, 1f)]
            public float MaxVolume;

            [Tooltip("Speed at which volume reaches MaxVolume.")]
            [Range(1f, 30f)]
            public float MaxSpeedForVolume;
        }

        [Header("Loop Definitions")]
        public SurfaceLoopEntry[] Entries;

        [Header("Crossfade")]
        [Tooltip("Duration of crossfade when switching surfaces.")]
        [Range(0.1f, 1f)]
        public float CrossfadeDuration = 0.3f;

        [Tooltip("Fade out duration when player stops.")]
        [Range(0.1f, 1f)]
        public float FadeOutDuration = 0.3f;

        public bool TryGetEntry(SurfaceID surface, out SurfaceLoopEntry entry)
        {
            if (Entries != null)
            {
                for (int i = 0; i < Entries.Length; i++)
                {
                    if (Entries[i].Surface == surface)
                    {
                        entry = Entries[i];
                        return true;
                    }
                }
            }
            entry = default;
            return false;
        }
    }
}
