using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using DIG.Combat.UI.WorldSpace;
using DIG.Widgets.Config;
using DIG.Widgets.Rendering;
using DIG.Widgets.Systems;

namespace DIG.Widgets.Adapters
{
    /// <summary>
    /// EPIC 15.26 Phase 3: Wraps EnemyHealthBarPool in the IWidgetRenderer adapter pattern.
    /// The widget framework handles projection, culling, and budget enforcement.
    /// This adapter delegates all rendering to the existing MeshRenderer health bar pool.
    ///
    /// When the widget framework is active:
    ///   - WidgetProjectionSystem decides which entities are visible (budget, LOD)
    ///   - WidgetBridgeSystem calls OnWidgetVisible/OnWidgetUpdate/OnWidgetHidden
    ///   - This adapter forwards to EnemyHealthBarPool.ShowHealthBar/HideHealthBar
    ///
    /// When the widget framework is not active:
    ///   - EnemyHealthBarBridgeSystem runs standalone (backward compatible)
    /// </summary>
    public class HealthBarWidgetAdapter : MonoBehaviour, IWidgetRenderer
    {
        public WidgetType SupportedType => WidgetType.HealthBar;

        private EnemyHealthBarPool _pool;

        // Track which entities received ShowHealthBar this frame so we can
        // clean up stale bars in OnFrameEnd (handles transition from old bridge).
        private readonly HashSet<Entity> _visibleThisFrame = new HashSet<Entity>();

        private void Awake()
        {
            WidgetRendererRegistry.Register(this);
        }

        private void OnDestroy()
        {
            WidgetRendererRegistry.Unregister(this);
        }

        public void OnFrameBegin()
        {
            // Lazy-acquire pool reference
            if (_pool == null)
                _pool = EnemyHealthBarPool.Instance;
            if (_pool == null) return;

            _visibleThisFrame.Clear();

            // Reset per-frame pool state (combat counters, etc.)
            // Without this call, the pool's visibility evaluation has stale data.
            _pool.BeginFrame();

            // Pass targeted/hovered entity from projection system so the pool's
            // visibility modes (WhenTargeted, WhenHovered) work correctly.
            _pool.SetTargetedEntity(WidgetProjectionSystem.TargetedEntity);
            _pool.SetHoveredEntity(WidgetProjectionSystem.HoveredEntity);
        }

        public void OnWidgetVisible(in WidgetRenderData data)
        {
            if (_pool == null) return;

            _visibleThisFrame.Add(data.Entity);

            _pool.ShowHealthBar(
                data.Entity,
                data.WorldPos,
                data.CurrentHealth,
                data.MaxHealth,
                null, // name — pool resolves internally
                data.IsInCombat,
                data.TimeSinceCombatEnded,
                true, // isInLineOfSight — widget framework already frustum-culled
                false // hasAggroOnPlayer — aggro check not yet in projection system
            );

            ApplyScale(data);
        }

        public void OnWidgetUpdate(in WidgetRenderData data)
        {
            if (_pool == null) return;

            _visibleThisFrame.Add(data.Entity);

            _pool.ShowHealthBar(
                data.Entity,
                data.WorldPos,
                data.CurrentHealth,
                data.MaxHealth,
                null,
                data.IsInCombat,
                data.TimeSinceCombatEnded,
                true,
                false
            );

            ApplyScale(data);
        }

        public void OnWidgetHidden(Entity entity)
        {
            if (_pool == null) return;
            _pool.HideHealthBar(entity);
        }

        public void OnFrameEnd()
        {
            if (_pool == null) return;

            // Hide any pool bars that weren't touched this frame.
            // This handles stale bars left over from the old EnemyHealthBarBridgeSystem
            // when the widget framework first activates (the old bridge stops running
            // but its bars remain in the pool because nobody called HideHealthBar).
            _pool.CleanupDeadEntities(_visibleThisFrame);
        }

        private void ApplyScale(in WidgetRenderData data)
        {
            if (_pool == null) return;

            // Apply paradigm scale multiplier to health bar
            var healthBar = _pool.GetHealthBar(data.Entity);
            if (healthBar != null)
            {
                healthBar.SetExternalScale(data.Scale);
            }
        }
    }
}
