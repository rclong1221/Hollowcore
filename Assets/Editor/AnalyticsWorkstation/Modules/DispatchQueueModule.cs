using UnityEditor;
using UnityEngine;

namespace DIG.Analytics.Editor.Modules
{
    /// <summary>
    /// Dispatch queue status: pending, dispatched, dropped counts.
    /// Manual force-flush button. Target health indicators.
    /// </summary>
    public class DispatchQueueModule : IAnalyticsWorkstationModule
    {
        public void OnGUI()
        {
            EditorGUILayout.LabelField("Dispatch Queue", EditorStyles.boldLabel);

            if (!Application.isPlaying || !AnalyticsAPI.IsInitialized)
            {
                EditorGUILayout.HelpBox("Enter Play Mode with analytics initialized.", MessageType.Info);
                return;
            }

            var dispatcher = AnalyticsAPI.Dispatcher;
            if (dispatcher == null)
            {
                EditorGUILayout.HelpBox("Dispatcher not available.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Queue Status", EditorStyles.boldLabel);

            int depth = dispatcher.QueueDepth;
            long enqueued = dispatcher.EventsEnqueued;
            long dispatched = dispatcher.EventsDispatched;
            long dropped = dispatcher.EventsDropped;

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            DrawStatusIndicator(depth < 1000 ? Color.green : depth < 5000 ? Color.yellow : Color.red);
            EditorGUILayout.LabelField($"Queue Depth: {depth}");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"Events Enqueued: {enqueued}");
            EditorGUILayout.LabelField($"Events Dispatched: {dispatched}");

            var prevColor = GUI.contentColor;
            if (dropped > 0) GUI.contentColor = Color.red;
            EditorGUILayout.LabelField($"Events Dropped: {dropped}");
            GUI.contentColor = prevColor;

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // Queue depth bar
            EditorGUILayout.LabelField("Queue Utilization");
            Rect barRect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f));

            float fill = Mathf.Clamp01(depth / 10000f);
            Color barColor = fill < 0.5f ? Color.green : fill < 0.8f ? Color.yellow : Color.red;
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width * fill, barRect.height), barColor);

            EditorGUILayout.Space(8);

            if (GUILayout.Button("Force Flush", GUILayout.Height(30)))
            {
                dispatcher.SignalFlush();
                Debug.Log("[Analytics] Manual flush triggered.");
            }
        }

        private static void DrawStatusIndicator(Color color)
        {
            Rect rect = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12));
            rect.y += 3;
            EditorGUI.DrawRect(rect, color);
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
