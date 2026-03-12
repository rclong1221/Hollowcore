#if UNITY_EDITOR
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace DIG.Achievement.Editor.Modules
{
    /// <summary>
    /// EPIC 17.7: Play-mode only: select player entity, view live AchievementProgress buffer.
    /// Columns: Name, Category, CurrentValue/NextThreshold, Tier, % Complete.
    /// "Grant Progress" and "Unlock Achievement" debug buttons.
    /// Also shows cumulative stats panel and category breakdown statistics.
    /// </summary>
    public class ProgressInspectorModule : IAchievementWorkstationModule
    {
        public string ModuleName => "Progress Inspector";

        private Vector2 _scroll;
        private bool _showStats = true;
        private bool _showProgress = true;

        // Cached queries
        private World _cachedWorld;
        private EntityQuery _childQuery;
        private EntityQuery _registryQuery;

        private void EnsureQueries(World world, EntityManager em)
        {
            if (_cachedWorld == world) return;
            _cachedWorld = world;
            _childQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<AchievementChildTag>(),
                ComponentType.ReadOnly<AchievementOwner>(),
                ComponentType.ReadWrite<AchievementProgress>(),
                ComponentType.ReadWrite<AchievementCumulativeStats>()
            );
            _registryQuery = em.CreateEntityQuery(ComponentType.ReadOnly<AchievementRegistrySingleton>());
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Achievement Progress Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect live achievement data.", MessageType.Info);
                return;
            }

            var world = AchievementWorkstationWindow.GetAchievementWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No game world found.", MessageType.Warning);
                return;
            }

            var em = world.EntityManager;
            EnsureQueries(world, em);

            if (_childQuery.CalculateEntityCount() == 0)
            {
                EditorGUILayout.HelpBox("No achievement child entities found. Is AchievementAuthoring on the player prefab?", MessageType.Warning);
                return;
            }

            // Use first child entity
            var entities = _childQuery.ToEntityArray(Allocator.Temp);
            var childEntity = entities[0];
            entities.Dispose();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // Cumulative Stats
            _showStats = EditorGUILayout.Foldout(_showStats, "Cumulative Stats", true);
            if (_showStats)
            {
                var stats = em.GetComponentData<AchievementCumulativeStats>(childEntity);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Total Kills: {stats.TotalKills}");
                EditorGUILayout.LabelField($"Total Deaths: {stats.TotalDeaths}");
                EditorGUILayout.LabelField($"Total Quests Completed: {stats.TotalQuestsCompleted}");
                EditorGUILayout.LabelField($"Total Items Crafted: {stats.TotalItemsCrafted}");
                EditorGUILayout.LabelField($"Total NPCs Interacted: {stats.TotalNPCsInteracted}");
                EditorGUILayout.LabelField($"Total Damage Dealt: {stats.TotalDamageDealt:N0}");
                EditorGUILayout.LabelField($"Total Loot Collected: {stats.TotalLootCollected}");
                EditorGUILayout.LabelField($"Highest Kill Streak: {stats.HighestKillStreak}");
                EditorGUILayout.LabelField($"Current Kill Streak: {stats.CurrentKillStreak}");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);

            // Progress Buffer
            var buffer = em.GetBuffer<AchievementProgress>(childEntity, true);
            bool hasRegistry = _registryQuery.CalculateEntityCount() > 0;
            BlobAssetReference<AchievementRegistryBlob> registryRef = default;
            if (hasRegistry)
            {
                var singleton = _registryQuery.GetSingleton<AchievementRegistrySingleton>();
                registryRef = singleton.Registry;
            }

            _showProgress = EditorGUILayout.Foldout(_showProgress, $"Achievement Progress ({buffer.Length} entries)", true);
            if (_showProgress)
            {
                // Header row
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("ID", GUILayout.Width(40));
                EditorGUILayout.LabelField("Name", GUILayout.Width(150));
                EditorGUILayout.LabelField("Progress", GUILayout.Width(80));
                EditorGUILayout.LabelField("Tier", GUILayout.Width(60));
                EditorGUILayout.LabelField("Status", GUILayout.Width(70));
                EditorGUILayout.LabelField("Actions", GUILayout.Width(120));
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < buffer.Length; i++)
                {
                    var entry = buffer[i];
                    string name = $"#{entry.AchievementId}";
                    int nextThreshold = 0;

                    if (hasRegistry && registryRef.IsCreated)
                    {
                        ref var blob = ref registryRef.Value;
                        for (int d = 0; d < blob.TotalAchievements; d++)
                        {
                            if (blob.Definitions[d].AchievementId == entry.AchievementId)
                            {
                                name = blob.Definitions[d].Name.ToString();
                                // Find next threshold
                                ref var tiers = ref blob.Definitions[d].Tiers;
                                for (int t = 0; t < tiers.Length; t++)
                                {
                                    if (entry.HighestTierUnlocked < (byte)tiers[t].Tier)
                                    {
                                        nextThreshold = tiers[t].Threshold;
                                        break;
                                    }
                                }
                                break;
                            }
                        }
                    }

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(entry.AchievementId.ToString(), GUILayout.Width(40));
                    EditorGUILayout.LabelField(name, GUILayout.Width(150));

                    string progressStr = nextThreshold > 0 ? $"{entry.CurrentValue}/{nextThreshold}" : $"{entry.CurrentValue}";
                    EditorGUILayout.LabelField(progressStr, GUILayout.Width(80));

                    string tierStr = ((AchievementTier)entry.HighestTierUnlocked).ToString();
                    EditorGUILayout.LabelField(tierStr, GUILayout.Width(60));

                    string statusStr = entry.IsUnlocked ? "Complete" : "Active";
                    EditorGUILayout.LabelField(statusStr, GUILayout.Width(70));

                    // Debug buttons
                    if (!entry.IsUnlocked)
                    {
                        if (GUILayout.Button("+10", GUILayout.Width(35)))
                        {
                            var writeBuffer = em.GetBuffer<AchievementProgress>(childEntity);
                            var e = writeBuffer[i];
                            e.CurrentValue += 10;
                            writeBuffer[i] = e;
                            // Mark dirty
                            if (em.HasComponent<AchievementDirtyFlags>(childEntity))
                                em.SetComponentData(childEntity, new AchievementDirtyFlags { Flags = 0x3 });
                        }
                        if (GUILayout.Button("Max", GUILayout.Width(35)))
                        {
                            var writeBuffer = em.GetBuffer<AchievementProgress>(childEntity);
                            var e = writeBuffer[i];
                            e.CurrentValue = nextThreshold > 0 ? nextThreshold : e.CurrentValue + 100;
                            writeBuffer[i] = e;
                            if (em.HasComponent<AchievementDirtyFlags>(childEntity))
                                em.SetComponentData(childEntity, new AchievementDirtyFlags { Flags = 0x3 });
                        }
                    }
                    else
                    {
                        GUILayout.Space(72);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
#endif
