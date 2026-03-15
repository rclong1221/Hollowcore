#if UNITY_EDITOR
using Hollowcore.Chassis;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace Hollowcore.Chassis.Systems
{
    /// <summary>
    /// Debug overlay for chassis state visualization in Scene view.
    /// Shows per-slot integrity bars, slot labels, destroyed indicators,
    /// and penalty state text.
    /// Toggle via: Hollowcore > Chassis > Toggle Debug Overlay
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ChassisDebugOverlaySystem : SystemBase
    {
        private static bool _enabled;
        private readonly List<OverlayEntry> _entries = new(32);

        private struct OverlayEntry
        {
            public Vector3 Position;
            public string Label;
            public Color Color;
            public float IntegrityPct;
            public bool IsDestroyed;
            public bool IsEmpty;
        }

        [MenuItem("Hollowcore/Chassis/Toggle Debug Overlay")]
        public static void ToggleOverlay() => _enabled = !_enabled;

        protected override void OnCreate()
        {
            SceneView.duringSceneGui += DrawOverlay;
        }

        protected override void OnDestroy()
        {
            SceneView.duringSceneGui -= DrawOverlay;
        }

        protected override void OnUpdate()
        {
            _entries.Clear();
            if (!_enabled) return;

            var limbLookup = GetComponentLookup<LimbInstance>(true);

            foreach (var (chassisLink, localTransform) in
                     SystemAPI.Query<RefRO<ChassisLink>, RefRO<LocalTransform>>())
            {
                var chassisEntity = chassisLink.ValueRO.ChassisEntity;
                if (chassisEntity == Entity.Null) continue;
                if (!EntityManager.HasComponent<ChassisState>(chassisEntity)) continue;

                var state = EntityManager.GetComponentData<ChassisState>(chassisEntity);
                var basePos = (Vector3)localTransform.ValueRO.Position + Vector3.up * 2.2f;

                string[] labels = { "H", "T", "LA", "RA", "LL", "RL" };
                float spacing = 0.15f;
                float startX = -spacing * 2.5f;

                for (int i = 0; i <= (int)ChassisSlot.RightLeg; i++)
                {
                    var slot = (ChassisSlot)i;
                    var slotEntity = state.GetSlot(slot);
                    var pos = basePos + Vector3.right * (startX + i * spacing);

                    var entry = new OverlayEntry
                    {
                        Position = pos,
                        Label = labels[i],
                        IsDestroyed = state.IsSlotDestroyed(slot),
                        IsEmpty = slotEntity == Entity.Null
                    };

                    if (entry.IsDestroyed)
                    {
                        entry.Color = Color.red;
                        entry.IntegrityPct = 0f;
                    }
                    else if (entry.IsEmpty)
                    {
                        entry.Color = Color.gray;
                        entry.IntegrityPct = 0f;
                    }
                    else if (limbLookup.HasComponent(slotEntity))
                    {
                        var limb = limbLookup[slotEntity];
                        entry.IntegrityPct = limb.MaxIntegrity > 0
                            ? limb.CurrentIntegrity / limb.MaxIntegrity
                            : 0f;
                        entry.Color = Color.Lerp(Color.red, Color.green, entry.IntegrityPct);
                    }
                    else
                    {
                        entry.Color = Color.yellow;
                        entry.IntegrityPct = 1f;
                    }

                    _entries.Add(entry);
                }
            }
        }

        private void DrawOverlay(SceneView sceneView)
        {
            if (!_enabled || _entries.Count == 0) return;

            Handles.BeginGUI();

            foreach (var entry in _entries)
            {
                var screenPos = HandleUtility.WorldToGUIPoint(entry.Position);

                // Integrity bar
                float barWidth = 16f;
                float barHeight = 30f;
                var barBg = new Rect(screenPos.x - barWidth / 2, screenPos.y - barHeight, barWidth, barHeight);
                EditorGUI.DrawRect(barBg, new Color(0.1f, 0.1f, 0.1f, 0.8f));

                if (!entry.IsEmpty && !entry.IsDestroyed)
                {
                    float fillHeight = barHeight * entry.IntegrityPct;
                    var barFill = new Rect(barBg.x + 1, barBg.yMax - fillHeight - 1, barWidth - 2, fillHeight);
                    EditorGUI.DrawRect(barFill, entry.Color);
                }
                else if (entry.IsDestroyed)
                {
                    var style = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.red },
                        fontStyle = FontStyle.Bold,
                        fontSize = 12
                    };
                    EditorGUI.LabelField(barBg, "X", style);
                }

                // Label
                var labelRect = new Rect(screenPos.x - 10, screenPos.y + 2, 20, 14);
                var labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = entry.Color },
                    fontSize = 9
                };
                EditorGUI.LabelField(labelRect, entry.Label, labelStyle);
            }

            Handles.EndGUI();
        }
    }
}
#endif
