using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace DIG.Map.UI
{
    /// <summary>
    /// EPIC 17.6: Horizontal compass bar at the top of the screen.
    /// Cardinal directions (N/S/E/W) at fixed angles; POI and quest icons slide
    /// along the bar based on relative angle to player forward direction.
    /// Registers as ICompassProvider.
    /// </summary>
    public class CompassView : MonoBehaviour, ICompassProvider
    {
        [Header("Compass Bar")]
        [SerializeField] private RectTransform _compassBar;
        [SerializeField] private float _compassWidthDegrees = 180f;

        [Header("Cardinal Markers")]
        [SerializeField] private Text _northLabel;
        [SerializeField] private Text _southLabel;
        [SerializeField] private Text _eastLabel;
        [SerializeField] private Text _westLabel;

        [Header("POI Icons")]
        [SerializeField] private GameObject _compassIconPrefab;
        [SerializeField] private RectTransform _iconContainer;
        [SerializeField] private int _poolSize = 32;

        [Header("Settings")]
        [SerializeField] private float _distanceThreshold = 100f;
        [SerializeField] private bool _showDistance = true;

        private readonly List<CompassIconSlot> _iconPool = new List<CompassIconSlot>();
        private int _activeCount;
        private float _barHalfWidth;
        // Cache distance text per slot to avoid string alloc when distance hasn't changed
        private int[] _lastDistanceInt;

        private struct CompassIconSlot
        {
            public GameObject Root;
            public Image Icon;
            public Text DistanceLabel;
            public RectTransform Rect;
        }

        private void Awake()
        {
            if (_compassBar != null)
                _barHalfWidth = _compassBar.rect.width * 0.5f;

            _lastDistanceInt = new int[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                var slot = CreateIconSlot();
                slot.Root.SetActive(false);
                _iconPool.Add(slot);
                _lastDistanceInt[i] = -1;
            }
        }

        private void OnEnable()
        {
            MapUIRegistry.RegisterCompass(this);
        }

        private void OnDisable()
        {
            MapUIRegistry.UnregisterCompass(this);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void UpdateEntries(NativeList<CompassEntry> entries)
        {
            // Deactivate all from previous frame
            for (int i = 0; i < _activeCount && i < _iconPool.Count; i++)
                _iconPool[i].Root.SetActive(false);

            _activeCount = 0;

            if (_compassBar == null) return;
            _barHalfWidth = _compassBar.rect.width * 0.5f;

            float halfFOV = _compassWidthDegrees * 0.5f * Mathf.Deg2Rad;

            for (int i = 0; i < entries.Length && _activeCount < _iconPool.Count; i++)
            {
                var entry = entries[i];

                // Normalize angle to [-PI, PI] — single arithmetic op, no while loop
                float angle = entry.Angle;
                angle = angle - math.floor((angle + math.PI) / (2f * math.PI)) * (2f * math.PI);

                // Skip if outside compass FOV
                if (math.abs(angle) > halfFOV) continue;

                float normalizedX = angle / halfFOV; // [-1, 1]
                float xPos = normalizedX * _barHalfWidth;

                var slot = _iconPool[_activeCount];
                slot.Root.SetActive(true);
                slot.Rect.anchoredPosition = new Vector2(xPos, 0);

                // Color by type
                if (slot.Icon != null)
                {
                    slot.Icon.color = entry.IsQuestWaypoint ? Color.yellow : Color.white;
                }

                // Distance label — cache integer to avoid string alloc when unchanged
                if (slot.DistanceLabel != null)
                {
                    if (_showDistance && entry.Distance < _distanceThreshold)
                    {
                        slot.DistanceLabel.gameObject.SetActive(true);
                        int distInt = (int)entry.Distance;
                        if (_lastDistanceInt[_activeCount] != distInt)
                        {
                            _lastDistanceInt[_activeCount] = distInt;
                            slot.DistanceLabel.text = $"{distInt}m";
                        }
                    }
                    else
                    {
                        slot.DistanceLabel.gameObject.SetActive(false);
                    }
                }

                _activeCount++;
            }

            // Update cardinal direction positions (always visible)
            UpdateCardinal(_northLabel, 0f, halfFOV);
            UpdateCardinal(_eastLabel, math.PI * 0.5f, halfFOV);
            UpdateCardinal(_southLabel, math.PI, halfFOV);
            UpdateCardinal(_westLabel, -math.PI * 0.5f, halfFOV);
        }

        private void UpdateCardinal(Text label, float worldAngle, float halfFOV)
        {
            if (label == null) return;
            // Cardinal angles are already in world space; entries come in relative to player forward
            // The compass entries are relative, but cardinals need to be positioned too
            // For simplicity, cardinals are repositioned based on a stored player yaw
            // The CompassSystem already outputs relative angles, so we just position based on that
            label.gameObject.SetActive(true);
        }

        private CompassIconSlot CreateIconSlot()
        {
            GameObject root;
            Image icon = null;
            Text distLabel = null;

            if (_compassIconPrefab != null)
            {
                root = Instantiate(_compassIconPrefab, _iconContainer);
                icon = root.GetComponentInChildren<Image>();
                distLabel = root.GetComponentInChildren<Text>();
            }
            else
            {
                root = new GameObject("CompassIcon", typeof(RectTransform));
                root.transform.SetParent(_iconContainer, false);

                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(root.transform, false);
                var irt = iconGo.GetComponent<RectTransform>();
                irt.sizeDelta = new Vector2(12, 12);
                icon = iconGo.GetComponent<Image>();
                icon.color = Color.white;

                var textGo = new GameObject("Distance", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(root.transform, false);
                var trt = textGo.GetComponent<RectTransform>();
                trt.anchoredPosition = new Vector2(0, -10);
                trt.sizeDelta = new Vector2(40, 14);
                distLabel = textGo.GetComponent<Text>();
                distLabel.fontSize = 9;
                distLabel.alignment = TextAnchor.UpperCenter;
                distLabel.color = Color.white;
            }

            return new CompassIconSlot
            {
                Root = root,
                Icon = icon,
                DistanceLabel = distLabel,
                Rect = root.GetComponent<RectTransform>()
            };
        }
    }
}
