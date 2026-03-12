using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Scene view gizmo overlay for active knockback visualization.
    /// Draws velocity arrows and immunity spheres on entities with active KnockbackState.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class KnockbackDebugSystem : SystemBase
    {
        public static bool Enabled;

        protected override void OnCreate()
        {
            RequireForUpdate<KnockbackState>();
        }

        protected override void OnUpdate()
        {
            if (!Enabled) return;

            foreach (var (knockbackState, transform) in
                SystemAPI.Query<RefRO<KnockbackState>, RefRO<LocalTransform>>())
            {
                var kb = knockbackState.ValueRO;
                float3 pos = transform.ValueRO.Position + new float3(0, 1f, 0); // Offset to entity center

                if (kb.IsActive)
                {
                    // Color by type
                    Color color = kb.Type switch
                    {
                        KnockbackType.Push => Color.cyan,
                        KnockbackType.Launch => Color.yellow,
                        KnockbackType.Pull => Color.magenta,
                        KnockbackType.Stagger => Color.red,
                        _ => Color.white
                    };

                    // Draw velocity arrow
                    float3 dir = math.normalizesafe(kb.Velocity, float3.zero);
                    float speed = math.length(kb.Velocity);
                    float arrowLength = math.min(speed * 0.3f, 3f);

                    Debug.DrawRay(pos, (Vector3)(dir * arrowLength), color);

                    // Draw arrowhead
                    float3 right = math.cross(dir, new float3(0, 1, 0));
                    right = math.normalizesafe(right, new float3(1, 0, 0));
                    float3 arrowTip = pos + dir * arrowLength;
                    Debug.DrawRay(arrowTip, (Vector3)((-dir + right * 0.3f) * arrowLength * 0.2f), color);
                    Debug.DrawRay(arrowTip, (Vector3)((-dir - right * 0.3f) * arrowLength * 0.2f), color);
                }
            }

            // Draw immunity spheres
            foreach (var (resistance, transform) in
                SystemAPI.Query<RefRO<KnockbackResistance>, RefRO<LocalTransform>>())
            {
                if (resistance.ValueRO.ImmunityTimeRemaining > 0f)
                {
                    float3 pos = transform.ValueRO.Position + new float3(0, 1f, 0);
                    float alpha = resistance.ValueRO.ImmunityTimeRemaining / math.max(resistance.ValueRO.ImmunityDuration, 0.01f);
                    Color immuneColor = new Color(0.3f, 0.3f, 1f, alpha);
                    // Wire sphere approximation using Debug.DrawLine
                    DrawWireCircle(pos, 0.5f, immuneColor);
                }
            }
        }

        private static void DrawWireCircle(float3 center, float radius, Color color)
        {
            const int segments = 16;
            for (int i = 0; i < segments; i++)
            {
                float a0 = (float)i / segments * math.PI * 2f;
                float a1 = (float)(i + 1) / segments * math.PI * 2f;
                float3 p0 = center + new float3(math.cos(a0) * radius, 0, math.sin(a0) * radius);
                float3 p1 = center + new float3(math.cos(a1) * radius, 0, math.sin(a1) * radius);
                Debug.DrawLine(p0, p1, color);
            }
        }
    }
}
