using UnityEngine;
using UnityEngine.UI;

namespace DIG.UI.Effects
{
    /// <summary>
    /// Simple component to drive shader-based battery/flashlight bar.
    /// Attach to a UI Image with the ProceduralBatteryBar material.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [ExecuteAlways]
    public class ShaderBatteryBar : MonoBehaviour
    {
        [Header("Battery State")]
        [Range(0f, 1f)]
        [SerializeField] private float _batteryPercent = 1f;
        [SerializeField] private bool _isOn = true;
        [SerializeField] private bool _isFlickering;
        [SerializeField] private bool _isLowBattery;
        
        [Header("Thresholds")]
        [SerializeField] private float _lowBatteryThreshold = 0.2f;
        [SerializeField] private float _flickerThreshold = 0.1f;
        
        [Header("Animation")]
        [SerializeField] private float _smoothSpeed = 8f;
        
        private Image _image;
        private Material _materialInstance;
        private float _displayBattery = 1f;
        
        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
        private static readonly int IsOnId = Shader.PropertyToID("_IsOn");
        private static readonly int IsFlickeringId = Shader.PropertyToID("_IsFlickering");
        private static readonly int IsLowBatteryId = Shader.PropertyToID("_IsLowBattery");
        
        public float BatteryPercent
        {
            get => _batteryPercent;
            set
            {
                _batteryPercent = Mathf.Clamp01(value);
                UpdateStates();
            }
        }
        
        public bool IsOn
        {
            get => _isOn;
            set => _isOn = value;
        }
        
        public bool IsFlickering => _isFlickering;
        public bool IsLowBattery => _isLowBattery;
        
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
            _displayBattery = _batteryPercent;
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
        
        private void UpdateStates()
        {
            _isLowBattery = _batteryPercent <= _lowBatteryThreshold;
            _isFlickering = _batteryPercent <= _flickerThreshold;
        }
        
        private void Update()
        {
            if (_materialInstance == null) return;
            
            // Smooth battery animation
            _displayBattery = Mathf.Lerp(_displayBattery, _batteryPercent, Time.deltaTime * _smoothSpeed);
            
            // Update shader
            _materialInstance.SetFloat(FillAmountId, _displayBattery);
            _materialInstance.SetFloat(IsOnId, _isOn ? 1f : 0f);
            _materialInstance.SetFloat(IsFlickeringId, _isFlickering ? 1f : 0f);
            _materialInstance.SetFloat(IsLowBatteryId, _isLowBattery ? 1f : 0f);
        }
        
        /// <summary>
        /// Drain battery by normalized amount.
        /// </summary>
        public void Drain(float normalizedAmount)
        {
            BatteryPercent = _batteryPercent - normalizedAmount;
        }
        
        /// <summary>
        /// Recharge battery.
        /// </summary>
        public void Recharge(float normalizedAmount)
        {
            BatteryPercent = _batteryPercent + normalizedAmount;
        }
        
        /// <summary>
        /// Toggle flashlight on/off.
        /// </summary>
        public void Toggle()
        {
            _isOn = !_isOn;
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            UpdateStates();
            if (_materialInstance != null)
            {
                _materialInstance.SetFloat(FillAmountId, _batteryPercent);
                _materialInstance.SetFloat(IsOnId, _isOn ? 1f : 0f);
                _materialInstance.SetFloat(IsFlickeringId, _isFlickering ? 1f : 0f);
                _materialInstance.SetFloat(IsLowBatteryId, _isLowBattery ? 1f : 0f);
            }
        }
#endif
    }
}
