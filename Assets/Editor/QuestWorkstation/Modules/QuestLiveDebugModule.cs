using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace DIG.Quest.Editor.Modules
{
    /// <summary>
    /// EPIC 16.12: Play-mode live debug — shows all active QuestInstance entities,
    /// progress bars, event counters.
    /// </summary>
    public class QuestLiveDebugModule : IQuestModule
    {
        private Vector2 _scroll;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Live Quest Debug", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to see live quest data.", MessageType.Info);
                return;
            }

            var world = QuestWorkstationWindow.GetQuestWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No ECS World available.", MessageType.Warning);
                return;
            }

            var em = world.EntityManager;

            // Get registry for names
            QuestRegistryManaged registry = null;
            var registryQuery = em.CreateEntityQuery(ComponentType.ReadOnly<QuestRegistryManaged>());
            if (registryQuery.CalculateEntityCount() > 0)
            {
                var registryEntity = registryQuery.GetSingletonEntity();
                registry = em.GetComponentObject<QuestRegistryManaged>(registryEntity);
            }

            // Query all quest instances
            var questQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<QuestProgress>(),
                ComponentType.ReadOnly<QuestPlayerLink>(),
                ComponentType.ReadOnly<ObjectiveProgress>()
            );

            var entities = questQuery.ToEntityArray(Allocator.Temp);
            var progressArray = questQuery.ToComponentDataArray<QuestProgress>(Allocator.Temp);
            var links = questQuery.ToComponentDataArray<QuestPlayerLink>(Allocator.Temp);

            EditorGUILayout.LabelField($"Active Quest Instances: {entities.Length}");
            EditorGUILayout.Space(4);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int i = 0; i < entities.Length; i++)
            {
                var progress = progressArray[i];
                var link = links[i];
                var def = registry?.Database.GetQuest(progress.QuestId);
                string questName = def?.DisplayName ?? $"Quest #{progress.QuestId}";

                EditorGUILayout.BeginVertical("box");

                // Header
                var stateColor = GetStateColor(progress.State);
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = stateColor;
                EditorGUILayout.LabelField($"{questName} [{progress.State}]", EditorStyles.boldLabel);
                GUI.backgroundColor = prevBg;

                EditorGUILayout.LabelField($"  Player: {link.PlayerEntity}", EditorStyles.miniLabel);
                if (progress.TimeRemaining > 0)
                    EditorGUILayout.LabelField($"  Time Remaining: {progress.TimeRemaining:F1}s", EditorStyles.miniLabel);

                // Objectives
                if (em.HasBuffer<ObjectiveProgress>(entities[i]))
                {
                    var objectives = em.GetBuffer<ObjectiveProgress>(entities[i], true);
                    for (int o = 0; o < objectives.Length; o++)
                    {
                        var obj = objectives[o];
                        string objDesc = "";
                        if (def != null)
                        {
                            foreach (var objDef in def.Objectives)
                            {
                                if (objDef.ObjectiveId == obj.ObjectiveId)
                                {
                                    objDesc = objDef.Description;
                                    break;
                                }
                            }
                        }

                        EditorGUILayout.BeginHorizontal();
                        string prefix = obj.IsOptional ? "(Optional) " : "";
                        string hidden = obj.IsHidden ? " [Hidden]" : "";
                        EditorGUILayout.LabelField($"  {prefix}{objDesc}{hidden}", GUILayout.Width(250));

                        // Progress bar
                        float t = obj.RequiredCount > 0 ? (float)obj.CurrentCount / obj.RequiredCount : 0f;
                        var barRect = GUILayoutUtility.GetRect(100, 16, GUILayout.Width(150));
                        EditorGUI.ProgressBar(barRect, t, $"{obj.CurrentCount}/{obj.RequiredCount} [{obj.State}]");

                        EditorGUILayout.EndHorizontal();
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();

            entities.Dispose();
            progressArray.Dispose();
            links.Dispose();
        }

        public void OnSceneGUI(UnityEditor.SceneView sceneView) { }

        private static Color GetStateColor(QuestState state) => state switch
        {
            QuestState.Active => new Color(0.3f, 0.7f, 1f),
            QuestState.Completed => new Color(0.3f, 1f, 0.3f),
            QuestState.Failed => new Color(1f, 0.3f, 0.3f),
            QuestState.TurnedIn => new Color(0.8f, 0.8f, 0.3f),
            _ => Color.white
        };
    }
}
