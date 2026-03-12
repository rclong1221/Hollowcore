using UnityEngine;

namespace DIG.Accessibility.Motor
{
    /// <summary>
    /// EPIC 18.12: Configurable input timing parameters for motor accessibility.
    /// Passive config store — values exposed as static properties for input systems to query.
    /// No active processing; systems read these values at their own update rate.
    /// </summary>
    public static class InputTimingService
    {
        private const string PrefDoubleTap = "Access_DoubleTapWindow";
        private const string PrefHoldThreshold = "Access_HoldThreshold";
        private const string PrefInputBuffer = "Access_InputBufferMs";

        private static float _doubleTapWindow = 0.3f;
        private static float _holdThreshold = 0.4f;
        private static int _inputBufferMs = 100;

        /// <summary>Double-tap detection window in seconds (0.1 to 1.0).</summary>
        public static float DoubleTapWindow => _doubleTapWindow;

        /// <summary>Hold detection threshold in seconds (0.1 to 1.0).</summary>
        public static float HoldThreshold => _holdThreshold;

        /// <summary>Input buffer window in milliseconds (0 to 500).</summary>
        public static int InputBufferMs => _inputBufferMs;

        /// <summary>Input buffer window in seconds.</summary>
        public static float InputBufferSeconds => _inputBufferMs * 0.001f;

        public static void SetDoubleTapWindow(float seconds)
        {
            _doubleTapWindow = Mathf.Clamp(seconds, 0.1f, 1f);
            PlayerPrefs.SetFloat(PrefDoubleTap, _doubleTapWindow);
        }

        public static void SetHoldThreshold(float seconds)
        {
            _holdThreshold = Mathf.Clamp(seconds, 0.1f, 1f);
            PlayerPrefs.SetFloat(PrefHoldThreshold, _holdThreshold);
        }

        public static void SetInputBufferMs(int ms)
        {
            _inputBufferMs = Mathf.Clamp(ms, 0, 500);
            PlayerPrefs.SetInt(PrefInputBuffer, _inputBufferMs);
        }

        /// <summary>Load settings from PlayerPrefs with profile defaults.</summary>
        public static void LoadSettings(float defaultDoubleTap = 0.3f, float defaultHold = 0.4f, int defaultBuffer = 100)
        {
            _doubleTapWindow = PlayerPrefs.GetFloat(PrefDoubleTap, defaultDoubleTap);
            _holdThreshold = PlayerPrefs.GetFloat(PrefHoldThreshold, defaultHold);
            _inputBufferMs = PlayerPrefs.GetInt(PrefInputBuffer, defaultBuffer);
        }
    }
}
