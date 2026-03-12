using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Core.Input;
using DIG.Targeting.Implementations;

namespace DIG.Targeting
{
    /// <summary>
    /// Raycasts under the free mouse cursor to identify hovered entities.
    /// Only active when InputSchemeManager.IsCursorFree is true.
    ///
    /// Camera-mode agnostic: works identically whether cursor was freed by
    /// HybridToggle modifier (TPS) or TacticalCursor (isometric).
    ///
    /// Uses SphereCast for forgiving hover selection.
    /// Writes CursorHoverResult to the local player entity.
    ///
    /// EPIC 15.18
    /// </summary>
    public class CursorHoverSystem : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Maximum raycast distance for hover detection.")]
        [SerializeField] private float _maxHoverRange = 100f;

        [Tooltip("Layers that can be hovered.")]
        [SerializeField] private LayerMask _hoverableLayers = ~0;

        [Tooltip("SphereCast radius for forgiving hover selection.")]
        [SerializeField] private float _hoverRayRadius = 0.15f;

        [Tooltip("Layers considered 'ground' for ground-hit classification.")]
        [SerializeField] private LayerMask _groundLayers = 1; // Default layer

        private Entity _playerEntity;
        private EntityManager _entityManager;
        private bool _isInitialized;
        private Entity _previousHoveredEntity;
        private World _clientWorld;
        
        private const string DEBUG_TAG = "[HB:HOVER]";
#if CURSOR_DEBUG
        private float _lastDebugLogTime;
        private const float DEBUG_LOG_INTERVAL = 5f;
#endif

        /// <summary>Fired when the hovered entity changes. Provides old and new entity.</summary>
        public event System.Action<Entity, Entity> OnHoverChanged;

        public void Initialize(EntityManager em, Entity playerEntity)
        {
            _entityManager = em;
            _playerEntity = playerEntity;
            _isInitialized = true;
        }

        private void Update()
        {
            var schemeManager = InputSchemeManager.Instance;
            
#if CURSOR_DEBUG
            bool shouldLogState = Time.time - _lastDebugLogTime > DEBUG_LOG_INTERVAL;
            if (shouldLogState)
            {
                _lastDebugLogTime = Time.time;
                UnityEngine.Debug.Log($"{DEBUG_TAG} CursorHoverSystem: init={_isInitialized}, schemeManager={(schemeManager != null)}, cursorFree={(schemeManager?.IsCursorFree ?? false)}, playerEntity={(_isInitialized ? _playerEntity.Index.ToString() : "N/A")}");
            }
#endif

            if (schemeManager == null || !schemeManager.IsCursorFree)
            {
                // Cursor is locked — clear hover result if we have one
                if (_isInitialized && _previousHoveredEntity != Entity.Null)
                {
                    ClearHoverResult();
                }
                return;
            }

            if (!_isInitialized)
            {
                TryAutoInitialize();
                if (!_isInitialized)
                {
                    return;
                }
                // Always log successful initialization
                UnityEngine.Debug.Log($"{DEBUG_TAG} Auto-initialized with player entity {_playerEntity.Index}");
            }

            PerformHoverRaycast();
        }

