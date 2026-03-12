// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · EnemyHealthBar (MeshRenderer)
// World-space health bar using standard Quad + Shader (Bypassing UI Toolkit)
// ════════════════════════════════════════════════════════════════════════════════
using UnityEngine;

namespace DIG.Combat.UI.WorldSpace
{
    /// <summary>
    /// EPIC 15.9: World-space enemy health bar using MeshRenderer + Shader.
    /// Replaces UI Toolkit implementation for robustness.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class EnemyHealthBar : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────
        // Configuration
        // ─────────────────────────────────────────────────────────────────
        [Header("Animation Settings")]
        [SerializeField] private float healthLerpSpeed = 8f;
        [SerializeField] private float trailLerpSpeed = 3f;
        [SerializeField] private float fadeInSpeed = 5f;
        [SerializeField] private float fadeOutSpeed = 2f;
        
        [Header("Colors (Overrides Shader Defaults if needed)")]
        [SerializeField] private Color healthyColor = Color.green;
        [SerializeField] private Color damagedColor = Color.yellow;
        [SerializeField] private Color criticalColor = Color.red;
        [SerializeField] private Color trailColor = new Color(1f, 0.3f, 0.3f, 0.8f);
        
        [Header("Offset")]
        [SerializeField] private Vector3 positionOffset = new Vector3(0, 3f, 0);

        [Header("Dimensions")]
        [SerializeField] private Vector3 defaultScale = new Vector3(1.5f, 0.25f, 1f);
        
        // ─────────────────────────────────────────────────────────────────
        // Components
        // ─────────────────────────────────────────────────────────────────
        private MeshRenderer _renderer;
        private Camera _mainCamera;
        private Transform _followTarget;
        private MaterialPropertyBlock _propBlock;
        
        // ─────────────────────────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────────────────────────
        private float _targetFill;
        private float _currentFill;
        private float _trailFill;
        private float _currentAlpha;
        private float _fadeTimer;
        private float _fadeDuration;
        private bool _isFadingOut;
        private bool _isVisible;
        
        // External control (from visibility system)
        private float _externalAlphaMultiplier = 1f;
        private float _externalScaleMultiplier = 1f;
        private bool _useExternalVisibility = false;
        
        // Shader property IDs
        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
        private static readonly int TrailAmountId = Shader.PropertyToID("_TrailAmount");
        private static readonly int DamageFlashId = Shader.PropertyToID("_DamageFlash");
        private static readonly int BaseColorId = Shader.PropertyToID("_Color"); // Alpha tint
        private static readonly int ScaleId = Shader.PropertyToID("_Scale"); // Explicit scale if shader supports it

        // UGUI / Shader Compatibility
        private static readonly int ZTestId = Shader.PropertyToID("unity_GUIZTestMode");
        
        // ─────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _mainCamera = Camera.main;
            _renderer = GetComponent<MeshRenderer>();
            _propBlock = new MaterialPropertyBlock();
            
            if (_renderer == null)
            {
                Debug.LogError($"[EnemyHealthBar] Missing MeshRenderer on {gameObject.name}!");
            }
            else
            {
                // Ensure we start invisible
                _renderer.enabled = false;
                
                // Initialize Block
                _renderer.GetPropertyBlock(_propBlock);
                // Force ZTest to LEqual (4) or Always (8) to ensure visibility?
                // Standard Transparent is usually LEqual.
                // The Shader uses [unity_GUIZTestMode]. Default might be weird.
                // Let's force it to 4 (LEqual) just in case.
                _propBlock.SetFloat(ZTestId, 4.0f);
                _renderer.SetPropertyBlock(_propBlock);
            }
            
