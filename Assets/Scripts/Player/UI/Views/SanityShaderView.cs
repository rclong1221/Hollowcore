using UnityEngine;
using UnityEngine.UI;
using Player.UI.ViewModels;

namespace Player.UI.Views
{
    /// <summary>
    /// ShaderView for Sanity bar.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class SanityShaderView : MonoBehaviour
    {
        [Header("ViewModel Reference")]
        [SerializeField] private SanityViewModel _viewModel;
        
        [Header("Shader Path")]
        [SerializeField] private string _shaderPath = "DIG/UI/ProceduralSanityBar";
        
        private RawImage _rawImage;
        private Material _material;
        
        private static readonly int FillID = Shader.PropertyToID("_Fill");
        private static readonly int DistortionID = Shader.PropertyToID("_Distortion");
        private static readonly int IsUnstableID = Shader.PropertyToID("_IsUnstable");
        
        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            InitMaterial();
            
            if (_viewModel == null)
                _viewModel = GetComponentInParent<SanityViewModel>();
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
        
        private void OnViewModelChanged(SanityViewModel vm)
        {
            if (_material == null) return;
            _material.SetFloat(FillID, vm.Percent);
            _material.SetFloat(DistortionID, vm.DistortionIntensity);
            _material.SetFloat(IsUnstableID, vm.IsUnstable || vm.IsInsane ? 1f : 0f);
        }
        
        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }
    }
}
