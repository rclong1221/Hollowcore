using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using DIG.Combat.Components;

namespace DIG.Progression.Editor.Modules
{
    /// <summary>
    /// EPIC 16.14: Play-mode XP simulator with "Grant XP" button,
    /// "Set Level" slider, and kill simulation tools.
    /// </summary>
    public class XPSimulatorModule : IProgressionWorkstationModule
    {
        private int _grantXPAmount = 500;
        private int _setLevel = 5;
        private int _simEnemyLevel = 5;
        private int _simKillCount = 10;
        private float _equipXPBonus;
        private Vector2 _scroll;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("XP Simulator", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to use the XP simulator.", MessageType.Info);
                DrawOfflineSimulator();
                return;
            }

            var world = ProgressionWorkstationWindow.GetProgressionWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No ECS World available.", MessageType.Warning);
                return;
            }

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(
                ComponentType.ReadWrite<PlayerProgression>(),
                ComponentType.ReadWrite<CharacterAttributes>());

            if (query.CalculateEntityCount() == 0)
            {
                EditorGUILayout.HelpBox("No entities with PlayerProgression found.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // Grant XP
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Grant XP", EditorStyles.boldLabel);
            _grantXPAmount = EditorGUILayout.IntSlider("Amount", _grantXPAmount, 1, 100000);
            if (GUILayout.Button("Grant XP to All Players"))
            {
                var entities = query.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    var prog = em.GetComponentData<PlayerProgression>(entities[i]);
                    prog.CurrentXP += _grantXPAmount;
                    prog.TotalXPEarned += _grantXPAmount;
                    em.SetComponentData(entities[i], prog);
                    LevelUpVisualQueue.EnqueueXPGain(_grantXPAmount, XPSourceType.Bonus);
                }
                entities.Dispose();
                Debug.Log($"[XPSimulator] Granted {_grantXPAmount} XP to {query.CalculateEntityCount()} players.");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Set Level
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Set Level", EditorStyles.boldLabel);
            _setLevel = EditorGUILayout.IntSlider("Target Level", _setLevel, 1, 50);
            if (GUILayout.Button("Set Level for All Players"))
            {
                var entities = query.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    var attrs = em.GetComponentData<CharacterAttributes>(entities[i]);
                    var prog = em.GetComponentData<PlayerProgression>(entities[i]);

                    int previousLevel = attrs.Level;
                    attrs.Level = _setLevel;
                    prog.CurrentXP = 0;

                    // Award stat points for level difference
                    var configQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ProgressionConfigSingleton>());
                    if (configQuery.CalculateEntityCount() > 0)
                    {
                        var config = configQuery.GetSingleton<ProgressionConfigSingleton>();
                        prog.UnspentStatPoints = (_setLevel - 1) * config.Curve.Value.StatPointsPerLevel;
                    }

                    em.SetComponentData(entities[i], attrs);
                    em.SetComponentData(entities[i], prog);

                    if (_setLevel != previousLevel)
                    {
                        em.SetComponentData(entities[i], new LevelUpEvent
                        {
                            NewLevel = _setLevel,
                            PreviousLevel = previousLevel
                        });
                        em.SetComponentEnabled<LevelUpEvent>(entities[i], true);
                    }
                }
                entities.Dispose();
                Debug.Log($"[XPSimulator] Set all players to level {_setLevel}.");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Kill Simulation
            DrawKillSimulation();

            EditorGUILayout.EndScrollView();
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private void DrawKillSimulation()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Kill Simulation (Offline)", EditorStyles.boldLabel);

            _simEnemyLevel = EditorGUILayout.IntSlider("Enemy Level", _simEnemyLevel, 1, 50);
            _simKillCount = EditorGUILayout.IntSlider("Kill Count", _simKillCount, 1, 1000);
            _equipXPBonus = EditorGUILayout.Slider("Equipment XP Bonus", _equipXPBonus, 0f, 1f);

            var curveSO = Resources.Load<ProgressionCurveSO>("ProgressionCurve");
            if (curveSO == null)
            {
                EditorGUILayout.HelpBox("No ProgressionCurve at Resources/. Cannot simulate.", MessageType.Warning);
            }
            else
            {
                if (GUILayout.Button("Simulate"))
                {
                    SimulateKills(curveSO, 1, _simEnemyLevel, _simKillCount, _equipXPBonus);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawOfflineSimulator()
        {
            EditorGUILayout.Space(8);
            DrawKillSimulation();
        }

        private static void SimulateKills(ProgressionCurveSO curve, int startLevel, int enemyLevel, int kills, float equipBonus)
        {
            int level = startLevel;
            int currentXP = 0;
            int totalXP = 0;
            int levelsGained = 0;

            for (int k = 0; k < kills; k++)
            {
                float rawXP = curve.BaseKillXP * Mathf.Pow(curve.KillXPPerEnemyLevel, enemyLevel - 1);

                int delta = level - enemyLevel;
                if (delta > curve.DiminishStartDelta)
                {
                    float diminish = Mathf.Pow(curve.DiminishFactorPerLevel, delta - curve.DiminishStartDelta);
                    diminish = Mathf.Max(diminish, curve.DiminishFloor);
                    rawXP *= diminish;
                }

                int xp = Mathf.RoundToInt(rawXP * (1f + equipBonus));
                currentXP += xp;
                totalXP += xp;

                while (level < curve.MaxLevel)
                {
                    int needed = curve.GetXPForLevel(level);
                    if (currentXP < needed) break;
                    currentXP -= needed;
                    level++;
                    levelsGained++;
                }
            }

            Debug.Log($"[XPSimulator] {kills} kills of level {enemyLevel} enemies " +
                      $"(+{equipBonus:P0} gear): Level 1 -> {level}, " +
                      $"Total XP: {totalXP:N0}, Levels gained: {levelsGained}, " +
                      $"Remaining XP: {currentXP:N0}");
        }
    }
}
