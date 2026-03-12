// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · InteractionProgressRing (UI Toolkit World-Space)
// World-space circular progress indicator using UI Toolkit with shader rendering
// Unity 6.2+: Uses PanelSettings WorldSpace render mode
// ════════════════════════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

namespace DIG.Combat.UI.WorldSpace
{
    /// <summary>
    /// EPIC 15.9: World-space radial progress indicator using UI Toolkit.
    /// Uses CombatUI_RadialFill shader for smooth animated progress.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class InteractionProgressRing : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────
        // Configuration
        // ─────────────────────────────────────────────────────────────────
        [Header("Colors")]
        [SerializeField] private Color _activeColor = new Color(0.2f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color _cancelledColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color _completedColor = new Color(1f, 1f, 1f, 1f);
        
        [Header("Animation")]
        [SerializeField] private float _fadeInDuration = 0.2f;
        [SerializeField] private float _fadeOutDuration = 0.3f;
        [SerializeField] private float _completedFlashDuration = 0.15f;
        [SerializeField] private AnimationCurve _progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        
        [Header("Shader")]
        [Tooltip("Assign CombatUI_RadialFill material for smooth radial progress")]
        [SerializeField] private Material radialFillMaterial;
        
        // ─────────────────────────────────────────────────────────────────
        // UI Elements
        // ─────────────────────────────────────────────────────────────────
        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _container;
        private VisualElement _ringBackground;
        private VisualElement _ringProgress;
        private Label _labelText;
        
        // ─────────────────────────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────────────────────────
        private Transform _cameraTransform;
        private Transform _followTarget;
        private Vector3 _positionOffset;
        private float _progress;
        private float _displayProgress;
        private float _currentAlpha;
        private float _targetAlpha;
        private bool _isActive;
        private InteractionState _currentState = InteractionState.Idle;
        private Material _instanceMaterial;
        
        // Shader property IDs
        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
        private static readonly int FillColorId = Shader.PropertyToID("_FillColor");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        
        // ─────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            
            if (radialFillMaterial != null)
            {
                _instanceMaterial = new Material(radialFillMaterial);
            }
        }
        
        private void OnEnable()
        {
            _cameraTransform = Camera.main?.transform;
            SetupUI();
        }
        
        private void OnDestroy()
        {
            if (_instanceMaterial != null)
            {
                Destroy(_instanceMaterial);
            }
        }
        
        private void SetupUI()
        {
            if (_document == null) return;
            
            _root = _document.rootVisualElement;
            if (_root == null) return;
            
            _container = _root.Q<VisualElement>("ring-container") ?? CreateContainer();
            _ringBackground = _container.Q<VisualElement>("ring-background") ?? CreateRingBackground();
            _ringProgress = _container.Q<VisualElement>("ring-progress") ?? CreateRingProgress();
            _labelText = _container.Q<Label>("ring-label") ?? CreateLabel();
            
            // Start hidden
            _container.style.opacity = 0;
            gameObject.SetActive(false);
        }
        
        private VisualElement CreateContainer()
        {
            var container = new VisualElement { name = "ring-container" };
            container.AddToClassList("interaction-ring-container");
            container.style.width = 64;
            container.style.height = 64;
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;
            _root.Add(container);
            return container;
        }
        
        private VisualElement CreateRingBackground()
        {
            var bg = new VisualElement { name = "ring-background" };
            bg.AddToClassList("ring-background");
            bg.style.position = Position.Absolute;
            bg.style.width = new Length(100, LengthUnit.Percent);
            bg.style.height = new Length(100, LengthUnit.Percent);
            bg.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            bg.style.borderTopLeftRadius = new Length(50, LengthUnit.Percent);
            bg.style.borderTopRightRadius = new Length(50, LengthUnit.Percent);
            bg.style.borderBottomLeftRadius = new Length(50, LengthUnit.Percent);
            bg.style.borderBottomRightRadius = new Length(50, LengthUnit.Percent);
            _container.Add(bg);
            return bg;
        }
        
