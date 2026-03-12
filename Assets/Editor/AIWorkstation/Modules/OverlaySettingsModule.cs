using UnityEngine;
using UnityEditor;
using Unity.Entities;
using DIG.AI.Overlay;

namespace DIG.Editor.AIWorkstation.Modules
{
    /// <summary>
    /// Overlay Settings: Configure the runtime world-space debug labels
    /// rendered above AI entities during play mode.
    /// </summary>
    public class OverlaySettingsModule : IAIWorkstationModule
    {
        public void OnEntityChanged(Entity entity, EntityManager entityManager) { }
        public void OnSceneGUI(SceneView sceneView) { }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Overlay Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure world-space debug labels above AI entities. Labels are visible in the Scene view and Game view during Play mode.",
                MessageType.Info);
            EditorGUILayout.Space(6);

            // Master toggle
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = AIDebugOverlayConfig.Enabled ? Color.green : Color.grey;
            if (GUILayout.Button(AIDebugOverlayConfig.Enabled ? "OVERLAY: ON" : "OVERLAY: OFF", GUILayout.Height(28)))
            {
                AIDebugOverlayConfig.Enabled = !AIDebugOverlayConfig.Enabled;
            }
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // Display options
            AIWorkstationStyles.DrawSectionHeader("Display Options");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            AIDebugOverlayConfig.ShowState = EditorGUILayout.Toggle("State (Idle/Combat/...)", AIDebugOverlayConfig.ShowState);
            AIDebugOverlayConfig.ShowSubState = EditorGUILayout.Toggle("Sub-State (Approach/Attack/...)", AIDebugOverlayConfig.ShowSubState);
            AIDebugOverlayConfig.ShowThreatValue = EditorGUILayout.Toggle("Threat Value", AIDebugOverlayConfig.ShowThreatValue);
            AIDebugOverlayConfig.ShowTargetName = EditorGUILayout.Toggle("Target Entity", AIDebugOverlayConfig.ShowTargetName);
            AIDebugOverlayConfig.ShowActiveAbility = EditorGUILayout.Toggle("Active Ability / Phase", AIDebugOverlayConfig.ShowActiveAbility);
            AIDebugOverlayConfig.ShowHealthPercent = EditorGUILayout.Toggle("Health %", AIDebugOverlayConfig.ShowHealthPercent);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // Filters
            AIWorkstationStyles.DrawSectionHeader("Filters");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            AIDebugOverlayConfig.OnlyCombat = EditorGUILayout.Toggle("Only Combat State", AIDebugOverlayConfig.OnlyCombat);
            AIDebugOverlayConfig.OnlyAggroed = EditorGUILayout.Toggle("Only Aggroed", AIDebugOverlayConfig.OnlyAggroed);
            AIDebugOverlayConfig.MaxCameraDistance = EditorGUILayout.Slider("Max Camera Distance", AIDebugOverlayConfig.MaxCameraDistance, 10f, 200f);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // Visual
            AIWorkstationStyles.DrawSectionHeader("Visual");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            AIDebugOverlayConfig.FontSize = EditorGUILayout.IntSlider("Font Size", AIDebugOverlayConfig.FontSize, 8, 20);
            AIDebugOverlayConfig.BackgroundAlpha = EditorGUILayout.Slider("Background Alpha", AIDebugOverlayConfig.BackgroundAlpha, 0f, 1f);
            EditorGUILayout.EndVertical();

            if (!Application.isPlaying)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox("Overlay labels will appear in Play mode when enabled.", MessageType.Info);
            }
        }
    }
}
