using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace DIG.Map.UI
{
    /// <summary>
    /// EPIC 17.6: Renders circular/square minimap overlay using RawImage + mask.
    /// Manages pooled icon Image elements positioned over the minimap via world-to-minimap projection.
    /// Registers as IMinimapProvider on OnEnable, unregisters on OnDisable.
    /// </summary>
    public class MinimapView : MonoBehaviour, IMinimapProvider
    {
        [Header("Minimap Display")]
        [SerializeField] private RawImage _minimapImage;
        [SerializeField] private RawImage _fogOverlayImage;
        [SerializeField] private RectTransform _iconContainer;
        [SerializeField] private RectTransform _playerArrow;

        [Header("Fog Stats")]
        [SerializeField] private Text _fogPercentText;

        [Header("Icon Pooling")]
        [SerializeField] private GameObject _iconPrefab;
        [SerializeField] private int _poolSize = 64;

        [Header("Settings")]
        [SerializeField] private float _minimapWorldRadius = 40f;

        private readonly List<Image> _iconPool = new List<Image>();
        private int _activeIconCount;
        private float _currentZoom;
        private int _lastFogRevealed = -1;
        private int _lastFogTotal = -1;

        private void Awake()
        {
            // Pre-allocate icon pool
            for (int i = 0; i < _poolSize; i++)
            {
                var go = _iconPrefab != null
                    ? Instantiate(_iconPrefab, _iconContainer)
                    : CreateDefaultIcon();
                go.SetActive(false);
                _iconPool.Add(go.GetComponent<Image>());
            }
        }

        private void OnEnable()
        {
            MapUIRegistry.RegisterMinimap(this);
        }

        private void OnDisable()
        {
            MapUIRegistry.UnregisterMinimap(this);
        }

        public void SetRenderTexture(RenderTexture minimapRT, RenderTexture fogRT)
        {
            if (_minimapImage != null)
                _minimapImage.texture = minimapRT;
            if (_fogOverlayImage != null)
                _fogOverlayImage.texture = fogRT;
        }

        public void SetZoom(float zoom)
        {
            _currentZoom = zoom;
            _minimapWorldRadius = zoom;
        }

        public void UpdateIcons(NativeList<MapIconEntry> icons, float3 playerPos, float playerYaw, float zoom)
        {
            _currentZoom = zoom;
            _minimapWorldRadius = zoom;

            // Deactivate all icons from previous frame
            for (int i = 0; i < _activeIconCount && i < _iconPool.Count; i++)
                _iconPool[i].gameObject.SetActive(false);

            _activeIconCount = 0;
            if (_iconContainer == null) return;

            float containerSize = _iconContainer.rect.width;
            float halfContainer = containerSize * 0.5f;
            float invWorldRadius = 1f / math.max(_minimapWorldRadius, 0.01f);

            // Rotation to map space (if minimap rotates with player)
            float cosYaw = math.cos(-playerYaw);
            float sinYaw = math.sin(-playerYaw);

            for (int i = 0; i < icons.Length && _activeIconCount < _iconPool.Count; i++)
            {
                var entry = icons[i];

                // World offset from player
                float dx = entry.WorldPos2D.x - playerPos.x;
                float dz = entry.WorldPos2D.y - playerPos.z;

                // Rotate into minimap space
                float rx = dx * cosYaw - dz * sinYaw;
                float ry = dx * sinYaw + dz * cosYaw;

                // Normalize to minimap [-0.5, 0.5]
                float nx = rx * invWorldRadius;
                float ny = ry * invWorldRadius;

                // Cull outside circle
                if (nx * nx + ny * ny > 0.25f) continue;

                // Position on UI
                var icon = _iconPool[_activeIconCount];
                icon.gameObject.SetActive(true);
                var rt = icon.rectTransform;
                rt.anchoredPosition = new Vector2(nx * containerSize, ny * containerSize);

                // Color from theme or custom
                if (entry.ColorPacked != 0)
                {
                    icon.color = UnpackColor(entry.ColorPacked);
                }

                _activeIconCount++;
            }

            // Update player arrow rotation
            if (_playerArrow != null)
            {
                _playerArrow.localRotation = Quaternion.Euler(0, 0, -math.degrees(playerYaw));
            }
        }

        public void UpdateFogProgress(int revealed, int total)
        {
            if (_fogPercentText == null || total <= 0) return;
            // Only update text when values actually change to avoid GC alloc every frame
            if (revealed == _lastFogRevealed && total == _lastFogTotal) return;
            _lastFogRevealed = revealed;
            _lastFogTotal = total;
            float pct = (float)revealed / total * 100f;
            _fogPercentText.text = $"{pct:F0}%";
        }

        private GameObject CreateDefaultIcon()
        {
            var go = new GameObject("MapIcon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_iconContainer, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(8, 8);
            var img = go.GetComponent<Image>();
            img.color = Color.white;
            return go;
        }

        private static Color UnpackColor(uint packed)
        {
            float r = ((packed >> 24) & 0xFF) / 255f;
            float g = ((packed >> 16) & 0xFF) / 255f;
            float b = ((packed >> 8) & 0xFF) / 255f;
            float a = (packed & 0xFF) / 255f;
            return new Color(r, g, b, a);
        }
    }
}
