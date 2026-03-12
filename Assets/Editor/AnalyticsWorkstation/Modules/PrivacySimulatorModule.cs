using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Analytics.Editor.Modules
{
    /// <summary>
    /// Toggle consent levels in editor. Preview which events pass/fail the privacy filter.
    /// Show PII scrubbing before/after comparison.
    /// </summary>
    public class PrivacySimulatorModule : IAnalyticsWorkstationModule
    {
        private bool _analyticsConsent;
        private bool _crashConsent;
        private bool _personalDataConsent;
        private Vector2 _scroll;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Privacy Simulator", EditorStyles.boldLabel);

            if (!Application.isPlaying || !AnalyticsAPI.IsInitialized)
            {
                EditorGUILayout.HelpBox("Enter Play Mode with analytics initialized.", MessageType.Info);
                return;
            }

            var filter = AnalyticsAPI.Privacy;
            if (filter == null)
            {
                EditorGUILayout.HelpBox("Privacy filter not available.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Current Consent Level", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Active: {filter.CurrentConsent}");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Simulate Consent", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _analyticsConsent = EditorGUILayout.Toggle("Analytics", _analyticsConsent);
            _crashConsent = EditorGUILayout.Toggle("Crash Reports", _crashConsent);
            _personalDataConsent = EditorGUILayout.Toggle("Personal Data", _personalDataConsent);

            if (EditorGUI.EndChangeCheck())
            {
                AnalyticsAPI.SetPrivacyConsent(_analyticsConsent, _crashConsent, _personalDataConsent);
            }

            EditorGUILayout.Space(8);

            if (GUILayout.Button("Reset Stored Consent"))
            {
                PlayerPrefs.DeleteKey("analytics_consent");
                PlayerPrefs.DeleteKey("crash_consent");
                PlayerPrefs.DeleteKey("personal_consent");
                PlayerPrefs.Save();
                _analyticsConsent = false;
                _crashConsent = false;
                _personalDataConsent = false;
                AnalyticsAPI.SetPrivacyConsent(false, false, false);
                Debug.Log("[Analytics] Stored consent cleared.");
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Scrub Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Events are scrubbed before dispatch:\n" +
                "- No Analytics consent: all non-Essential events blocked\n" +
                "- No Personal Data consent: PlayerId hashed (SHA256 truncated)\n" +
                "- Essential events (Session) always pass if configured",
                MessageType.Info);

            // Show last few events with scrub preview
            var recent = AnalyticsAPI.RecentEvents;
            int previewCount = Mathf.Min(5, recent.Count);

            if (previewCount > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Recent Events (Before / After Scrub)");
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(200));

                for (int i = recent.Count - previewCount; i < recent.Count; i++)
                {
                    var evt = recent[i];
                    var scrubbed = filter.ScrubBatch(new[] { evt });

                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField($"Before: [{evt.Category}] {evt.Action} pid={evt.PlayerId}", EditorStyles.miniLabel);

                    if (scrubbed.Length > 0)
                    {
                        GUI.contentColor = Color.green;
                        EditorGUILayout.LabelField($"After:  [{scrubbed[0].Category}] {scrubbed[0].Action} pid={scrubbed[0].PlayerId}", EditorStyles.miniLabel);
                    }
                    else
                    {
                        GUI.contentColor = Color.red;
                        EditorGUILayout.LabelField("After:  BLOCKED", EditorStyles.miniLabel);
                    }
                    GUI.contentColor = Color.white;
                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.EndScrollView();
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
