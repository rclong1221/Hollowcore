using UnityEngine;
using UnityEngine.UIElements;
using System;

namespace DIG.UI.Effects
{
    /// <summary>
    /// Renders a shader effect to a RenderTexture and displays it in UI Toolkit.
    /// Attach to a GameObject and reference from your UIDocument.
    /// </summary>
    [ExecuteAlways]
    public class ShaderBarRenderer : MonoBehaviour
    {
        [Header("Shader Settings")]
        [SerializeField] private Material _barMaterial;
        [SerializeField] private int _textureWidth = 512;
        [SerializeField] private int _textureHeight = 64;
        
        [Header("Bar Properties")]
        [Range(0f, 1f)]
        [SerializeField] private float _fillAmount = 1f;
        [SerializeField] private bool _isCritical;
        [SerializeField] private bool _showShine = true;
        [SerializeField] private float _glowIntensity = 0.5f;
        
        [Header("Colors")]
        [SerializeField] private Color _colorFull = new Color(0.2f, 0.9f, 0.3f, 1f);
        [SerializeField] private Color _colorMid = new Color(0.9f, 0.9f, 0.2f, 1f);
        [SerializeField] private Color _colorLow = new Color(0.9f, 0.3f, 0.2f, 1f);
        [SerializeField] private Color _colorCritical = new Color(1f, 0.1f, 0.1f, 1f);
        
        private RenderTexture _renderTexture;
        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
        private static readonly int IsCriticalId = Shader.PropertyToID("_IsCritical");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int ShowShineId = Shader.PropertyToID("_ShowShine");
        private static readonly int ColorFullId = Shader.PropertyToID("_ColorFull");
        private static readonly int ColorMidId = Shader.PropertyToID("_ColorMid");
        private static readonly int ColorLowId = Shader.PropertyToID("_ColorLow");
        private static readonly int ColorCriticalId = Shader.PropertyToID("_ColorCritical");
        
        public RenderTexture OutputTexture => _renderTexture;
        
        public float FillAmount
        {
            get => _fillAmount;
            set => _fillAmount = Mathf.Clamp01(value);
        }
        
        public bool IsCritical
        {
            get => _isCritical;
            set => _isCritical = value;
        }
        
        public Color ColorFull
        {
            get => _colorFull;
            set => _colorFull = value;
        }
        
        public Color ColorLow
        {
            get => _colorLow;
            set => _colorLow = value;
        }
        
        private void OnEnable()
        {
            CreateRenderTexture();
        }
        
        private void OnDisable()
        {
            ReleaseRenderTexture();
        }
        
        private void OnValidate()
        {
            if (_renderTexture != null)
            {
                RenderBar();
            }
        }
        
        private void Update()
        {
            if (_barMaterial != null && _renderTexture != null)
            {
                RenderBar();
            }
        }
        
        private void CreateRenderTexture()
        {
            if (_renderTexture != null && 
                _renderTexture.width == _textureWidth && 
                _renderTexture.height == _textureHeight)
            {
                return;
            }
            
            ReleaseRenderTexture();
            
            _renderTexture = new RenderTexture(_textureWidth, _textureHeight, 0, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                name = $"ShaderBar_{gameObject.name}"
            };
            _renderTexture.Create();
        }
        
        private void ReleaseRenderTexture()
        {
            if (_renderTexture != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_renderTexture);
                }
                else
                {
                    DestroyImmediate(_renderTexture);
                }
                _renderTexture = null;
            }
        }
        
        private void RenderBar()
        {
            if (_barMaterial == null || _renderTexture == null) return;
            
            // Update material properties
            _barMaterial.SetFloat(FillAmountId, _fillAmount);
            _barMaterial.SetFloat(IsCriticalId, _isCritical ? 1f : 0f);
            _barMaterial.SetFloat(GlowIntensityId, _glowIntensity);
            _barMaterial.SetFloat(ShowShineId, _showShine ? 1f : 0f);
            _barMaterial.SetColor(ColorFullId, _colorFull);
            _barMaterial.SetColor(ColorMidId, _colorMid);
            _barMaterial.SetColor(ColorLowId, _colorLow);
            _barMaterial.SetColor(ColorCriticalId, _colorCritical);
            
            // Render to texture
            var prevRT = RenderTexture.active;
            RenderTexture.active = _renderTexture;
            GL.Clear(true, true, Color.clear);
            
            GL.PushMatrix();
            GL.LoadOrtho();
            
            _barMaterial.SetPass(0);
            
            GL.Begin(GL.QUADS);
            GL.TexCoord2(0, 0); GL.Vertex3(0, 0, 0);
            GL.TexCoord2(1, 0); GL.Vertex3(1, 0, 0);
            GL.TexCoord2(1, 1); GL.Vertex3(1, 1, 0);
            GL.TexCoord2(0, 1); GL.Vertex3(0, 1, 0);
            GL.End();
            
            GL.PopMatrix();
            RenderTexture.active = prevRT;
        }
        
        /// <summary>
        /// Apply the rendered texture to a UI Toolkit element as a background image.
        /// </summary>
        public void ApplyToElement(VisualElement element)
        {
            if (element == null || _renderTexture == null) return;
            
            element.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_renderTexture));
        }
    }
}
