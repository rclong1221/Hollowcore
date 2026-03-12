using UnityEngine;
using UnityEngine.UI;
using Player.UI.ViewModels;

namespace Player.UI.Views
{
    /// <summary>
    /// ShaderView for Ability Charges (segmented discrete charges).
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class AbilityChargesShaderView : MonoBehaviour
    {
        [Header("ViewModel Reference")]
        [SerializeField] private AbilityChargesViewModel _viewModel;
        
        [Header("Shader Path")]
        [SerializeField] private string _shaderPath = "DIG/UI/ProceduralChargesBar";
        
        private RawImage _rawImage;
        private Material _material;
        
        private static readonly int ChargesID = Shader.PropertyToID("_Charges");
        private static readonly int MaxChargesID = Shader.PropertyToID("_MaxCharges");
        private static readonly int RechargeProgressID = Shader.PropertyToID("_RechargeProgress");
        
        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            InitMaterial();
            
            if (_viewModel == null)
                _viewModel = GetComponentInParent<AbilityChargesViewModel>();
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
                _viewModel.OnChargeGained += OnChargeGained;
            }
        }
        
        private void OnDisable()
        {
            if (_viewModel != null)
            {
                _viewModel.OnChanged -= OnViewModelChanged;
                _viewModel.OnChargeGained -= OnChargeGained;
            }
        }
        
        private void OnViewModelChanged(AbilityChargesViewModel vm)
        {
            if (_material == null) return;
            _material.SetFloat(ChargesID, vm.CurrentCharges);
            _material.SetFloat(MaxChargesID, vm.MaxCharges);
            _material.SetFloat(RechargeProgressID, vm.RechargeProgress);
        }
        
        private void OnChargeGained()
        {
            // Can trigger VFX/sound here
        }
        
        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }
    }
}
