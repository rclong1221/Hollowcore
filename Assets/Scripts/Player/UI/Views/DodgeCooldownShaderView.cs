using UnityEngine;
using UnityEngine.UI;
using Player.UI.ViewModels;

namespace Player.UI.Views
{
    /// <summary>
    /// ShaderView for Dodge Cooldown (circular sweep).
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class DodgeCooldownShaderView : MonoBehaviour
    {
        [Header("ViewModel Reference")]
        [SerializeField] private DodgeCooldownViewModel _viewModel;
        
        [Header("Shader Path")]
        [SerializeField] private string _shaderPath = "DIG/UI/ProceduralCooldownBar";
        
        [Header("Flash on Ready")]
        [SerializeField] private bool _flashOnReady = true;
        [SerializeField] private float _flashDuration = 0.3f;
        
        private RawImage _rawImage;
        private Material _material;
        private float _flashTimer;
        
        private static readonly int FillID = Shader.PropertyToID("_Fill");
        private static readonly int IsReadyID = Shader.PropertyToID("_IsReady");
        private static readonly int FlashID = Shader.PropertyToID("_Flash");
        
        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            InitMaterial();
            
            if (_viewModel == null)
                _viewModel = GetComponentInParent<DodgeCooldownViewModel>();
        }
        
        private void InitMaterial()
        {
            var shader = Shader.Find(_shaderPath);
            if (shader == null) { Debug.LogError($"Shader not found: {_shaderPath}"); return; }
            _material = new Material(shader);
            _rawImage.material = _material;
        }
        
        private void OnEnable()
        {
            if (_viewModel != null)
            {
                _viewModel.OnChanged += OnViewModelChanged;
                _viewModel.OnBecameReady += OnBecameReady;
            }
        }
        
        private void OnDisable()
        {
            if (_viewModel != null)
            {
                _viewModel.OnChanged -= OnViewModelChanged;
                _viewModel.OnBecameReady -= OnBecameReady;
            }
        }
        
        private void Update()
        {
            if (_flashTimer > 0)
            {
                _flashTimer -= Time.deltaTime;
                if (_material != null)
                    _material.SetFloat(FlashID, Mathf.Clamp01(_flashTimer / _flashDuration));
            }
        }
        
        private void OnViewModelChanged(DodgeCooldownViewModel vm)
        {
            if (_material == null) return;
            _material.SetFloat(FillID, vm.ReadyPercent);
            _material.SetFloat(IsReadyID, vm.IsReady ? 1f : 0f);
        }
        
        private void OnBecameReady()
        {
            if (_flashOnReady)
                _flashTimer = _flashDuration;
        }
        
        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }
    }
}