            // Auto-Layout: Force standard health bar proportions
            transform.localScale = defaultScale;
        }
        
        private void LateUpdate()
        {
            // Culling optimized
            if (!_isVisible && _currentAlpha <= 0.01f)
            {
                if (_renderer != null && _renderer.enabled) _renderer.enabled = false;
                return;
            }
            
            // Follow target
            if (_followTarget != null)
            {
                transform.position = _followTarget.position + positionOffset;
            }
            // Child Mode (Directly attached) - enforce offset local to parent
            else if (transform.parent != null)
            {
                // Only enforce if we are purely data-driven, otherwise this fights the Move Gizmo.
                // But user expects 'Position Offset' to work.
                transform.localPosition = positionOffset;
            }
            
            // Billboard toward camera
            if (_mainCamera != null)
            {
                // UI / Quads usually face -Z (front). 
                // We want that face pointing to camera.
                // transform.LookAt(camera) points +Z to camera.
                // So we look at (Pos + (Pos - Cam)) -> Look away from camera? 
                // Standard Billboard: transform.forward = camera.transform.forward; (Parallel)
                transform.forward = _mainCamera.transform.forward;
            }
            
            // Smooth health fill
            _currentFill = Mathf.Lerp(_currentFill, _targetFill, Time.deltaTime * healthLerpSpeed);
            _trailFill = Mathf.Lerp(_trailFill, _targetFill, Time.deltaTime * trailLerpSpeed);
            
            // Fade Logic - SKIP if using external visibility control
            if (!_useExternalVisibility)
            {
                if (_isVisible && !_isFadingOut)
                {
                    _currentAlpha = Mathf.MoveTowards(_currentAlpha, 1f, Time.deltaTime * fadeInSpeed);
                }
                else
                {
                    _currentAlpha = Mathf.MoveTowards(_currentAlpha, 0f, Time.deltaTime * fadeOutSpeed);
                    if (_isFadingOut)
                    {
                        _fadeTimer -= Time.deltaTime;
                        if (_fadeTimer <= 0) _isVisible = false;
                    }
                }
            }
            else
            {
                // External visibility mode: keep internal alpha at 1, let external multiplier control
                _currentAlpha = 1f;
            }

            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (_renderer == null) return;
            
            // Apply external multipliers
            float effectiveAlpha = _currentAlpha * _externalAlphaMultiplier;
            
            if (effectiveAlpha > 0.01f)
            {
                if (!_renderer.enabled) _renderer.enabled = true;
                
                _renderer.GetPropertyBlock(_propBlock);
                
                _propBlock.SetFloat(FillAmountId, _currentFill);
                _propBlock.SetFloat(TrailAmountId, _trailFill);
                
                // Handle Alpha via Tint Color
                var color = Color.white; // Start white to preserve vertex colors if any
                color.a = effectiveAlpha;
                _propBlock.SetColor(BaseColorId, color);
                
                // Note: The shader uses [unity_GUIZTestMode]. We set it in Awake.
                
                _renderer.SetPropertyBlock(_propBlock);
                
                // Apply external scale
                if (_externalScaleMultiplier != 1f)
                {
                    transform.localScale = defaultScale * _externalScaleMultiplier;
                }
            }
            else
            {
                if (_renderer.enabled) _renderer.enabled = false;
            }
        }
        
        /// <summary>
        /// Update health bar values.
        /// </summary>
        public void UpdateHealth(Transform target, float currentHealth, float maxHealth, string name, float fadeDuration)
        {
            UpdateHealth(target.position, currentHealth, maxHealth, name, fadeDuration);
            _followTarget = target;
        }

        public void UpdateHealth(Vector3 position, float currentHealth, float maxHealth, string name, float fadeDuration)
        {
            if (_renderer == null) return;

            // Track if bar was just reactivated from pool
            bool wasInactive = !gameObject.activeSelf;
            if (wasInactive) gameObject.SetActive(true);

            _followTarget = null;
            transform.position = position + positionOffset;

            float newFill = maxHealth > 0 ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;

            // On Damage (skip false flash when reactivating from pool)
            if (newFill < _targetFill && !wasInactive)
            {
                _trailFill = _currentFill; // Snap trail to old fill
                _renderer.GetPropertyBlock(_propBlock);
                _propBlock.SetFloat(DamageFlashId, 1.0f); // Flash
                _renderer.SetPropertyBlock(_propBlock);
            }

            _targetFill = newFill;

            // First show or pool reactivation — instant snap to correct values
            if (!_isVisible || wasInactive)
            {
                _currentFill = _targetFill;
                _trailFill = _targetFill;
                _currentAlpha = 0f;
            }

            _isVisible = true;
            _isFadingOut = false;
            _fadeDuration = fadeDuration;
            _fadeTimer = fadeDuration;
        }
        
        public void SetLevel(int level) { } // No text on Mesh yet
        public void SetElite(bool isElite) { } // No elite marker on Mesh yet
        
        /// <summary>
        /// Set external alpha multiplier (from visibility system).
        /// 0 = fully hidden, 1 = full visibility.
        /// </summary>
        public void SetExternalAlpha(float alpha)
        {
            _externalAlphaMultiplier = Mathf.Clamp01(alpha);
        }
        
        /// <summary>
        /// Set external scale multiplier (for boss/elite bars).
        /// </summary>
        public void SetExternalScale(float scale)
        {
            _externalScaleMultiplier = Mathf.Max(0.1f, scale);
        }
        
        /// <summary>
        /// Enable external visibility control mode.
        /// When enabled, the visibility system controls show/hide via alpha.
        /// </summary>
        public void SetUseExternalVisibility(bool enabled)
        {
            _useExternalVisibility = enabled;
            if (enabled)
            {
                // In external mode, always consider internally visible
                _isVisible = true;
                _isFadingOut = false;
            }
        }
        
        /// <summary>
        /// Get current effective alpha (internal * external).
        /// </summary>
        public float EffectiveAlpha => _currentAlpha * _externalAlphaMultiplier;
        
        public void StartFadeOut()
        {
            _isFadingOut = true;
        }
        
        public void Hide()
        {
            _isVisible = false;
            _followTarget = null;
            if (_renderer) _renderer.enabled = false;
            gameObject.SetActive(false);
        }
        
        public void ResetForPool()
        {
            Hide();
            _targetFill = 1f;
            _currentFill = 1f;
            _trailFill = 1f;
            _externalAlphaMultiplier = 1f;
            _externalScaleMultiplier = 1f;
            _useExternalVisibility = false;
            if (_propBlock != null) _propBlock.Clear();
            if (_renderer) _renderer.SetPropertyBlock(_propBlock);
        }
    }
}
