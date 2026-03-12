#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using DIG.Roguelite.Rewards;

namespace DIG.Roguelite.Editor.Modules
{
    /// <summary>
    /// EPIC 23.6: Reward Configurator module.
    /// Edit RewardPoolSO and RewardDefinitionSO assets inline.
    /// Rarity distribution chart, expected value calculator, seed-based choice preview.
    /// </summary>
    public class RewardModule : IRunWorkstationModule
    {
        public string TabName => "Rewards";

        private RewardPoolSO _rewardPool;
        private UnityEditor.Editor _poolEditor;
        private Vector2 _scrollPos;
        private uint _previewSeed = 42;
        private int _previewZoneIndex;
        private bool _showDistribution = true;
        private bool _showPreview;

        public void OnEnable() { }
        public void OnDisable()
        {
            if (_poolEditor != null)
                Object.DestroyImmediate(_poolEditor);
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Reward Configurator", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _rewardPool = (RewardPoolSO)EditorGUILayout.ObjectField(
                "Reward Pool", _rewardPool, typeof(RewardPoolSO), false);

            if (_rewardPool == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a RewardPoolSO to edit its entries.\n" +
                    "Create one via Assets > Create > DIG > Roguelite > Reward Pool.",
                    MessageType.Info);
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Rarity distribution
            _showDistribution = EditorGUILayout.Foldout(_showDistribution, "Rarity Distribution", true);
            if (_showDistribution)
            {
                DrawRarityDistribution();
            }

            EditorGUILayout.Space(8);

            // Default inspector
            if (_poolEditor == null || _poolEditor.target != _rewardPool)
            {
                if (_poolEditor != null)
                    Object.DestroyImmediate(_poolEditor);
                _poolEditor = UnityEditor.Editor.CreateEditor(_rewardPool);
            }
            _poolEditor.OnInspectorGUI();

            EditorGUILayout.Space(8);

            // Roll preview
            _showPreview = EditorGUILayout.Foldout(_showPreview, "Roll Preview", true);
            if (_showPreview)
            {
                DrawRollPreview();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawRarityDistribution()
        {
            EditorGUI.indentLevel++;

            var so = new SerializedObject(_rewardPool);
            var entries = so.FindProperty("Entries");
            if (entries == null || !entries.isArray || entries.arraySize == 0)
            {
                EditorGUILayout.LabelField("No entries.", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                return;
            }

            // Count by rarity
            var rarityCounts = new Dictionary<string, int>();
            float totalWeight = 0f;
            var rarityWeights = new Dictionary<string, float>();

            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var rarity = entry.FindPropertyRelative("Rarity");
                var weight = entry.FindPropertyRelative("Weight");

                string rarityName = rarity != null ? rarity.enumDisplayNames[rarity.enumValueIndex] : "Unknown";
                float w = weight != null ? weight.floatValue : 1f;

                rarityCounts.TryGetValue(rarityName, out int count);
                rarityCounts[rarityName] = count + 1;

                rarityWeights.TryGetValue(rarityName, out float wAcc);
                rarityWeights[rarityName] = wAcc + w;

                totalWeight += w;
            }

            if (totalWeight <= 0f) totalWeight = 1f;

            foreach (var kvp in rarityWeights)
            {
                float pct = kvp.Value / totalWeight;
                var rect = EditorGUILayout.GetControlRect(false, 20);
                var barRect = new Rect(rect.x, rect.y, rect.width * pct, rect.height);
                EditorGUI.DrawRect(barRect, GetRarityColor(kvp.Key));

                rarityCounts.TryGetValue(kvp.Key, out int count);
                EditorGUI.LabelField(rect, $"  {kvp.Key}: {count} entries, {pct:P1} weight");
            }

            EditorGUI.indentLevel--;
        }

        private void DrawRollPreview()
        {
            EditorGUI.indentLevel++;

            _previewSeed = (uint)EditorGUILayout.IntField("Seed", (int)_previewSeed);
            _previewZoneIndex = EditorGUILayout.IntSlider("Zone Index", _previewZoneIndex, 0, 20);

            if (GUILayout.Button("Roll Reward Choices"))
            {
                uint rewardSeed = Unity.Mathematics.math.hash(
                    new Unity.Mathematics.uint3(_previewSeed, (uint)_previewZoneIndex, 0xBEEF));
                var rng = new Unity.Mathematics.Random(rewardSeed | 1);

                var so = new SerializedObject(_rewardPool);
                var entries = so.FindProperty("Entries");
                var choiceCountProp = so.FindProperty("ChoiceCount");
                int choiceCount = choiceCountProp != null ? choiceCountProp.intValue : 3;

                if (entries != null && entries.isArray && entries.arraySize > 0)
                {
                    Debug.Log($"[Reward Preview] Seed={_previewSeed}, Zone={_previewZoneIndex}, Choices={choiceCount}");

                    float totalWeight = 0f;
                    for (int i = 0; i < entries.arraySize; i++)
                    {
                        var w = entries.GetArrayElementAtIndex(i).FindPropertyRelative("Weight");
                        if (w != null) totalWeight += w.floatValue;
                    }

                    for (int c = 0; c < choiceCount && c < entries.arraySize; c++)
                    {
                        float r = rng.NextFloat() * totalWeight;
                        float acc = 0f;
                        for (int i = 0; i < entries.arraySize; i++)
                        {
                            var entry = entries.GetArrayElementAtIndex(i);
                            var w = entry.FindPropertyRelative("Weight");
                            acc += w != null ? w.floatValue : 0f;
                            if (r <= acc)
                            {
                                var reward = entry.FindPropertyRelative("Reward");
                                string name = reward?.objectReferenceValue != null
                                    ? reward.objectReferenceValue.name : $"Entry {i}";
                                var rarity = entry.FindPropertyRelative("Rarity");
                                string rarityName = rarity != null
                                    ? rarity.enumDisplayNames[rarity.enumValueIndex] : "?";
                                Debug.Log($"  Choice {c}: {name} ({rarityName})");
                                break;
                            }
                        }
                    }
                }
            }

            EditorGUI.indentLevel--;
        }

        private static Color GetRarityColor(string rarity)
        {
            return rarity switch
            {
                "Common" => new Color(0.6f, 0.6f, 0.6f, 0.5f),
                "Uncommon" => new Color(0.3f, 0.7f, 0.3f, 0.5f),
                "Rare" => new Color(0.3f, 0.4f, 0.9f, 0.5f),
                "Epic" => new Color(0.7f, 0.3f, 0.8f, 0.5f),
                "Legendary" => new Color(0.9f, 0.7f, 0.2f, 0.5f),
                _ => new Color(0.5f, 0.5f, 0.5f, 0.5f)
            };
        }
    }
}
#endif
