using UnityEngine;

namespace DIG.Combat.Resources.UI
{
    /// <summary>
    /// EPIC 16.8 Phase 5: Drives a shader-based resource bar via ResourceBarViewModel.
    /// Same pattern as ShaderStaminaBarSync. Configurable color per resource type.
    /// </summary>
    public class ShaderResourceBarSync : MonoBehaviour
    {
        [SerializeField] private ResourceBarViewModel _viewModel;
        [SerializeField] private Renderer _barRenderer;
        [SerializeField] private string _fillPropertyName = "_FillAmount";
        [SerializeField] private string _colorPropertyName = "_BarColor";
        [SerializeField] private Color _barColor = new Color(0.2f, 0.4f, 1f); // Default blue (mana)

        private MaterialPropertyBlock _mpb;
        private int _fillPropertyId;
        private int _colorPropertyId;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _fillPropertyId = Shader.PropertyToID(_fillPropertyName);
            _colorPropertyId = Shader.PropertyToID(_colorPropertyName);
        }

        private void OnEnable()
        {
            if (_viewModel != null)
                _viewModel.OnChanged += OnResourceChanged;
        }

        private void OnDisable()
        {
            if (_viewModel != null)
                _viewModel.OnChanged -= OnResourceChanged;
        }

        private void OnResourceChanged(ResourceBarViewModel vm)
        {
            if (_barRenderer == null) return;

            _barRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(_fillPropertyId, vm.Percent);
            _mpb.SetColor(_colorPropertyId, _barColor);
            _barRenderer.SetPropertyBlock(_mpb);
        }
    }
}
