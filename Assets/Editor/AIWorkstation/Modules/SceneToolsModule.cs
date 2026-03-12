using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using DIG.AI.Components;
using DIG.Aggro.Components;
using DIG.Vision.Components;

namespace DIG.Editor.AIWorkstation.Modules
{
    /// <summary>
    /// Scene Tools: Toggle interactive Scene view gizmos for AI entities.
    /// Draws patrol radius, leash, detection range, melee range, and threat lines.
    /// </summary>
    public class SceneToolsModule : IAIWorkstationModule
    {
        private Entity _entity = Entity.Null;
        private EntityManager _em;

        // Toggle flags
        private bool _showPatrolRadius = true;
        private bool _showLeashRadius = true;
        private bool _showDetectionRange;
        private bool _showMeleeRange = true;
        private bool _showThreatLines = true;
        private bool _showAllThreatLines;

        public void OnEntityChanged(Entity entity, EntityManager entityManager)
        {
            _entity = entity;
            _em = entityManager;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Scene Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Toggle gizmos drawn in the Scene view for the selected AI entity. Some gizmos can show for all entities.",
                MessageType.Info);
            EditorGUILayout.Space(6);

            AIWorkstationStyles.DrawSectionHeader("Selected Entity Gizmos");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _showPatrolRadius = EditorGUILayout.Toggle("Patrol Radius", _showPatrolRadius);
            _showLeashRadius = EditorGUILayout.Toggle("Leash Radius", _showLeashRadius);
            _showDetectionRange = EditorGUILayout.Toggle("Detection Range (Vision Cone)", _showDetectionRange);
            _showMeleeRange = EditorGUILayout.Toggle("Melee Range", _showMeleeRange);
            _showThreatLines = EditorGUILayout.Toggle("Threat Lines (Selected)", _showThreatLines);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            AIWorkstationStyles.DrawSectionHeader("Global Gizmos");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _showAllThreatLines = EditorGUILayout.Toggle("All Threat Lines (All Aggroed)", _showAllThreatLines);
            EditorGUILayout.EndVertical();

