using UnityEngine;
using UnityEngine.UI;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections.Generic;
using DIG.Widgets.Config;
using DIG.Widgets.Systems;

namespace DIG.Widgets.Rendering
{
    /// <summary>
    /// EPIC 15.26 Phase 6: Pooled cast bar renderer for enemy ability casting.
    /// Reads AbilityExecutionState (Phase == Casting) to show an interruptible
    /// progress bar above the enemy. Hides automatically when casting completes
    /// or is interrupted.
    ///
    /// Requires a Canvas + RectTransform prefab with:
    ///   - Image (background)
    ///   - Image (fill, type=Filled, fillMethod=Horizontal)
    ///   - Text (ability name, optional)
    ///
    /// Max pool size matches ParadigmWidgetProfile.MaxActiveWidgets (default 8).
    /// Only shown at LOD Full (culled at Reduced+).
    /// </summary>
    [AddComponentMenu("DIG/Widgets/Cast Bar Renderer")]
    public class CastBarRenderer : MonoBehaviour, IWidgetRenderer
    {
        public WidgetType SupportedType => WidgetType.CastBar;

        [Header("Prefab")]
        [Tooltip("World-space cast bar prefab. Must have CastBarUI component or follow naming convention.")]
        [SerializeField] private GameObject _castBarPrefab;

        [Header("Settings")]
        [SerializeField] private int _poolSize = 8;
        [SerializeField] private float _yOffsetAboveHealthBar = 0.35f;

        private readonly List<CastBarInstance> _pool = new();
        private readonly Dictionary<Entity, int> _activeMap = new();

        private struct CastBarInstance
        {
            public GameObject Root;
            public Image FillImage;
            public Transform Transform;
            public bool InUse;
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
            if (_castBarPrefab == null) return;

            for (int i = 0; i < _poolSize; i++)
            {
                var go = Instantiate(_castBarPrefab, transform);
                go.SetActive(false);

                // Try to find fill image (child named "Fill" or first filled Image)
                Image fill = null;
                var images = go.GetComponentsInChildren<Image>(true);
                for (int j = 0; j < images.Length; j++)
                {
                    if (images[j].type == Image.Type.Filled ||
                        images[j].gameObject.name.Contains("Fill"))
                    {
                        fill = images[j];
                        break;
                    }
                }

                _pool.Add(new CastBarInstance
                {
                    Root = go,
                    FillImage = fill,
                    Transform = go.transform,
                    InUse = false
                });
            }
        }

        public void OnFrameBegin()
        {
        }

        public void OnWidgetVisible(in WidgetRenderData data)
        {
            if (_castBarPrefab == null) return;
            if (data.LOD != WidgetLODTier.Full) return;

            int slotIndex = AcquireSlot();
            if (slotIndex < 0) return;

            _activeMap[data.Entity] = slotIndex;

            var inst = _pool[slotIndex];
            inst.InUse = true;
            inst.Root.SetActive(true);
            _pool[slotIndex] = inst;

            UpdatePosition(slotIndex, in data);
        }

        public void OnWidgetUpdate(in WidgetRenderData data)
        {
            if (!_activeMap.TryGetValue(data.Entity, out int slotIndex)) return;
            if (data.LOD != WidgetLODTier.Full)
            {
                ReleaseSlot(data.Entity, slotIndex);
                return;
            }

            UpdatePosition(slotIndex, in data);
        }

        public void OnWidgetHidden(Entity entity)
        {
            if (_activeMap.TryGetValue(entity, out int slotIndex))
            {
                ReleaseSlot(entity, slotIndex);
            }
        }

        public void OnFrameEnd()
        {
        }

        /// <summary>
        /// Called externally by a bridge system or adapter to update cast progress.
        /// </summary>
        public void UpdateCastProgress(Entity entity, float progress01)
        {
            if (!_activeMap.TryGetValue(entity, out int slotIndex)) return;

            var inst = _pool[slotIndex];
            if (inst.FillImage != null)
            {
                inst.FillImage.fillAmount = math.saturate(progress01);
            }
        }

        private void UpdatePosition(int slotIndex, in WidgetRenderData data)
        {
            var inst = _pool[slotIndex];
            if (inst.Transform == null) return;

            float3 pos = data.WorldPos;
            pos.y += data.YOffset + _yOffsetAboveHealthBar;
            inst.Transform.position = new Vector3(pos.x, pos.y, pos.z);

            float scale = data.Scale;
            inst.Transform.localScale = new Vector3(scale, scale, scale);
        }

        private int AcquireSlot()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].InUse) return i;
            }
            return -1;
        }

        private void ReleaseSlot(Entity entity, int slotIndex)
        {
            _activeMap.Remove(entity);
            var inst = _pool[slotIndex];
            inst.InUse = false;
            if (inst.Root != null)
                inst.Root.SetActive(false);
            _pool[slotIndex] = inst;
        }
    }
}
