// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · FloatingTextElement (UI Toolkit World-Space)
// World-space floating damage/heal numbers using UI Toolkit
// Unity 6.2+: Uses PanelSettings WorldSpace render mode
// ════════════════════════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UIElements;
using System;

namespace DIG.Combat.UI.FloatingText
{
    /// <summary>
    /// EPIC 15.9: Individual floating text element using UI Toolkit.
    /// Billboards toward camera and animates upward with fade.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class FloatingTextElement : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────
        // Configuration
        // ─────────────────────────────────────────────────────────────────
        [Header("Defaults")]
        [SerializeField] private float defaultDuration = 1.5f;
        [SerializeField] private float defaultRiseSpeed = 1f;
        [SerializeField] private float defaultFadeStart = 0.8f;
        
        [Header("Shader (Optional)")]
        [Tooltip("Assign CombatUI_Glow material for glow effects")]
        [SerializeField] private Material glowMaterial;
        
        /// <summary>
        /// Event fired when animation completes (for pool return).
        /// </summary>
        public event Action<FloatingTextElement> OnComplete;
        
        // ─────────────────────────────────────────────────────────────────
        // UI Elements
        // ─────────────────────────────────────────────────────────────────
        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _container;
        private Label _textLabel;
        private VisualElement _glowElement;
        
        // ─────────────────────────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────────────────────────
        private Camera _mainCamera;
        private float _timer;
        private float _duration;
        private float _riseSpeed;
        private float _fadeStartTime;
        private Color _baseColor;
        private Vector3 _startScale;
        private AnimationCurve _scaleCurve;
        private bool _isActive;
        private Material _instanceMaterial;
        
        // Shader property IDs
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        
        // ─────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _mainCamera = Camera.main;
            _document = GetComponent<UIDocument>();
            
            if (glowMaterial != null)
            {
                _instanceMaterial = new Material(glowMaterial);
            }
        }
        
        private void OnEnable()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;
            
            SetupUI();
        }
        
        private void OnDestroy()
        {
            if (_instanceMaterial != null)
            {
                Destroy(_instanceMaterial);
            }
        }
        
        private void SetupUI()
        {
            if (_document == null) return;
            
            _root = _document.rootVisualElement;
            if (_root == null) return;
            
            // Find or create container
            _container = _root.Q<VisualElement>("floating-text-container") ?? CreateContainer();
            _textLabel = _container.Q<Label>("text-label") ?? CreateTextLabel();
            _glowElement = _container.Q<VisualElement>("glow-element");
        }
        
        private VisualElement CreateContainer()
        {
            var container = new VisualElement { name = "floating-text-container" };
            container.AddToClassList("floating-text-container");
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;
            _root.Add(container);
            return container;
        }
        
        private Label CreateTextLabel()
        {
            var label = new Label { name = "text-label" };
            label.AddToClassList("floating-text");
            label.style.fontSize = 24;
            label.style.color = Color.white;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            // Text outline via text-shadow USS or custom rendering
            _container.Add(label);
            return label;
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Update
        // ─────────────────────────────────────────────────────────────────
        private void LateUpdate()
        {
            if (!_isActive) return;
            
            _timer += Time.deltaTime;
            
            // Rise upward
            transform.position += Vector3.up * (_riseSpeed * Time.deltaTime);
            
            // Billboard toward camera
            if (_mainCamera != null)
            {
                transform.forward = _mainCamera.transform.forward;
            }
            
            // Scale animation
            if (_scaleCurve != null && _scaleCurve.length > 0)
            {
                float t = Mathf.Clamp01(_timer / 0.3f); // Scale in first 0.3s
                float scale = _scaleCurve.Evaluate(t);
                transform.localScale = _startScale * scale;
            }
            
            // Fade out
            if (_timer > _fadeStartTime)
            {
                float fadeProgress = Mathf.Clamp01((_timer - _fadeStartTime) / (_duration - _fadeStartTime));
                float alpha = 1f - fadeProgress;
                
                if (_textLabel != null)
                {
                    _textLabel.style.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha);
                }
                
                if (_container != null)
                {
                    _container.style.opacity = alpha;
                }
                
                // Update glow shader
                if (_instanceMaterial != null)
                {
                    _instanceMaterial.SetFloat(GlowIntensityId, alpha * 0.5f);
                }
            }
            
            // Complete
            if (_timer >= _duration)
            {
                Complete();
            }
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────
        
        /// <summary>
        /// Initialize the floating text with style configuration.
        /// </summary>
        public void Initialize(string text, Vector3 position, FloatingTextStyleConfig.TextStyleData style)
        {
            transform.position = position;
            _timer = 0f;
            _isActive = true;
            
            _duration = style.Duration;
            _riseSpeed = style.RiseSpeed;
            _fadeStartTime = style.FadeStartTime;
            _baseColor = style.Color;
            _scaleCurve = style.ScaleCurve;
            _startScale = Vector3.one;
            
            if (_textLabel != null)
            {
                _textLabel.text = text;
                _textLabel.style.color = style.Color;
                _textLabel.style.fontSize = style.FontSize;
                
                // Apply outline via USS class or style
                if (style.UseOutline)
                {
                    _textLabel.AddToClassList("outlined-text");
                }
                else
                {
                    _textLabel.RemoveFromClassList("outlined-text");
                }
            }
            
            // Update glow shader color
            if (_instanceMaterial != null)
            {
                _instanceMaterial.SetColor(GlowColorId, style.Color);
                _instanceMaterial.SetFloat(GlowIntensityId, 0.5f);
            }
            
            transform.localScale = _startScale * 0.5f; // Start small for scale-in
            
            if (_container != null)
                _container.style.opacity = 1f;
            
            gameObject.SetActive(true);
        }
        
        /// <summary>
        /// Initialize with simple parameters (no style config).
        /// </summary>
        public void Initialize(string text, Vector3 position, Color color, float fontSize = 24f)
        {
            transform.position = position;
            _timer = 0f;
            _isActive = true;
            
            _duration = defaultDuration;
            _riseSpeed = defaultRiseSpeed;
            _fadeStartTime = defaultFadeStart;
            _baseColor = color;
            _scaleCurve = null;
            _startScale = Vector3.one;
            
            if (_textLabel != null)
            {
                _textLabel.text = text;
                _textLabel.style.color = color;
                _textLabel.style.fontSize = fontSize;
            }
            
            if (_instanceMaterial != null)
            {
                _instanceMaterial.SetColor(GlowColorId, color);
            }
            
            transform.localScale = _startScale;
            
            if (_container != null)
                _container.style.opacity = 1f;
            
            gameObject.SetActive(true);
        }
        
        /// <summary>
        /// Reset for pooling.
        /// </summary>
        public void ResetForPool()
        {
            _isActive = false;
            _timer = 0f;
            transform.localScale = Vector3.one;
            
            if (_container != null)
                _container.style.opacity = 0f;
            
            if (_textLabel != null)
                _textLabel.text = "";
            
            gameObject.SetActive(false);
        }
        
        private void Complete()
        {
            _isActive = false;
            OnComplete?.Invoke(this);
        }
        
        /// <summary>
        /// Force complete and return to pool.
        /// </summary>
        public void ForceComplete()
        {
            if (_isActive)
                Complete();
        }
    }
}