        private void PerformHoverRaycast()
        {
            var camera = Camera.main;
            if (camera == null) return;

            // EPIC 15.21: Use PlayerInputState for mouse position (centralized input)
            // CursorScreenPosition is updated by PlayerInputReader
            var cursorVal = global::Player.Systems.PlayerInputState.CursorScreenPosition;
            Vector2 mousePos = new Vector2(cursorVal.x, cursorVal.y);
            Ray ray = camera.ScreenPointToRay(mousePos);

            CursorHoverResult result = default;

            // SphereCast for forgiving entity hover detection
            if (Physics.SphereCast(ray, _hoverRayRadius, out RaycastHit hit, _maxHoverRange, _hoverableLayers))
            {
                result.HitPoint = (float3)hit.point;
                result.IsValid = true;

                // Debug: what did we actually hit?
                var hitObj = hit.collider.gameObject;
                
                // Try to resolve an ECS entity from the hit collider
                var entityLink = hit.collider.GetComponentInParent<EntityLink>();
                if (entityLink != null)
                {
                    result.HoveredEntity = entityLink.Entity;
                    result.Category = ClassifyEntity(entityLink.Entity);
                }
                else
                {
                    // No EntityLink - try position-based matching for combat entities
                    var foundEntity = FindEntityByPosition((float3)hit.point);
                    if (foundEntity != Entity.Null)
                    {
                        result.HoveredEntity = foundEntity;
                        result.Category = ClassifyEntity(foundEntity);
                    }
                    else
                    {
                        // No entity — classify as ground or none
                        result.HoveredEntity = Entity.Null;
                        result.Category = IsGroundLayer(hit.collider.gameObject.layer)
                            ? HoverCategory.Ground
                            : HoverCategory.None;
                    }
                }
            }
            else
            {
                // Raycast hit nothing — project to ground plane as fallback
                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                if (groundPlane.Raycast(ray, out float enter))
                {
                    result.HitPoint = (float3)ray.GetPoint(enter);
                    result.IsValid = true;
                    result.Category = HoverCategory.Ground;
                }
                else
                {
                    result.IsValid = false;
                    result.Category = HoverCategory.None;
                }
                result.HoveredEntity = Entity.Null;
            }

            WriteHoverResult(result);
            
            // Fire hover changed event if entity changed
            if (result.HoveredEntity != _previousHoveredEntity)
            {
                OnHoverChanged?.Invoke(_previousHoveredEntity, result.HoveredEntity);
                _previousHoveredEntity = result.HoveredEntity;
            }
        }

        private HoverCategory ClassifyEntity(Entity entity)
        {
            if (entity == Entity.Null || !_entityManager.Exists(entity))
                return HoverCategory.None;

            // Check for health component (combat entity)
            if (_entityManager.HasComponent<global::Player.Components.Health>(entity))
            {
                // TODO: Check faction/team component when available.
                // For now, default to Enemy for any entity with Health.
                return HoverCategory.Enemy;
            }

            // Future: check IInteractable, LootContainer, etc.
            return HoverCategory.None;
        }

        private bool IsGroundLayer(int layer)
        {
            return (_groundLayers.value & (1 << layer)) != 0;
        }

        /// <summary>
        /// Fallback: Find ECS entity near a world position.
        /// Used when GameObject doesn't have EntityLink but we hit something.
        /// Searches for entities with Health component (combat entities).
        /// Excludes the local player entity.
        /// </summary>
        private Entity FindEntityByPosition(float3 hitPoint)
        {
            if (_clientWorld == null || !_clientWorld.IsCreated) return Entity.Null;
            
            var em = _clientWorld.EntityManager;
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<global::Player.Components.Health>(),
                ComponentType.ReadOnly<Unity.Transforms.LocalTransform>()
            );
            
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            Entity closest = Entity.Null;
            float closestDist = 3f; // Max match distance
            
