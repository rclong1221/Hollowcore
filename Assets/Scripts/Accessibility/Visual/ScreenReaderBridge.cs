using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace DIG.Accessibility.Visual
{
    /// <summary>
    /// EPIC 18.12: Platform TTS bridge for screen reader accessibility.
    /// macOS: 'say' command. Windows: PowerShell SAPI5. Async via Process.Start.
    /// No-op on unsupported platforms (Linux, consoles).
    /// </summary>
    public static class ScreenReaderBridge
    {
        private static bool _enabled;
        private static float _rate = 1f;
        private static float _volume = 0.8f;
        private static Process _currentProcess;
        private static readonly Queue<SpeechRequest> _queue = new();

        public static bool IsEnabled => _enabled;

        public static void SetEnabled(bool enabled) => _enabled = enabled;
        public static void SetRate(float rate) => _rate = Mathf.Clamp(rate, 0.5f, 2f);
        public static void SetVolume(float volume) => _volume = Mathf.Clamp01(volume);

        /// <summary>
        /// Speak text via platform TTS. High priority interrupts current speech.
        /// </summary>
        public static void Speak(string text, SpeechPriority priority)
        {
            if (!_enabled || string.IsNullOrEmpty(text)) return;

            if (priority == SpeechPriority.High)
            {
                InterruptCurrent();
                SpeakImmediate(text);
            }
            else
            {
                _queue.Enqueue(new SpeechRequest { Text = text, Priority = priority });
                TryProcessQueue();
            }
        }

        /// <summary>Process queued speech if no active speech.</summary>
        public static void Tick()
        {
            if (!_enabled) return;
            if (_currentProcess != null && !_currentProcess.HasExited) return;
            _currentProcess = null;
            TryProcessQueue();
        }

        private static void TryProcessQueue()
        {
            if (_currentProcess != null && !_currentProcess.HasExited) return;
            if (_queue.Count == 0) return;

            var request = _queue.Dequeue();
            SpeakImmediate(request.Text);
        }

        private static void InterruptCurrent()
        {
            if (_currentProcess != null && !_currentProcess.HasExited)
            {
                try { _currentProcess.Kill(); } catch { /* Process already exited */ }
            }
            _currentProcess = null;
        }

        // Reusable StringBuilder to avoid per-call allocation
        private static readonly StringBuilder _sanitizeBuffer = new(256);

        private static void SpeakImmediate(string text)
        {
            // Single-pass sanitization to prevent command injection
            _sanitizeBuffer.Clear();
            foreach (var c in text)
            {
                switch (c)
                {
                    case '"': case '\'': case '`': case '$':
                    case ';': case '&': case '|':
                        break; // strip
                    case '\n': case '\r':
                        _sanitizeBuffer.Append(' ');
                        break;
                    default:
                        _sanitizeBuffer.Append(c);
                        break;
                }
            }

            if (_sanitizeBuffer.Length == 0) return;
            text = _sanitizeBuffer.ToString();

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            SpeakMacOS(text);
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            SpeakWindows(text);
#endif
        }

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        private static void SpeakMacOS(string text)
        {
            // 'say' rate: ~175 WPM default, scale proportionally
            int rateWPM = Mathf.RoundToInt(175 * _rate);
            int volumeInt = Mathf.RoundToInt(_volume * 100);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/usr/bin/say",
                    Arguments = $"-r {rateWPM} \"{text}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                _currentProcess = Process.Start(psi);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[ScreenReader] macOS TTS failed: {e.Message}");
            }
        }
#endif

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private static void SpeakWindows(string text)
        {
            int rateScaled = Mathf.RoundToInt((_rate - 1f) * 10f); // SAPI rate: -10 to 10
            int volumeInt = Mathf.RoundToInt(_volume * 100);

            string script = $"Add-Type -AssemblyName System.Speech; " +
                            $"$s = New-Object System.Speech.Synthesis.SpeechSynthesizer; " +
                            $"$s.Rate = {rateScaled}; $s.Volume = {volumeInt}; " +
                            $"$s.Speak('{text}')";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                _currentProcess = Process.Start(psi);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[ScreenReader] Windows TTS failed: {e.Message}");
            }
        }
#endif

        private struct SpeechRequest
        {
            public string Text;
            public SpeechPriority Priority;
        }
    }

    public enum SpeechPriority : byte
    {
        Normal = 0,
        High = 1
    }
}
