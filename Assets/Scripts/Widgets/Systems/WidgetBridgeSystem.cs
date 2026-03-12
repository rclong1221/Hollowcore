// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.26 · WidgetBridgeSystem
// Managed bridge that reads projection results from WidgetProjectionSystem and
// routes to registered IWidgetRenderer adapters. Handles lifecycle transitions
// (visible/hidden), dirty-checking, and paradigm profile billboard mode.
//
// Runs after WidgetProjectionSystem in PresentationSystemGroup.
// ════════════════════════════════════════════════════════════════════════════════
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using DIG.Widgets.Config;
using DIG.Widgets.Rendering;

namespace DIG.Widgets.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(WidgetProjectionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class WidgetBridgeSystem : SystemBase
    {
        private NativeHashMap<Entity, WidgetRenderData> _previousState;
        private NativeHashSet<Entity> _previouslyVisible;
        private NativeHashSet<Entity> _currentlyVisible;
        private NativeList<Entity> _toHide;

        protected override void OnCreate()
        {
            _previousState = new NativeHashMap<Entity, WidgetRenderData>(128, Allocator.Persistent);
            _previouslyVisible = new NativeHashSet<Entity>(128, Allocator.Persistent);
            _currentlyVisible = new NativeHashSet<Entity>(128, Allocator.Persistent);
            _toHide = new NativeList<Entity>(32, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (_previousState.IsCreated) _previousState.Dispose();
            if (_previouslyVisible.IsCreated) _previouslyVisible.Dispose();
            if (_currentlyVisible.IsCreated) _currentlyVisible.Dispose();
            if (_toHide.IsCreated) _toHide.Dispose();
        }

        protected override void OnUpdate()
        {
            // ── 1. Early-exit if framework didn't run ──────────────
            if (!WidgetProjectionSystem.FrameworkActive)
                return;

            if (!WidgetRendererRegistry.HasAnyRenderers)
                return;

            var projectedWidgets = WidgetProjectionSystem.ProjectedWidgets;
            if (!projectedWidgets.IsCreated)
                return;

            // ── 2. Get paradigm billboard mode ─────────────────────
            BillboardMode billboard = BillboardMode.CameraAligned;
            if (ParadigmWidgetConfig.HasInstance && ParadigmWidgetConfig.Instance.ActiveProfile != null)
            {
                billboard = ParadigmWidgetConfig.Instance.ActiveProfile.Billboard;
            }

            // ── 3. Notify renderers: frame begin ──────────────────
            var allRenderers = WidgetRendererRegistry.AllRenderers;
            for (int r = 0; r < allRenderers.Count; r++)
            {
                allRenderers[r].OnFrameBegin();
            }

            // ── 4. Process visible widgets ─────────────────────────
            _currentlyVisible.Clear();

            using var projectedEntries = projectedWidgets.GetKeyValueArrays(Allocator.Temp);

            for (int i = 0; i < projectedEntries.Keys.Length; i++)
            {
                var entity = projectedEntries.Keys[i];
                var proj = projectedEntries.Values[i];

                if (!proj.IsVisible) continue;

                _currentlyVisible.Add(entity);

                var renderData = WidgetRenderData.FromProjection(in proj, billboard);

                bool wasVisible = _previouslyVisible.Contains(entity);

                if (!wasVisible)
                {
                    // New widget — spawn callback
                    DispatchToRenderers(proj.ActiveFlags, WidgetCallback.Visible, in renderData, entity);
                }
                else
                {
                    // Always dispatch update — pool-based renderers (health bars)
                    // require ShowHealthBar every frame to maintain visibility state.
                    DispatchToRenderers(proj.ActiveFlags, WidgetCallback.Update, in renderData, entity);
                }

                _previousState[entity] = renderData;
            }

            // ── 5. Hide widgets that were visible last frame but aren't now
            _toHide.Clear();
            using var previousKeys = _previouslyVisible.ToNativeArray(Allocator.Temp);
            for (int i = 0; i < previousKeys.Length; i++)
            {
                if (!_currentlyVisible.Contains(previousKeys[i]))
                {
                    _toHide.Add(previousKeys[i]);
                }
            }

            for (int i = 0; i < _toHide.Length; i++)
            {
                var entity = _toHide[i];
                WidgetFlags flags = WidgetFlags.HealthBar; // default
                if (_previousState.TryGetValue(entity, out var prevData))
                {
                    flags = prevData.ActiveFlags;
                }

                DispatchHideToRenderers(flags, entity);
                _previousState.Remove(entity);
            }

            // ── 6. Swap visibility sets ────────────────────────────
            _previouslyVisible.Clear();
            using var currentKeys = _currentlyVisible.ToNativeArray(Allocator.Temp);
            for (int i = 0; i < currentKeys.Length; i++)
            {
                _previouslyVisible.Add(currentKeys[i]);
            }

            // ── 7. Notify renderers: frame end ────────────────────
            for (int r = 0; r < allRenderers.Count; r++)
            {
                allRenderers[r].OnFrameEnd();
            }
        }

        // ── Dirty checking ─────────────────────────────────────────

        /// <summary>
        /// Returns true if the render data has meaningfully changed since last frame.
        /// Skips update calls for widgets that haven't moved or changed health.
        /// </summary>
        private bool IsDirty(Entity entity, in WidgetRenderData current)
        {
            if (!_previousState.TryGetValue(entity, out var prev))
                return true;

            // Screen position moved more than 0.5 pixels
            float2 posDelta = current.ScreenPos - prev.ScreenPos;
            if (math.lengthsq(posDelta) > 0.25f)
                return true;

            // Health changed
            if (math.abs(current.Health01 - prev.Health01) > 0.001f)
                return true;

            // LOD tier changed
            if (current.LOD != prev.LOD)
                return true;

            // Scale changed
            if (math.abs(current.Scale - prev.Scale) > 0.01f)
                return true;

            return false;
        }

        // ── Dispatch to renderers ──────────────────────────────────

        private enum WidgetCallback { Visible, Update }

        private static void DispatchToRenderers(WidgetFlags flags, WidgetCallback callback, in WidgetRenderData data, Entity entity)
        {
            // Iterate over set flag bits and dispatch to matching renderers
            if ((flags & WidgetFlags.HealthBar) != 0) DispatchSingle(WidgetType.HealthBar, callback, in data, entity);
            if ((flags & WidgetFlags.Nameplate) != 0) DispatchSingle(WidgetType.Nameplate, callback, in data, entity);
            if ((flags & WidgetFlags.CastBar) != 0) DispatchSingle(WidgetType.CastBar, callback, in data, entity);
            if ((flags & WidgetFlags.BuffRow) != 0) DispatchSingle(WidgetType.BuffRow, callback, in data, entity);
            if ((flags & WidgetFlags.InteractPrompt) != 0) DispatchSingle(WidgetType.InteractPrompt, callback, in data, entity);
            if ((flags & WidgetFlags.QuestMarker) != 0) DispatchSingle(WidgetType.QuestMarker, callback, in data, entity);
            if ((flags & WidgetFlags.LootLabel) != 0) DispatchSingle(WidgetType.LootLabel, callback, in data, entity);
            if ((flags & WidgetFlags.BossPlate) != 0) DispatchSingle(WidgetType.BossPlate, callback, in data, entity);
        }

        private static void DispatchSingle(WidgetType type, WidgetCallback callback, in WidgetRenderData data, Entity entity)
        {
            var renderers = WidgetRendererRegistry.GetRenderers(type);
            if (renderers == null) return;

            for (int r = 0; r < renderers.Count; r++)
            {
                if (callback == WidgetCallback.Visible)
                    renderers[r].OnWidgetVisible(in data);
                else
                    renderers[r].OnWidgetUpdate(in data);
            }
        }

        private static void DispatchHideToRenderers(WidgetFlags flags, Entity entity)
        {
            if ((flags & WidgetFlags.HealthBar) != 0) DispatchHideSingle(WidgetType.HealthBar, entity);
            if ((flags & WidgetFlags.Nameplate) != 0) DispatchHideSingle(WidgetType.Nameplate, entity);
            if ((flags & WidgetFlags.CastBar) != 0) DispatchHideSingle(WidgetType.CastBar, entity);
            if ((flags & WidgetFlags.BuffRow) != 0) DispatchHideSingle(WidgetType.BuffRow, entity);
            if ((flags & WidgetFlags.InteractPrompt) != 0) DispatchHideSingle(WidgetType.InteractPrompt, entity);
            if ((flags & WidgetFlags.QuestMarker) != 0) DispatchHideSingle(WidgetType.QuestMarker, entity);
            if ((flags & WidgetFlags.LootLabel) != 0) DispatchHideSingle(WidgetType.LootLabel, entity);
            if ((flags & WidgetFlags.BossPlate) != 0) DispatchHideSingle(WidgetType.BossPlate, entity);
        }

        private static void DispatchHideSingle(WidgetType type, Entity entity)
        {
            var renderers = WidgetRendererRegistry.GetRenderers(type);
            if (renderers == null) return;

            for (int r = 0; r < renderers.Count; r++)
            {
                renderers[r].OnWidgetHidden(entity);
            }
        }
    }
}
