using UnityEngine;
using UnityEditor;
using Audio.Systems;

namespace DIG.Editor.AudioWorkstation.Modules
{
    /// <summary>
    /// Audio Workstation module: real-time telemetry display.
    /// Shows event rates, pool usage, playback failures, voice counts, and per-bus metrics.
    /// EPIC 15.27 Phase 8.
    /// </summary>
    public class TelemetryModule : IAudioModule
    {
        private Vector2 _scrollPos;

        public void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("Audio Telemetry", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see real-time telemetry data.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            // Event counts
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Event Counts", EditorStyles.miniLabel);
            DrawMetricRow("Footstep Events", AudioTelemetry.FootstepEventsThisSession.ToString());
            DrawMetricRow("Footstep Rate", $"{AudioTelemetry.CurrentFootstepRate:F1}/s");
            DrawMetricRow("Landing Events", AudioTelemetry.LandingEventsThisSession.ToString());
            DrawMetricRow("Action Events", AudioTelemetry.ActionEventsThisSession.ToString());
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Voice management
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Voice Management", EditorStyles.miniLabel);
            DrawMetricRow("Active Voices", AudioTelemetry.ActiveVoiceCount.ToString());
            DrawMetricRow("Culled Voices", AudioTelemetry.CulledVoiceCount.ToString());
            DrawMetricRow("Priority Evictions", AudioTelemetry.PriorityEvictionsThisSession.ToString());
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Pool usage
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Pool Usage", EditorStyles.miniLabel);
            var pool = AudioSourcePool.Instance;
            if (pool != null)
            {
                DrawMetricRow("Pool Active", $"{pool.ActiveCount} / {pool.PoolSize}");
                DrawMetricRow("Pool Peak", pool.PeakCount.ToString());
                DrawMetricRow("Evictions/Frame", pool.EvictionsThisFrame.ToString());

                Rect usageBar = GUILayoutUtility.GetRect(0, 16, GUILayout.ExpandWidth(true));
                float usage = (float)pool.ActiveCount / pool.PoolSize;
                Color barColor = usage < 0.5f ? Color.green : usage < 0.8f ? Color.yellow : Color.red;
                EditorGUI.DrawRect(new Rect(usageBar.x, usageBar.y, usageBar.width * usage, usageBar.height), barColor);
                EditorGUI.DrawRect(new Rect(usageBar.x + usageBar.width * usage, usageBar.y,
                    usageBar.width * (1f - usage), usageBar.height), new Color(0.15f, 0.15f, 0.15f));
            }
            else
            {
                EditorGUILayout.LabelField("AudioSourcePool not available");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Error tracking
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Errors & Warnings", EditorStyles.miniLabel);
            var prevColor = GUI.color;
            GUI.color = AudioTelemetry.PlaybackFailuresThisSession > 0 ? Color.red : Color.white;
            DrawMetricRow("Playback Failures", AudioTelemetry.PlaybackFailuresThisSession.ToString());
            GUI.color = AudioTelemetry.CacheMissesThisSession > 0 ? Color.yellow : Color.white;
            DrawMetricRow("Cache Misses", AudioTelemetry.CacheMissesThisSession.ToString());
            GUI.color = Color.white;
            DrawMetricRow("Throttled Events", AudioTelemetry.ThrottledEventsThisSession.ToString());
            GUI.color = prevColor;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // Reset button
            if (GUILayout.Button("Reset Counters"))
            {
                AudioTelemetry.ResetCounters();
            }

            EditorGUILayout.EndScrollView();

            EditorUtility.SetDirty(EditorWindow.focusedWindow);
        }

        private void DrawMetricRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(160));
            EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }
    }
}
