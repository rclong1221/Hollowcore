using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.UI.Effects
{
    /// <summary>
    /// Applies shader-based materials to UI Toolkit elements via RenderTexture.
    /// Attach to a GameObject with a RawImage or use with custom VisualElement.
    /// 
    /// EPIC 15.8: AAA Shader Effects for UI
    /// </summary>
    [ExecuteAlways]
    public class UIShaderEffect : MonoBehaviour
    {
        public enum EffectType
        {
            FrostedGlass,
            GradientBar,
            GlowBorder,
            BatteryCell,
            Scanlines
        }
        
        [Header("Effect Configuration")]
        [SerializeField] private EffectType _effectType = EffectType.GradientBar;
        [SerializeField] private Vector2Int _renderSize = new Vector2Int(256, 32);
        
        [Header("Common Properties")]
        [SerializeField, Range(0, 1)] private float _fillAmount = 1f;
        [SerializeField] private Color _primaryColor = new Color(0.2f, 0.8f, 0.44f);
        [SerializeField] private Color _secondaryColor = new Color(0.91f, 0.3f, 0.24f);
        [SerializeField, Range(0, 2)] private float _glowIntensity = 0.5f;
        [SerializeField, Range(0, 1)] private float _isCritical = 0f;
        
        [Header("Battery Cell Properties")]
        [SerializeField, Range(1, 20)] private int _cellCount = 10;
        [SerializeField] private bool _isOn = true;
        [SerializeField] private bool _isLowBattery = false;
        [SerializeField] private bool _isFlickering = false;
        
        private Material _material;
        private RenderTexture _renderTexture;
        private static Shader _frostedGlassShader;
        private static Shader _gradientBarShader;
        private static Shader _glowBorderShader;
        private static Shader _batteryCellShader;
        private static Shader _scanlinesShader;
        
        public RenderTexture RenderTexture => _renderTexture;
        public Material Material => _material;
        
        public float FillAmount
        {
            get => _fillAmount;
            set
            {
                _fillAmount = Mathf.Clamp01(value);
                UpdateMaterial();
            }
        }
        
        public bool IsCritical
        {
            get => _isCritical > 0.5f;
            set
            {
                _isCritical = value ? 1f : 0f;
                UpdateMaterial();
            }
        }
        
        public bool IsOn
        {
            get => _isOn;
            set
            {
                _isOn = value;
                UpdateMaterial();
            }
        }
        
        public bool IsLowBattery
        {
            get => _isLowBattery;
            set
            {
                _isLowBattery = value;
                UpdateMaterial();
            }
        }
        
        public bool IsFlickering
        {
            get => _isFlickering;
            set
            {
                _isFlickering = value;
                UpdateMaterial();
            }
        }
        
        private void OnEnable()
        {
            LoadShaders();
            CreateMaterial();
            CreateRenderTexture();
        }
        
        private void OnDisable()
        {
            CleanupResources();
        }
        
        private void OnValidate()
        {
            if (_material != null)
            {
                UpdateMaterial();
            }
        }
        
        private void Update()
        {
            // Render to texture each frame for animated effects
            if (_material != null && _renderTexture != null)
            {
                RenderToTexture();
            }
        }
        
        private void LoadShaders()
        {
            if (_frostedGlassShader == null)
                _frostedGlassShader = Shader.Find("DIG/UI/FrostedGlass");
            if (_gradientBarShader == null)
                _gradientBarShader = Shader.Find("DIG/UI/GradientBar");
            if (_glowBorderShader == null)
                _glowBorderShader = Shader.Find("DIG/UI/GlowBorder");
            if (_batteryCellShader == null)
                _batteryCellShader = Shader.Find("DIG/UI/BatteryCell");
            if (_scanlinesShader == null)
                _scanlinesShader = Shader.Find("DIG/UI/Scanlines");
        }
        
        private Shader GetShaderForEffect()
        {
            return _effectType switch
            {
                EffectType.FrostedGlass => _frostedGlassShader,
                EffectType.GradientBar => _gradientBarShader,
                EffectType.GlowBorder => _glowBorderShader,
                EffectType.BatteryCell => _batteryCellShader,
                EffectType.Scanlines => _scanlinesShader,
                _ => _gradientBarShader
            };
        }
        
        private void CreateMaterial()
        {
            var shader = GetShaderForEffect();
            if (shader == null)
            {
                Debug.LogError($"[UIShaderEffect] Shader not found for effect type: {_effectType}");
                return;
            }
            
            _material = new Material(shader);
            _material.hideFlags = HideFlags.HideAndDontSave;
            UpdateMaterial();
        }
        
        private void CreateRenderTexture()
        {
            _renderTexture = new RenderTexture(_renderSize.x, _renderSize.y, 0, RenderTextureFormat.ARGB32);
            _renderTexture.filterMode = FilterMode.Bilinear;
            _renderTexture.Create();
        }
        
        private void CleanupResources()
        {
            if (_material != null)
            {
                if (Application.isPlaying)
                    Destroy(_material);
                else
                    DestroyImmediate(_material);
                _material = null;
            }
            
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                if (Application.isPlaying)
                    Destroy(_renderTexture);
                else
                    DestroyImmediate(_renderTexture);
                _renderTexture = null;
            }
        }
        
        private void UpdateMaterial()
        {
            if (_material == null) return;
            
            switch (_effectType)
            {
                case EffectType.GradientBar:
                    _material.SetFloat("_FillAmount", _fillAmount);
                    _material.SetFloat("_GlowIntensity", _glowIntensity);
                    _material.SetFloat("_IsCritical", _isCritical);
                    _material.SetColor("_ColorFull", _primaryColor);
                    _material.SetColor("_ColorCritical", _secondaryColor);
                    break;
                    
                case EffectType.GlowBorder:
                    _material.SetColor("_GlowColor", _primaryColor);
                    _material.SetFloat("_GlowIntensity", _glowIntensity);
                    break;
                    
                case EffectType.BatteryCell:
                    _material.SetFloat("_FillAmount", _fillAmount);
                    _material.SetFloat("_CellCount", _cellCount);
                    _material.SetFloat("_IsOn", _isOn ? 1f : 0f);
                    _material.SetFloat("_IsLowBattery", _isLowBattery ? 1f : 0f);
                    _material.SetFloat("_IsFlickering", _isFlickering ? 1f : 0f);
                    _material.SetColor("_ColorFull", _primaryColor);
                    _material.SetColor("_ColorLow", _secondaryColor);
                    break;
                    
                case EffectType.FrostedGlass:
                    _material.SetColor("_TintColor", _primaryColor);
                    break;
                    
                case EffectType.Scanlines:
                    _material.SetFloat("_ScanlineIntensity", _glowIntensity);
                    break;
            }
        }
        
        private void RenderToTexture()
        {
            // Render a fullscreen quad with the material to the render texture
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _renderTexture;
            
            GL.Clear(true, true, Color.clear);
            GL.PushMatrix();
            GL.LoadOrtho();
            
            _material.SetPass(0);
            
            GL.Begin(GL.QUADS);
            GL.Color(Color.white);
            GL.TexCoord2(0, 0); GL.Vertex3(0, 0, 0);
            GL.TexCoord2(1, 0); GL.Vertex3(1, 0, 0);
            GL.TexCoord2(1, 1); GL.Vertex3(1, 1, 0);
            GL.TexCoord2(0, 1); GL.Vertex3(0, 1, 0);
            GL.End();
            
            GL.PopMatrix();
            RenderTexture.active = prev;
        }
    }
}
