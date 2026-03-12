using UnityEngine;
using UnityEngine.UI;
using Player.UI.ViewModels;

namespace Player.UI.Views
{
    /// <summary>
    /// ShaderView for Infection bar.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class InfectionShaderView : MonoBehaviour
    {
        [Header("ViewModel Reference")]
        [SerializeField] private InfectionViewModel _viewModel;
        
        [Header("Shader Path")]
        [SerializeField] private string _shaderPath = "DIG/UI/ProceduralInfectionBar";
        
        private RawImage _rawImage;
        private Material _material;
        
        private static readonly int FillID = Shader.PropertyToID("_Fill");
        private static readonly int IsSpreadingID = Shader.PropertyToID("_IsSpreading");
        
        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            InitMaterial();
            
            if (_viewModel == null)
                _viewModel = GetComponentInParent<InfectionViewModel>();
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
        
        private void OnViewModelChanged(InfectionViewModel vm)
        {
            if (_material == null) return;
            // Infection fills from right-to-left, shader handles this
            _material.SetFloat(FillID, vm.Percent);
            _material.SetFloat(IsSpreadingID, vm.IsSpreading ? 1f : 0f);
        }
        
        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }
    }
}
