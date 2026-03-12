using UnityEngine;
using Unity.Mathematics;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: ScriptableObject defining a multi-stem music track.
    /// Each track has up to 4 stems that layer based on combat intensity.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Music/Music Track")]
    public class MusicTrackSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier (must match MusicZone.TrackId references).")]
        public int TrackId;

        [Tooltip("Display name for editor tooling.")]
        public string TrackName;

        public MusicTrackCategory Category;

        [Header("Tempo")]
        [Tooltip("Beats per minute (for beat-synced transitions).")]
        public float BPM = 120f;

        [Header("Stems")]
        [Tooltip("Always-playing foundation layer.")]
        public AudioClip BaseStem;

        [Tooltip("Rhythmic layer, activated at low combat intensity.")]
        public AudioClip PercussionStem;

        [Tooltip("Melodic layer, activated at medium combat intensity.")]
        public AudioClip MelodyStem;

        [Tooltip("Full combat layer, activated at high combat intensity.")]
        public AudioClip IntensityStem;

        [Header("Loop Points")]
        [Tooltip("Sample offset for loop start (0 = beginning).")]
        public int LoopStartSample;

        [Tooltip("Sample offset for loop end (0 = clip length).")]
        public int LoopEndSample;

        [Tooltip("Optional non-looping intro (plays once before loop).")]
        public AudioClip IntroClip;

        [Header("Volume")]
        [Range(0f, 1f)]
        [Tooltip("Master volume for this track (default 1.0).")]
        public float BaseVolume = 1f;

        [Header("Combat Intensity Thresholds")]
        [Tooltip("x=Percussion threshold, y=Melody threshold, z=Intensity threshold.")]
        public float3 CombatIntensityThresholds = new float3(0.2f, 0.5f, 0.8f);

        [Header("Stem Fading")]
        [Tooltip("Per-stem fade-in duration override (default 0.5s).")]
        public float StemFadeInTime = 0.5f;

        [Tooltip("Per-stem fade-out duration override (default 1.0s).")]
        public float StemFadeOutTime = 1.0f;
    }
}