            if (!Application.isPlaying)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox("Scene gizmos appear during Play mode.", MessageType.Info);
            }
        }

        public void OnSceneGUI(SceneView sceneView)
        {
            if (!Application.isPlaying) return;

            var world = AIWorkstationWindow.GetAIWorld();
            if (world == null || !world.IsCreated) return;
            _em = world.EntityManager;

            // Per-entity gizmos for selected
            if (_entity != Entity.Null && _em.Exists(_entity) && _em.HasComponent<LocalTransform>(_entity))
            {
                var pos = _em.GetComponentData<LocalTransform>(_entity).Position;

                if (_showPatrolRadius) DrawPatrolRadius(pos);
                if (_showLeashRadius) DrawLeashRadius(pos);
                if (_showDetectionRange) DrawDetectionRange(pos);
                if (_showMeleeRange) DrawMeleeRange(pos);
                if (_showThreatLines) DrawThreatLinesForEntity(_entity, pos);
            }

            // Global threat lines
            if (_showAllThreatLines)
            {
                DrawAllThreatLines();
            }
        }

        private void DrawPatrolRadius(float3 entityPos)
        {
            if (!_em.HasComponent<AIBrain>(_entity)) return;
            if (!_em.HasComponent<SpawnPosition>(_entity)) return;

            var spawnPos = _em.GetComponentData<SpawnPosition>(_entity);
            if (!spawnPos.IsInitialized) return;

            var brain = _em.GetComponentData<AIBrain>(_entity);

            Handles.color = new Color(0.3f, 0.6f, 0.9f, 0.3f);
            Handles.DrawWireDisc((Vector3)(float3)spawnPos.Position, Vector3.up, brain.PatrolRadius);
            Handles.color = new Color(0.3f, 0.6f, 0.9f, 0.08f);
            Handles.DrawSolidDisc((Vector3)(float3)spawnPos.Position, Vector3.up, brain.PatrolRadius);
        }

        private void DrawLeashRadius(float3 entityPos)
        {
            if (!_em.HasComponent<AggroConfig>(_entity)) return;
            if (!_em.HasComponent<SpawnPosition>(_entity)) return;

            var spawnPos = _em.GetComponentData<SpawnPosition>(_entity);
            if (!spawnPos.IsInitialized) return;

            var config = _em.GetComponentData<AggroConfig>(_entity);
            if (config.LeashDistance <= 0f) return;

            float distFromSpawn = math.distance(entityPos, spawnPos.Position);
            float ratio = distFromSpawn / config.LeashDistance;
            Color leashColor = ratio < 0.6f ? new Color(0.3f, 0.9f, 0.3f, 0.2f)
                : ratio < 0.85f ? new Color(0.9f, 0.9f, 0.2f, 0.3f)
                : new Color(0.9f, 0.2f, 0.2f, 0.4f);

            Handles.color = leashColor;
            Handles.DrawWireDisc((Vector3)(float3)spawnPos.Position, Vector3.up, config.LeashDistance);

            // Spawn position marker
            Handles.color = Color.white;
            Handles.DrawWireCube((Vector3)(float3)spawnPos.Position, Vector3.one * 0.3f);
        }

        private void DrawDetectionRange(float3 entityPos)
        {
            if (!_em.HasComponent<DetectionSensor>(_entity)) return;

            var sensor = _em.GetComponentData<DetectionSensor>(_entity);
            float3 eyePos = entityPos + new float3(0, sensor.EyeHeight, 0);

            // Forward direction from entity rotation
            float3 forward = new float3(0, 0, 1);
            if (_em.HasComponent<LocalTransform>(_entity))
            {
                var rot = _em.GetComponentData<LocalTransform>(_entity).Rotation;
                forward = math.mul(rot, new float3(0, 0, 1));
            }

            // Draw cone wireframe
            Handles.color = new Color(0.9f, 0.7f, 0.2f, 0.3f);
            float halfAngleRad = math.radians(sensor.ViewAngle);

            // Arc at max distance
            Vector3 fwd = ((Vector3)(float3)forward).normalized;
            Handles.DrawWireArc((Vector3)eyePos, Vector3.up,
                Quaternion.AngleAxis(-sensor.ViewAngle, Vector3.up) * fwd,
                sensor.ViewAngle * 2f, sensor.ViewDistance);

            // Side lines
            Vector3 leftDir = Quaternion.AngleAxis(-sensor.ViewAngle, Vector3.up) * fwd;
            Vector3 rightDir = Quaternion.AngleAxis(sensor.ViewAngle, Vector3.up) * fwd;
            Handles.DrawLine((Vector3)eyePos, (Vector3)eyePos + leftDir * sensor.ViewDistance);
            Handles.DrawLine((Vector3)eyePos, (Vector3)eyePos + rightDir * sensor.ViewDistance);
        }

        private void DrawMeleeRange(float3 entityPos)
        {
            if (!_em.HasComponent<AIBrain>(_entity)) return;

            float meleeRange = _em.GetComponentData<AIBrain>(_entity).MeleeRange;
            Handles.color = new Color(0.9f, 0.2f, 0.2f, 0.25f);
            Handles.DrawWireDisc((Vector3)entityPos, Vector3.up, meleeRange);
        }

        private void DrawThreatLinesForEntity(Entity entity, float3 pos)
        {
            if (!_em.HasBuffer<ThreatEntry>(entity)) return;
            if (!_em.HasComponent<AggroState>(entity)) return;

            var buffer = _em.GetBuffer<ThreatEntry>(entity, true);
            var aggroState = _em.GetComponentData<AggroState>(entity);

            for (int i = 0; i < buffer.Length; i++)
            {
                var entry = buffer[i];
                bool isLeader = entry.SourceEntity == aggroState.CurrentThreatLeader;

                Handles.color = isLeader ? new Color(1f, 0.2f, 0.2f, 0.8f)
                    : entry.IsCurrentlyVisible ? new Color(1f, 0.8f, 0.2f, 0.5f)
                    : new Color(0.5f, 0.5f, 0.5f, 0.3f);

                float thickness = isLeader ? 3f : 1.5f;
                Handles.DrawLine((Vector3)pos, (Vector3)(float3)entry.LastKnownPosition, thickness);
                Handles.DrawWireCube((Vector3)(float3)entry.LastKnownPosition, Vector3.one * 0.25f);
            }
        }

        private void DrawAllThreatLines()
        {
            var query = _em.CreateEntityQuery(typeof(AggroState), typeof(LocalTransform));
            var entities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var aggroState = _em.GetComponentData<AggroState>(entity);
                if (!aggroState.IsAggroed) continue;
                if (aggroState.CurrentThreatLeader == Entity.Null) continue;

                var pos = _em.GetComponentData<LocalTransform>(entity).Position;

                // Line to current target only (not full threat table, for performance)
                if (_em.Exists(aggroState.CurrentThreatLeader) &&
                    _em.HasComponent<LocalTransform>(aggroState.CurrentThreatLeader))
                {
                    var targetPos = _em.GetComponentData<LocalTransform>(aggroState.CurrentThreatLeader).Position;
                    Handles.color = new Color(1f, 0.2f, 0.2f, 0.15f);
                    Handles.DrawLine((Vector3)pos, (Vector3)targetPos);
                }
            }

            entities.Dispose();
        }
    }
}
