using Hollowcore.Chassis;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace Hollowcore.Editor.Chassis
{
    /// <summary>
    /// Draws chassis slot gizmos in the Scene view when a player entity is selected.
    /// Shows wireframe socket positions with color indicating slot state.
    /// </summary>
    public static class ChassisGizmoDrawer
    {
        private static readonly Vector3[] SlotOffsets =
        {
            new Vector3(0f, 1.8f, 0f),      // Head
            new Vector3(0f, 1.2f, 0f),      // Torso
            new Vector3(-0.5f, 1.3f, 0f),   // LeftArm
            new Vector3(0.5f, 1.3f, 0f),    // RightArm
            new Vector3(-0.2f, 0.5f, 0f),   // LeftLeg
            new Vector3(0.2f, 0.5f, 0f)     // RightLeg
        };

        private static readonly string[] SlotLabels = { "H", "T", "LA", "RA", "LL", "RL" };

        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void DrawChassisGizmos(Transform transform, GizmoType gizmoType)
        {
            if (!Application.isPlaying) return;

            var worlds = World.All;
            foreach (var world in worlds)
            {
                if (world == null || !world.IsCreated) continue;

                var em = world.EntityManager;
                var query = em.CreateEntityQuery(
                    typeof(ChassisLink),
                    typeof(LocalTransform));

                if (query.IsEmpty) continue;

                var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
                foreach (var entity in entities)
                {
                    var lt = em.GetComponentData<LocalTransform>(entity);
                    var chassisLink = em.GetComponentData<ChassisLink>(entity);

                    if (chassisLink.ChassisEntity == Entity.Null) continue;
                    if (!em.HasComponent<ChassisState>(chassisLink.ChassisEntity)) continue;

                    var state = em.GetComponentData<ChassisState>(chassisLink.ChassisEntity);
                    var basePos = (Vector3)lt.Position;

                    for (int i = 0; i <= (int)ChassisSlot.RightLeg; i++)
                    {
                        var slot = (ChassisSlot)i;
                        var slotEntity = state.GetSlot(slot);
                        var pos = basePos + SlotOffsets[i];

                        Color color;
                        string label;

                        if (state.IsSlotDestroyed(slot))
                        {
                            color = Color.red;
                            label = $"{SlotLabels[i]} [X]";
                        }
                        else if (slotEntity == Entity.Null)
                        {
                            color = Color.gray;
                            label = $"{SlotLabels[i]} [Empty]";
                        }
                        else
                        {
                            color = Color.green;
                            label = SlotLabels[i];
                            if (em.HasComponent<LimbInstance>(slotEntity))
                            {
                                var limb = em.GetComponentData<LimbInstance>(slotEntity);
                                float pct = limb.MaxIntegrity > 0
                                    ? limb.CurrentIntegrity / limb.MaxIntegrity * 100f
                                    : 0f;
                                label = $"{SlotLabels[i]} {limb.DisplayName} {pct:F0}%";

                                // Color by integrity
                                if (pct < 25f) color = Color.red;
                                else if (pct < 50f) color = Color.yellow;
                            }

                            // Draw line to limb entity
                            Gizmos.color = color * 0.5f;
                            Gizmos.DrawLine(basePos, pos);
                        }

                        Gizmos.color = color;
                        Gizmos.DrawWireSphere(pos, 0.08f);
                        Handles.Label(pos + Vector3.up * 0.12f, label,
                            new GUIStyle(GUI.skin.label) { normal = { textColor = color }, fontSize = 10 });
                    }
                }
                entities.Dispose();
            }
        }
    }
}
