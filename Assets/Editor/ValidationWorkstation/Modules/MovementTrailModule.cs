using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace DIG.Validation.Editor.Modules
{
    /// <summary>
    /// EPIC 17.11: SceneView overlay drawing player movement trails.
    /// Color-coded by validation result (green=valid, yellow=error, red=violation).
    /// Shows teleport immunity windows.
    /// </summary>
    public class MovementTrailModule : IValidationWorkstationModule
    {
        public string ModuleName => "Movement Trail";

        private const int MaxTrailLength = 120;
        private float3[][] _trails;
        private int[] _trailHeads;
        private int[] _trailCounts;
        private int _playerCount;
        private bool _enabled = true;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Movement Trail Overlay", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _enabled = EditorGUILayout.Toggle("Enable Overlay", _enabled);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to see movement trails in Scene view.", MessageType.Info);
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
                ComponentType.ReadOnly<ValidationLink>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<GhostOwner>());

            int count = query.CalculateEntityCount();
            EditorGUILayout.LabelField($"Tracked players: {count}");

            if (count > 0)
            {
                var links = query.ToComponentDataArray<ValidationLink>(Allocator.Temp);
                for (int i = 0; i < Mathf.Min(count, 8); i++)
                {
                    var child = links[i].ValidationChild;
                    if (child != Entity.Null && em.HasComponent<MovementValidationState>(child))
                    {
                        var state = em.GetComponentData<MovementValidationState>(child);
                        EditorGUILayout.BeginHorizontal("box");
                        EditorGUILayout.LabelField($"Player {i}", GUILayout.Width(70));
                        EditorGUILayout.LabelField($"Error: {state.AccumulatedError:F2}", GUILayout.Width(100));
                        EditorGUILayout.LabelField($"Pos: ({state.LastValidatedPosition.x:F1}, {state.LastValidatedPosition.y:F1}, {state.LastValidatedPosition.z:F1})");
                        EditorGUILayout.EndHorizontal();
                    }
                }
                links.Dispose();
            }

            if (GUILayout.Button("Clear Trails"))
            {
                _trails = null;
                _trailHeads = null;
                _trailCounts = null;
                _playerCount = 0;
            }
        }

        public void OnSceneGUI(SceneView sceneView)
        {
            if (!_enabled || !Application.isPlaying) return;

            var world = ValidationWorkstationWindow.GetValidationWorld();
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<ValidationLink>(),
                ComponentType.ReadOnly<LocalTransform>());

            int count = query.CalculateEntityCount();
            if (count == 0) return;

            // Initialize or resize trail arrays
            if (_trails == null || _playerCount != count)
            {
                _playerCount = count;
                _trails = new float3[count][];
                _trailHeads = new int[count];
                _trailCounts = new int[count];
                for (int i = 0; i < count; i++)
                    _trails[i] = new float3[MaxTrailLength];
            }

            var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var links = query.ToComponentDataArray<ValidationLink>(Allocator.Temp);

            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            for (int i = 0; i < Mathf.Min(count, _playerCount); i++)
            {
                float3 pos = transforms[i].Position;

                // Record position
                _trails[i][_trailHeads[i]] = pos;
                _trailHeads[i] = (_trailHeads[i] + 1) % MaxTrailLength;
                if (_trailCounts[i] < MaxTrailLength) _trailCounts[i]++;

                // Get error for color
                float error = 0f;
                var child = links[i].ValidationChild;
                if (child != Entity.Null && em.HasComponent<MovementValidationState>(child))
                    error = em.GetComponentData<MovementValidationState>(child).AccumulatedError;

                Color lineColor = error > 5f ? Color.red :
                                  error > 1f ? Color.yellow : Color.green;

                // Draw trail
                Handles.color = lineColor;
                int drawn = 0;
                int idx = (_trailHeads[i] - _trailCounts[i] + MaxTrailLength) % MaxTrailLength;
                Vector3 prev = (Vector3)_trails[i][idx];

                for (int j = 1; j < _trailCounts[i]; j++)
                {
                    int nextIdx = (idx + j) % MaxTrailLength;
                    Vector3 next = (Vector3)_trails[i][nextIdx];
                    Handles.DrawLine(prev, next);
                    prev = next;
                    drawn++;
                }

                // Draw current position sphere
                Handles.color = lineColor;
                Handles.SphereHandleCap(0, (Vector3)pos, Quaternion.identity, 0.3f, EventType.Repaint);
            }

            transforms.Dispose();
            links.Dispose();
        }
    }
}
