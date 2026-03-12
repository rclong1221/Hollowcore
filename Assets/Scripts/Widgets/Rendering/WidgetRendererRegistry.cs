using System.Collections.Generic;
using UnityEngine;

namespace DIG.Widgets.Rendering
{
    /// <summary>
    /// EPIC 15.26 Phase 2: Static registry for IWidgetRenderer adapters.
    /// Follows the CombatUIRegistry pattern — adapters register on Awake/OnEnable
    /// and unregister on OnDestroy/OnDisable.
    ///
    /// Multiple renderers can be registered per WidgetType (e.g., a debug renderer
    /// alongside the primary renderer). The bridge calls all registered renderers.
    /// </summary>
    public static class WidgetRendererRegistry
    {
        private static readonly Dictionary<WidgetType, List<IWidgetRenderer>> _renderers = new();
        private static readonly List<IWidgetRenderer> _allRenderers = new();

        /// <summary>Whether any renderers are registered.</summary>
        public static bool HasAnyRenderers => _allRenderers.Count > 0;

        /// <summary>Total registered renderer count.</summary>
        public static int RendererCount => _allRenderers.Count;

        /// <summary>
        /// Register a widget renderer adapter. Called from adapter's Awake/OnEnable.
        /// </summary>
        public static void Register(IWidgetRenderer renderer)
        {
            if (renderer == null) return;

            var type = renderer.SupportedType;
            if (!_renderers.TryGetValue(type, out var list))
            {
                list = new List<IWidgetRenderer>(2);
                _renderers[type] = list;
            }

            if (!list.Contains(renderer))
            {
                list.Add(renderer);
                _allRenderers.Add(renderer);
            }
        }

        /// <summary>
        /// Unregister a widget renderer adapter. Called from adapter's OnDestroy/OnDisable.
        /// </summary>
        public static void Unregister(IWidgetRenderer renderer)
        {
            if (renderer == null) return;

            var type = renderer.SupportedType;
            if (_renderers.TryGetValue(type, out var list))
            {
                list.Remove(renderer);
                if (list.Count == 0)
                    _renderers.Remove(type);
            }

            _allRenderers.Remove(renderer);
        }

        /// <summary>
        /// Get all renderers for a specific widget type. Returns null if none registered.
        /// </summary>
        public static List<IWidgetRenderer> GetRenderers(WidgetType type)
        {
            return _renderers.TryGetValue(type, out var list) ? list : null;
        }

        /// <summary>
        /// Get the read-only list of all registered renderers (all types).
        /// </summary>
        public static IReadOnlyList<IWidgetRenderer> AllRenderers => _allRenderers;

        /// <summary>
        /// Check if a specific widget type has any registered renderers.
        /// </summary>
        public static bool HasRenderer(WidgetType type)
        {
            return _renderers.TryGetValue(type, out var list) && list.Count > 0;
        }

        /// <summary>
        /// Unregister all renderers. Call on scene unload or cleanup.
        /// </summary>
        public static void UnregisterAll()
        {
            _renderers.Clear();
            _allRenderers.Clear();
        }
    }
}
