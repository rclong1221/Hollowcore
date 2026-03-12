using UnityEngine;
using UnityEngine.UI;

namespace DIG.UI.Effects
{
    /// <summary>
    /// Simple component to drive shader-based health bar.
    /// Attach to a UI Image with the ProceduralHealthBar material.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [ExecuteAlways]
    public class ShaderHealthBar : MonoBehaviour
    {
        [Header("Health")]
        [Range(0f, 1f)]
        [SerializeField] private float _healthPercent = 1f;
        [SerializeField] private bool _isCritical;
        
        [Header("Thresholds")]
        [SerializeField] private float _criticalThreshold = 0.25f;
        
        [Header("Animation")]
        [SerializeField] private float _smoothSpeed = 5f;
        [SerializeField] private bool _animateDamage = true;
        
        private Image _image;
        private Material _materialInstance;
        private float _displayHealth = 1f;
        private float _targetHealth = 1f;
        
        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
        private static readonly int IsCriticalId = Shader.PropertyToID("_IsCritical");
        
        public float HealthPercent
        {
            get => _healthPercent;
            set
            {
                _targetHealth = Mathf.Clamp01(value);
                _healthPercent = _targetHealth;
                _isCritical = _healthPercent <= _criticalThreshold;
            }
        }
        
        public bool IsCritical => _isCritical;
        
        private void Awake()
        {
            _image = GetComponent<Image>();
            CreateMaterialInstance();
        }
        
        private void OnEnable()
        {
            if (_materialInstance == null)
            {
                CreateMaterialInstance();
            }
            _displayHealth = _healthPercent;
        }
        
        private void OnDestroy()
        {
            if (_materialInstance != null)
            {
                if (Application.isPlaying)
                    Destroy(_materialInstance);
                else
                    DestroyImmediate(_materialInstance);
            }
        }
        
        private void CreateMaterialInstance()
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_image.material != null)
            {
                _materialInstance = new Material(_image.material);
                _image.material = _materialInstance;
            }
        }
        
        private void Update()
        {
            if (_materialInstance == null) return;
            
            // Smooth health animation
            if (_animateDamage)
            {
                _displayHealth = Mathf.Lerp(_displayHealth, _targetHealth, Time.deltaTime * _smoothSpeed);
            }
            else
            {
                _displayHealth = _targetHealth;
            }
            
            // Update shader
            _materialInstance.SetFloat(FillAmountId, _displayHealth);
            _materialInstance.SetFloat(IsCriticalId, _isCritical ? 1f : 0f);
        }
        
        /// <summary>
        /// Apply damage with optional animation burst.
        /// </summary>
        public void TakeDamage(float normalizedDamage)
        {
            HealthPercent = _healthPercent - normalizedDamage;
        }
        
        /// <summary>
        /// Heal with optional animation.
        /// </summary>
        public void Heal(float normalizedAmount)
        {
            HealthPercent = _healthPercent + normalizedAmount;
        }
        
        /// <summary>
        /// Set health immediately without animation.
        /// </summary>
        public void SetHealthImmediate(float percent)
        {
            _healthPercent = Mathf.Clamp01(percent);
            _targetHealth = _healthPercent;
            _displayHealth = _healthPercent;
            _isCritical = _healthPercent <= _criticalThreshold;
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            _isCritical = _healthPercent <= _criticalThreshold;
            if (_materialInstance != null)
            {
                _materialInstance.SetFloat(FillAmountId, _healthPercent);
                _materialInstance.SetFloat(IsCriticalId, _isCritical ? 1f : 0f);
            }
        }
#endif
    }
}
