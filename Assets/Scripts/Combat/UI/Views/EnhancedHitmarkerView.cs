// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · EnhancedHitmarkerView
// Enhanced hitmarker UI with animations and hit type differentiation
// ════════════════════════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UIElements;
using DIG.Targeting.Theming;

namespace DIG.Combat.UI.Views
{
    /// <summary>
    /// EPIC 15.9: Enhanced hitmarker view with animations, hit type styling,
    /// and configurable visuals. Uses UI Toolkit for screen-space display.
    /// </summary>
    public class EnhancedHitmarkerView : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────
        // Configuration
        // ─────────────────────────────────────────────────────────────────
        [Header("Configuration")]
        [SerializeField] private Config.HitmarkerConfig _config;
        
        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        
        // ─────────────────────────────────────────────────────────────────
        // UI Elements
        // ─────────────────────────────────────────────────────────────────
        private UIDocument _document;
        private VisualElement _hitmarkerContainer;
        private VisualElement _hitmarkerImage;
        
        // ─────────────────────────────────────────────────────────────────
        // Animation State
        // ─────────────────────────────────────────────────────────────────
        private float _displayTimer;
        private float _currentDuration;
        private float _startScale;
        private float _targetScale;
        private bool _isAnimating;
        private HitType _currentHitType;
        
        // ─────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null)
            {
                Debug.LogError("[EnhancedHitmarkerView] No UIDocument found!");
                return;
            }
        }
        
        private void OnEnable()
        {
            SetupUI();
        }
        
        private void SetupUI()
        {
            if (_document == null) return;
            
            var root = _document.rootVisualElement;
            
            _hitmarkerContainer = root.Q<VisualElement>("hitmarker-container");
            if (_hitmarkerContainer == null)
            {
                _hitmarkerContainer = new VisualElement { name = "hitmarker-container" };
                _hitmarkerContainer.AddToClassList("hitmarker-container");
                _hitmarkerContainer.style.position = Position.Absolute;
                _hitmarkerContainer.style.left = new Length(50, LengthUnit.Percent);
                _hitmarkerContainer.style.top = new Length(50, LengthUnit.Percent);
                _hitmarkerContainer.style.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(-50, LengthUnit.Percent));
                root.Add(_hitmarkerContainer);
            }
            
            _hitmarkerImage = _hitmarkerContainer.Q<VisualElement>("hitmarker-image");
            if (_hitmarkerImage == null)
            {
                _hitmarkerImage = new VisualElement { name = "hitmarker-image" };
                _hitmarkerImage.AddToClassList("hitmarker-image");
                _hitmarkerContainer.Add(_hitmarkerImage);
            }
            
            // Initially hidden
            _hitmarkerContainer.style.opacity = 0f;
        }
        
        private void Update()
        {
            if (!_isAnimating) return;
            
            _displayTimer += Time.deltaTime;
            float t = _displayTimer / _currentDuration;
            
            if (t >= 1f)
            {
                _hitmarkerContainer.style.opacity = 0f;
                _isAnimating = false;
                return;
            }
            
            // Apply fade curve
            float alpha = _config != null ? _config.FadeOutCurve.Evaluate(t) : 1f - t;
            _hitmarkerContainer.style.opacity = alpha;
            
            // Apply scale animation
            if (_config != null && _config.EnableScalePunch)
            {
                float scaleT = Mathf.Clamp01(_displayTimer / _config.ScalePunchDuration);
                float scale = Mathf.Lerp(_startScale, _targetScale, scaleT);
                _hitmarkerImage.style.scale = new Scale(new Vector2(scale, scale));
            }
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────
        
        /// <summary>Show hitmarker for a hit</summary>
        public void ShowHit(HitType hitType = HitType.Hit)
        {
            if (_hitmarkerContainer == null) return;
            
            _currentHitType = hitType;
            _displayTimer = 0f;
            _isAnimating = true;
            
            // Calculate duration
            _currentDuration = _config?.DisplayDuration ?? 0.15f;
            if (hitType == HitType.Critical)
            {
                // No extra duration for crits, just scale
            }
            
            // Calculate scale
            float baseSize = _config?.DefaultSize ?? 32f;
            _targetScale = 1f;
            _startScale = _config?.EnableScalePunch == true ? (_config.ScalePunchAmount) : 1f;
            
            if (hitType == HitType.Critical)
            {
                _startScale *= _config?.CriticalScaleMultiplier ?? 1.5f;
            }
            
            // Set color
            Color color = GetColorForHitType(hitType);
            _hitmarkerImage.style.unityBackgroundImageTintColor = color;
            
            // Set sprite/class based on type
            _hitmarkerImage.ClearClassList();
            _hitmarkerImage.AddToClassList("hitmarker-image");
            _hitmarkerImage.AddToClassList($"hitmarker-{hitType.ToString().ToLowerInvariant()}");
            
            // Set size
            _hitmarkerImage.style.width = baseSize;
            _hitmarkerImage.style.height = baseSize;
            
            // Show
            _hitmarkerContainer.style.opacity = 1f;
            _hitmarkerImage.style.scale = new Scale(new Vector2(_startScale, _startScale));
            
            // Play sound
            PlayHitSound(hitType);
        }
        
        /// <summary>Show kill confirmation hitmarker</summary>
        public void ShowKill(bool isHeadshot = false)
        {
            if (_hitmarkerContainer == null) return;
            
            _displayTimer = 0f;
            _isAnimating = true;
            _currentDuration = (_config?.DisplayDuration ?? 0.15f) * (_config?.KillDurationMultiplier ?? 2f);
            
            // Set kill styling
            _hitmarkerImage.ClearClassList();
            _hitmarkerImage.AddToClassList("hitmarker-image");
            _hitmarkerImage.AddToClassList("hitmarker-kill");
            if (isHeadshot) _hitmarkerImage.AddToClassList("hitmarker-headshot-kill");
            
            _hitmarkerImage.style.unityBackgroundImageTintColor = _config?.KillConfirmColor ?? Color.red;
            
            float size = (_config?.DefaultSize ?? 32f) * (_config?.CriticalScaleMultiplier ?? 1.5f);
            _hitmarkerImage.style.width = size;
            _hitmarkerImage.style.height = size;
            
            _startScale = _config?.EnableScalePunch == true ? 1.5f : 1f;
            _targetScale = 1f;
            
            _hitmarkerContainer.style.opacity = 1f;
            _hitmarkerImage.style.scale = new Scale(new Vector2(_startScale, _startScale));
            
            // Play kill sound
            PlaySound(_config?.KillSound);
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────
        private Color GetColorForHitType(HitType hitType)
        {
            if (_config == null) return Color.white;
            
            return hitType switch
            {
                HitType.Critical => _config.CriticalHitColor,
                HitType.Miss or HitType.Graze => _config.ShieldHitColor,
                _ => _config.NormalHitColor
            };
        }
        
        private void PlayHitSound(HitType hitType)
        {
            if (_config == null || !_config.EnableHitSounds) return;
            
            AudioClip clip = hitType switch
            {
                HitType.Critical => _config.CriticalHitSound,
                _ => _config.NormalHitSound
            };
            
            PlaySound(clip);
        }
        
        private void PlaySound(AudioClip clip)
        {
            if (clip == null) return;
            
            if (_audioSource != null)
            {
                _audioSource.PlayOneShot(clip, _config?.HitSoundVolume ?? 0.5f);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position, _config?.HitSoundVolume ?? 0.5f);
            }
        }
    }
}
