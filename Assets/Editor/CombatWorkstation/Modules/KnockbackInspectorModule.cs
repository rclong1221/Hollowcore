using UnityEngine;
using UnityEditor;
using Unity.Entities;
using DIG.Combat.Knockback;

namespace DIG.Combat.Editor
{
    /// <summary>
    /// EPIC 16.9: Knockback inspector module for the AI/Combat Workstation.
    /// Shows live KnockbackState + KnockbackResistance for selected entity.
    /// </summary>
    public static class KnockbackInspectorModule
    {
        public static void DrawInspector(EntityManager entityManager, Entity entity)
        {
            if (entity == Entity.Null || !entityManager.Exists(entity)) return;

            if (entityManager.HasComponent<KnockbackState>(entity))
            {
                var state = entityManager.GetComponentData<KnockbackState>(entity);
                EditorGUILayout.LabelField("KnockbackState", EditorStyles.boldLabel);

                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.LabelField("Active", state.IsActive.ToString());
                    if (state.IsActive)
                    {
                        EditorGUILayout.LabelField("Type", state.Type.ToString());
                        EditorGUILayout.LabelField("Velocity", $"({state.Velocity.x:F1}, {state.Velocity.y:F1}, {state.Velocity.z:F1})");
                        EditorGUILayout.LabelField("Speed", $"{state.InitialSpeed:F1} m/s");

                        float progress = state.Progress;
                        var rect = EditorGUILayout.GetControlRect(GUILayout.Height(18));
                        EditorGUI.ProgressBar(rect, progress, $"{state.Elapsed:F2}s / {state.Duration:F2}s");

                        EditorGUILayout.LabelField("Easing", state.Easing.ToString());
                    }
                }

                EditorGUILayout.Space(4);
            }

            if (entityManager.HasComponent<KnockbackResistance>(entity))
            {
                var resistance = entityManager.GetComponentData<KnockbackResistance>(entity);
                EditorGUILayout.LabelField("KnockbackResistance", EditorStyles.boldLabel);

                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.LabelField("Resistance", $"{resistance.ResistancePercent * 100f:F0}%");
                    EditorGUILayout.LabelField("SuperArmor", $"{resistance.SuperArmorThreshold:F0}N");
                    EditorGUILayout.LabelField("Immunity",
                        $"{resistance.ImmunityTimeRemaining:F2}s / {resistance.ImmunityDuration:F2}s");
                    EditorGUILayout.LabelField("IsImmune", resistance.IsImmune.ToString());
                }
            }
        }
    }
}
