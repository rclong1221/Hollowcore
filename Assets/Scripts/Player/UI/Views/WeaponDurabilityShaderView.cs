using UnityEngine;
using UnityEngine.UI;
using Player.UI.ViewModels;

namespace Player.UI.Views
{
    /// <summary>
    /// ShaderView for Weapon Durability.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class WeaponDurabilityShaderView : MonoBehaviour
    {
        [Header("ViewModel Reference")]
        [SerializeField] private WeaponDurabilityViewModel _viewModel;
        
        [Header("Shader Path")]
        [SerializeField] private string _shaderPath = "DIG/UI/ProceduralDurabilityBar";
        
        private RawImage _rawImage;
        private Material _material;
        
        private static readonly int FillID = Shader.PropertyToID("_Fill");
        private static readonly int IsLowID = Shader.PropertyToID("_IsLow");
        private static readonly int IsBrokenID = Shader.PropertyToID("_IsBroken");
        
        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            InitMaterial();
            
            if (_viewModel == null)
                _viewModel = GetComponentInParent<WeaponDurabilityViewModel>();
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
                _viewModel.OnBroke += OnWeaponBroke;
            }
        }
        
        private void OnDisable()
        {
            if (_viewModel != null)
            {
                _viewModel.OnChanged -= OnViewModelChanged;
                _viewModel.OnBroke -= OnWeaponBroke;
            }
        }
        
        private void OnViewModelChanged(WeaponDurabilityViewModel vm)
        {
            if (_material == null) return;
            _material.SetFloat(FillID, vm.Percent);
            _material.SetFloat(IsLowID, vm.IsLow ? 1f : 0f);
            _material.SetFloat(IsBrokenID, vm.IsBroken ? 1f : 0f);
        }
        
        private void OnWeaponBroke()
        {
            // Can trigger weapon break VFX/sound here
        }
        
        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }
    }
}
