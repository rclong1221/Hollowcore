using UnityEngine;
using UnityEngine.UI;
using Player.UI.ViewModels;

namespace Player.UI.Views
{
    /// <summary>
    /// ShaderView for Oxygen bar - subscribes to OxygenViewModel, updates shader.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class OxygenShaderView : MonoBehaviour
    {
        [Header("ViewModel Reference")]
        [SerializeField] private OxygenViewModel _viewModel;
        
        [Header("Shader Path")]
        [SerializeField] private string _shaderPath = "DIG/UI/ProceduralOxygenBar";
        
        private RawImage _rawImage;
        private Material _material;
        
        // Shader property IDs
        private static readonly int FillID = Shader.PropertyToID("_Fill");
        private static readonly int IsSuffocatingID = Shader.PropertyToID("_IsSuffocating");
        private static readonly int FillColorID = Shader.PropertyToID("_FillColor");
        
        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            InitMaterial();
            
            if (_viewModel == null)
                _viewModel = GetComponentInParent<OxygenViewModel>();
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
                _viewModel.OnChanged += OnViewModelChanged;
        }
        
        private void OnDisable()
        {
            if (_viewModel != null)
                _viewModel.OnChanged -= OnViewModelChanged;
        }
        
        private void OnViewModelChanged(OxygenViewModel vm)
        {
            if (_material == null) return;
            _material.SetFloat(FillID, vm.Percent);
            _material.SetFloat(IsSuffocatingID, vm.IsSuffocating ? 1f : 0f);
        }
        
        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }
    }
}