        private VisualElement CreateRingProgress()
        {
            var ring = new VisualElement { name = "ring-progress" };
            ring.AddToClassList("ring-progress");
            ring.style.position = Position.Absolute;
            ring.style.width = new Length(100, LengthUnit.Percent);
            ring.style.height = new Length(100, LengthUnit.Percent);
            
            // Will use generateVisualContent for radial fill rendering
            ring.generateVisualContent += OnGenerateRingVisualContent;
            
            _container.Add(ring);
            return ring;
        }
        
        private Label CreateLabel()
        {
            var label = new Label { name = "ring-label" };
            label.AddToClassList("ring-label");
            label.style.fontSize = 10;
            label.style.color = Color.white;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.display = DisplayStyle.None;
            _container.Add(label);
            return label;
        }
        
        private void OnGenerateRingVisualContent(MeshGenerationContext mgc)
        {
            var painter = mgc.painter2D;
            var rect = _ringProgress.contentRect;
            var center = new Vector2(rect.width / 2, rect.height / 2);
            var radius = Mathf.Min(rect.width, rect.height) / 2 - 4; // Padding
            
            if (_displayProgress <= 0) return;
            
            // Draw progress arc
            float startAngle = -90f; // Start from top
            float sweepAngle = _displayProgress * 360f;
            
            painter.strokeColor = GetCurrentColor();
            painter.lineWidth = 4;
            painter.lineCap = LineCap.Round;
            
            painter.BeginPath();
            painter.Arc(center, radius, startAngle, startAngle + sweepAngle);
            painter.Stroke();
            
            // Draw glow at leading edge
            if (_displayProgress > 0.01f && _displayProgress < 1f)
            {
                float edgeAngle = (startAngle + sweepAngle) * Mathf.Deg2Rad;
                var edgePos = center + new Vector2(
                    Mathf.Cos(edgeAngle) * radius,
                    Mathf.Sin(edgeAngle) * radius
                );
                
                painter.fillColor = new Color(1f, 1f, 1f, 0.6f);
                painter.BeginPath();
                painter.Arc(edgePos, 3, 0, 360);
                painter.Fill();
            }
        }
        
        private Color GetCurrentColor()
        {
            return _currentState switch
            {
                InteractionState.Completed => _completedColor,
                InteractionState.Cancelled => _cancelledColor,
                _ => _activeColor
            };
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Update
        // ─────────────────────────────────────────────────────────────────
        private void LateUpdate()
        {
            // Billboard toward camera
            if (_cameraTransform != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - _cameraTransform.position);
            }
            
            // Follow target
            if (_followTarget != null)
            {
                transform.position = _followTarget.position + _positionOffset;
            }
            
            // Smooth progress animation
            float prevProgress = _displayProgress;
            _displayProgress = Mathf.MoveTowards(_displayProgress, _progress, Time.deltaTime * 5f);
            
            // Update shader
            if (_instanceMaterial != null)
            {
                _instanceMaterial.SetFloat(FillAmountId, _progressCurve.Evaluate(_displayProgress));
                _instanceMaterial.SetColor(FillColorId, GetCurrentColor());
            }
            
            // Request repaint if progress changed
            if (Mathf.Abs(prevProgress - _displayProgress) > 0.001f)
            {
                _ringProgress?.MarkDirtyRepaint();
            }
            
            // Fade animation
            if (Mathf.Abs(_currentAlpha - _targetAlpha) > 0.01f)
            {
                float fadeSpeed = _targetAlpha > _currentAlpha ? (1f / _fadeInDuration) : (1f / _fadeOutDuration);
                _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, Time.deltaTime * fadeSpeed);
                
                if (_container != null)
                    _container.style.opacity = _currentAlpha;
                
                // Auto-disable when faded out
                if (_currentAlpha <= 0.01f && _targetAlpha <= 0f)
                {
                    gameObject.SetActive(false);
                }
            }
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────
        
