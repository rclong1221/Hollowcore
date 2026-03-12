#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DIG.Performance.Editor
{
    [CustomEditor(typeof(PerformanceCaptureSession))]
    public class PerformanceCaptureSessionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var session = (PerformanceCaptureSession)target;

            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);

            // Only show controls in play mode
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use capture controls.", MessageType.Info);
                return;
            }

            // Progress bar during capture
            if (session.IsCapturing)
            {
                EditorGUI.ProgressBar(
                    EditorGUILayout.GetControlRect(false, 20),
                    session.CaptureProgress,
                    $"Capturing... {session.CaptureProgress * 100:F0}%"
                );
                EditorGUILayout.Space(5);
            }

            // Start/Stop button
            EditorGUILayout.BeginHorizontal();
            {
                GUI.backgroundColor = session.IsCapturing ? Color.red : Color.green;
                if (GUILayout.Button(session.IsCapturing ? "Stop Capture" : "Start Capture", GUILayout.Height(30)))
                {
                    if (session.IsCapturing)
                        session.StopCapture();
                    else
                        session.StartCapture();
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Copy to clipboard button
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(session.LastReport));
            {
                if (GUILayout.Button("Copy Report to Clipboard", GUILayout.Height(25)))
                {
                    session.CopyReportToClipboard();
                }
            }
            EditorGUI.EndDisabledGroup();

            // Show report preview
            if (!string.IsNullOrEmpty(session.LastReport))
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Report Preview", EditorStyles.boldLabel);

                // Show first 500 chars with scroll
                var previewStyle = new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = true,
                    font = Font.CreateDynamicFontFromOSFont("Courier New", 10)
                };

                string preview = session.LastReport;
                if (preview.Length > 2000)
                {
                    preview = preview.Substring(0, 2000) + "\n\n... (truncated, copy to clipboard for full report)";
                }

                EditorGUILayout.TextArea(preview, previewStyle, GUILayout.MaxHeight(300));
            }

            // Repaint during capture for progress updates
            if (session.IsCapturing)
            {
                Repaint();
            }
        }
    }
}
#endif
