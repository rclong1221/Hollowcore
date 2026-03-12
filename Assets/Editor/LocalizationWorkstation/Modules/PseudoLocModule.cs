using UnityEditor;
using UnityEngine;

namespace DIG.Localization.Editor.Modules
{
    public class PseudoLocModule : ILocalizationWorkstationModule
    {
        private LocalizationDatabase _db;
        private string _previewInput = "Kill the Wolves";
        private string _previewOutput = "";

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Pseudo-Localization", EditorStyles.boldLabel);

            _db = LocalizationWorkstationWindow.LoadDatabase();
            if (_db == null)
            {
                EditorGUILayout.HelpBox("No LocalizationDatabase found.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(8);

            bool wasEnabled = _db.EnablePseudoLocalization;
            bool newEnabled = EditorGUILayout.Toggle("Enable Pseudo-Localization", _db.EnablePseudoLocalization);
            if (newEnabled != wasEnabled)
            {
                _db.EnablePseudoLocalization = newEnabled;
                EditorUtility.SetDirty(_db);
            }

            EditorGUILayout.Space(4);

            if (_db.EnablePseudoLocalization)
            {
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.6f, 0.2f);
                EditorGUILayout.BeginVertical("box");
                GUI.backgroundColor = prevBg;
                EditorGUILayout.LabelField("PSEUDO-LOC IS ACTIVE", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "All LocalizationManager.Get() calls will return pseudo-localized strings.\n" +
                    "This helps identify truncation, concatenation, and hardcoded string bugs.",
                    EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            _previewInput = EditorGUILayout.TextField("Input", _previewInput);

            if (GUILayout.Button("Generate Preview"))
                _previewOutput = LocalizationManager.GeneratePseudoLocalized(_previewInput);

            if (!string.IsNullOrEmpty(_previewOutput))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Output:", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(_previewOutput, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(40));
                EditorGUILayout.LabelField($"Original length: {_previewInput.Length}  |  Pseudo length: {_previewOutput.Length}  |  Ratio: {(_previewOutput.Length / (float)_previewInput.Length):F1}x", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("What Pseudo-Localization Reveals", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("• Truncated text (UI elements too narrow for doubled-length text)", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("• Concatenated strings (broken by word order changes in real locales)", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("• Hardcoded strings (text not going through LocalizationManager.Get())", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("• Font rendering issues (accented characters not in font atlas)", EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
