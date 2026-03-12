using UnityEngine;
using UnityEngine.UI;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Widgets.Config;
using DIG.Widgets.Systems;

namespace DIG.Widgets.Rendering
{
    /// <summary>
    /// EPIC 15.26 Phase 6: Screen-space boss health plate rendered via UGUI.
    /// Shows a large health bar at the top or bottom of the screen when a boss
    /// entity is visible. Position controlled by ParadigmWidgetProfile.BossPlatePosition.
    ///
    /// Unlike world-space widgets, the boss plate is screen-space overlay.
    /// Only one boss plate shown at a time (highest importance boss wins).
    ///
    /// Requires a Canvas (ScreenSpace-Overlay) with child layout:
    ///   - Background Image (bar frame)
    ///   - Fill Image (type=Filled, fillMethod=Horizontal)
    ///   - Text (boss name, optional)
    ///   - Text (health text, optional)
    /// </summary>
    [AddComponentMenu("DIG/Widgets/Boss Plate Renderer")]
    public class BossPlateRenderer : MonoBehaviour, IWidgetRenderer
    {
        public WidgetType SupportedType => WidgetType.BossPlate;

        [Header("UI References")]
        [SerializeField] private GameObject _plateRoot;
        [SerializeField] private Image _fillImage;
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _healthText;

        [Header("Positioning")]
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private float _topYAnchor = 0.92f;
        [SerializeField] private float _bottomYAnchor = 0.08f;

        [Header("Animation")]
        [SerializeField] private float _fillLerpSpeed = 5f;

        private Entity _activeBossEntity;
        private bool _isActive;
        private float _displayedHealth01;
        private float _targetHealth01;

        private void Awake()
        {
            WidgetRendererRegistry.Register(this);

            if (_plateRoot != null)
                _plateRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            WidgetRendererRegistry.Unregister(this);
        }

        public void OnFrameBegin()
        {
            // Reset active tracking — will be re-set if boss is still visible
            _isActive = false;
        }

        public void OnWidgetVisible(in WidgetRenderData data)
        {
            ActivateBoss(in data);
        }

        public void OnWidgetUpdate(in WidgetRenderData data)
        {
            ActivateBoss(in data);
        }

        public void OnWidgetHidden(Entity entity)
        {
            if (entity == _activeBossEntity)
            {
                _activeBossEntity = Entity.Null;
                if (_plateRoot != null)
                    _plateRoot.SetActive(false);
            }
        }

        public void OnFrameEnd()
        {
            if (!_isActive && _plateRoot != null && _plateRoot.activeSelf)
            {
                _plateRoot.SetActive(false);
                _activeBossEntity = Entity.Null;
            }

            // Smooth health bar fill
            if (_isActive && _fillImage != null)
            {
                _displayedHealth01 = math.lerp(_displayedHealth01, _targetHealth01,
                    _fillLerpSpeed * Time.deltaTime);
                _fillImage.fillAmount = _displayedHealth01;
            }
        }

        private void ActivateBoss(in WidgetRenderData data)
        {
            _isActive = true;
            _activeBossEntity = data.Entity;
            _targetHealth01 = data.Health01;

            if (_plateRoot != null && !_plateRoot.activeSelf)
            {
                _plateRoot.SetActive(true);
                _displayedHealth01 = data.Health01; // snap on first show
            }

            UpdatePosition();
            UpdateHealthText(data.CurrentHealth, data.MaxHealth);
        }

        private void UpdatePosition()
        {
            if (_rectTransform == null) return;

            BossPlatePosition position = BossPlatePosition.Top;
            if (ParadigmWidgetConfig.HasInstance && ParadigmWidgetConfig.Instance.ActiveProfile != null)
            {
                position = ParadigmWidgetConfig.Instance.ActiveProfile.BossPlatePosition;
            }

            float yAnchor = position == BossPlatePosition.Top ? _topYAnchor : _bottomYAnchor;
            _rectTransform.anchorMin = new Vector2(0.5f, yAnchor);
            _rectTransform.anchorMax = new Vector2(0.5f, yAnchor);
            _rectTransform.anchoredPosition = Vector2.zero;
        }

        private void UpdateHealthText(float current, float max)
        {
            if (_healthText != null)
            {
                int curInt = Mathf.CeilToInt(current);
                int maxInt = Mathf.CeilToInt(max);
                _healthText.text = $"{curInt:N0} / {maxInt:N0}";
            }
        }

        /// <summary>
        /// Set the boss name text. Called externally by a bridge system.
        /// </summary>
        public void SetBossName(string name)
        {
            if (_nameText != null)
                _nameText.text = name;
        }
    }
}
