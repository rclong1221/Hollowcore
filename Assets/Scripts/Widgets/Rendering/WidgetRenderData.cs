using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Widgets.Rendering
{
    /// <summary>
    /// EPIC 15.26 Phase 2: Data passed from WidgetBridgeSystem to IWidgetRenderer adapters.
    /// Contains all information needed to render/update a single widget.
    /// </summary>
    public struct WidgetRenderData
    {
        /// <summary>Entity this widget belongs to.</summary>
        public Entity Entity;

        /// <summary>World-space position of the entity root.</summary>
        public float3 WorldPos;

        /// <summary>Screen-space position in pixels (0,0 = bottom-left).</summary>
        public float2 ScreenPos;

        /// <summary>Normalized health (0-1).</summary>
        public float Health01;

        /// <summary>Current health value.</summary>
        public float CurrentHealth;

        /// <summary>Maximum health value.</summary>
        public float MaxHealth;

        /// <summary>Distance from camera in meters.</summary>
        public float Distance;

        /// <summary>Computed importance score.</summary>
        public float Importance;

        /// <summary>Distance-based LOD tier.</summary>
        public WidgetLODTier LOD;

        /// <summary>Paradigm-scaled widget size multiplier.</summary>
        public float Scale;

        /// <summary>Billboard orientation mode for this frame.</summary>
        public BillboardMode Billboard;

        /// <summary>Active widget type flags on this entity.</summary>
        public WidgetFlags ActiveFlags;

        /// <summary>Y offset applied to world position for widget anchor.</summary>
        public float YOffset;

        /// <summary>Whether entity is currently in combat.</summary>
        public bool IsInCombat;

        /// <summary>Seconds since combat ended (0 if in combat).</summary>
        public float TimeSinceCombatEnded;

        /// <summary>
        /// Create WidgetRenderData from a WidgetProjection and paradigm settings.
        /// </summary>
        public static WidgetRenderData FromProjection(in WidgetProjection proj, BillboardMode billboard)
        {
            return new WidgetRenderData
            {
                Entity = proj.Entity,
                WorldPos = proj.WorldPos,
                ScreenPos = proj.ScreenPos,
                Health01 = proj.Health01,
                CurrentHealth = proj.CurrentHealth,
                MaxHealth = proj.MaxHealth,
                Distance = proj.Distance,
                Importance = proj.Importance,
                LOD = proj.LOD,
                Scale = proj.Scale,
                Billboard = billboard,
                ActiveFlags = proj.ActiveFlags,
                YOffset = proj.YOffset,
                IsInCombat = proj.IsInCombat,
                TimeSinceCombatEnded = proj.TimeSinceCombatEnded
            };
        }
    }
}
