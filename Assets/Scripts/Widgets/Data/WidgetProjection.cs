using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Widgets
{
    /// <summary>
    /// EPIC 15.26 Phase 1: Result of projecting a widget-bearing entity to screen space.
    /// Computed by WidgetProjectionSystem each frame. Read by WidgetBridgeSystem.
    /// </summary>
    public struct WidgetProjection
    {
        /// <summary>Entity this projection belongs to.</summary>
        public Entity Entity;

        /// <summary>World-space position of the entity (from LocalToWorld).</summary>
        public float3 WorldPos;

        /// <summary>Screen-space position in pixels (0,0 = bottom-left).</summary>
        public float2 ScreenPos;

        /// <summary>Distance from camera to entity in meters.</summary>
        public float Distance;

        /// <summary>Computed importance score (higher = more important, shown first).</summary>
        public float Importance;

        /// <summary>Distance-based LOD tier.</summary>
        public WidgetLODTier LOD;

        /// <summary>Whether this widget passed budget enforcement and should be rendered.</summary>
        public bool IsVisible;

        /// <summary>Active widget types for this entity.</summary>
        public WidgetFlags ActiveFlags;

        /// <summary>Paradigm-scaled widget size multiplier.</summary>
        public float Scale;

        /// <summary>Normalized health (0-1). Cached for dirty-checking.</summary>
        public float Health01;

        /// <summary>Maximum health. Cached for adapters.</summary>
        public float MaxHealth;

        /// <summary>Current health. Cached for adapters.</summary>
        public float CurrentHealth;

        /// <summary>Y offset applied to world position for widget anchor.</summary>
        public float YOffset;

        /// <summary>Whether entity is off-screen (failed frustum test but tracked).</summary>
        public bool IsOffScreen;

        /// <summary>Whether entity is currently in combat (from CombatState).</summary>
        public bool IsInCombat;

        /// <summary>Seconds since combat ended (0 if in combat, 100+ if never).</summary>
        public float TimeSinceCombatEnded;
    }
}
