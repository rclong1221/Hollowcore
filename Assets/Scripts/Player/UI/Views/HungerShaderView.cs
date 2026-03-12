using UnityEngine;
using UnityEngine.UI;
using Player.UI.ViewModels;

namespace Player.UI.Views
{
    /// <summary>
    /// ShaderView for Hunger bar.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class HungerShaderView : MonoBehaviour
    {
        [Header("ViewModel Reference")]
        [SerializeField] private HungerViewModel _viewModel;
        
        [Header("Shader Path")]
        [SerializeField] private string _shaderPath = "DIG/UI/ProceduralHungerBar";
        
        private RawImage _rawImage;
        private Material _material;
        
        private static readonly int FillID = Shader.PropertyToID("_Fill");
        private static readonly int IsHungryID = Shader.PropertyToID("_IsHungry");
        private static readonly int IsStarvingID = Shader.PropertyToID("_IsStarving");
        
        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            InitMaterial();
            
            if (_viewModel == null)
                _viewModel = GetComponentInParent<HungerViewModel>();
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
        
        private void OnViewModelChanged(HungerViewModel vm)
        {
            if (_material == null) return;
            _material.SetFloat(FillID, vm.SatietyPercent);
            _material.SetFloat(IsHungryID, vm.IsHungry ? 1f : 0f);
            _material.SetFloat(IsStarvingID, vm.IsStarving ? 1f : 0f);
        }
        
        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }
    }
}
