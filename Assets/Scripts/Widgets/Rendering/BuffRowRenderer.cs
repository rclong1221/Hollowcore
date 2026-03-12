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
    /// EPIC 15.26 Phase 6: Pooled buff/debuff icon row renderer.
    /// Shows status effect icons below or above the health bar for each entity.
    /// Reads from StatusEffect buffer (Player.Components) via a bridge.
    ///
    /// Icons are arranged horizontally, limited by ParadigmWidgetProfile.BuffRowMaxIcons.
    /// Each icon shows the effect type sprite with optional duration overlay.
    ///
    /// Only shown at LOD Full and Reduced (top N only at Reduced).
    /// Culled at Minimal+.
    /// </summary>
    [AddComponentMenu("DIG/Widgets/Buff Row Renderer")]
    public class BuffRowRenderer : MonoBehaviour, IWidgetRenderer
    {
        public WidgetType SupportedType => WidgetType.BuffRow;

        [Header("Prefab")]
        [Tooltip("Single buff icon prefab. Must have Image component for icon sprite.")]
        [SerializeField] private GameObject _iconPrefab;

        [Header("Layout")]
        [SerializeField] private int _maxIconsPerEntity = 5;
        [SerializeField] private float _yOffsetBelowHealthBar = -0.2f;
        [SerializeField] private float _iconSize = 0.1f;

        [Header("Pool")]
        [SerializeField] private int _maxEntities = 16;

        private readonly List<BuffRowInstance> _pool = new();
        private readonly Dictionary<Entity, int> _activeMap = new();

        private struct BuffRowInstance
        {
            public GameObject Root;
            public Transform Transform;
            public Image[] Icons;
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
            if (_iconPrefab == null) return;

            for (int i = 0; i < _maxEntities; i++)
            {
                var root = new GameObject($"BuffRow_{i}");
                root.transform.SetParent(transform);
                root.SetActive(false);

                var icons = new Image[_maxIconsPerEntity];
                for (int j = 0; j < _maxIconsPerEntity; j++)
                {
                    var iconGO = Instantiate(_iconPrefab, root.transform);
                    iconGO.name = $"Icon_{j}";
                    iconGO.SetActive(false);

                    var img = iconGO.GetComponent<Image>();
                    if (img == null)
                        img = iconGO.GetComponentInChildren<Image>();

                    icons[j] = img;
                }

                _pool.Add(new BuffRowInstance
                {
                    Root = root,
                    Transform = root.transform,
                    Icons = icons,
                    InUse = false
                });
            }
        }

        public void OnFrameBegin()
        {
        }

        public void OnWidgetVisible(in WidgetRenderData data)
        {
            if (_iconPrefab == null) return;
            if (data.LOD >= WidgetLODTier.Minimal) return;

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
            if (data.LOD >= WidgetLODTier.Minimal)
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
        /// Called externally by a bridge to update which status effects are active.
        /// </summary>
        public void UpdateBuffIcons(Entity entity, BuffIconData[] activeBuffs, int count)
        {
            if (!_activeMap.TryGetValue(entity, out int slotIndex)) return;

            var inst = _pool[slotIndex];
            int maxIcons = GetMaxIconsForLOD(inst);
            int showCount = math.min(count, math.min(maxIcons, _maxIconsPerEntity));

            for (int i = 0; i < _maxIconsPerEntity; i++)
            {
                if (inst.Icons[i] == null) continue;

                if (i < showCount && activeBuffs != null)
                {
                    inst.Icons[i].gameObject.SetActive(true);
                    if (activeBuffs[i].Icon != null)
                        inst.Icons[i].sprite = activeBuffs[i].Icon;
                    inst.Icons[i].color = activeBuffs[i].IsDebuff
                        ? new Color(1f, 0.4f, 0.4f, 1f)
                        : new Color(0.3f, 0.8f, 1f, 1f);
                }
                else
                {
                    inst.Icons[i].gameObject.SetActive(false);
                }
            }
        }

        private void UpdatePosition(int slotIndex, in WidgetRenderData data)
        {
            var inst = _pool[slotIndex];
            if (inst.Transform == null) return;

            float3 pos = data.WorldPos;
            pos.y += data.YOffset + _yOffsetBelowHealthBar;
            inst.Transform.position = new Vector3(pos.x, pos.y, pos.z);

            float scale = data.Scale * _iconSize;
            inst.Transform.localScale = new Vector3(scale, scale, scale);
        }

        private int GetMaxIconsForLOD(in BuffRowInstance inst)
        {
            // Reduced LOD shows fewer icons
            if (ParadigmWidgetConfig.HasInstance && ParadigmWidgetConfig.Instance.ActiveProfile != null)
                return ParadigmWidgetConfig.Instance.ActiveProfile.BuffRowMaxIcons;

            return _maxIconsPerEntity;
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

            // Hide all icons
            for (int i = 0; i < inst.Icons.Length; i++)
            {
                if (inst.Icons[i] != null)
                    inst.Icons[i].gameObject.SetActive(false);
            }

            _pool[slotIndex] = inst;
        }
    }

    /// <summary>
    /// Data for a single buff/debuff icon in the row.
    /// Populated by a bridge system that reads StatusEffect buffers.
    /// </summary>
    public struct BuffIconData
    {
        public Sprite Icon;
        public float TimeRemaining;
        public bool IsDebuff;
        public byte EffectType;
    }
}
