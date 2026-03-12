#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DIG.Roguelite.Editor.Modules
{
    /// <summary>
    /// EPIC 23.6: Modifier Designer module.
    /// Create/edit RunModifierDefinitionSO assets with polarity color-coding,
    /// stacking rules, and ascension tier builder with cumulative difficulty graph.
    /// </summary>
    public class ModifierModule : IRunWorkstationModule
    {
        public string TabName => "Modifiers";

        private RunModifierPoolSO _modifierPool;
        private AscensionDefinitionSO _ascensionDef;
        private UnityEditor.Editor _poolEditor;
        private UnityEditor.Editor _ascensionEditor;
        private Vector2 _scrollPos;
        private bool _showModifiers = true;
        private bool _showAscension = true;
        private bool _showDifficultyGraph;

        // Cached GUIStyles — avoid allocation per repaint
        private static GUIStyle _positiveStyle;
        private static GUIStyle _negativeStyle;
        private static GUIStyle _neutralStyle;

        public void OnEnable() { }
        public void OnDisable()
        {
            if (_poolEditor != null) Object.DestroyImmediate(_poolEditor);
            if (_ascensionEditor != null) Object.DestroyImmediate(_ascensionEditor);
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Modifier Designer", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _modifierPool = (RunModifierPoolSO)EditorGUILayout.ObjectField(
                "Modifier Pool", _modifierPool, typeof(RunModifierPoolSO), false);

            _ascensionDef = (AscensionDefinitionSO)EditorGUILayout.ObjectField(
                "Ascension Definition", _ascensionDef, typeof(AscensionDefinitionSO), false);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Modifier pool editor
            if (_modifierPool != null)
            {
                _showModifiers = EditorGUILayout.Foldout(_showModifiers, "Modifier Pool", true);
                if (_showModifiers)
                {
                    DrawModifierPool();
                }
            }

            EditorGUILayout.Space(8);

            // Ascension editor
            if (_ascensionDef != null)
            {
                _showAscension = EditorGUILayout.Foldout(_showAscension, "Ascension Tiers", true);
                if (_showAscension)
                {
                    DrawAscensionTiers();
                }

                EditorGUILayout.Space(8);

                _showDifficultyGraph = EditorGUILayout.Foldout(_showDifficultyGraph, "Cumulative Difficulty Graph", true);
                if (_showDifficultyGraph)
                {
                    DrawDifficultyGraph();
                }
            }

            if (_modifierPool == null && _ascensionDef == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a RunModifierPoolSO or AscensionDefinitionSO to begin editing.\n" +
                    "Create via Assets > Create > DIG > Roguelite.",
                    MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawModifierPool()
        {
            EditorGUI.indentLevel++;

            if (_poolEditor == null || _poolEditor.target != _modifierPool)
            {
                if (_poolEditor != null)
                    Object.DestroyImmediate(_poolEditor);
                _poolEditor = UnityEditor.Editor.CreateEditor(_modifierPool);
            }

            // Polarity summary
            var so = new SerializedObject(_modifierPool);
            var modifiers = so.FindProperty("Modifiers");
            if (modifiers != null && modifiers.isArray)
            {
                int positive = 0, negative = 0, neutral = 0;
                for (int i = 0; i < modifiers.arraySize; i++)
                {
                    var mod = modifiers.GetArrayElementAtIndex(i);
                    var polarity = mod.FindPropertyRelative("Polarity");
                    if (polarity != null)
                    {
                        switch (polarity.enumValueIndex)
                        {
                            case 0: positive++; break;
                            case 1: negative++; break;
                            default: neutral++; break;
                        }
                    }
                }

                EnsureStyles();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Positive: {positive}", _positiveStyle, GUILayout.Width(100));
                EditorGUILayout.LabelField($"Negative: {negative}", _negativeStyle, GUILayout.Width(100));
                EditorGUILayout.LabelField($"Neutral: {neutral}", _neutralStyle, GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();
            }

            _poolEditor.OnInspectorGUI();

            EditorGUI.indentLevel--;
        }

        private void DrawAscensionTiers()
        {
            EditorGUI.indentLevel++;

            if (_ascensionEditor == null || _ascensionEditor.target != _ascensionDef)
            {
                if (_ascensionEditor != null)
                    Object.DestroyImmediate(_ascensionEditor);
                _ascensionEditor = UnityEditor.Editor.CreateEditor(_ascensionDef);
            }

            _ascensionEditor.OnInspectorGUI();

            EditorGUI.indentLevel--;
        }

        private void DrawDifficultyGraph()
        {
            EditorGUI.indentLevel++;

            var so = new SerializedObject(_ascensionDef);
            var tiers = so.FindProperty("Tiers");
            if (tiers == null || !tiers.isArray || tiers.arraySize == 0)
            {
                EditorGUILayout.LabelField("No tiers defined.", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                return;
            }

            // Simple text-based cumulative graph
            float cumulativeMultiplier = 1f;
            for (int i = 0; i < tiers.arraySize; i++)
            {
                var tier = tiers.GetArrayElementAtIndex(i);
                var multiplier = tier.FindPropertyRelative("RewardMultiplier");
                float m = multiplier != null ? multiplier.floatValue : 1f;
                cumulativeMultiplier *= m;

                int barWidth = Mathf.Clamp((int)(cumulativeMultiplier * 20f), 1, 60);
                string bar = new string('|', barWidth);
                Color barColor = cumulativeMultiplier > 3f ? Color.red :
                                 cumulativeMultiplier > 2f ? Color.yellow : Color.green;
                GUI.color = barColor;
                EditorGUILayout.LabelField($"  Tier {i}: {bar} x{cumulativeMultiplier:F2}");
                GUI.color = Color.white;
            }

            EditorGUI.indentLevel--;
        }

        private static void EnsureStyles()
        {
            if (_positiveStyle != null) return;
            _positiveStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.3f, 0.8f, 0.3f) } };
            _negativeStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.8f, 0.3f, 0.3f) } };
            _neutralStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.7f, 0.7f, 0.3f) } };
        }
    }
}
#endif
