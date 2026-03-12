using UnityEngine;
using UnityEditor;
using DIG.Surface.Debug;

namespace DIG.Surface.Editor
{
    /// <summary>
    /// EPIC 15.24 Phase 12: Editor window for real-time surface FX stats.
    /// Window > DIG > Surface FX Debug
    /// </summary>
    public class SurfaceFXDebugWindow : EditorWindow
    {
        private bool _autoRepaint = true;
        private double _nextRepaintTime;
        private const double RepaintInterval = 0.25; // 4 Hz instead of every frame

        [MenuItem("Window/DIG/Surface FX Debug")]
        public static void ShowWindow()
        {
            GetWindow<SurfaceFXDebugWindow>("Surface FX Debug");
        }

        private void OnInspectorUpdate()
        {
            if (_autoRepaint && EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= _nextRepaintTime)
            {
                _nextRepaintTime = EditorApplication.timeSinceStartup + RepaintInterval;
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Surface FX Pipeline", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            _autoRepaint = EditorGUILayout.Toggle("Auto Repaint (Play Mode)", _autoRepaint);
            EditorGUILayout.Space(4);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see live stats.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Frame Stats", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                DrawStat("Queue Depth", SurfaceFXProfiler.QueueDepthAtFrameStart);
                DrawStat("Events Processed", SurfaceFXProfiler.EventsProcessedThisFrame);
                DrawStat("Events Culled", SurfaceFXProfiler.EventsCulledThisFrame);
                DrawStat("VFX Spawned", SurfaceFXProfiler.VFXSpawnedThisFrame);
                DrawStat("Decals Spawned", SurfaceFXProfiler.DecalsSpawnedThisFrame);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Ricochet / Penetration", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                DrawStat("Ricochets", SurfaceFXProfiler.RicochetsThisFrame);
                DrawStat("Penetrations", SurfaceFXProfiler.PenetrationsThisFrame);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Queue", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                DrawStat("Current Queue Count", SurfaceImpactQueue.Count);
            }

            EditorGUILayout.Space(8);

            if (GUILayout.Button("Clear Impact Queue"))
            {
                SurfaceImpactQueue.Clear();
            }
        }

        private void DrawStat(string label, int value)
        {
            EditorGUILayout.LabelField(label, value.ToString());
        }
    }
}
