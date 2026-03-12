using UnityEngine;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Global configuration for the dialogue system.
    /// Loaded from Resources/DialogueConfig by DialogueBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueConfig", menuName = "DIG/Dialogue/Dialogue Config", order = 3)]
    public class DialogueConfigSO : ScriptableObject
    {
        [Header("Session")]
        [Tooltip("Maximum session duration in ticks (default 1800 = 60s at 30Hz).")]
        public uint MaxSessionDurationTicks = 1800;

        [Tooltip("Maximum dialogue flags per NPC entity.")]
        [Range(4, 32)] public byte MaxFlagsPerNpc = 16;

        [Tooltip("Honor Duration field on Speech nodes for auto-advance.")]
        public bool AutoAdvanceEnabled = true;

        [Header("Typewriter (EPIC 18.5)")]
        [Tooltip("Default typewriter speed in characters per second. 0 = instant.")]
        [Min(0f)] public float TypewriterCharsPerSecond = 40f;

        [Tooltip("Pause duration in seconds after a period.")]
        [Min(0f)] public float PausePeriod = 0.3f;

        [Tooltip("Pause duration in seconds after a comma.")]
        [Min(0f)] public float PauseComma = 0.15f;

        [Tooltip("Pause duration in seconds after ! or ?.")]
        [Min(0f)] public float PauseExclamation = 0.25f;

        [Header("History (EPIC 18.5)")]
        [Tooltip("Maximum dialogue lines stored in history.")]
        [Range(10, 200)] public int HistoryCapacity = 50;

        [Header("Barks")]
        [Tooltip("Proximity range for bark triggers in meters.")]
        [Min(1f)] public float BarkProximityRange = 8f;

        [Tooltip("Seconds between bark proximity checks per NPC.")]
        [Min(0.5f)] public float BarkCheckInterval = 2f;

        [Tooltip("Frame-slot spread for bark checks (prevents thundering herd).")]
        [Range(1, 30)] public int BarkCheckFrameSpread = 10;
    }
}
