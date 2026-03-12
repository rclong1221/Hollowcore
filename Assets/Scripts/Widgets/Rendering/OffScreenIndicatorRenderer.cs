using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections.Generic;
using DIG.Widgets.Config;
using DIG.Widgets.Systems;

namespace DIG.Widgets.Rendering
{
    /// <summary>
    /// EPIC 15.26 Phase 5: Renders edge-of-screen arrows for off-screen tracked entities.
    /// Reads off-screen entries from WidgetProjectionSystem and clamps to screen edges.
    ///
    /// Tracked entities that fail frustum culling get an arrow icon pointing toward
    /// their world position, clamped to the screen edge with a configurable margin.
    /// Distance text shown below the arrow.
    ///
    /// Max 5 off-screen indicators (budget separately from world-space widgets).
    /// Priority: Boss > Quest > Party > Waypoint > Loot > Targeted.
    /// </summary>
    [AddComponentMenu("DIG/Widgets/Off-Screen Indicator Renderer")]
    public class OffScreenIndicatorRenderer : MonoBehaviour, IWidgetRenderer
    {
        public WidgetType SupportedType => WidgetType.OffScreenIndicator;

        [Header("Config")]
        [SerializeField] private OffScreenIndicatorConfig _config;

        [Header("Prefab")]
        [Tooltip("UI prefab for the indicator arrow. Must have RectTransform.")]
        [SerializeField] private RectTransform _indicatorPrefab;

        [Header("Canvas")]
        [Tooltip("Screen-space overlay canvas to parent indicators to.")]
        [SerializeField] private Canvas _canvas;

        [Header("Settings")]
        [SerializeField] private int _maxIndicators = 5;
        [SerializeField] private float _edgeMargin = 40f;

        private readonly List<IndicatorInstance> _pool = new();
        private readonly List<IndicatorData> _activeIndicators = new();

        private struct IndicatorInstance
        {
            public RectTransform Transform;
            public bool InUse;
        }

        private struct IndicatorData
        {
            public Entity Entity;
            public float3 WorldPos;
            public TrackedEntityType Type;
            public float Distance;
        }

        private void Awake()
        {
            WidgetRendererRegistry.Register(this);
            InitializePool();
        }

        private void OnDestroy()
        {
            WidgetRendererRegistry.Unregister(this);
        }

        private void InitializePool()
        {
            if (_indicatorPrefab == null || _canvas == null) return;

            for (int i = 0; i < _maxIndicators; i++)
            {
                var instance = Instantiate(_indicatorPrefab, _canvas.transform);
                instance.gameObject.SetActive(false);
                _pool.Add(new IndicatorInstance { Transform = instance, InUse = false });
            }
        }

        public void OnFrameBegin()
        {
            _activeIndicators.Clear();

            // Hide all indicators at start of frame
            for (int i = 0; i < _pool.Count; i++)
            {
                var inst = _pool[i];
                inst.InUse = false;
                if (inst.Transform != null)
                    inst.Transform.gameObject.SetActive(false);
                _pool[i] = inst;
            }
        }

        public void OnWidgetVisible(in WidgetRenderData data)
        {
            // Off-screen indicators are computed from off-screen entities,
            // so this callback isn't used directly. See OnFrameEnd.
        }

        public void OnWidgetUpdate(in WidgetRenderData data)
        {
        }

        public void OnWidgetHidden(Entity entity)
        {
        }

        public void OnFrameEnd()
        {
            if (!WidgetProjectionSystem.FrameworkActive) return;
            if (!WidgetProjectionSystem.OffScreenWidgets.IsCreated) return;

            var offScreen = WidgetProjectionSystem.OffScreenWidgets;

            // Collect and sort by tracked type priority
            for (int i = 0; i < offScreen.Length && _activeIndicators.Count < _maxIndicators; i++)
            {
                var proj = offScreen[i];
                _activeIndicators.Add(new IndicatorData
                {
                    Entity = proj.Entity,
                    WorldPos = proj.WorldPos,
                    Type = TrackedEntityType.Boss, // TODO: read from OffScreenTracker component
                    Distance = proj.Distance
                });
            }

            // Render active indicators
            var screenSize = new float2(Screen.width, Screen.height);

            for (int i = 0; i < _activeIndicators.Count && i < _pool.Count; i++)
            {
                var data = _activeIndicators[i];
                var inst = _pool[i];

                if (inst.Transform == null) continue;

                // Project world position to screen (may be behind camera)
                WidgetCameraData.WorldToScreen(data.WorldPos, out float2 screenPos);

                // Clamp to screen edges with margin
                float2 clampedPos = ClampToScreenEdge(screenPos, screenSize, _edgeMargin);

                // Compute arrow rotation (point toward actual world position)
                float2 center = screenSize * 0.5f;
                float2 dir = screenPos - center;
                float angle = math.degrees(math.atan2(dir.y, dir.x));

                inst.Transform.anchoredPosition = new Vector2(clampedPos.x, clampedPos.y);
                inst.Transform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                inst.Transform.gameObject.SetActive(true);
                inst.InUse = true;
                _pool[i] = inst;
            }
        }

        private static float2 ClampToScreenEdge(float2 screenPos, float2 screenSize, float margin)
        {
            float2 center = screenSize * 0.5f;
            float2 dir = screenPos - center;

            if (math.lengthsq(dir) < 1f)
                return new float2(margin, screenSize.y * 0.5f);

            // Find intersection with screen edge
            float2 absDir = math.abs(dir);
            float2 halfScreen = (screenSize * 0.5f) - new float2(margin, margin);

            float scaleX = absDir.x > 0.001f ? halfScreen.x / absDir.x : float.MaxValue;
            float scaleY = absDir.y > 0.001f ? halfScreen.y / absDir.y : float.MaxValue;
            float scale = math.min(scaleX, scaleY);

            return center + dir * math.min(scale, 1f);
        }
    }
}
