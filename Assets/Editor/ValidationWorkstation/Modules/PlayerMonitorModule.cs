using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEditor;
using UnityEngine;

namespace DIG.Validation.Editor.Modules
{
    /// <summary>
    /// EPIC 17.11: Live violation score per connected player (color-coded).
    /// PenaltyLevel indicator, ConsecutiveKicks counter, session duration.
    /// </summary>
    public class PlayerMonitorModule : IValidationWorkstationModule
    {
        public string ModuleName => "Player Monitor";

        private Vector2 _scroll;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Player Validation Monitor", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to see live player validation data.", MessageType.Info);
                return;
            }

            var world = ValidationWorkstationWindow.GetValidationWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No ECS World available.", MessageType.Warning);
                return;
            }

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerValidationState>(),
                ComponentType.ReadOnly<ValidationOwner>(),
                ComponentType.ReadOnly<ValidationChildTag>());

            int count = query.CalculateEntityCount();
            if (count == 0)
            {
                EditorGUILayout.HelpBox("No players with validation state found.", MessageType.Info);
                return;
            }

            // Get config for thresholds
            float warnThreshold = 5f, kickThreshold = 20f;
            var configQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ValidationConfig>());
            if (configQuery.CalculateEntityCount() > 0)
            {
                var config = configQuery.GetSingleton<ValidationConfig>();
                warnThreshold = config.WarnThreshold;
                kickThreshold = config.KickThreshold;
            }

            var states = query.ToComponentDataArray<PlayerValidationState>(Allocator.Temp);
            var owners = query.ToComponentDataArray<ValidationOwner>(Allocator.Temp);

            // Header
            EditorGUILayout.BeginHorizontal("box");
            GUILayout.Label("Entity", EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.Label("NetID", EditorStyles.boldLabel, GUILayout.Width(50));
            GUILayout.Label("Score", EditorStyles.boldLabel, GUILayout.Width(60));
            GUILayout.Label("Penalty", EditorStyles.boldLabel, GUILayout.Width(70));
            GUILayout.Label("Warns", EditorStyles.boldLabel, GUILayout.Width(50));
            GUILayout.Label("Kicks", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int i = 0; i < states.Length; i++)
            {
                var state = states[i];
                var player = owners[i].Owner;

                // Color-code by violation score
                var prevBg = GUI.backgroundColor;
                if (state.ViolationScore >= kickThreshold)
                    GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
                else if (state.ViolationScore >= warnThreshold)
                    GUI.backgroundColor = new Color(1f, 0.9f, 0.3f);
                else
                    GUI.backgroundColor = new Color(0.3f, 1f, 0.3f);

                EditorGUILayout.BeginHorizontal("box");

                // Entity index
                GUILayout.Label(player.Index.ToString(), GUILayout.Width(80));

                // Network ID
                int netId = 0;
                if (em.Exists(player) && em.HasComponent<GhostOwner>(player))
                    netId = em.GetComponentData<GhostOwner>(player).NetworkId;
                GUILayout.Label(netId.ToString(), GUILayout.Width(50));

                // Violation score
                GUILayout.Label(state.ViolationScore.ToString("F1"), GUILayout.Width(60));

                // Penalty level
                var penalty = (PenaltyLevel)state.PenaltyLevel;
                GUILayout.Label(penalty.ToString(), GUILayout.Width(70));

                // Warning count
                GUILayout.Label(state.WarningCount.ToString(), GUILayout.Width(50));

                // Consecutive kicks
                GUILayout.Label(state.ConsecutiveKicks.ToString(), GUILayout.Width(50));

                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = prevBg;
            }

            EditorGUILayout.EndScrollView();

            states.Dispose();
            owners.Dispose();
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
