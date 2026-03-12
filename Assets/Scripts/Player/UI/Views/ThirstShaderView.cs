using UnityEngine;
using UnityEngine.UI;
using Player.UI.ViewModels;

namespace Player.UI.Views
{
    /// <summary>
    /// ShaderView for Thirst bar.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class ThirstShaderView : MonoBehaviour
    {
        [Header("ViewModel Reference")]
        [SerializeField] private ThirstViewModel _viewModel;
        
        [Header("Shader Path")]
        [SerializeField] private string _shaderPath = "DIG/UI/ProceduralThirstBar";
        
        private RawImage _rawImage;
        private Material _material;
        
        private static readonly int FillID = Shader.PropertyToID("_Fill");
        private static readonly int IsThirstyID = Shader.PropertyToID("_IsThirsty");
        private static readonly int IsDehydratedID = Shader.PropertyToID("_IsDehydrated");
        
        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            InitMaterial();
            
            if (_viewModel == null)
                _viewModel = GetComponentInParent<ThirstViewModel>();
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
        
        private void OnViewModelChanged(ThirstViewModel vm)
        {
            if (_material == null) return;
            _material.SetFloat(FillID, vm.HydrationPercent);
            _material.SetFloat(IsThirstyID, vm.IsThirsty ? 1f : 0f);
            _material.SetFloat(IsDehydratedID, vm.IsDehydrated ? 1f : 0f);
        }
        
        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }
    }
}
