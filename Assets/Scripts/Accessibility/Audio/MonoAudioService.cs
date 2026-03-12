using Audio.Systems;
using UnityEngine;
using UnityEngine.Audio;

namespace DIG.Accessibility.Audio
{
    /// <summary>
    /// EPIC 18.12: Mono audio downmix service for single-ear hearing accessibility.
    /// Sets AudioMixer parameter to collapse stereo to mono.
    /// Uses the same AudioManager.MasterMixer pattern as AudioSettingsPage.
    /// Caches mixer reference; retries once per scene load if initial lookup fails.
    /// </summary>
    public static class MonoAudioService
    {
        private static bool _enabled;
        private static AudioMixer _cachedMixer;
        private static bool _lookupAttempted;

        // Exposed mixer parameter for stereo width (0 = mono, 1 = full stereo)
        private const string StereoWidthParam = "StereoWidth";

        public static bool IsEnabled => _enabled;

        /// <summary>Enable/disable mono audio downmix.</summary>
        public static void SetEnabled(bool enabled)
        {
            if (_enabled == enabled) return;
            _enabled = enabled;

            var mixer = GetMixer();
            if (mixer == null) return;

            // Set stereo width: 0 = mono, 1 = full stereo
            mixer.SetFloat(StereoWidthParam, enabled ? 0f : 1f);
        }

        /// <summary>Reset lookup state (call on scene load to allow re-discovery).</summary>
        public static void InvalidateCache()
        {
            _cachedMixer = null;
            _lookupAttempted = false;
        }

        private static AudioMixer GetMixer()
        {
            if (_cachedMixer != null) return _cachedMixer;
            if (_lookupAttempted) return null;

            _lookupAttempted = true;
            var audioManager = Object.FindAnyObjectByType<AudioManager>();
            if (audioManager != null)
                _cachedMixer = audioManager.MasterMixer;

            return _cachedMixer;
        }
    }
}
