using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using DIG.Combat.Components;
using Player.Components;

namespace DIG.Progression.Editor.Modules
{
    /// <summary>
    /// EPIC 16.14: Play-mode player inspector showing live Level, XP,
    /// stat points, base vs allocated vs equipped stats, rested XP pool.
    /// </summary>
    public class PlayerInspectorModule : IProgressionWorkstationModule
    {
        private Vector2 _scroll;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Player Progression Inspector", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to see live progression data.", MessageType.Info);
                return;
            }

            var world = ProgressionWorkstationWindow.GetProgressionWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No ECS World available.", MessageType.Warning);
                return;
            }

            var em = world.EntityManager;

            // Find player with PlayerProgression
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerProgression>(),
                ComponentType.ReadOnly<CharacterAttributes>());

            if (query.CalculateEntityCount() == 0)
            {
                EditorGUILayout.HelpBox("No entities with PlayerProgression found.", MessageType.Info);
                return;
            }

            var entities = query.ToEntityArray(Allocator.Temp);
            var progressions = query.ToComponentDataArray<PlayerProgression>(Allocator.Temp);
            var attributes = query.ToComponentDataArray<CharacterAttributes>(Allocator.Temp);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int i = 0; i < entities.Length; i++)
            {
                var prog = progressions[i];
                var attrs = attributes[i];

                EditorGUILayout.BeginVertical("box");

                // Header
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
                EditorGUILayout.LabelField($"Player Entity #{entities[i].Index}", EditorStyles.boldLabel);
                GUI.backgroundColor = prevBg;

                // Level and XP
                EditorGUILayout.LabelField($"Level: {attrs.Level}");

                int xpToNext = GetXPToNext(em, attrs.Level);
                float percent = xpToNext > 0 ? (float)prog.CurrentXP / xpToNext : 1f;
                var barRect = GUILayoutUtility.GetRect(200, 18, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(barRect, percent, $"XP: {prog.CurrentXP} / {xpToNext}");
                EditorGUILayout.LabelField($"Total XP Earned: {prog.TotalXPEarned}");
                EditorGUILayout.LabelField($"Unspent Stat Points: {prog.UnspentStatPoints}");

                if (prog.RestedXP > 0)
                    EditorGUILayout.LabelField($"Rested XP: {prog.RestedXP:F0} (bonus active)");

                // Attributes
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Attributes", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"  STR: {attrs.Strength}  DEX: {attrs.Dexterity}  INT: {attrs.Intelligence}  VIT: {attrs.Vitality}");

                // Combat Stats
                if (em.HasComponent<AttackStats>(entities[i]))
                {
                    var attack = em.GetComponentData<AttackStats>(entities[i]);
                    EditorGUILayout.LabelField("Attack Stats", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"  ATK: {attack.AttackPower:F1}  SPL: {attack.SpellPower:F1}  Crit: {attack.CritChance:P1}");
                }

                if (em.HasComponent<DefenseStats>(entities[i]))
                {
                    var defense = em.GetComponentData<DefenseStats>(entities[i]);
                    EditorGUILayout.LabelField("Defense Stats", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"  DEF: {defense.Defense:F1}  Armor: {defense.Armor:F1}");
                }

                if (em.HasComponent<Health>(entities[i]))
                {
                    var health = em.GetComponentData<Health>(entities[i]);
                    EditorGUILayout.LabelField("Health", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"  {health.Current:F0} / {health.Max:F0} ({health.Normalized:P0})");
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.EndScrollView();

            entities.Dispose();
            progressions.Dispose();
            attributes.Dispose();
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private static int GetXPToNext(EntityManager em, int level)
        {
            var configQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ProgressionConfigSingleton>());
            if (configQuery.CalculateEntityCount() == 0) return 100;
            var config = configQuery.GetSingleton<ProgressionConfigSingleton>();
            ref var curve = ref config.Curve.Value;
            int index = level - 1;
            if (index < 0 || index >= curve.XPPerLevel.Length) return 0;
            return curve.XPPerLevel[index];
        }
    }
}
