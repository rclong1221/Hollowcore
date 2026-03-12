using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.UI.Effects
{
    /// <summary>
    /// A custom VisualElement that displays a shader-rendered texture.
    /// Used to integrate shader effects with UI Toolkit.
    /// 
    /// EPIC 15.8: Shader Integration for UI Toolkit
    /// 
    /// Usage:
    ///   var shaderElement = new ShaderVisualElement(ShaderVisualElement.EffectType.GradientBar);
    ///   shaderElement.FillAmount = 0.75f;
    ///   parent.Add(shaderElement);
    /// </summary>
    public class ShaderVisualElement : VisualElement
    {
        public enum EffectType
        {
            GradientBar,
            GlowBorder,
            BatteryCell
        }
        
        private EffectType _effectType;
        private Material _material;
        private RenderTexture _renderTexture;
        private float _fillAmount = 1f;
        private bool _isCritical = false;
        private bool _isOn = true;
        private bool _isLowBattery = false;
        private bool _isFlickering = false;
        private int _cellCount = 10;
        
        private static Shader _gradientBarShader;
        private static Shader _glowBorderShader;
        private static Shader _batteryCellShader;
        
        public float FillAmount
        {
            get => _fillAmount;
            set
            {
                _fillAmount = Mathf.Clamp01(value);
                UpdateMaterial();
                MarkDirtyRepaint();
            }
        }
        
        public bool IsCritical
        {
            get => _isCritical;
            set
            {
                _isCritical = value;
                UpdateMaterial();
                MarkDirtyRepaint();
            }
        }
        
        public bool IsOn
        {
            get => _isOn;
            set
            {
                _isOn = value;
                UpdateMaterial();
                MarkDirtyRepaint();
            }
        }
        
        public bool IsLowBattery
        {
            get => _isLowBattery;
            set
            {
                _isLowBattery = value;
                UpdateMaterial();
                MarkDirtyRepaint();
            }
        }
        
        public bool IsFlickering
        {
            get => _isFlickering;
            set
            {
                _isFlickering = value;
                UpdateMaterial();
                MarkDirtyRepaint();
            }
        }
        
        public ShaderVisualElement() : this(EffectType.GradientBar) { }
        
        public ShaderVisualElement(EffectType effectType)
        {
            _effectType = effectType;
            
            LoadShaders();
            CreateMaterial();
            CreateRenderTexture();
            
            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            
            // Schedule updates for animations
            schedule.Execute(UpdateAnimation).Every(16); // ~60fps
        }
        
        private void LoadShaders()
        {
            if (_gradientBarShader == null)
                _gradientBarShader = Shader.Find("DIG/UI/GradientBar");
            if (_glowBorderShader == null)
                _glowBorderShader = Shader.Find("DIG/UI/GlowBorder");
            if (_batteryCellShader == null)
                _batteryCellShader = Shader.Find("DIG/UI/BatteryCell");
        }
        
        private Shader GetShader()
        {
            return _effectType switch
            {
                EffectType.GradientBar => _gradientBarShader,
                EffectType.GlowBorder => _glowBorderShader,
                EffectType.BatteryCell => _batteryCellShader,
                _ => _gradientBarShader
            };
        }
        
        private void CreateMaterial()
        {
            var shader = GetShader();
            if (shader != null)
            {
                _material = new Material(shader);
                _material.hideFlags = HideFlags.HideAndDontSave;
                UpdateMaterial();
            }
        }
        
        private void CreateRenderTexture()
        {
            int width = Mathf.Max(1, (int)resolvedStyle.width);
            int height = Mathf.Max(1, (int)resolvedStyle.height);
            
            // Default size if not yet laid out
            if (width <= 1) width = 256;
            if (height <= 1) height = 32;
            
            _renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            _renderTexture.filterMode = FilterMode.Bilinear;
            _renderTexture.Create();
        }
        
        private void UpdateMaterial()
        {
            if (_material == null) return;
            
            switch (_effectType)
            {
                case EffectType.GradientBar:
                    _material.SetFloat("_FillAmount", _fillAmount);
                    _material.SetFloat("_IsCritical", _isCritical ? 1f : 0f);
                    break;
                    
                case EffectType.BatteryCell:
                    _material.SetFloat("_FillAmount", _fillAmount);
                    _material.SetFloat("_CellCount", _cellCount);
                    _material.SetFloat("_IsOn", _isOn ? 1f : 0f);
                    _material.SetFloat("_IsLowBattery", _isLowBattery ? 1f : 0f);
                    _material.SetFloat("_IsFlickering", _isFlickering ? 1f : 0f);
                    break;
            }
        }
        
        private void UpdateAnimation()
        {
            // Trigger repaint for animated shaders
            if (_isFlickering || _isCritical)
            {
                MarkDirtyRepaint();
            }
        }
        
        private void RenderToTexture()
        {
            if (_material == null || _renderTexture == null) return;
            
            // Resize if needed
            int width = Mathf.Max(1, (int)resolvedStyle.width);
            int height = Mathf.Max(1, (int)resolvedStyle.height);
            
            if (_renderTexture.width != width || _renderTexture.height != height)
            {
                _renderTexture.Release();
                _renderTexture.width = width;
                _renderTexture.height = height;
                _renderTexture.Create();
            }
            
            // Render
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
        
        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            RenderToTexture();
            
            if (_renderTexture == null) return;
            
            var rect = contentRect;
            
            // Create mesh for the texture
            var mesh = mgc.Allocate(4, 6, _renderTexture);
            
            // Vertices
            mesh.SetNextVertex(new Vertex
            {
                position = new Vector3(rect.xMin, rect.yMax, Vertex.nearZ),
                tint = Color.white,
                uv = new Vector2(0, 0)
            });
            mesh.SetNextVertex(new Vertex
            {
                position = new Vector3(rect.xMax, rect.yMax, Vertex.nearZ),
                tint = Color.white,
                uv = new Vector2(1, 0)
            });
            mesh.SetNextVertex(new Vertex
            {
                position = new Vector3(rect.xMax, rect.yMin, Vertex.nearZ),
                tint = Color.white,
                uv = new Vector2(1, 1)
            });
            mesh.SetNextVertex(new Vertex
            {
                position = new Vector3(rect.xMin, rect.yMin, Vertex.nearZ),
                tint = Color.white,
                uv = new Vector2(0, 1)
            });
            
            // Indices
            mesh.SetNextIndex(0);
            mesh.SetNextIndex(1);
            mesh.SetNextIndex(2);
            mesh.SetNextIndex(0);
            mesh.SetNextIndex(2);
            mesh.SetNextIndex(3);
        }
        
        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            Cleanup();
        }
        
        private void Cleanup()
        {
            if (_material != null)
            {
                Object.DestroyImmediate(_material);
                _material = null;
            }
            
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Object.DestroyImmediate(_renderTexture);
                _renderTexture = null;
            }
        }
    }
}
