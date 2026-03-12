using UnityEngine;
using Unity.Entities;
using DIG.Core.Input;
using DIG.Targeting;

namespace DIG.Targeting.UI
{
    /// <summary>
    /// Reads CursorHoverResult and swaps cursor icon based on hover category.
    /// Self-activates/deactivates based on InputSchemeManager.IsCursorFree.
    ///
    /// EPIC 15.18
    /// </summary>
    public class CursorIconController : MonoBehaviour
    {
        [System.Serializable]
        public struct CursorEntry
        {
            public HoverCategory Category;
            public Texture2D Texture;
            public Vector2 Hotspot;
        }

        [Header("Cursor Icons")]
        [Tooltip("Map of hover categories to cursor textures.")]
        [SerializeField] private CursorEntry[] _cursorMap;

        [Tooltip("Default cursor when hovering nothing or when cursor is not free.")]
        [SerializeField] private Texture2D _defaultCursor;

        [Tooltip("Hotspot for the default cursor.")]
        [SerializeField] private Vector2 _defaultHotspot = Vector2.zero;

        private Entity _playerEntity;
        private EntityManager _entityManager;
        private bool _isInitialized;
        private HoverCategory _lastCategory = HoverCategory.None;
        private bool _wasActive;

        private void OnEnable()
        {
            if (InputSchemeManager.Instance != null)
                InputSchemeManager.Instance.OnCursorFreeChanged += HandleCursorFreeChanged;
        }

        private void OnDisable()
        {
            RestoreDefaultCursor();
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
            bool isActive = schemeManager != null && schemeManager.IsCursorFree;

            if (!isActive)
            {
                if (_wasActive)
                {
                    RestoreDefaultCursor();
                    _wasActive = false;
                }
                return;
            }

            _wasActive = true;

            if (!_isInitialized)
            {
                TryAutoInitialize();
                if (!_isInitialized) return;
            }

            if (!_entityManager.Exists(_playerEntity) || !_entityManager.HasComponent<CursorHoverResult>(_playerEntity))
                return;

            var hover = _entityManager.GetComponentData<CursorHoverResult>(_playerEntity);
            HoverCategory category = hover.IsValid ? hover.Category : HoverCategory.None;

            if (category != _lastCategory)
            {
                _lastCategory = category;
                ApplyCursor(category);
            }
        }

        private void ApplyCursor(HoverCategory category)
        {
            if (_cursorMap != null)
            {
                foreach (var entry in _cursorMap)
                {
                    if (entry.Category == category && entry.Texture != null)
                    {
                        Cursor.SetCursor(entry.Texture, entry.Hotspot, CursorMode.Auto);
                        return;
                    }
                }
            }

            // Fallback to default
            Cursor.SetCursor(_defaultCursor, _defaultHotspot, CursorMode.Auto);
        }

        private void RestoreDefaultCursor()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            _lastCategory = HoverCategory.None;
        }

        private void HandleCursorFreeChanged(bool isFree)
        {
            if (!isFree)
                RestoreDefaultCursor();
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
