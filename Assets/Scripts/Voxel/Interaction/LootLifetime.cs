using UnityEngine;

namespace DIG.Voxel.Interaction
{
    /// <summary>
    /// Task 10.17.15: Auto-destroys loot after configurable lifetime with optional fade.
    /// Attach this to loot prefabs to prevent memory leaks.
    /// </summary>
    public class LootLifetime : MonoBehaviour
    {
        [Header("Lifetime Settings")]
        [Tooltip("Time until destruction (set by spawn system)")]
        public float Lifetime = 60f;
        
        [Tooltip("Duration of fade-out before destruction")]
        public float FadeDuration = 2f;
        
        private float _spawnTime;
        private float _fadeStartTime;
        private bool _isFading;
        private Renderer _renderer;
        private MaterialPropertyBlock _propBlock;
        private Color _originalColor;
        
        private void Awake()
        {
            _spawnTime = Time.time;
            _fadeStartTime = _spawnTime + Lifetime - FadeDuration;
            _renderer = GetComponent<Renderer>();
            _propBlock = new MaterialPropertyBlock();
            
            if (_renderer != null && _renderer.material != null)
            {
                _originalColor = _renderer.material.color;
            }
        }
        
        private void Update()
        {
            float elapsed = Time.time - _spawnTime;
            
            // Check if lifetime expired
            if (elapsed >= Lifetime)
            {
                Destroy(gameObject);
                return;
            }
            
            // Handle fading
            if (FadeDuration > 0 && Time.time >= _fadeStartTime)
            {
                if (!_isFading)
                {
                    _isFading = true;
                    // Ensure we have a renderer
                    if (_renderer == null)
                    {
                        _renderer = GetComponent<Renderer>();
                    }
                }
                
                if (_renderer != null)
                {
                    float fadeProgress = (Time.time - _fadeStartTime) / FadeDuration;
                    float alpha = Mathf.Lerp(1f, 0f, fadeProgress);
                    
                    // Use property block to avoid material instance creation
                    _renderer.GetPropertyBlock(_propBlock);
                    Color fadedColor = _originalColor;
                    fadedColor.a = alpha;
                    _propBlock.SetColor("_BaseColor", fadedColor);
                    _propBlock.SetColor("_Color", fadedColor); // Fallback for non-URP
                    _renderer.SetPropertyBlock(_propBlock);
                }
            }
        }
        
        /// <summary>
        /// Initialize lifetime settings from LootPhysicsSettings.
        /// Called by spawn system after instantiation.
        /// </summary>
        public void Initialize(float lifetime, float fadeDuration)
        {
            Lifetime = lifetime;
            FadeDuration = fadeDuration;
            _spawnTime = Time.time;
            _fadeStartTime = _spawnTime + Lifetime - FadeDuration;
            _isFading = false;
        }
        
        /// <summary>
        /// Reset lifetime (e.g., when player interacts with loot).
        /// </summary>
        public void ResetLifetime()
        {
            _spawnTime = Time.time;
            _fadeStartTime = _spawnTime + Lifetime - FadeDuration;
            _isFading = false;
            
            // Reset alpha if was fading
            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_BaseColor", _originalColor);
                _propBlock.SetColor("_Color", _originalColor);
                _renderer.SetPropertyBlock(_propBlock);
            }
        }
    }
}
