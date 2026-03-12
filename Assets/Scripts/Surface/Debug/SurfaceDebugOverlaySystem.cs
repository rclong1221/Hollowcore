using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 16.10 Phase 9: Debug overlay showing surface gameplay data in Scene view.
    /// Per-entity labels with surface name, noise multiplier, speed multiplier.
    /// Editor-only, managed system.
    /// </summary>
#if UNITY_EDITOR
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SurfaceDebugOverlaySystem : SystemBase
    {
        public static bool ShowOverlay;

        protected override void OnCreate()
        {
            RequireForUpdate<GroundSurfaceState>();
        }

        protected override void OnUpdate()
        {
            if (!ShowOverlay) return;

            bool hasConfig = SystemAPI.TryGetSingleton<SurfaceGameplayConfigSingleton>(out var configSingleton);

            foreach (var (groundSurface, transform) in
                SystemAPI.Query<RefRO<GroundSurfaceState>, RefRO<LocalToWorld>>())
            {
                var gs = groundSurface.ValueRO;
                float3 pos = transform.ValueRO.Position + new float3(0, 2.5f, 0);

                string surfaceName = gs.SurfaceId.ToString();
                string label = $"{surfaceName}\nH:{gs.CachedHardness} D:{gs.CachedDensity}";

                if (hasConfig)
                {
                    int idx = (int)gs.SurfaceId;
                    ref var blob = ref configSingleton.Config.Value;
                    if (idx >= 0 && idx < blob.Modifiers.Length)
                    {
                        var mods = blob.Modifiers[idx];
                        label += $"\nNoise:{mods.NoiseMultiplier:F1}x Spd:{mods.SpeedMultiplier:F1}x";
                        if (mods.SlipFactor > 0.01f)
                            label += $"\nSlip:{mods.SlipFactor:F2}";
                    }
                }

                // Color coding by surface type
                Color color;
                if ((gs.Flags & SurfaceFlags.IsSlippery) != 0)
                    color = new Color(0.3f, 0.7f, 1f); // Blue = slippery
                else if ((gs.Flags & SurfaceFlags.IsLiquid) != 0)
                    color = new Color(0.2f, 0.5f, 1f); // Dark blue = liquid
                else if (gs.CachedHardness > 200)
                    color = new Color(1f, 0.6f, 0.3f); // Orange = hard
                else if (gs.CachedHardness < 80)
                    color = new Color(0.3f, 0.9f, 0.3f); // Green = soft
                else
                    color = Color.white;

                DrawLabel(pos, label, color);
            }
        }

        private static void DrawLabel(float3 worldPos, string text, Color color)
        {
#if UNITY_EDITOR
            var style = new GUIStyle(UnityEditor.EditorStyles.label)
            {
                normal = { textColor = color },
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter
            };
            UnityEditor.Handles.Label(worldPos, text, style);
#endif
        }
    }
#endif
}
