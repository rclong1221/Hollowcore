using UnityEngine;
using UnityEngine.UI;
using Visuals.UI.ViewModels;

namespace Visuals.UI.Views
{
    /// <summary>
    /// View adapter that connects FlashlightViewModel to a procedural shader material.
    /// This is the "View" layer - purely visual, no ECS logic.
    /// 
    /// Architecture:
    ///   [ECS] -> [FlashlightViewModel] -> [FlashlightShaderView] -> [Shader Material]
    ///                                      (this class)
    /// 
    /// To use a different UI (UI Toolkit, IMGUI, etc.), create a different View class
    /// that reads from the same FlashlightViewModel.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class FlashlightShaderView : MonoBehaviour
    {
        [Header("Data Source")]
        [Tooltip("If null, will search parent hierarchy for FlashlightViewModel")]
        [SerializeField] private FlashlightViewModel _viewModel;
        
        [Header("Shader")]
        [SerializeField] private Shader _shader;

        [Header("Shader Settings")]
        [Tooltip("When true, fill drains smoothly. When false, uses chunky segments.")]
        [SerializeField] private bool _smoothFill = true;

        private Image _image;
        private Material _materialInstance;
        
        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
        private static readonly int IsOnId = Shader.PropertyToID("_IsOn");
        private static readonly int IsFlickeringId = Shader.PropertyToID("_IsFlickering");
        private static readonly int IsLowBatteryId = Shader.PropertyToID("_IsLowBattery");
        private static readonly int SmoothFillId = Shader.PropertyToID("_SmoothFill");
        
        private const string ShaderName = "DIG/UI/ProceduralBatteryBar";
        
        private void Awake()
        {
            _image = GetComponent<Image>();
            
            var shader = _shader != null ? _shader : Shader.Find(ShaderName);
            if (shader != null)
            {
                _materialInstance = new Material(shader);
                _materialInstance.SetFloat(SmoothFillId, _smoothFill ? 1f : 0f);
                _image.material = _materialInstance;
            }
            else
            {
                Debug.LogError($"[FlashlightShaderView] Shader '{ShaderName}' not found! Assign it in the Inspector.");
            }
        }
        
        private void OnEnable()
        {
            // Find ViewModel if not assigned
            if (_viewModel == null)
                _viewModel = GetComponentInParent<FlashlightViewModel>();
            
            if (_viewModel != null)
                _viewModel.OnChanged += OnViewModelChanged;
        }
        
        private void OnDisable()
        {
            if (_viewModel != null)
                _viewModel.OnChanged -= OnViewModelChanged;
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
                private void OnViewModelChanged(FlashlightViewModel vm)
        {
            if (_materialInstance == null) return;
            
            _materialInstance.SetFloat(FillAmountId, vm.BatteryPercent);
            _materialInstance.SetFloat(IsOnId, vm.IsOn ? 1f : 0f);
            _materialInstance.SetFloat(IsFlickeringId, vm.IsFlickering ? 1f : 0f);
            _materialInstance.SetFloat(IsLowBatteryId, vm.IsLowBattery ? 1f : 0f);
        }
    }
}
