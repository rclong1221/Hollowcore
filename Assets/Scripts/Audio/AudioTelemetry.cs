using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Audio.Systems
{
    /// <summary>
    /// Telemetry and debug logging for the audio/VFX surface system.
    /// Tracks playback rates, cache performance, and errors.
    /// Enable DEBUG_LOG_AUDIO for verbose logging.
    /// </summary>
    public static class AudioTelemetry
    {
        // Runtime counters
        public static int FootstepEventsThisSession { get; private set; }
        public static int LandingEventsThisSession { get; private set; }
        public static int ActionEventsThisSession { get; private set; }
        public static int CacheMissesThisSession { get; private set; }
        public static int PlaybackFailuresThisSession { get; private set; }
        public static int ThrottledEventsThisSession { get; private set; }

        // EPIC 15.27 Phase 5: Voice management metrics
        public static int ActiveVoiceCount { get; set; }
        public static int CulledVoiceCount { get; set; }
        public static int PriorityEvictionsThisSession { get; set; }

        // EPIC 17.5: Music system telemetry
        public static int TrackTransitionsThisSession { get; set; }
        public static int StingersPlayedThisSession { get; set; }
        public static float CurrentCombatIntensity { get; set; }
        public static int CurrentTrackId { get; set; }
        public static int ActiveStemCount { get; set; }

        // Rate tracking
        private static float _lastFootstepTime;
        private static int _footstepsThisSecond;
        public static float CurrentFootstepRate { get; private set; }

        /// <summary>
        /// Log a footstep event.
        /// </summary>
        public static void LogFootstep(int materialId, Vector3 position)
        {
            FootstepEventsThisSession++;
            UpdateFootstepRate();
            LogVerbose($"[AudioTelemetry] Footstep: mat={materialId} pos={position}");
        }

        /// <summary>
        /// Log a landing event.
        /// </summary>
        public static void LogLanding(int materialId, float intensity, Vector3 position)
        {
            LandingEventsThisSession++;
            LogVerbose($"[AudioTelemetry] Landing: mat={materialId} intensity={intensity:F2} pos={position}");
        }

        /// <summary>
        /// Log an action audio event (jump, roll, dive, climb, slide).
        /// </summary>
        public static void LogActionEvent(string actionType, int materialId, Vector3 position)
        {
            ActionEventsThisSession++;
            LogVerbose($"[AudioTelemetry] {actionType}: mat={materialId} pos={position}");
        }

        /// <summary>
        /// Log a cache miss during material lookup.
        /// </summary>
        public static void LogCacheMiss(string lookupType, string key)
        {
            CacheMissesThisSession++;
            LogVerboseWarning($"[AudioTelemetry] Cache miss: {lookupType}='{key}'");
        }

        /// <summary>
        /// Log a playback failure.
        /// </summary>
        public static void LogPlaybackFailure(string reason)
        {
            PlaybackFailuresThisSession++;
            LogVerboseError($"[AudioTelemetry] Playback failed: {reason}");
        }

        /// <summary>
        /// Log a throttled event.
        /// </summary>
        public static void LogThrottled()
        {
            ThrottledEventsThisSession++;
            LogVerbose("[AudioTelemetry] Event throttled");
        }

        /// <summary>
        /// Reset all counters.
        /// </summary>
        public static void ResetCounters()
        {
            FootstepEventsThisSession = 0;
            LandingEventsThisSession = 0;
            ActionEventsThisSession = 0;
            CacheMissesThisSession = 0;
            PlaybackFailuresThisSession = 0;
            ThrottledEventsThisSession = 0;
            ActiveVoiceCount = 0;
            CulledVoiceCount = 0;
            PriorityEvictionsThisSession = 0;
            TrackTransitionsThisSession = 0;
            StingersPlayedThisSession = 0;
            CurrentCombatIntensity = 0;
            CurrentTrackId = 0;
            ActiveStemCount = 0;
            _footstepsThisSecond = 0;
            CurrentFootstepRate = 0;
        }

        /// <summary>
        /// Get a summary string of current telemetry.
        /// </summary>
        public static string GetSummary()
        {
            return $"Audio Telemetry:\n" +
                   $"  Footsteps: {FootstepEventsThisSession} (rate: {CurrentFootstepRate:F1}/s)\n" +
                   $"  Landings: {LandingEventsThisSession}\n" +
                   $"  Actions: {ActionEventsThisSession}\n" +
                   $"  Cache Misses: {CacheMissesThisSession}\n" +
                   $"  Failures: {PlaybackFailuresThisSession}\n" +
                   $"  Throttled: {ThrottledEventsThisSession}\n" +
                   $"  Active Voices: {ActiveVoiceCount}\n" +
                   $"  Culled Voices: {CulledVoiceCount}\n" +
                   $"  Priority Evictions: {PriorityEvictionsThisSession}\n" +
                   $"  Track Transitions: {TrackTransitionsThisSession}\n" +
                   $"  Stingers Played: {StingersPlayedThisSession}\n" +
                   $"  Combat Intensity: {CurrentCombatIntensity:F2}\n" +
                   $"  Current Track: {CurrentTrackId}\n" +
                   $"  Active Stems: {ActiveStemCount}";
        }

        private static void UpdateFootstepRate()
        {
            float now = Time.time;
            if (now - _lastFootstepTime >= 1f)
            {
                CurrentFootstepRate = _footstepsThisSecond;
                _footstepsThisSecond = 0;
                _lastFootstepTime = now;
            }
            _footstepsThisSecond++;
        }

        // Conditional logging methods - compiled out when DEBUG_LOG_AUDIO is not defined
        [Conditional("DEBUG_LOG_AUDIO")]
        private static void LogVerbose(string message) => Debug.Log(message);

        [Conditional("DEBUG_LOG_AUDIO")]
        private static void LogVerboseWarning(string message) => Debug.LogWarning(message);

        [Conditional("DEBUG_LOG_AUDIO")]
        private static void LogVerboseError(string message) => Debug.LogError(message);
    }
}
