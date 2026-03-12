using UnityEngine;
using UnityEngine.Timeline;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Designer-authored cinematic definition.
    /// Maps CinematicId to TimelineAsset + playback configuration.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Cinematic/Cinematic Definition")]
    public class CinematicDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable identifier referenced by triggers. Must be unique.")]
        public int CinematicId;
        public string Name = "";
        [TextArea(2, 4)]
        public string Description = "";

        [Header("Playback")]
        [Tooltip("Unity Timeline asset to play. Null for TextOverlay type.")]
        public TimelineAsset TimelineAsset;
        public CinematicType CinematicType = CinematicType.FullCinematic;
        [Tooltip("Total duration in seconds (fallback if Timeline is null).")]
        public float Duration = 10f;

        [Header("Skip")]
        public SkipPolicy SkipPolicy = SkipPolicy.AnyoneCanSkip;

        [Header("Dialogue Integration")]
        [Tooltip("Optional dialogue tree to trigger (0 = none, EPIC 16.16).")]
        public int DialogueTreeId;

        [Header("Audio")]
        [Tooltip("Optional music stinger to play (0 = none).")]
        public int MusicStingerId;
        [Tooltip("Optional voice line played on AudioBusType.Dialogue.")]
        public AudioClip VoiceLineClip;

        [Header("Camera")]
        [Tooltip("Camera rig prefab for FullCinematic (null = use Timeline binding).")]
        public GameObject CinematicCameraPrefab;

        [Header("Subtitles")]
        [Tooltip("Localization keys for subtitle text (TextOverlay type).")]
        public string[] SubtitleKeys = System.Array.Empty<string>();
        [Tooltip("Timestamp per subtitle line (parallel to SubtitleKeys).")]
        public float[] SubtitleTimings = System.Array.Empty<float>();
    }
}
