using UnityEngine;
using UnityEditor;
using Audio.Systems;
using Audio.Config;

namespace DIG.Editor.AudioWorkstation.Modules
{
    /// <summary>
    /// Audio Workstation module: real-time per-bus monitoring.
    /// Shows VU meters, active source counts, sidechain duck indicators, and solo/mute toggles.
    /// EPIC 15.27 Phase 1.
    /// </summary>
    public class BusMonitorModule : IAudioModule
    {
        private bool[] _busMuted = new bool[6];
        private bool[] _busSoloed = new bool[6];
        private Vector2 _scrollPos;

        private static readonly string[] BusNames = { "Combat", "Ambient", "Music", "Dialogue", "UI", "Footstep" };
        private static readonly Color[] BusColors =
        {
            new Color(0.9f, 0.3f, 0.3f), // Combat - red
            new Color(0.3f, 0.8f, 0.4f), // Ambient - green
            new Color(0.4f, 0.4f, 0.9f), // Music - blue
            new Color(0.9f, 0.8f, 0.3f), // Dialogue - yellow
            new Color(0.7f, 0.7f, 0.7f), // UI - gray
            new Color(0.6f, 0.5f, 0.3f), // Footstep - brown
        };

        public void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("Bus Monitor", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            var pool = Application.isPlaying ? AudioSourcePool.Instance : null;

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see real-time bus activity.", MessageType.Info);
                EditorGUILayout.Space(4);
            }

            // Pool overview
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Pool Status", EditorStyles.miniLabel);
            if (pool != null)
            {
                EditorGUILayout.LabelField($"Active Sources: {pool.ActiveCount} / {pool.PoolSize}");
                EditorGUILayout.LabelField($"Peak: {pool.PeakCount}");
                EditorGUILayout.LabelField($"Evictions This Frame: {pool.EvictionsThisFrame}");

                // Pool usage bar
                float usage = (float)pool.ActiveCount / pool.PoolSize;
                Rect barRect = GUILayoutUtility.GetRect(0, 16, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(barRect, usage, $"{pool.ActiveCount}/{pool.PoolSize}");
            }
            else
            {
                EditorGUILayout.LabelField("Pool: Not Available");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // Per-bus display
            for (int i = 0; i < 6; i++)
            {
                DrawBusRow((AudioBusType)i, i, pool);
            }

            EditorGUILayout.Space(8);

            // Sidechain indicators
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Sidechain Ducking", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("  Combat → ducks Ambient (-6dB, 0.3s attack, 1s release)");
            EditorGUILayout.LabelField("  Dialogue → ducks Music (-9dB, 0.2s attack, 1.5s release)");

            if (pool != null)
            {
                bool combatActive = pool.GetActiveCountForBus(AudioBusType.Combat) > 0;
                bool dialogueActive = pool.GetActiveCountForBus(AudioBusType.Dialogue) > 0;

                var prevColor = GUI.color;
                GUI.color = combatActive ? new Color(1f, 0.6f, 0.3f) : Color.gray;
                EditorGUILayout.LabelField(combatActive ? "  ● Combat ducking Ambient ACTIVE" : "  ○ Combat→Ambient idle");
                GUI.color = dialogueActive ? new Color(1f, 0.6f, 0.3f) : Color.gray;
                EditorGUILayout.LabelField(dialogueActive ? "  ● Dialogue ducking Music ACTIVE" : "  ○ Dialogue→Music idle");
                GUI.color = prevColor;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // Telemetry summary
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Audio Telemetry", EditorStyles.miniLabel);
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField($"Footstep Rate: {AudioTelemetry.CurrentFootstepRate:F1}/s");
                EditorGUILayout.LabelField($"Playback Failures: {AudioTelemetry.PlaybackFailuresThisSession}");
                EditorGUILayout.LabelField($"Throttled Events: {AudioTelemetry.ThrottledEventsThisSession}");
            }
            else
            {
                EditorGUILayout.LabelField("Enter Play Mode for telemetry data.");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();

            if (Application.isPlaying)
                EditorUtility.SetDirty(EditorWindow.focusedWindow);
        }

        private void DrawBusRow(AudioBusType bus, int index, AudioSourcePool pool)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            // Color indicator
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = BusColors[index];
            GUILayout.Box("", GUILayout.Width(12), GUILayout.Height(16));
            GUI.backgroundColor = prevBg;

            // Bus name + active count
            int activeCount = pool != null ? pool.GetActiveCountForBus(bus) : 0;
            EditorGUILayout.LabelField($"{BusNames[index]}", EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField($"Active: {activeCount}", GUILayout.Width(70));

            // VU meter placeholder
            if (pool != null && activeCount > 0)
            {
                Rect vuRect = GUILayoutUtility.GetRect(0, 14, GUILayout.ExpandWidth(true));
                float level = Mathf.Clamp01(activeCount / 10f); // approximate
                EditorGUI.DrawRect(new Rect(vuRect.x, vuRect.y, vuRect.width * level, vuRect.height), BusColors[index]);
                EditorGUI.DrawRect(new Rect(vuRect.x + vuRect.width * level, vuRect.y, vuRect.width * (1f - level), vuRect.height),
                    new Color(0.15f, 0.15f, 0.15f));
            }
            else
            {
                Rect vuRect = GUILayoutUtility.GetRect(0, 14, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(vuRect, new Color(0.15f, 0.15f, 0.15f));
            }

            // Solo/Mute toggles
            var prevColor = GUI.color;
            GUI.color = _busSoloed[index] ? Color.yellow : Color.white;
            if (GUILayout.Button("S", GUILayout.Width(22)))
                _busSoloed[index] = !_busSoloed[index];
            GUI.color = _busMuted[index] ? Color.red : Color.white;
            if (GUILayout.Button("M", GUILayout.Width(22)))
                _busMuted[index] = !_busMuted[index];
            GUI.color = prevColor;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
    }
}
