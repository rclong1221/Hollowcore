using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DIG.Combat.Resources.Debug
{
    /// <summary>
    /// EPIC 16.8 Phase 7: Debug overlay showing resource pool state above entities.
    /// Color-coded by resource type. Editor-only, managed system.
    /// </summary>
#if UNITY_EDITOR
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ResourceDebugSystem : SystemBase
    {
        public static bool ShowOverlay;

        protected override void OnCreate()
        {
            RequireForUpdate<ResourcePool>();
        }

        protected override void OnUpdate()
        {
            if (!ShowOverlay) return;

            foreach (var (pool, transform) in
                SystemAPI.Query<RefRO<ResourcePool>, RefRO<LocalToWorld>>())
            {
                float3 pos = transform.ValueRO.Position + new float3(0, 3f, 0);
                var p = pool.ValueRO;

                string label = "";
                if (p.Slot0.Type != ResourceType.None)
                {
                    label += FormatSlot(p.Slot0);
                }
                if (p.Slot1.Type != ResourceType.None)
                {
                    if (label.Length > 0) label += "\n";
                    label += FormatSlot(p.Slot1);
                }

                if (label.Length > 0)
                {
                    Color color = GetColor(p.Slot0.Type != ResourceType.None ? p.Slot0.Type : p.Slot1.Type);
                    DrawLabel(pos, label, color);
                }
            }
        }

        private static string FormatSlot(ResourceSlot slot)
        {
            string name = slot.Type.ToString();
            if ((slot.Flags & ResourceFlags.IsInteger) != 0)
                return $"{name}: {(int)slot.Current}/{(int)slot.Max}";
            return $"{name}: {slot.Current:F0}/{slot.Max:F0}";
        }

        private static Color GetColor(ResourceType type)
        {
            return type switch
            {
                ResourceType.Stamina => new Color(0.3f, 0.9f, 0.3f),  // Green
                ResourceType.Mana    => new Color(0.3f, 0.5f, 1.0f),  // Blue
                ResourceType.Energy  => new Color(1.0f, 0.9f, 0.2f),  // Yellow
                ResourceType.Rage    => new Color(1.0f, 0.2f, 0.2f),  // Red
                ResourceType.Combo   => new Color(0.7f, 0.3f, 1.0f),  // Purple
                _ => Color.white
            };
        }

        private static void DrawLabel(float3 worldPos, string text, Color color)
        {
            var style = new GUIStyle(UnityEditor.EditorStyles.label)
            {
                normal = { textColor = color },
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter
            };
            UnityEditor.Handles.Label(worldPos, text, style);
        }
    }
#endif
}
