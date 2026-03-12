#if UNITY_EDITOR
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using DIG.AI.Components;
using DIG.Aggro.Components;
using Player.Components;
using System.Collections.Generic;

namespace DIG.AI.Overlay
{
    /// <summary>
    /// Runtime debug overlay: draws world-space labels above AI entities in Scene view.
    /// Editor-only. Reads AIDebugOverlayConfig for what to display.
    /// Collects data in OnUpdate (ECS-safe), draws in SceneView callback (GUI-safe).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class AIDebugOverlaySystem : SystemBase
    {
        private GUIStyle _labelStyle;

        private struct OverlayEntry
        {
            public float3 Position;
            public string Text;
            public AIBehaviorState State;
        }

        private readonly List<OverlayEntry> _entries = new List<OverlayEntry>(256);

        protected override void OnCreate()
        {
            UnityEditor.SceneView.duringSceneGui += DrawOverlay;
        }

        protected override void OnDestroy()
        {
            UnityEditor.SceneView.duringSceneGui -= DrawOverlay;
        }

        protected override void OnUpdate()
        {
            if (!AIDebugOverlayConfig.Enabled)
            {
                _entries.Clear();
                return;
            }

            _entries.Clear();

            var em = EntityManager;

            // Query all AI entities and collect overlay data
            var query = em.CreateEntityQuery(typeof(AIState), typeof(LocalTransform));
            var entities = query.ToEntityArray(Allocator.Temp);
            var aiStates = query.ToComponentDataArray<AIState>(Allocator.Temp);
            var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var ai = aiStates[i];
                var entity = entities[i];
                float3 pos = transforms[i].Position;

                // State filters
                if (AIDebugOverlayConfig.OnlyCombat && ai.CurrentState != AIBehaviorState.Combat) continue;

                bool isAggroed = false;
                float threatValue = 0f;
                Entity targetEntity = Entity.Null;

                if (em.HasComponent<AggroState>(entity))
                {
                    var aggro = em.GetComponentData<AggroState>(entity);
                    isAggroed = aggro.IsAggroed;
                    threatValue = aggro.CurrentLeaderThreat;
                    targetEntity = aggro.CurrentThreatLeader;
                }

                if (AIDebugOverlayConfig.OnlyAggroed && !isAggroed) continue;

                // Build label text
                var sb = new System.Text.StringBuilder(64);

                if (AIDebugOverlayConfig.ShowState)
                    sb.Append(ai.CurrentState.ToString());

                if (AIDebugOverlayConfig.ShowSubState && ai.CurrentState == AIBehaviorState.Combat)
                    sb.Append(':').Append(ai.SubState.ToString());

                if (AIDebugOverlayConfig.ShowThreatValue && isAggroed)
                    sb.Append(" T:").Append(threatValue.ToString("F0"));

                if (AIDebugOverlayConfig.ShowTargetName && targetEntity != Entity.Null)
                    sb.Append(" ->E:").Append(targetEntity.Index);

                if (AIDebugOverlayConfig.ShowActiveAbility && em.HasComponent<AbilityExecutionState>(entity))
                {
                    var exec = em.GetComponentData<AbilityExecutionState>(entity);
                    if (exec.Phase != AbilityCastPhase.Idle)
                        sb.Append(" [").Append(exec.Phase.ToString()).Append(']');
                }

                if (AIDebugOverlayConfig.ShowHealthPercent && em.HasComponent<Health>(entity))
                {
                    var health = em.GetComponentData<Health>(entity);
                    float pct = health.Max > 0 ? (health.Current / health.Max) * 100f : 0f;
                    sb.Append(" HP:").Append(pct.ToString("F0")).Append('%');
                }

                if (sb.Length == 0) continue;

                _entries.Add(new OverlayEntry
                {
                    Position = pos,
                    Text = sb.ToString(),
                    State = ai.CurrentState
                });
            }

            entities.Dispose();
            aiStates.Dispose();
            transforms.Dispose();
        }

        private void DrawOverlay(UnityEditor.SceneView sceneView)
        {
            if (!AIDebugOverlayConfig.Enabled || _entries.Count == 0) return;

            EnsureStyles();

            var camera = sceneView.camera;
            if (camera == null) return;

            float maxDistSq = AIDebugOverlayConfig.MaxCameraDistance * AIDebugOverlayConfig.MaxCameraDistance;
            float3 camPos = camera.transform.position;

            UnityEditor.Handles.BeginGUI();

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];

                // Distance filter (camera-relative, done at draw time for correct camera)
                if (math.distancesq(entry.Position, camPos) > maxDistSq) continue;

                // Project to screen
                Vector3 worldPos = new Vector3(entry.Position.x, entry.Position.y + 2.2f, entry.Position.z);
                Vector3 screenPos = camera.WorldToScreenPoint(worldPos);
                if (screenPos.z < 0) continue;

                float guiY = sceneView.position.height - screenPos.y;

                var content = new GUIContent(entry.Text);
                var size = _labelStyle.CalcSize(content);
                var rect = new Rect(screenPos.x - size.x * 0.5f, guiY - size.y, size.x + 8, size.y + 2);

                var stateColor = AIWorkstationStylesRuntime.GetStateColor(entry.State);
                var bgColor = new Color(0f, 0f, 0f, AIDebugOverlayConfig.BackgroundAlpha);
                UnityEditor.EditorGUI.DrawRect(rect, bgColor);
                UnityEditor.EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2), stateColor);

                _labelStyle.normal.textColor = stateColor;
                GUI.Label(new Rect(rect.x + 4, rect.y + 1, size.x, size.y), entry.Text, _labelStyle);
            }

            UnityEditor.Handles.EndGUI();
        }

        private void EnsureStyles()
        {
            if (_labelStyle != null && _labelStyle.fontSize == AIDebugOverlayConfig.FontSize) return;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = AIDebugOverlayConfig.FontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
        }
    }

    /// <summary>
    /// Runtime-compatible state color mapping (no UnityEditor dependency in the static class itself).
    /// </summary>
    internal static class AIWorkstationStylesRuntime
    {
        public static Color GetStateColor(AIBehaviorState state)
        {
            switch (state)
            {
                case AIBehaviorState.Idle: return new Color(0.6f, 0.6f, 0.6f);
                case AIBehaviorState.Patrol: return new Color(0.3f, 0.7f, 1f);
                case AIBehaviorState.Investigate: return new Color(1f, 0.8f, 0.2f);
                case AIBehaviorState.Combat: return new Color(1f, 0.3f, 0.3f);
                case AIBehaviorState.Flee: return new Color(1f, 0.5f, 1f);
                case AIBehaviorState.ReturnHome: return new Color(0.3f, 1f, 0.3f);
                default: return Color.white;
            }
        }
    }
}
#endif
