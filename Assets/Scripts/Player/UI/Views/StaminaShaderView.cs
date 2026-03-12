using UnityEngine;
using UnityEngine.UI;
using Player.UI.ViewModels;

namespace Player.UI.Views
{
    /// <summary>
    /// View adapter that connects StaminaViewModel to a procedural shader material.
    /// This is the "View" layer - purely visual, no ECS logic.
    /// 
    /// Architecture:
    ///   [ECS] -> [StaminaViewModel] -> [StaminaShaderView] -> [Shader Material]
    ///                                   (this class)
    /// 
    /// To use a different UI (UI Toolkit, IMGUI, etc.), create a different View class
    /// that reads from the same StaminaViewModel.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class StaminaShaderView : MonoBehaviour
    {
        [Header("Data Source")]
        [Tooltip("If null, will search parent hierarchy for StaminaViewModel")]
        [SerializeField] private StaminaViewModel _viewModel;
        
        [Header("Shader")]
        [SerializeField] private Shader _shader;

        [Header("Shader Settings")]
        [Tooltip("When true, fill drains smoothly. When false, uses chunky segments.")]
        [SerializeField] private bool _smoothFill = true;

        private Image _image;
        private Material _materialInstance;
        
        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
        private static readonly int IsDrainingId = Shader.PropertyToID("_IsDraining");
        private static readonly int IsRecoveringId = Shader.PropertyToID("_IsRecovering");
        private static readonly int IsEmptyId = Shader.PropertyToID("_IsEmpty");
        private static readonly int SmoothFillId = Shader.PropertyToID("_SmoothFill");
        
        private const string ShaderName = "DIG/UI/ProceduralStaminaBar";
        
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
                Debug.LogError($"[StaminaShaderView] Shader '{ShaderName}' not found! Assign it in the Inspector.");
            }
        }
        
        private void OnEnable()
        {
            // Find ViewModel if not assigned
            if (_viewModel == null)
                _viewModel = GetComponentInParent<StaminaViewModel>();
            
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
                private void OnViewModelChanged(StaminaViewModel vm)
        {
            if (_materialInstance == null) return;
            
            _materialInstance.SetFloat(FillAmountId, vm.Percent);
            _materialInstance.SetFloat(IsDrainingId, vm.IsDraining ? 1f : 0f);
            _materialInstance.SetFloat(IsRecoveringId, vm.IsRecovering ? 1f : 0f);
            _materialInstance.SetFloat(IsEmptyId, vm.IsEmpty ? 1f : 0f);
        }
    }
}
