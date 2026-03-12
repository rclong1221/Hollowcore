using UnityEditor;
using UnityEngine;

namespace DIG.SceneManagement.Editor.Modules
{
    /// <summary>
    /// EPIC 18.6: Preview loading screen profiles in the editor.
    /// Shows background sprite, sample tip, progress bar style, timing parameters.
    /// </summary>
    public class LoadingScreenPreviewModule : ISceneModule
    {
        private LoadingScreenProfileSO _profile;
        private int _bgIndex;
        private int _tipIndex;
        private float _previewProgress = 0.35f;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Loading Screen Preview", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _profile = (LoadingScreenProfileSO)EditorGUILayout.ObjectField(
                "Profile", _profile, typeof(LoadingScreenProfileSO), false);

            if (_profile == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a LoadingScreenProfileSO to preview.\n" +
                    "Create one via: Create > DIG > Scene Management > Loading Screen Profile",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space(8);

            // Background preview
            EditorGUILayout.LabelField("Background", EditorStyles.boldLabel);
            if (_profile.BackgroundSprites != null && _profile.BackgroundSprites.Length > 0)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("<", GUILayout.Width(30)))
                    _bgIndex = (_bgIndex - 1 + _profile.BackgroundSprites.Length) % _profile.BackgroundSprites.Length;
                EditorGUILayout.LabelField($"{_bgIndex + 1}/{_profile.BackgroundSprites.Length}",
                    EditorStyles.centeredGreyMiniLabel);
                if (GUILayout.Button(">", GUILayout.Width(30)))
                    _bgIndex = (_bgIndex + 1) % _profile.BackgroundSprites.Length;
                EditorGUILayout.EndHorizontal();

                var sprite = _profile.BackgroundSprites[_bgIndex];
                if (sprite != null && sprite.texture != null)
                {
                    var texRect = GUILayoutUtility.GetRect(200, 120, GUILayout.ExpandWidth(true));
                    GUI.DrawTexture(texRect, sprite.texture, ScaleMode.ScaleToFit);
                }
            }
            else
            {
                EditorGUILayout.LabelField("  (no backgrounds assigned)");
            }

            EditorGUILayout.Space(8);

            // Tip preview
            EditorGUILayout.LabelField("Tips", EditorStyles.boldLabel);
            if (_profile.Tips != null && _profile.Tips.Length > 0)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Randomize", GUILayout.Width(80)))
                    _tipIndex = Random.Range(0, _profile.Tips.Length);
                EditorGUILayout.LabelField($"[{_tipIndex + 1}/{_profile.Tips.Length}]",
                    GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox(_profile.Tips[_tipIndex], MessageType.None);
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.LabelField("  (no tips assigned)");
            }

            EditorGUILayout.Space(8);

            // Progress bar preview
            EditorGUILayout.LabelField("Progress Bar", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Style", _profile.ProgressBarStyle.ToString());
            EditorGUILayout.LabelField("Show", _profile.ShowProgressBar ? "Yes" : "No");

            if (_profile.ShowProgressBar && _profile.ProgressBarStyle != ProgressBarStyle.Indeterminate)
            {
                _previewProgress = EditorGUILayout.Slider("Preview Progress", _previewProgress, 0f, 1f);
                var barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                    GUILayout.Height(20), GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f));

                float fillWidth = _profile.ProgressBarStyle == ProgressBarStyle.Stepped
                    ? Mathf.Floor(_previewProgress * 10f) / 10f * barRect.width
                    : _previewProgress * barRect.width;

                EditorGUI.DrawRect(
                    new Rect(barRect.x, barRect.y, fillWidth, barRect.height),
                    new Color(0.2f, 0.7f, 0.3f));
            }

            EditorGUILayout.Space(8);

            // Timing parameters
            EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Min Display", $"{_profile.MinDisplaySeconds:F1}s");
            EditorGUILayout.LabelField("Fade In", $"{_profile.FadeInDuration:F2}s");
            EditorGUILayout.LabelField("Fade Out", $"{_profile.FadeOutDuration:F2}s");
            EditorGUILayout.LabelField("Music", _profile.MusicClip != null ? _profile.MusicClip.name : "(none)");
            EditorGUI.indentLevel--;
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
