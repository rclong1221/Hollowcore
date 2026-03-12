using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace DIG.Map.UI
{
    /// <summary>
    /// EPIC 17.6: Fullscreen world map panel toggled via M key.
    /// Displays fog-of-war overlay, player marker, POI zone labels, and fast travel points.
    /// Supports zoom + pan via scroll wheel and drag.
    /// </summary>
    public class WorldMapView : MonoBehaviour, IWorldMapProvider
    {
        [Header("Map Display")]
        [SerializeField] private RawImage _mapImage;
        [SerializeField] private RawImage _fogOverlayImage;
        [SerializeField] private RectTransform _mapContent;
        [SerializeField] private RectTransform _playerMarker;

        [Header("Labels")]
        [SerializeField] private RectTransform _labelContainer;
        [SerializeField] private GameObject _labelPrefab;

        [Header("Controls")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.M;
        [SerializeField] private float _zoomSpeed = 0.1f;
        [SerializeField] private float _minZoom = 0.5f;
        [SerializeField] private float _maxZoom = 3f;
        [SerializeField] private float _panSpeed = 500f;

        [Header("World Bounds (must match MinimapConfigSO)")]
        [SerializeField] private float _worldMinX = -500f;
        [SerializeField] private float _worldMaxX = 500f;
        [SerializeField] private float _worldMinZ = -500f;
        [SerializeField] private float _worldMaxZ = 500f;

        private float _currentZoom = 1f;
        private Vector2 _panOffset;
        private bool _isDragging;
        private Vector2 _dragStartPos;
        private Vector2 _panStartOffset;
        private readonly List<GameObject> _labels = new List<GameObject>();
        private bool _isVisible;

        private void OnEnable()
        {
            MapUIRegistry.RegisterWorldMap(this);
        }

        private void OnDisable()
        {
            MapUIRegistry.UnregisterWorldMap(this);
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                _isVisible = !_isVisible;
                ShowWorldMap(_isVisible);
            }

            if (!_isVisible) return;

            HandleZoom();
            HandlePan();
        }

        public void ShowWorldMap(bool show)
        {
            _isVisible = show;
            if (_mapContent != null)
                _mapContent.gameObject.SetActive(show);
        }

        public void SetFogTexture(RenderTexture fogRT)
        {
            if (_fogOverlayImage != null)
                _fogOverlayImage.texture = fogRT;
        }

        public void UpdatePlayerMarker(float3 worldPos, float yaw)
        {
            if (_playerMarker == null || _mapContent == null) return;

            float worldRangeX = _worldMaxX - _worldMinX;
            float worldRangeZ = _worldMaxZ - _worldMinZ;
            if (worldRangeX <= 0f || worldRangeZ <= 0f) return;

            float u = (worldPos.x - _worldMinX) / worldRangeX;
            float v = (worldPos.z - _worldMinZ) / worldRangeZ;

            var contentSize = _mapContent.rect.size;
            _playerMarker.anchoredPosition = new Vector2(
                (u - 0.5f) * contentSize.x,
                (v - 0.5f) * contentSize.y
            );
            _playerMarker.localRotation = Quaternion.Euler(0, 0, -math.degrees(yaw));
        }

        public void SetZoneLabels(POIDefinition[] pois)
        {
            // Clear existing labels
            foreach (var label in _labels)
                Destroy(label);
            _labels.Clear();

            if (pois == null || _labelContainer == null) return;

            float worldRangeX = _worldMaxX - _worldMinX;
            float worldRangeZ = _worldMaxZ - _worldMinZ;
            if (worldRangeX <= 0f || worldRangeZ <= 0f) return;

            var contentSize = _mapContent != null ? _mapContent.rect.size : _labelContainer.rect.size;

            foreach (var poi in pois)
            {
                float u = (poi.WorldPosition.x - _worldMinX) / worldRangeX;
                float v = (poi.WorldPosition.z - _worldMinZ) / worldRangeZ;

                GameObject labelGo;
                if (_labelPrefab != null)
                {
                    labelGo = Instantiate(_labelPrefab, _labelContainer);
                }
                else
                {
                    labelGo = new GameObject(poi.Label, typeof(RectTransform), typeof(Text));
                    labelGo.transform.SetParent(_labelContainer, false);
                }

                var rt = labelGo.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(
                    (u - 0.5f) * contentSize.x,
                    (v - 0.5f) * contentSize.y
                );

                var text = labelGo.GetComponent<Text>();
                if (text != null)
                {
                    text.text = poi.Label;
                    text.fontSize = 12;
                    text.alignment = TextAnchor.MiddleCenter;
                    text.color = Color.white;
                }

                _labels.Add(labelGo);
            }
        }

        public void HighlightFastTravel(int poiId)
        {
            // Visually pulse or highlight the fast travel label/icon matching this POI ID
            // Implementation depends on label prefab setup — basic version does nothing extra
        }

        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) < 0.01f) return;

            _currentZoom = Mathf.Clamp(_currentZoom + scroll * _zoomSpeed * 10f, _minZoom, _maxZoom);
            if (_mapContent != null)
                _mapContent.localScale = Vector3.one * _currentZoom;
        }

        private void HandlePan()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _isDragging = true;
                _dragStartPos = Input.mousePosition;
                _panStartOffset = _panOffset;
            }

            if (Input.GetMouseButtonUp(0))
                _isDragging = false;

            if (_isDragging && _mapContent != null)
            {
                Vector2 delta = (Vector2)Input.mousePosition - _dragStartPos;
                _panOffset = _panStartOffset + delta;
                _mapContent.anchoredPosition = _panOffset;
            }
        }
    }
}
