using UnityEngine;
using UnityEngine.UI;
using Player.UI.ViewModels;

namespace Player.UI.Views
{
    /// <summary>
    /// ShaderView for Shield bar - hexagon pattern, recharge shimmer.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class ShieldShaderView : MonoBehaviour
    {
        [Header("ViewModel Reference")]
        [SerializeField] private ShieldViewModel _viewModel;
        
        [Header("Shader Path")]
        [SerializeField] private string _shaderPath = "DIG/UI/ProceduralShieldBar";
        
        private RawImage _rawImage;
        private Material _material;
        
        private static readonly int FillID = Shader.PropertyToID("_Fill");
        private static readonly int IsRechargingID = Shader.PropertyToID("_IsRecharging");
        private static readonly int IsBrokenID = Shader.PropertyToID("_IsBroken");
        private static readonly int RechargeDelayID = Shader.PropertyToID("_RechargeDelay");
        
        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            InitMaterial();
            
            if (_viewModel == null)
                _viewModel = GetComponentInParent<ShieldViewModel>();
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
                _viewModel.OnShieldBroke += OnShieldBroke;
            }
        }
        
        private void OnDisable()
        {
            if (_viewModel != null)
            {
                _viewModel.OnChanged -= OnViewModelChanged;
                _viewModel.OnShieldBroke -= OnShieldBroke;
            }
        }
        
        private void OnViewModelChanged(ShieldViewModel vm)
        {
            if (_material == null) return;
            _material.SetFloat(FillID, vm.Percent);
            _material.SetFloat(IsRechargingID, vm.IsRecharging ? 1f : 0f);
            _material.SetFloat(IsBrokenID, vm.IsBroken ? 1f : 0f);
            _material.SetFloat(RechargeDelayID, vm.RechargeDelay);
        }
        
        private void OnShieldBroke()
        {
            // Can trigger shield break VFX/sound here
        }
        
        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }
    }
}