        /// <summary>Begin showing the interaction ring</summary>
        public void Show(Transform target, Vector3 offset, string label = null)
        {
            _followTarget = target;
            _positionOffset = offset;
            _progress = 0f;
            _displayProgress = 0f;
            _currentState = InteractionState.Active;
            
            if (_labelText != null)
            {
                _labelText.text = label ?? "";
                _labelText.style.display = string.IsNullOrEmpty(label) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            
            if (_instanceMaterial != null)
            {
                _instanceMaterial.SetColor(FillColorId, _activeColor);
                _instanceMaterial.SetFloat(FillAmountId, 0f);
            }
            
            gameObject.SetActive(true);
            _targetAlpha = 1f;
            _isActive = true;
            
            _ringProgress?.MarkDirtyRepaint();
        }
        
        /// <summary>Update progress (0-1)</summary>
        public void SetProgress(float progress)
        {
            _progress = Mathf.Clamp01(progress);
        }
        
        /// <summary>Mark interaction as completed</summary>
        public void Complete()
        {
            if (_currentState == InteractionState.Completed) return;
            
            _currentState = InteractionState.Completed;
            _progress = 1f;
            
            if (_instanceMaterial != null)
            {
                _instanceMaterial.SetColor(FillColorId, _completedColor);
            }
            
            // Flash then hide
            StartCoroutine(CompleteSequence());
        }
        
        /// <summary>Cancel the interaction</summary>
        public void Cancel()
        {
            if (_currentState == InteractionState.Completed) return;
            
            _currentState = InteractionState.Cancelled;
            
            if (_instanceMaterial != null)
            {
                _instanceMaterial.SetColor(FillColorId, _cancelledColor);
            }
            
            _ringProgress?.MarkDirtyRepaint();
            Hide();
        }
        
        /// <summary>Hide the ring with fade</summary>
        public void Hide()
        {
            _targetAlpha = 0f;
            _isActive = false;
            _followTarget = null;
        }
        
        /// <summary>Immediately hide without animation</summary>
        public void HideImmediate()
        {
            _currentAlpha = 0f;
            if (_container != null)
                _container.style.opacity = 0f;
            _isActive = false;
            _followTarget = null;
            gameObject.SetActive(false);
        }
        
        /// <summary>Reset for reuse from pool</summary>
        public void ResetForPool()
        {
            _progress = 0f;
            _displayProgress = 0f;
            _currentState = InteractionState.Idle;
            _followTarget = null;
            _isActive = false;
            _currentAlpha = 0f;
            
            if (_container != null)
                _container.style.opacity = 0f;
            
            if (_instanceMaterial != null)
            {
                _instanceMaterial.SetFloat(FillAmountId, 0f);
                _instanceMaterial.SetColor(FillColorId, _activeColor);
            }
            
            gameObject.SetActive(false);
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Animation Coroutines
        // ─────────────────────────────────────────────────────────────────
        private IEnumerator CompleteSequence()
        {
            // Flash white via shader
            if (_instanceMaterial != null)
            {
                _instanceMaterial.SetFloat(GlowIntensityId, 1f);
            }
            
            yield return new WaitForSeconds(_completedFlashDuration);
            
            if (_instanceMaterial != null)
            {
                _instanceMaterial.SetFloat(GlowIntensityId, 0.3f);
            }
            
            yield return new WaitForSeconds(0.2f);
            Hide();
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Properties
        // ─────────────────────────────────────────────────────────────────
        public bool IsActive => _isActive;
        public float Progress => _progress;
        public InteractionState State => _currentState;
    }
    
    /// <summary>Interaction ring state</summary>
    public enum InteractionState
    {
        Idle,
        Active,
        Completed,
        Cancelled
    }
}
