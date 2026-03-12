using UnityEngine;
using UnityEditor;
using DIG.AI.Components;

namespace DIG.Editor.AIWorkstation
{
    /// <summary>
    /// Shared GUIStyles, colors, and helper drawing methods for AI Workstation modules.
    /// </summary>
    public static class AIWorkstationStyles
    {
        // State colors
        public static readonly Color IdleColor = new Color(0.5f, 0.5f, 0.5f);
        public static readonly Color PatrolColor = new Color(0.3f, 0.6f, 0.9f);
        public static readonly Color InvestigateColor = new Color(0.9f, 0.7f, 0.2f);
        public static readonly Color CombatColor = new Color(0.9f, 0.2f, 0.2f);
        public static readonly Color FleeColor = new Color(0.9f, 0.5f, 0.9f);
        public static readonly Color ReturnHomeColor = new Color(0.3f, 0.9f, 0.3f);

        // Cast phase colors
        public static readonly Color TelegraphColor = new Color(1f, 0.8f, 0.2f);
        public static readonly Color CastingColor = new Color(1f, 0.5f, 0f);
        public static readonly Color ActiveColor = new Color(1f, 0.2f, 0.2f);
        public static readonly Color RecoveryColor = new Color(0.5f, 0.5f, 1f);

        // Threat colors
        public static readonly Color ThreatLeaderColor = new Color(1f, 0.2f, 0.2f);
        public static readonly Color ThreatVisibleColor = new Color(1f, 0.8f, 0.2f);
        public static readonly Color ThreatHiddenColor = new Color(0.5f, 0.5f, 0.5f);

        public static Color GetStateColor(AIBehaviorState state)
        {
            switch (state)
            {
                case AIBehaviorState.Idle: return IdleColor;
                case AIBehaviorState.Patrol: return PatrolColor;
                case AIBehaviorState.Investigate: return InvestigateColor;
                case AIBehaviorState.Combat: return CombatColor;
                case AIBehaviorState.Flee: return FleeColor;
                case AIBehaviorState.ReturnHome: return ReturnHomeColor;
                default: return Color.white;
            }
        }

        public static Color GetPhaseColor(AbilityCastPhase phase)
        {
            switch (phase)
            {
                case AbilityCastPhase.Telegraph: return TelegraphColor;
                case AbilityCastPhase.Casting: return CastingColor;
                case AbilityCastPhase.Active: return ActiveColor;
                case AbilityCastPhase.Recovery: return RecoveryColor;
                default: return Color.grey;
            }
        }

        public static void DrawStatBox(string label, string value, Color color)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, EditorStyles.centeredGreyMiniLabel);
            var prevColor = GUI.color;
            GUI.color = color;
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14
            };
            EditorGUILayout.LabelField(value, style);
            GUI.color = prevColor;
            EditorGUILayout.EndVertical();
        }

        public static void DrawProgressBar(Rect rect, float value, float max, Color fillColor, string label = null)
        {
            float ratio = max > 0f ? Mathf.Clamp01(value / max) : 0f;
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width * ratio, rect.height), fillColor);
            if (label != null)
            {
                var style = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
                GUI.Label(rect, label, style);
            }
        }

        public static void DrawColoredLabel(string text, Color color)
        {
            var prevColor = GUI.color;
            GUI.color = color;
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
            GUI.color = prevColor;
        }

        public static void DrawSectionHeader(string title)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }
    }
}
