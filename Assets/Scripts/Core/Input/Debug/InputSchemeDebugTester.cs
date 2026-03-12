using UnityEngine;
using Unity.Entities;
using DIG.Targeting;

namespace DIG.Core.Input
{
    /// <summary>
    /// Inspector component for testing and visualizing InputScheme state at runtime.
    /// Displays current scheme, cursor state, hover results, and allows scheme switching.
    ///
    /// EPIC 15.18
    /// </summary>
    public class InputSchemeDebugTester : MonoBehaviour
    {
        [Header("Runtime State (Read Only)")]
        [SerializeField] private InputScheme _activeScheme;
        [SerializeField] private bool _isCursorFree;
        [SerializeField] private bool _isTemporaryCursorActive;
        [SerializeField] private string _cursorLockState = "Unknown";
        [SerializeField] private string _activeCameraMode = "Unknown";
        [SerializeField] private bool _cameraSupportsOrbit;
        [SerializeField] private bool _cameraUsesCursorAim;

        [Header("Hover State (Read Only)")]
        [SerializeField] private bool _hoverIsValid;
        [SerializeField] private HoverCategory _hoverCategory;
        [SerializeField] private string _hoveredEntityInfo = "None";
        [SerializeField] private Vector3 _hoverHitPoint;

        [Header("Actions")]
        [Tooltip("Set this to switch scheme at runtime.")]
        [SerializeField] private InputScheme _requestedScheme = InputScheme.ShooterDirect;

        private InputScheme _lastRequestedScheme = InputScheme.ShooterDirect;

        private void Update()
        {
            UpdateRuntimeState();
            UpdateHoverState();

            // Detect inspector-driven scheme change
            if (_requestedScheme != _lastRequestedScheme)
            {
                _lastRequestedScheme = _requestedScheme;
                if (InputSchemeManager.Instance != null)
                {
                    if (!InputSchemeManager.Instance.TrySetScheme(_requestedScheme))
                    {
                        // Revert to actual scheme if rejected
                        _requestedScheme = InputSchemeManager.Instance.ActiveScheme;
                        _lastRequestedScheme = _requestedScheme;
                    }
                }
            }
        }

        private void UpdateRuntimeState()
        {
            var manager = InputSchemeManager.Instance;
            if (manager != null)
            {
                _activeScheme = manager.ActiveScheme;
                _isCursorFree = manager.IsCursorFree;
                _isTemporaryCursorActive = manager.IsTemporaryCursorActive;
            }

            _cursorLockState = Cursor.lockState.ToString();

            var cameraProvider = DIG.CameraSystem.CameraModeProvider.Instance;
            if (cameraProvider != null)
            {
                _activeCameraMode = cameraProvider.CurrentMode.ToString();
                _cameraSupportsOrbit = cameraProvider.SupportsOrbitRotation;
                _cameraUsesCursorAim = cameraProvider.UsesCursorAiming;
            }
            else
            {
                _activeCameraMode = "No CameraModeProvider";
                _cameraSupportsOrbit = true;
                _cameraUsesCursorAim = false;
            }
        }

        private void UpdateHoverState()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<CursorHoverResult>(),
                ComponentType.ReadOnly<Unity.NetCode.GhostOwnerIsLocal>());

            if (query.IsEmpty)
            {
                _hoverIsValid = false;
                _hoverCategory = HoverCategory.None;
                _hoveredEntityInfo = "No player entity";
                return;
            }

            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length > 0)
            {
                var hover = em.GetComponentData<CursorHoverResult>(entities[0]);
                _hoverIsValid = hover.IsValid;
                _hoverCategory = hover.Category;
                _hoverHitPoint = hover.HitPoint;

                if (hover.HoveredEntity != Entity.Null && em.Exists(hover.HoveredEntity))
                    _hoveredEntityInfo = $"Entity {hover.HoveredEntity.Index}:{hover.HoveredEntity.Version}";
                else
                    _hoveredEntityInfo = "None";
            }
            entities.Dispose();
        }

        [ContextMenu("Switch to ShooterDirect")]
        private void SwitchToShooterDirect()
        {
            InputSchemeManager.Instance?.TrySetScheme(InputScheme.ShooterDirect);
            _requestedScheme = InputScheme.ShooterDirect;
            _lastRequestedScheme = _requestedScheme;
        }

        [ContextMenu("Switch to HybridToggle")]
        private void SwitchToHybridToggle()
        {
            InputSchemeManager.Instance?.TrySetScheme(InputScheme.HybridToggle);
            _requestedScheme = InputScheme.HybridToggle;
            _lastRequestedScheme = _requestedScheme;
        }

        [ContextMenu("Switch to TacticalCursor")]
        private void SwitchToTacticalCursor()
        {
            InputSchemeManager.Instance?.TrySetScheme(InputScheme.TacticalCursor);
            _requestedScheme = InputScheme.TacticalCursor;
            _lastRequestedScheme = _requestedScheme;
        }
    }
}
