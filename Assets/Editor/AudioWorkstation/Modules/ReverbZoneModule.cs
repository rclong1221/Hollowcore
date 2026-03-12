using UnityEngine;
using UnityEditor;
using Audio.Zones;

namespace DIG.Editor.AudioWorkstation.Modules
{
    /// <summary>
    /// Audio Workstation module: reverb zone management and preview.
    /// Lists all zones in scene, shows active zone stack, preset comparison,
    /// and overlap warnings.
    /// EPIC 15.27 Phase 4.
    /// </summary>
    public class ReverbZoneModule : IAudioModule
    {
        private Vector2 _scrollPos;

        private static readonly string[] PresetNames =
        {
            "OpenField", "Forest", "SmallRoom", "LargeHall", "Tunnel",
            "Cave", "Underwater", "Ship Interior", "Ship Exterior"
        };

        // Reference reverb parameters for display
        private static readonly string[,] PresetParams =
        {
            // Decay, WetMix, HFDamp, Notes
            { "0.3s", "0.05", "0.8", "Dry, minimal reflection" },
            { "0.6s", "0.10", "0.6", "Soft diffuse scatter" },
            { "1.2s", "0.25", "0.4", "Tight, metallic" },
            { "2.5s", "0.35", "0.3", "Spacious, warm" },
            { "3.0s", "0.45", "0.5", "Long tail, hard walls" },
            { "4.0s", "0.50", "0.2", "Very long, dark resonance" },
            { "1.5s", "0.30", "0.9", "Heavy HF damp, murky" },
            { "1.0s", "0.20", "0.5", "Metallic, contained" },
            { "0.1s", "0.02", "0.9", "Near-silent (vacuum nearby)" },
        };

        public void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("Reverb Zones", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Runtime state
            if (Application.isPlaying)
            {
                DrawRuntimeState();
                EditorGUILayout.Space(4);
            }

            // Scene zones
            DrawSceneZones();
            EditorGUILayout.Space(4);

            // Preset reference table
            DrawPresetTable();

            EditorGUILayout.EndScrollView();

            if (Application.isPlaying)
                EditorUtility.SetDirty(EditorWindow.focusedWindow);
        }

        private void DrawRuntimeState()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Active Zone State", EditorStyles.miniLabel);

            var mgr = AudioReverbZoneManager.Instance;
            if (mgr == null)
            {
                EditorGUILayout.LabelField("AudioReverbZoneManager not found in scene.");
                EditorGUILayout.EndVertical();
                return;
            }

            var current = mgr.CurrentZone;
            EditorGUILayout.LabelField($"Current Zone: {(current != null ? current.ZoneName : "None (OpenField fallback)")}");
            if (current != null)
            {
                EditorGUILayout.LabelField($"  Preset: {current.Preset}");
                EditorGUILayout.LabelField($"  Priority: {current.Priority}");
                EditorGUILayout.LabelField($"  Interior: {current.IsInterior}");
            }

            EditorGUILayout.LabelField($"Indoor Factor: {mgr.IndoorFactor:F2}");
            EditorGUILayout.LabelField($"Transition Progress: {mgr.TransitionProgress:F2}");

            // Zone stack
            var stack = mgr.GetZoneStack();
            if (stack.Count > 0)
            {
                EditorGUILayout.LabelField($"Zone Stack ({stack.Count}):", EditorStyles.miniLabel);
                for (int i = stack.Count - 1; i >= 0; i--)
                {
                    string label = stack[i] != null ? $"  [{i}] {stack[i].ZoneName} (pri={stack[i].Priority})" : $"  [{i}] null";
                    EditorGUILayout.LabelField(label);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSceneZones()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Scene Reverb Zones", EditorStyles.miniLabel);

            var zones = Object.FindObjectsByType<ReverbZoneAuthoring>(FindObjectsSortMode.None);
            if (zones.Length == 0)
            {
                EditorGUILayout.LabelField("No ReverbZoneAuthoring components found in scene.");
                EditorGUILayout.EndVertical();
                return;
            }

            // Check for overlap warnings
            bool hasOverlap = false;
            for (int i = 0; i < zones.Length; i++)
            {
                for (int j = i + 1; j < zones.Length; j++)
                {
                    if (zones[i].Priority == zones[j].Priority)
                    {
                        var b1 = zones[i].GetComponent<Collider>()?.bounds ?? default;
                        var b2 = zones[j].GetComponent<Collider>()?.bounds ?? default;
                        if (b1.Intersects(b2))
                        {
                            hasOverlap = true;
                            break;
                        }
                    }
                }
                if (hasOverlap) break;
            }

            if (hasOverlap)
            {
                EditorGUILayout.HelpBox("Warning: Overlapping zones with same priority detected. Set different priorities to control which zone wins.", MessageType.Warning);
            }

            foreach (var zone in zones)
            {
                EditorGUILayout.BeginHorizontal();
                var icon = zone.IsInterior ? "d_BuildSettings.Standalone.Small" : "d_Terrain Icon";
                EditorGUILayout.LabelField(new GUIContent($"{zone.ZoneName}", EditorGUIUtility.IconContent(icon).image), GUILayout.Width(200));
                EditorGUILayout.LabelField($"{zone.Preset}", GUILayout.Width(100));
                EditorGUILayout.LabelField($"pri={zone.Priority}", GUILayout.Width(60));
                EditorGUILayout.LabelField($"{zone.TransitionDuration:F1}s", GUILayout.Width(40));
                if (GUILayout.Button("Select", GUILayout.Width(50)))
                {
                    Selection.activeGameObject = zone.gameObject;
                    EditorGUIUtility.PingObject(zone.gameObject);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPresetTable()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Preset Reference", EditorStyles.miniLabel);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Preset", EditorStyles.miniLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("Decay", EditorStyles.miniLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Wet Mix", EditorStyles.miniLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("HF Damp", EditorStyles.miniLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Notes", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < PresetNames.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(PresetNames[i], GUILayout.Width(100));
                EditorGUILayout.LabelField(PresetParams[i, 0], GUILayout.Width(50));
                EditorGUILayout.LabelField(PresetParams[i, 1], GUILayout.Width(50));
                EditorGUILayout.LabelField(PresetParams[i, 2], GUILayout.Width(50));
                EditorGUILayout.LabelField(PresetParams[i, 3]);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }
    }
}
