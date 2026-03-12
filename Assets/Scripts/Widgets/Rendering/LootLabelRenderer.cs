using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections.Generic;
using TMPro;
using DIG.Widgets.Systems;

namespace DIG.Widgets.Rendering
{
    /// <summary>
    /// EPIC 15.26 Phase 6: Pooled loot label renderer using TextMeshPro.
    /// Shows item names above dropped loot with rarity-based coloring.
    ///
    /// Rarity colors follow standard MMO conventions:
    ///   Common (white), Uncommon (green), Rare (blue),
    ///   Epic (purple), Legendary (orange).
    ///
    /// Only shown at LOD Full and Reduced. Culled at Minimal+.
    /// </summary>
    [AddComponentMenu("DIG/Widgets/Loot Label Renderer")]
    public class LootLabelRenderer : MonoBehaviour, IWidgetRenderer
    {
        public WidgetType SupportedType => WidgetType.LootLabel;

        [Header("Prefab")]
        [Tooltip("World-space TextMeshPro prefab for loot labels.")]
        [SerializeField] private GameObject _labelPrefab;

        [Header("Settings")]
        [SerializeField] private int _poolSize = 16;
        [SerializeField] private float _yOffset = 0.3f;
        [SerializeField] private float _baseFontSize = 3f;

        [Header("Rarity Colors")]
        [SerializeField] private Color _commonColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        [SerializeField] private Color _uncommonColor = new Color(0.12f, 1f, 0f, 1f);
        [SerializeField] private Color _rareColor = new Color(0f, 0.44f, 0.87f, 1f);
        [SerializeField] private Color _epicColor = new Color(0.64f, 0.21f, 0.93f, 1f);
        [SerializeField] private Color _legendaryColor = new Color(1f, 0.5f, 0f, 1f);

        private readonly List<LabelInstance> _pool = new();
        private readonly Dictionary<Entity, int> _activeMap = new();

        private struct LabelInstance
        {
            public GameObject Root;
            public TextMeshPro Text;
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
            if (_labelPrefab == null) return;

            for (int i = 0; i < _poolSize; i++)
            {
                var go = Instantiate(_labelPrefab, transform);
                go.SetActive(false);

                var tmp = go.GetComponent<TextMeshPro>();
                if (tmp == null)
                    tmp = go.GetComponentInChildren<TextMeshPro>();

                _pool.Add(new LabelInstance
                {
                    Root = go,
                    Text = tmp,
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
            if (_labelPrefab == null) return;
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
        /// Set the label text and rarity for a loot entity.
        /// Called externally by a loot bridge system.
        /// </summary>
        public void SetLootLabel(Entity entity, string itemName, LootRarity rarity)
        {
            if (!_activeMap.TryGetValue(entity, out int slotIndex)) return;

            var inst = _pool[slotIndex];
            if (inst.Text != null)
            {
                inst.Text.text = itemName;
                inst.Text.color = GetRarityColor(rarity);
                inst.Text.fontSize = _baseFontSize;
            }
        }

        private Color GetRarityColor(LootRarity rarity)
        {
            return rarity switch
            {
                LootRarity.Common => _commonColor,
                LootRarity.Uncommon => _uncommonColor,
                LootRarity.Rare => _rareColor,
                LootRarity.Epic => _epicColor,
                LootRarity.Legendary => _legendaryColor,
                _ => _commonColor
            };
        }

        private void UpdatePosition(int slotIndex, in WidgetRenderData data)
        {
            var inst = _pool[slotIndex];
            if (inst.Transform == null) return;

            float3 pos = data.WorldPos;
            pos.y += _yOffset;
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

    /// <summary>
    /// Loot rarity tiers for label coloring.
    /// </summary>
    [System.Obsolete("Use DIG.Items.ItemRarity instead. This enum will be removed in a future update.")]
    public enum LootRarity : byte
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }
}
