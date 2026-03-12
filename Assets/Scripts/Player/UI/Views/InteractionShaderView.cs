using UnityEngine;
using UnityEngine.UI;
using Player.UI.ViewModels;

namespace Player.UI.Views
{
    /// <summary>
    /// ShaderView for Interaction Progress (circular progress ring).
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class InteractionShaderView : MonoBehaviour
    {
        [Header("ViewModel Reference")]
        [SerializeField] private InteractionViewModel _viewModel;
        
        [Header("Shader Path")]
        [SerializeField] private string _shaderPath = "DIG/UI/ProceduralInteractionBar";
        
        [Header("Auto-Hide")]
        [SerializeField] private bool _autoHide = true;
        
        private RawImage _rawImage;
        private Material _material;
        private CanvasGroup _canvasGroup;
        
        private static readonly int FillID = Shader.PropertyToID("_Fill");
        private static readonly int IsActiveID = Shader.PropertyToID("_IsActive");
        
        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null && _autoHide)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            InitMaterial();
            
            if (_viewModel == null)
                _viewModel = GetComponentInParent<InteractionViewModel>();
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
                _viewModel.OnCompleted += OnInteractionCompleted;
            }
        }
        
        private void OnDisable()
        {
            if (_viewModel != null)
            {
                _viewModel.OnChanged -= OnViewModelChanged;
                _viewModel.OnCompleted -= OnInteractionCompleted;
            }
        }
        
        private void OnViewModelChanged(InteractionViewModel vm)
        {
            if (_material == null) return;
            _material.SetFloat(FillID, vm.Percent);
            _material.SetFloat(IsActiveID, vm.IsActive ? 1f : 0f);
            
            if (_autoHide && _canvasGroup != null)
                _canvasGroup.alpha = vm.IsActive ? 1f : 0f;
        }
        
        private void OnInteractionCompleted()
        {
            // Can trigger VFX or sound here
        }
        
        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }
    }
}
