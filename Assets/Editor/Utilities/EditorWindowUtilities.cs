#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace DIG.Editor.Utilities
{
    /// <summary>
    /// Shared utilities for DIG editor windows/workbenches.
    /// Provides consistent styling and scroll handling.
    /// </summary>
    public static class EditorWindowUtilities
    {
        /// <summary>
        /// Begins a scrollable content area that fills the window.
        /// Always call EndScrollArea() when done.
        /// </summary>
        public static Vector2 BeginScrollArea(Vector2 scrollPos, bool withPadding = true)
        {
            if (withPadding)
            {
                GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });
            }
            return EditorGUILayout.BeginScrollView(scrollPos);
        }
        
        /// <summary>
        /// Ends the scrollable content area.
        /// </summary>
        public static void EndScrollArea(bool withPadding = true)
        {
            EditorGUILayout.EndScrollView();
            if (withPadding)
            {
                GUILayout.EndVertical();
            }
        }
        
        /// <summary>
        /// Draws a section header with consistent styling.
        /// </summary>
        public static void DrawSectionHeader(string title)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }
        
        /// <summary>
        /// Draws a horizontal separator line.
        /// </summary>
        public static void DrawSeparator()
        {
            EditorGUILayout.Space(5);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f));
            EditorGUILayout.Space(5);
        }
        
        /// <summary>
        /// Draws a foldout section with consistent styling.
        /// </summary>
        public static bool DrawFoldoutSection(bool isExpanded, string title, System.Action drawContent)
        {
            EditorGUILayout.Space(5);
            isExpanded = EditorGUILayout.Foldout(isExpanded, title, true, EditorStyles.foldoutHeader);
            
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                drawContent?.Invoke();
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
            
            return isExpanded;
        }
        
        /// <summary>
        /// Draws a button row with consistent styling.
        /// </summary>
        public static void DrawButtonRow(params (string label, System.Action onClick, bool enabled)[] buttons)
        {
            EditorGUILayout.BeginHorizontal();
            foreach (var (label, onClick, enabled) in buttons)
            {
                GUI.enabled = enabled;
                if (GUILayout.Button(label))
                    onClick?.Invoke();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }
        
        /// <summary>
        /// Draws a status indicator (checkmark or X).
        /// </summary>
        public static void DrawStatusRow(string label, bool success, string successText = null, string failText = null)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(success ? "✅" : "❌", GUILayout.Width(20));
            GUILayout.Label(label);
            
            if (!string.IsNullOrEmpty(success ? successText : failText))
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(success ? successText : failText, EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        /// <summary>
        /// Draws a count badge in colored text.
        /// </summary>
        public static void DrawCountBadge(string label, int count, int goodThreshold = 0, Color? goodColor = null, Color? badColor = null)
        {
            var style = new GUIStyle(EditorStyles.label);
            if (count >= goodThreshold)
                style.normal.textColor = goodColor ?? new Color(0.4f, 0.8f, 0.4f);
            else
                style.normal.textColor = badColor ?? Color.gray;
            
            EditorGUILayout.LabelField($"📊 {count} {label}", style);
        }
    }
}
#endif
