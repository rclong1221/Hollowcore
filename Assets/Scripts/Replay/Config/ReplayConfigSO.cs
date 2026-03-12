using UnityEngine;

namespace DIG.Replay
{
    /// <summary>
    /// EPIC 18.10: Configuration for the replay recording system.
    /// Create via: Create > DIG > Replay > Replay Config
    /// Place in Resources/ as "ReplayConfig" for auto-loading.
    /// </summary>
    [CreateAssetMenu(fileName = "ReplayConfig", menuName = "DIG/Replay/Replay Config")]
    public class ReplayConfigSO : ScriptableObject
    {
        [Header("Recording")]
        [Tooltip("Master toggle for recording.")]
        public bool RecordingEnabled = true;

        [Tooltip("Start recording automatically when gameplay begins.")]
        public bool AutoRecord;

        [Tooltip("Record a snapshot every N server ticks (1 = every tick, 3 = every third).")]
        [Range(1, 10)]
        public int TickInterval = 1;

        [Tooltip("Maximum recording duration in minutes.")]
        [Range(1f, 60f)]
        public float MaxDurationMinutes = 30f;

        [Tooltip("Seconds of uncompressed data to keep in the in-memory ring buffer.")]
        [Range(10f, 120f)]
        public float RingBufferSeconds = 60f;

        [Tooltip("Flush ring buffer to disk every N seconds.")]
        [Range(5f, 30f)]
        public float FlushIntervalSeconds = 10f;

        [Tooltip("Enable delta encoding (only store changed entities between keyframes).")]
        public bool DeltaEncoding = true;

        [Tooltip("Full keyframe every N recorded frames (allows seeking).")]
        [Range(30, 120)]
        public int KeyframeInterval = 60;

        [Header("File Output")]
        [Tooltip("Subdirectory under Application.persistentDataPath for replay files.")]
        public string SaveSubdirectory = "Replays";

        [Header("Kill Cam")]
        [Tooltip("Seconds of gameplay to buffer for kill-cam replay.")]
        public float KillCamBufferSeconds = 5f;

        [Tooltip("Playback speed for kill-cam slow motion.")]
        public float KillCamPlaybackSpeed = 0.25f;
    }
}
