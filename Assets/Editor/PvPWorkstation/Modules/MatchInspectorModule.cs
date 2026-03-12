#if UNITY_EDITOR
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace DIG.PvP.Editor.Modules
{
    /// <summary>
    /// EPIC 17.10: Live match state viewer (play mode).
    /// Shows phase, timer, scores, player count, and phase transition buttons for testing.
    /// </summary>
    public class MatchInspectorModule : IPvPWorkstationModule
    {
        public string ModuleName => "Match Inspector";

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Match Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect live match state.", MessageType.Info);
                return;
            }

            var world = PvPWorkstationWindow.GetPvPWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No active ECS world found.", MessageType.Warning);
                return;
            }

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<PvPMatchState>());

            if (query.CalculateEntityCount() == 0)
            {
                EditorGUILayout.LabelField("No active PvP match.");
                EditorGUILayout.Space(8);

                EditorGUILayout.LabelField("Test Actions", EditorStyles.boldLabel);
                if (GUILayout.Button("Create Test Match (TDM)", GUILayout.Height(24)))
                {
                    var requestEntity = em.CreateEntity();
                    em.AddComponentData(requestEntity, new PvPMatchRequest
                    {
                        GameMode = PvPGameMode.TeamDeathmatch,
                        MapId = 1,
                        MaxPlayers = 8,
                        NormalizationMode = 0,
                        MaxScore = 50,
                        MatchDuration = 600f
                    });
                }
                if (GUILayout.Button("Create Test Match (FFA)", GUILayout.Height(24)))
                {
                    var requestEntity = em.CreateEntity();
                    em.AddComponentData(requestEntity, new PvPMatchRequest
                    {
                        GameMode = PvPGameMode.FreeForAll,
                        MapId = 1,
                        MaxPlayers = 4,
                        NormalizationMode = 0,
                        MaxScore = 20,
                        MatchDuration = 300f
                    });
                }
                query.Dispose();
                return;
            }

            var state = query.GetSingleton<PvPMatchState>();
            query.Dispose();

            // Match state display
            EditorGUILayout.LabelField("Phase", state.Phase.ToString());
            EditorGUILayout.LabelField("Game Mode", state.GameMode.ToString());
            EditorGUILayout.LabelField("Map ID", state.MapId.ToString());

            int minutes = (int)(state.Timer / 60f);
            int seconds = (int)(state.Timer % 60f);
            EditorGUILayout.LabelField("Timer", $"{minutes:D2}:{seconds:D2}");
            EditorGUILayout.LabelField("Match Duration", $"{state.MatchDuration}s");
            EditorGUILayout.LabelField("Max Score", state.MaxScore.ToString());
            EditorGUILayout.LabelField("Overtime", state.OvertimeEnabled == 1 ? "Enabled" : "Disabled");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Scores", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Team 1", state.TeamScore0.ToString());
            EditorGUILayout.LabelField("Team 2", state.TeamScore1.ToString());
            if (state.GameMode == PvPGameMode.FreeForAll)
            {
                EditorGUILayout.LabelField("Team 3", state.TeamScore2.ToString());
                EditorGUILayout.LabelField("Team 4", state.TeamScore3.ToString());
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
#endif
