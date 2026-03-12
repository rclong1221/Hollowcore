using Unity.Collections;
using Unity.Entities;
using DIG.SkillTree;
using UnityEditor;
using UnityEngine;

namespace DIG.Editor.SkillTreeWorkstation.Modules
{
    /// <summary>
    /// EPIC 17.1: Play-mode live talent state viewer.
    /// Shows TalentState, allocations, passive stats, and tree progress for all players.
    /// </summary>
    public class PlayerInspectorModule : ISkillTreeWorkstationModule
    {
        private Vector2 _scroll;

        public void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Player Talent Inspector", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect player talent data.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            var world = SkillTreeWorkstationWindow.GetSkillTreeWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No valid ECS World found.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            var em = world.EntityManager;

            // Query for entities with TalentLink
            EntityQuery query;
            try
            {
                query = em.CreateEntityQuery(ComponentType.ReadOnly<TalentLink>());
            }
            catch
            {
                EditorGUILayout.HelpBox("TalentLink component not found in world.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            if (query.CalculateEntityCount() == 0)
            {
                EditorGUILayout.HelpBox("No entities with TalentLink found. Is TalentAuthoring on the player prefab?", MessageType.Info);
                query.Dispose();
                EditorGUILayout.EndVertical();
                return;
            }

            var entities = query.ToEntityArray(Allocator.Temp);
            var links = query.ToComponentDataArray<TalentLink>(Allocator.Temp);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int i = 0; i < entities.Length; i++)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField($"Player Entity {entities[i].Index}:{entities[i].Version}", EditorStyles.boldLabel);

                var child = links[i].TalentChild;
                if (child == Entity.Null || !em.Exists(child))
                {
                    EditorGUILayout.LabelField("  TalentChild: NULL or invalid");
                    continue;
                }

                // TalentState
                if (em.HasComponent<TalentState>(child))
                {
                    var state = em.GetComponentData<TalentState>(child);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Talent State", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total Points: {state.TotalTalentPoints}");
                    EditorGUILayout.LabelField($"Spent Points: {state.SpentTalentPoints}");
                    EditorGUILayout.LabelField($"Available: {state.TotalTalentPoints - state.SpentTalentPoints}");
                    EditorGUILayout.LabelField($"Active Trees: {state.ActiveTreeCount}");
                    EditorGUILayout.LabelField($"Respec Count: {state.RespecCount}");
                    EditorGUI.indentLevel--;
                }

                // TalentPassiveStats
                if (em.HasComponent<TalentPassiveStats>(child))
                {
                    var stats = em.GetComponentData<TalentPassiveStats>(child);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Passive Stat Bonuses", EditorStyles.boldLabel);

                    if (stats.BonusAttackPower != 0) EditorGUILayout.LabelField($"Attack Power: +{stats.BonusAttackPower:F1}");
                    if (stats.BonusSpellPower != 0) EditorGUILayout.LabelField($"Spell Power: +{stats.BonusSpellPower:F1}");
                    if (stats.BonusCritChance != 0) EditorGUILayout.LabelField($"Crit Chance: +{stats.BonusCritChance:F2}");
                    if (stats.BonusCritDamage != 0) EditorGUILayout.LabelField($"Crit Damage: +{stats.BonusCritDamage:F2}");
                    if (stats.BonusDefense != 0) EditorGUILayout.LabelField($"Defense: +{stats.BonusDefense:F1}");
                    if (stats.BonusArmor != 0) EditorGUILayout.LabelField($"Armor: +{stats.BonusArmor:F1}");
                    if (stats.BonusMaxHealth != 0) EditorGUILayout.LabelField($"Max Health: +{stats.BonusMaxHealth:F0}");
                    if (stats.BonusMovementSpeed != 0) EditorGUILayout.LabelField($"Move Speed: +{stats.BonusMovementSpeed:F2}");
                    if (stats.BonusCooldownReduction != 0) EditorGUILayout.LabelField($"Cooldown Reduction: +{stats.BonusCooldownReduction:F2}");
                    if (stats.BonusResourceRegen != 0) EditorGUILayout.LabelField($"Resource Regen: +{stats.BonusResourceRegen:F2}");
                    if (stats.BonusDamagePercent != 0) EditorGUILayout.LabelField($"Damage %: +{stats.BonusDamagePercent:F2}");
                    if (stats.BonusHealingPercent != 0) EditorGUILayout.LabelField($"Healing %: +{stats.BonusHealingPercent:F2}");

                    EditorGUI.indentLevel--;
                }

                // Allocations
                if (em.HasBuffer<TalentAllocation>(child))
                {
                    var buffer = em.GetBuffer<TalentAllocation>(child, true);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Allocations ({buffer.Length})", EditorStyles.boldLabel);
                    for (int a = 0; a < buffer.Length; a++)
                        EditorGUILayout.LabelField($"  Tree {buffer[a].TreeId} / Node {buffer[a].NodeId} (tick {buffer[a].AllocatedTick})");
                    EditorGUI.indentLevel--;
                }

                // Tree Progress
                if (em.HasBuffer<TalentTreeProgress>(child))
                {
                    var buffer = em.GetBuffer<TalentTreeProgress>(child, true);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Tree Progress ({buffer.Length})", EditorStyles.boldLabel);
                    for (int t = 0; t < buffer.Length; t++)
                        EditorGUILayout.LabelField($"  Tree {buffer[t].TreeId}: {buffer[t].PointsSpent} spent, highest tier {buffer[t].HighestTier}");
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndScrollView();

            entities.Dispose();
            links.Dispose();
            query.Dispose();

            EditorGUILayout.EndVertical();
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
