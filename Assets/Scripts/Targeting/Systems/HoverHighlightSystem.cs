using UnityEngine;
using Unity.Entities;
using DIG.Core.Input;
using DIG.Targeting.Implementations;

namespace DIG.Targeting
{
    /// <summary>
    /// Applies/removes highlight effects on the entity under the cursor.
    /// Reads CursorHoverResult from the local player entity.
    /// Manages a single active highlight at a time.
    ///
    /// Decoupled from input — any system that writes CursorHoverResult drives this.
    /// Self-cleans when IsCursorFree transitions false.
    ///
    /// EPIC 15.18
    /// </summary>
    public class HoverHighlightSystem : MonoBehaviour
    {
        [Header("Highlight Colors")]
        [SerializeField] private Color _enemyColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color _friendlyColor = new Color(0.2f, 1f, 0.2f, 1f);
        [SerializeField] private Color _interactableColor = new Color(1f, 1f, 0.2f, 1f);
        [SerializeField] private Color _lootableColor = new Color(0.6f, 0.4f, 1f, 1f);

        [Header("Highlight Settings")]
        [Tooltip("Emission intensity multiplier for the highlight effect.")]
        [SerializeField] private float _emissionIntensity = 0.3f;

        private Entity _playerEntity;
        private EntityManager _entityManager;
        private bool _isInitialized;

        private GameObject _currentHighlightTarget;
        private MaterialPropertyBlock _propertyBlock;
        private Renderer[] _currentRenderers;
        private Color[] _originalEmissionColors;
        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (InputSchemeManager.Instance != null)
                InputSchemeManager.Instance.OnCursorFreeChanged += HandleCursorFreeChanged;
        }

        private void OnDisable()
        {
            ClearHighlight();
            if (InputSchemeManager.Instance != null)
                InputSchemeManager.Instance.OnCursorFreeChanged -= HandleCursorFreeChanged;
        }

        public void Initialize(EntityManager em, Entity playerEntity)
        {
            _entityManager = em;
            _playerEntity = playerEntity;
            _isInitialized = true;
        }

        private void Update()
        {
            var schemeManager = InputSchemeManager.Instance;
            if (schemeManager == null || !schemeManager.IsCursorFree)
                return;

            if (!_isInitialized)
            {
                TryAutoInitialize();
                if (!_isInitialized) return;
            }

            if (!_entityManager.Exists(_playerEntity) || !_entityManager.HasComponent<CursorHoverResult>(_playerEntity))
                return;

            var hover = _entityManager.GetComponentData<CursorHoverResult>(_playerEntity);

            if (!hover.IsValid || hover.HoveredEntity == Entity.Null || hover.Category == HoverCategory.Ground || hover.Category == HoverCategory.None)
            {
                ClearHighlight();
                return;
            }

            // Resolve the entity to a GameObject via EntityLink
            var targetGO = ResolveEntityGameObject(hover.HoveredEntity);
            if (targetGO == null)
            {
                ClearHighlight();
                return;
            }

            if (targetGO != _currentHighlightTarget)
            {
                ClearHighlight();
                ApplyHighlight(targetGO, GetColorForCategory(hover.Category));
            }
        }

        private void ApplyHighlight(GameObject target, Color color)
        {
            _currentHighlightTarget = target;
            _currentRenderers = target.GetComponentsInChildren<Renderer>();

            if (_currentRenderers.Length == 0) return;

            _originalEmissionColors = new Color[_currentRenderers.Length];
            Color emissionColor = color * _emissionIntensity;

            for (int i = 0; i < _currentRenderers.Length; i++)
            {
                var renderer = _currentRenderers[i];
                renderer.GetPropertyBlock(_propertyBlock);
                // Store original if it exists
                if (_propertyBlock.HasColor(EmissionColorID))
                    _originalEmissionColors[i] = _propertyBlock.GetColor(EmissionColorID);
                else
                    _originalEmissionColors[i] = Color.black;

                _propertyBlock.SetColor(EmissionColorID, emissionColor);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void ClearHighlight()
        {
            if (_currentRenderers != null && _currentHighlightTarget != null)
            {
                for (int i = 0; i < _currentRenderers.Length; i++)
                {
                    if (_currentRenderers[i] == null) continue;
                    _currentRenderers[i].GetPropertyBlock(_propertyBlock);
                    _propertyBlock.SetColor(EmissionColorID, _originalEmissionColors[i]);
                    _currentRenderers[i].SetPropertyBlock(_propertyBlock);
                }
            }

            _currentHighlightTarget = null;
            _currentRenderers = null;
            _originalEmissionColors = null;
        }

        private Color GetColorForCategory(HoverCategory category)
        {
            return category switch
            {
                HoverCategory.Enemy => _enemyColor,
                HoverCategory.Friendly => _friendlyColor,
                HoverCategory.Interactable => _interactableColor,
                HoverCategory.Lootable => _lootableColor,
                _ => Color.white,
            };
        }

        private GameObject ResolveEntityGameObject(Entity entity)
        {
            // Find EntityLink in scene that references this entity
            // This is the reverse lookup — EntityLink is on the GO side
            var links = FindObjectsByType<EntityLink>(FindObjectsSortMode.None);
            foreach (var link in links)
            {
                if (link.Entity == entity)
                    return link.gameObject;
            }
            return null;
        }

        private void HandleCursorFreeChanged(bool isFree)
        {
            if (!isFree)
                ClearHighlight();
        }

        private void TryAutoInitialize()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<CursorHoverResult>(),
                ComponentType.ReadOnly<Unity.NetCode.GhostOwnerIsLocal>());

            if (query.IsEmpty) return;

            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length > 0)
                Initialize(em, entities[0]);
            entities.Dispose();
        }
    }
}