            foreach (var entity in entities)
            {
                // Skip the local player entity - don't target yourself
                if (entity == _playerEntity) continue;
                
                var transform = em.GetComponentData<Unity.Transforms.LocalTransform>(entity);
                float dist = math.distance(transform.Position, hitPoint);
                
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = entity;
                }
            }
            
            return closest;
        }

        private void WriteHoverResult(CursorHoverResult result)
        {
            if (_entityManager.Exists(_playerEntity) && _entityManager.HasComponent<CursorHoverResult>(_playerEntity))
            {
                _entityManager.SetComponentData(_playerEntity, result);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"{DEBUG_TAG} WriteHoverResult FAILED: playerExists={_entityManager.Exists(_playerEntity)}, hasComponent={_entityManager.HasComponent<CursorHoverResult>(_playerEntity)}");
            }
        }

        private void ClearHoverResult()
        {
            var cleared = new CursorHoverResult
            {
                HoveredEntity = Entity.Null,
                HitPoint = float3.zero,
                Category = HoverCategory.None,
                IsValid = false,
            };
            WriteHoverResult(cleared);

            if (_previousHoveredEntity != Entity.Null)
            {
                OnHoverChanged?.Invoke(_previousHoveredEntity, Entity.Null);
                _previousHoveredEntity = Entity.Null;
            }
        }

        private void TryAutoInitialize()
        {
            // Find ClientWorld (NetCode) - re-search if stuck on LocalWorld
            bool needsResearch = _clientWorld == null || !_clientWorld.IsCreated 
                || (_clientWorld.Name == "LocalWorld" && World.All.Count > 1);
            
            if (needsResearch)
            {
                _clientWorld = null;
                foreach (var world in World.All)
                {
                    // Prefer ClientWorld
                    if (world.IsCreated && world.Name == "ClientWorld")
                    {
                        _clientWorld = world;
                        break;
                    }
                    // Fall back to LocalWorld
                    if (world.IsCreated && world.Name == "LocalWorld" && _clientWorld == null)
                    {
                        _clientWorld = world;
                    }
                }
                if (_clientWorld == null)
                    _clientWorld = World.DefaultGameObjectInjectionWorld;
            }
            
            if (_clientWorld == null || !_clientWorld.IsCreated) return;

            var em = _clientWorld.EntityManager;
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<CursorHoverResult>(),
                ComponentType.ReadOnly<Unity.NetCode.GhostOwnerIsLocal>());

            if (query.IsEmpty) return;

            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length > 0)
            {
                Initialize(em, entities[0]);
                
                // Ensure all dependent systems exist and are initialized
                EnsureClickTargetSystem(em, entities[0]);
                EnsureHighlightSystem(em, entities[0]);
                EnsureCursorIconController(em, entities[0]);
            }
            entities.Dispose();
        }
        
        /// <summary>
        /// Ensures CursorClickTargetSystem is present and initialized.
        /// Creates one on this GameObject if not found.
        /// </summary>
        private void EnsureClickTargetSystem(EntityManager em, Entity playerEntity)
        {
            var clickSystem = GetComponent<CursorClickTargetSystem>();
            if (clickSystem == null)
            {
                // Also check if one exists anywhere in scene
                clickSystem = FindFirstObjectByType<CursorClickTargetSystem>();
            }
            
            if (clickSystem == null)
            {
                // Create one on the same GameObject as this system
                clickSystem = gameObject.AddComponent<CursorClickTargetSystem>();
                UnityEngine.Debug.Log($"{DEBUG_TAG} Created CursorClickTargetSystem on {gameObject.name}");
            }
            
            // Initialize it with the same player entity
            clickSystem.Initialize(em, playerEntity);
            UnityEngine.Debug.Log($"{DEBUG_TAG} Initialized CursorClickTargetSystem with player entity {playerEntity.Index}");
        }
        
        /// <summary>
        /// Ensures HoverHighlightSystem is present and initialized.
        /// Creates one on this GameObject if not found.
        /// </summary>
        private void EnsureHighlightSystem(EntityManager em, Entity playerEntity)
        {
            var highlightSystem = GetComponent<HoverHighlightSystem>();
            if (highlightSystem == null)
            {
                highlightSystem = FindFirstObjectByType<HoverHighlightSystem>();
            }
            
            if (highlightSystem == null)
            {
                highlightSystem = gameObject.AddComponent<HoverHighlightSystem>();
            }
            
            highlightSystem.Initialize(em, playerEntity);
        }
        
        /// <summary>
        /// Ensures CursorIconController is present and initialized.
        /// Creates one on this GameObject if not found.
        /// </summary>
        private void EnsureCursorIconController(EntityManager em, Entity playerEntity)
        {
            var iconController = GetComponent<UI.CursorIconController>();
            if (iconController == null)
            {
                iconController = FindFirstObjectByType<UI.CursorIconController>();
            }
            
            if (iconController == null)
            {
                iconController = gameObject.AddComponent<UI.CursorIconController>();
            }
            
            iconController.Initialize(em, playerEntity);
        }
    }
}
