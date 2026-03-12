using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Vision.Components;

#pragma warning disable CS0414 // Private fields assigned but read via Inspector
namespace DIG.Vision.Debug
{
    /// <summary>
    /// Runtime debug tool for the Vision system. Attach to a GameObject in the scene
    /// to visualize vision cones and list seen targets in the Inspector.
    /// Follows the TargetingModeTester pattern.
    /// EPIC 15.17: Vision / Line-of-Sight System
    /// </summary>
    [AddComponentMenu("DIG/Detection/Debug/Detection Debug Tester")]
    public class VisionDebugTester : MonoBehaviour
    {
        [Header("Debug Display")]
        [Tooltip("Draw vision cones as gizmos in Scene view.")]
        public bool DrawVisionCones = true;

        [Tooltip("Draw lines to currently seen targets.")]
        public bool DrawSeenTargetLines = true;

        [Tooltip("Color for the vision cone wireframe.")]
        public Color ConeColor = new Color(1f, 1f, 0f, 0.3f);

        [Tooltip("Color for lines to visible targets.")]
        public Color VisibleLineColor = Color.green;

        [Tooltip("Color for lines to remembered (not currently visible) targets.")]
        public Color MemoryLineColor = Color.yellow;

        [Header("Runtime Info (Read-Only)")]
        [SerializeField] private int _totalSensors;
        [SerializeField] private int _totalSeenTargets;
        [SerializeField] private int _totalVisibleNow;

        [Header("Override Settings")]
        [Tooltip("Override the global update interval at runtime for testing.")]
        public bool OverrideUpdateInterval;
        [Range(0.01f, 2f)]
        public float OverrideInterval = 0.1f;

        [Tooltip("Override stealth multiplier on all Detectable entities for testing.")]
        public bool OverrideStealthMultiplier;
        [Range(0f, 1f)]
        public float OverrideStealth = 1.0f;

        private void Update()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;

            // Apply overrides
            if (OverrideUpdateInterval)
            {
                ApplyUpdateIntervalOverride(em);
            }

            if (OverrideStealthMultiplier)
            {
                ApplyStealthOverride(em);
            }

            // Gather stats
            GatherStats(em);
        }

        private void ApplyUpdateIntervalOverride(EntityManager em)
        {
            var query = em.CreateEntityQuery(typeof(DetectionSensor));
            if (query.IsEmpty) return;

            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var entity in entities)
            {
                var sensor = em.GetComponentData<DetectionSensor>(entity);
                sensor.UpdateInterval = OverrideInterval;
                em.SetComponentData(entity, sensor);
            }
        }

        private void ApplyStealthOverride(EntityManager em)
        {
            var query = em.CreateEntityQuery(typeof(Detectable));
            if (query.IsEmpty) return;

            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var entity in entities)
            {
                var detectable = em.GetComponentData<Detectable>(entity);
                detectable.StealthMultiplier = OverrideStealth;
                em.SetComponentData(entity, detectable);
            }
        }

        private void GatherStats(EntityManager em)
        {
            _totalSensors = 0;
            _totalSeenTargets = 0;
            _totalVisibleNow = 0;

            var query = em.CreateEntityQuery(typeof(DetectionSensor), typeof(SeenTargetElement));
            if (query.IsEmpty) return;

            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            _totalSensors = entities.Length;

            foreach (var entity in entities)
            {
                var buffer = em.GetBuffer<SeenTargetElement>(entity, true);
                _totalSeenTargets += buffer.Length;

                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i].IsVisibleNow)
                        _totalVisibleNow++;
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            if (!DrawVisionCones && !DrawSeenTargetLines) return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(typeof(DetectionSensor), typeof(LocalTransform), typeof(SeenTargetElement));
            if (query.IsEmpty) return;

            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

            foreach (var entity in entities)
            {
                var sensor = em.GetComponentData<DetectionSensor>(entity);
                var transform = em.GetComponentData<LocalTransform>(entity);

                float3 eyePos = transform.Position + new float3(0f, sensor.EyeHeight, 0f);
                float3 forward = math.normalize(math.forward(transform.Rotation));

                // Draw vision cone
                if (DrawVisionCones)
                {
                    Gizmos.color = ConeColor;
                    DrawWireCone(eyePos, forward, sensor.ViewAngle, sensor.ViewDistance);
                }

                // Draw lines to seen targets
                if (DrawSeenTargetLines)
                {
                    var buffer = em.GetBuffer<SeenTargetElement>(entity, true);
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        var entry = buffer[i];
                        Gizmos.color = entry.IsVisibleNow ? VisibleLineColor : MemoryLineColor;
                        Gizmos.DrawLine(eyePos, entry.LastKnownPosition);

                        // Small sphere at last known position
                        Gizmos.DrawWireSphere(entry.LastKnownPosition, 0.3f);
                    }
                }
            }
        }

        private static void DrawWireCone(float3 origin, float3 forward, float halfAngleDeg, float distance)
        {
            float halfAngleRad = math.radians(halfAngleDeg);
            float radius = distance * math.tan(halfAngleRad);
            float3 endCenter = origin + forward * distance;

            // Get perpendicular axes
            float3 up = math.abs(math.dot(forward, new float3(0, 1, 0))) > 0.99f
                ? new float3(1, 0, 0)
                : new float3(0, 1, 0);
            float3 right = math.normalize(math.cross(forward, up));
            up = math.normalize(math.cross(right, forward));

            // Draw cone edges
            int segments = 16;
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * math.PI * 2f;
                float nextAngle = (float)(i + 1) / segments * math.PI * 2f;

                float3 point = endCenter + (right * math.cos(angle) + up * math.sin(angle)) * radius;
                float3 nextPoint = endCenter + (right * math.cos(nextAngle) + up * math.sin(nextAngle)) * radius;

                // Circle at far end
                Gizmos.DrawLine(point, nextPoint);

                // Lines from origin to edge (every 4th segment)
                if (i % 4 == 0)
                    Gizmos.DrawLine(origin, point);
            }

            // Center line
            Gizmos.DrawLine(origin, endCenter);
        }
    }
}
