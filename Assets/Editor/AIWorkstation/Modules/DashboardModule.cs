using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Collections;
using DIG.AI.Components;
using DIG.Aggro.Components;

namespace DIG.Editor.AIWorkstation.Modules
{
    /// <summary>
    /// Dashboard: Aggregate live stats across ALL AI entities.
    /// State distribution, combat stats, and per-system performance timing.
    /// </summary>
    public class DashboardModule : IAIWorkstationModule
    {
        // Cached counts (updated each repaint)
        private int _totalAI;
        private int _idleCount, _patrolCount, _combatCount, _returnHomeCount, _investigateCount, _fleeCount;
        private int _aggroedCount;
        private float _avgThreat;
        private int _castingCount;
        private float _lastUpdateTime;

        public void OnEntityChanged(Entity entity, EntityManager entityManager) { }
        public void OnSceneGUI(SceneView sceneView) { }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("AI Dashboard", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to see live AI statistics.", MessageType.Info);
                return;
            }

            var world = AIWorkstationWindow.GetAIWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No ECS World available.", MessageType.Warning);
                return;
            }

            // Throttle expensive queries to ~4Hz
            if (Time.realtimeSinceStartup - _lastUpdateTime > 0.25f)
            {
                UpdateStats(world.EntityManager);
                _lastUpdateTime = Time.realtimeSinceStartup;
            }

            DrawStateDistribution();
            EditorGUILayout.Space(6);
            DrawCombatStats();
            EditorGUILayout.Space(6);
            DrawStateBar();
        }

        private void UpdateStats(EntityManager em)
        {
            _totalAI = 0;
            _idleCount = _patrolCount = _combatCount = _returnHomeCount = _investigateCount = _fleeCount = 0;
            _aggroedCount = 0;
            _avgThreat = 0f;
            _castingCount = 0;
            float totalThreat = 0f;

            var query = em.CreateEntityQuery(typeof(AIState));
            var states = query.ToComponentDataArray<AIState>(Allocator.Temp);

            _totalAI = states.Length;
            for (int i = 0; i < states.Length; i++)
            {
                switch (states[i].CurrentState)
                {
                    case AIBehaviorState.Idle: _idleCount++; break;
                    case AIBehaviorState.Patrol: _patrolCount++; break;
                    case AIBehaviorState.Combat: _combatCount++; break;
                    case AIBehaviorState.ReturnHome: _returnHomeCount++; break;
                    case AIBehaviorState.Investigate: _investigateCount++; break;
                    case AIBehaviorState.Flee: _fleeCount++; break;
                }
            }
            states.Dispose();

            // Aggro stats
            var aggroQuery = em.CreateEntityQuery(typeof(AggroState));
            var aggroStates = aggroQuery.ToComponentDataArray<AggroState>(Allocator.Temp);
            for (int i = 0; i < aggroStates.Length; i++)
            {
                if (aggroStates[i].IsAggroed)
                {
                    _aggroedCount++;
                    totalThreat += aggroStates[i].CurrentLeaderThreat;
                }
            }
            _avgThreat = _aggroedCount > 0 ? totalThreat / _aggroedCount : 0f;
            aggroStates.Dispose();

            // Casting count
            var execQuery = em.CreateEntityQuery(typeof(AbilityExecutionState));
            var execStates = execQuery.ToComponentDataArray<AbilityExecutionState>(Allocator.Temp);
            for (int i = 0; i < execStates.Length; i++)
            {
                if (execStates[i].Phase != AbilityCastPhase.Idle) _castingCount++;
            }
            execStates.Dispose();
        }

        private void DrawStateDistribution()
        {
            AIWorkstationStyles.DrawSectionHeader("State Distribution");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Total count
            EditorGUILayout.LabelField($"Total AI Entities: {_totalAI}", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Color-coded counts in two rows
            EditorGUILayout.BeginHorizontal();
            AIWorkstationStyles.DrawStatBox("Idle", _idleCount.ToString(), AIWorkstationStyles.IdleColor);
            AIWorkstationStyles.DrawStatBox("Patrol", _patrolCount.ToString(), AIWorkstationStyles.PatrolColor);
            AIWorkstationStyles.DrawStatBox("Combat", _combatCount.ToString(), AIWorkstationStyles.CombatColor);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            AIWorkstationStyles.DrawStatBox("Return", _returnHomeCount.ToString(), AIWorkstationStyles.ReturnHomeColor);
            AIWorkstationStyles.DrawStatBox("Investigate", _investigateCount.ToString(), AIWorkstationStyles.InvestigateColor);
            AIWorkstationStyles.DrawStatBox("Flee", _fleeCount.ToString(), AIWorkstationStyles.FleeColor);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawCombatStats()
        {
            AIWorkstationStyles.DrawSectionHeader("Combat Stats");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            AIWorkstationStyles.DrawStatBox("Aggroed", _aggroedCount.ToString(), Color.red);
            AIWorkstationStyles.DrawStatBox("Avg Threat", $"{_avgThreat:F1}", Color.yellow);
            AIWorkstationStyles.DrawStatBox("Casting", _castingCount.ToString(), AIWorkstationStyles.CastingColor);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawStateBar()
        {
            if (_totalAI == 0) return;

            AIWorkstationStyles.DrawSectionHeader("Distribution Bar");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var rect = GUILayoutUtility.GetRect(0, 24, GUILayout.ExpandWidth(true));
            float total = _totalAI;

            float x = rect.x;
            DrawSegment(ref x, rect.y, rect.height, rect.width * (_idleCount / total), AIWorkstationStyles.IdleColor);
            DrawSegment(ref x, rect.y, rect.height, rect.width * (_patrolCount / total), AIWorkstationStyles.PatrolColor);
            DrawSegment(ref x, rect.y, rect.height, rect.width * (_combatCount / total), AIWorkstationStyles.CombatColor);
            DrawSegment(ref x, rect.y, rect.height, rect.width * (_returnHomeCount / total), AIWorkstationStyles.ReturnHomeColor);
            DrawSegment(ref x, rect.y, rect.height, rect.width * (_investigateCount / total), AIWorkstationStyles.InvestigateColor);
            DrawSegment(ref x, rect.y, rect.height, rect.width * (_fleeCount / total), AIWorkstationStyles.FleeColor);

            // Legend
            EditorGUILayout.BeginHorizontal();
            DrawLegendDot("Idle", AIWorkstationStyles.IdleColor);
            DrawLegendDot("Patrol", AIWorkstationStyles.PatrolColor);
            DrawLegendDot("Combat", AIWorkstationStyles.CombatColor);
            DrawLegendDot("Return", AIWorkstationStyles.ReturnHomeColor);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSegment(ref float x, float y, float height, float width, Color color)
        {
            if (width < 1f) return;
            EditorGUI.DrawRect(new Rect(x, y, width, height), color);
            x += width;
        }

        private void DrawLegendDot(string label, Color color)
        {
            var rect = GUILayoutUtility.GetRect(10, 10, GUILayout.Width(10));
            EditorGUI.DrawRect(rect, color);
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(50));
        }
    }
}
