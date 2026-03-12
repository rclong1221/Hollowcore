using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Localization.Editor.Modules
{
    public class FontPreviewModule : ILocalizationWorkstationModule
    {
        private LocalizationDatabase _db;
        private int _selectedLocaleIdx;
        private string _sampleText = "The quick brown fox jumps over the lazy dog. 0123456789";
        private string _sampleCJK = "天気は晴れです。気温は25度です。";
        private string _sampleRTL = "مرحبا بالعالم";

        private static readonly string[] FontStyleNames =
        {
            "Body", "Header", "Tooltip", "Combat", "Button", "Mono"
        };

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Font Preview", EditorStyles.boldLabel);

            _db = LocalizationWorkstationWindow.LoadDatabase();
            if (_db == null)
            {
                EditorGUILayout.HelpBox("No LocalizationDatabase found.", MessageType.Info);
                return;
            }

            if (_db.Locales.Count == 0)
            {
                EditorGUILayout.HelpBox("No locales defined.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);

            var localeNames = new List<string>();
            foreach (var l in _db.Locales)
                localeNames.Add(l != null ? $"{l.LocaleCode} - {l.DisplayName}" : "(null)");

            _selectedLocaleIdx = EditorGUILayout.Popup("Locale", _selectedLocaleIdx, localeNames.ToArray());
            if (_selectedLocaleIdx >= _db.Locales.Count)
                _selectedLocaleIdx = 0;

            var locale = _db.Locales[_selectedLocaleIdx];
            if (locale == null) return;

            EditorGUILayout.Space(4);
            _sampleText = EditorGUILayout.TextField("Sample Text", _sampleText);

            EditorGUILayout.Space(8);

            // Locale info
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Locale Info", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Code: {locale.LocaleCode}");
            EditorGUILayout.LabelField($"Direction: {locale.TextDirection}");
            EditorGUILayout.LabelField($"Plural Rules: {locale.PluralRuleSet}");
            EditorGUILayout.LabelField($"Line Spacing: {locale.LineSpacingMultiplier:F2}x");
            EditorGUILayout.LabelField($"Char Spacing: {locale.CharacterSpacingMultiplier:F2}x");
            EditorGUILayout.LabelField($"Default Font: {(locale.DefaultFont != null ? locale.DefaultFont.name : "(none)")}");
            EditorGUILayout.LabelField($"Complete: {(locale.IsComplete ? "Yes" : "No")}");
            EditorGUILayout.EndVertical();

            // Font mapping
            FontMappingSO fontMap = null;
            foreach (var fm in _db.FontMappings)
            {
                if (fm != null && fm.LocaleCode == locale.LocaleCode)
                {
                    fontMap = fm;
                    break;
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Font Mapping", EditorStyles.boldLabel);

            if (fontMap == null)
            {
                EditorGUILayout.HelpBox(
                    $"No FontMappingSO found for {locale.LocaleCode}. Default font will be used for all styles.",
                    MessageType.Info);
            }

            for (int i = 0; i < FontStyleNames.Length; i++)
            {
                var style = (FontStyle)i;
                var font = fontMap != null
                    ? fontMap.GetFont(style, locale.DefaultFont)
                    : locale.DefaultFont;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"{FontStyleNames[i]}: {(font != null ? font.name : "(no font)")}", EditorStyles.boldLabel);

                if (font != null)
                {
                    var previewRect = GUILayoutUtility.GetRect(400, 30, GUILayout.ExpandWidth(true));
                    EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.15f, 0.15f));
                    var labelStyle = new GUIStyle(EditorStyles.label)
                    {
                        fontSize = style == FontStyle.Header ? 18 :
                                   style == FontStyle.Combat ? 20 :
                                   style == FontStyle.Mono ? 11 : 14,
                        normal = { textColor = Color.white },
                        wordWrap = true,
                        alignment = locale.TextDirection == TextDirection.RTL
                            ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft
                    };
                    EditorGUI.LabelField(previewRect, _sampleText, labelStyle);
                }

                EditorGUILayout.EndVertical();
            }

            // CJK / RTL sample
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Special Character Samples", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"CJK: {_sampleCJK}");
            EditorGUILayout.LabelField($"RTL: {_sampleRTL}");
            EditorGUILayout.LabelField($"Accented: àèìòù çñšÿž ÀÈÌÒÙ");
            EditorGUILayout.EndVertical();
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
