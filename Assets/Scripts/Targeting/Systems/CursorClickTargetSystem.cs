using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Core.Input;
using DIG.Targeting.Implementations;

namespace DIG.Targeting
{
    /// <summary>
    /// Bridges cursor hover to target selection on click.
    /// When IsCursorFree and the player clicks, reads CursorHoverResult
    /// and writes TargetData — no redundant raycast.
    ///
    /// Uses LateUpdate to write after regular targeting systems (Update).
    /// Only writes while cursor is free; once cursor locks, regular
    /// targeting resumes and overwrites TargetData normally.
    ///
    /// EPIC 15.18
    /// </summary>
    public class CursorClickTargetSystem : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Mouse button for target selection (0=Left, 1=Right).")]
        [SerializeField] private int _selectButton = 0;

        [Tooltip("Mouse button to clear target (set to -1 to disable).")]
        [SerializeField] private int _clearButton = 1;

        private Entity _playerEntity;
        private EntityManager _entityManager;
        private bool _isInitialized;
        private World _clientWorld;

        private Entity _selectedTarget;
        private float3 _selectedPoint;
        private bool _hasSelection;
        private bool _dirty;
        
        private const string DEBUG_TAG = "[HB:HOVER]";

        // Edge detection state
        private bool _wasSelectPressed;
        private bool _wasClearPressed;

        /// <summary>Fired when the player clicks to select a target entity.</summary>
        public event System.Action<Entity> OnTargetSelected;

        /// <summary>Fired when the player clicks to clear their target.</summary>
        public event System.Action OnTargetCleared;

        public Entity SelectedTarget => _selectedTarget;
        public bool HasSelection => _hasSelection;

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

            if (!_entityManager.Exists(_playerEntity))
                return;

            // Block clicks on UI elements
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            // EPIC 15.21: Use PlayerInputState for actions (mapped from Fire/Aim OR Select/CameraOrbit)
            // This ensures it works in both Shooter (Fire) and MMO (Select) paradigms when cursor is free
            bool isSelectHeld = false;
            
            if (_selectButton == 0) // Left Button
                isSelectHeld = global::Player.Systems.PlayerInputState.Fire || global::Player.Systems.PlayerInputState.Select;
            else if (_selectButton == 1) // Right Button
                isSelectHeld = global::Player.Systems.PlayerInputState.Aim || global::Player.Systems.PlayerInputState.CameraOrbit;
            
            // Edge detection for Select
            if (isSelectHeld && !_wasSelectPressed)
            {
                HandleSelectClick();
            }
            _wasSelectPressed = isSelectHeld;

            // Clear target on secondary click (right mouse = 1)
            if (_clearButton >= 0)
            {
                bool isClearHeld = false;
                if (_clearButton == 0) 
                    isClearHeld = global::Player.Systems.PlayerInputState.Fire || global::Player.Systems.PlayerInputState.Select;
                else if (_clearButton == 1) 
                    isClearHeld = global::Player.Systems.PlayerInputState.Aim || global::Player.Systems.PlayerInputState.CameraOrbit;
                
                // Edge detection for Clear
                if (isClearHeld && !_wasClearPressed)
                {
                    ClearSelection();
                }
                _wasClearPressed = isClearHeld;
            }
        }

        private void LateUpdate()
        {
            var schemeManager = InputSchemeManager.Instance;
            if (schemeManager == null || !schemeManager.IsCursorFree)
                return;

            if (!_isInitialized || !_dirty) return;
            if (!_entityManager.Exists(_playerEntity)) return;
            if (!_entityManager.HasComponent<TargetData>(_playerEntity)) return;

            // Read current TargetData to preserve fields we don't set
            var current = _entityManager.GetComponentData<TargetData>(_playerEntity);

            if (_hasSelection)
            {
                // Compute aim direction from player to selected point
                float3 aimDir = current.AimDirection;
                float targetDist = current.TargetDistance;

                if (_entityManager.HasComponent<Unity.Transforms.LocalTransform>(_playerEntity))
                {
                    var lt = _entityManager.GetComponentData<Unity.Transforms.LocalTransform>(_playerEntity);
                    float3 toTarget = _selectedPoint - lt.Position;
                    toTarget.y = 0f;
                    float len = math.length(toTarget);
                    if (len > 0.01f)
                    {
                        aimDir = math.normalize(toTarget);
                        targetDist = len;
                    }
                }

                _entityManager.SetComponentData(_playerEntity, new TargetData
                {
                    TargetEntity = _selectedTarget,
                    TargetPoint = _selectedPoint,
                    AimDirection = aimDir,
                    HasValidTarget = _selectedTarget != Entity.Null,
                    TargetDistance = targetDist,
                    Mode = TargetingMode.ClickSelect
                });
            }
            else
            {
                _entityManager.SetComponentData(_playerEntity, new TargetData
                {
                    TargetEntity = Entity.Null,
                    TargetPoint = current.TargetPoint,
                    AimDirection = current.AimDirection,
                    HasValidTarget = false,
                    TargetDistance = current.TargetDistance,
                    Mode = current.Mode
                });
            }

            _dirty = false;
        }

        private void HandleSelectClick()
        {
            if (!_entityManager.HasComponent<CursorHoverResult>(_playerEntity))
            {
                UnityEngine.Debug.LogWarning($"{DEBUG_TAG} ClickTarget: Player has no CursorHoverResult component!");
                return;
            }

            var hover = _entityManager.GetComponentData<CursorHoverResult>(_playerEntity);

            if (hover.IsValid && hover.HoveredEntity != Entity.Null
                && hover.Category != HoverCategory.Ground
                && hover.Category != HoverCategory.None)
            {
                _selectedTarget = hover.HoveredEntity;
                _selectedPoint = hover.HitPoint;
                _hasSelection = true;
                _dirty = true;
                OnTargetSelected?.Invoke(_selectedTarget);
            }
            else if (hover.IsValid)
            {
                // Clicked on ground/nothing — clear selection
                ClearSelection();
            }
        }

        private void ClearSelection()
        {
            if (!_hasSelection) return;

            _selectedTarget = Entity.Null;
            _selectedPoint = float3.zero;
            _hasSelection = false;
            _dirty = true;
            OnTargetCleared?.Invoke();
        }

        /// <summary>
        /// Programmatically clear the current selection.
        /// </summary>
        public void ForceClean()
        {
            ClearSelection();
        }

        private void OnDisable()
        {
            if (_hasSelection)
                ClearSelection();
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
                Initialize(em, entities[0]);
            entities.Dispose();
        }
    }
}
