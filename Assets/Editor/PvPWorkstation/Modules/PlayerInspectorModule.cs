#if UNITY_EDITOR
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using Player.Components;

namespace DIG.PvP.Editor.Modules
{
    /// <summary>
    /// EPIC 17.10: Live player K/D/A, team assignment, spawn protection,
    /// Elo/Tier, and stat override state viewer.
    /// </summary>
    public class PlayerInspectorModule : IPvPWorkstationModule
    {
        public string ModuleName => "Player Inspector";
        private Vector2 _scroll;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Player Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect player PvP state.", MessageType.Info);
                return;
            }

            var world = PvPWorkstationWindow.GetPvPWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No active ECS world found.", MessageType.Warning);
                return;
            }

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<PvPPlayerStats>(),
                ComponentType.ReadOnly<PvPTeam>(),
                ComponentType.ReadOnly<PlayerTag>());

            var entities = query.ToEntityArray(Allocator.Temp);
            var stats = query.ToComponentDataArray<PvPPlayerStats>(Allocator.Temp);
            var teams = query.ToComponentDataArray<PvPTeam>(Allocator.Temp);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField($"Players: {entities.Length}", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            for (int i = 0; i < entities.Length; i++)
            {
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.LabelField($"Player {i} (Entity {entities[i].Index})", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Team", teams[i].TeamId.ToString());
                EditorGUILayout.LabelField("K / D / A", $"{stats[i].Kills} / {stats[i].Deaths} / {stats[i].Assists}");
                EditorGUILayout.LabelField("Damage Dealt", $"{stats[i].DamageDealt:F0}");
                EditorGUILayout.LabelField("Damage Received", $"{stats[i].DamageReceived:F0}");
                EditorGUILayout.LabelField("Match Score", stats[i].MatchScore.ToString());

                // Spawn protection
                if (em.HasComponent<PvPSpawnProtection>(entities[i]))
                {
                    bool protEnabled = em.IsComponentEnabled<PvPSpawnProtection>(entities[i]);
                    EditorGUILayout.LabelField("Spawn Protection", protEnabled ? "ACTIVE" : "Inactive");
                }

                // Ranking
                if (em.HasComponent<PvPRankingLink>(entities[i]))
                {
                    var link = em.GetComponentData<PvPRankingLink>(entities[i]);
                    if (link.RankingChild != Entity.Null && em.HasComponent<PvPRanking>(link.RankingChild))
                    {
                        var ranking = em.GetComponentData<PvPRanking>(link.RankingChild);
                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField("Ranking", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField("Elo", ranking.Elo.ToString());
                        EditorGUILayout.LabelField("Tier", ranking.Tier.ToString());
                        EditorGUILayout.LabelField("W/L", $"{ranking.Wins}/{ranking.Losses}");
                        EditorGUILayout.LabelField("Win Streak", ranking.WinStreak.ToString());
                        EditorGUILayout.LabelField("Peak Elo", ranking.HighestElo.ToString());
                        EditorGUILayout.LabelField("Placement", $"{ranking.PlacementMatchesPlayed}/10");
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.EndScrollView();

            entities.Dispose();
            stats.Dispose();
            teams.Dispose();
            query.Dispose();
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
#endif
