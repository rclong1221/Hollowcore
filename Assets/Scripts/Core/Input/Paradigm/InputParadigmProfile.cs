using UnityEngine;
using DIG.CameraSystem;
using DIG.Targeting;
using DIG.Targeting.Core;

namespace DIG.Core.Input
{
    /// <summary>
    /// ScriptableObject configuration for an input paradigm.
    /// Defines all settings for cursor, camera, movement, and facing behavior.
    /// 
    /// Create via: Assets > Create > DIG/Input/Input Paradigm Profile
    /// 
    /// EPIC 15.20 - Input Paradigm Framework
    /// </summary>
    [CreateAssetMenu(fileName = "NewParadigmProfile", menuName = "DIG/Input/Input Paradigm Profile", order = 1)]
    public class InputParadigmProfile : ScriptableObject
    {
        // ============================================================
        // IDENTITY
        // ============================================================

        [Header("Identity")]
        [Tooltip("The paradigm type this profile represents.")]
        public InputParadigm paradigm = InputParadigm.Shooter;

        [Tooltip("Display name shown in UI.")]
        public string displayName = "Shooter";

        [Tooltip("Description for settings UI.")]
        [TextArea(2, 4)]
        public string description = "Mouse controls camera. WASD moves relative to camera.";

        [Tooltip("Icon for settings UI (optional).")]
        public Sprite icon;

        // ============================================================
        // CURSOR BEHAVIOR
        // ============================================================

        [Header("Cursor Behavior")]
        [Tooltip("If true, cursor is visible and free by default.")]
        public bool cursorFreeByDefault = false;

        [Tooltip("Key to temporarily free cursor (only used when cursorFreeByDefault = false).")]
        public KeyCode temporaryCursorFreeKey = KeyCode.LeftAlt;

        [Tooltip("How camera orbit is controlled.")]
        public CameraOrbitMode cameraOrbitMode = CameraOrbitMode.AlwaysOrbit;

        // ============================================================
        // MOVEMENT
        // ============================================================

        [Header("Movement")]
        [Tooltip("Whether WASD direct movement is enabled.")]
        public bool wasdEnabled = true;

        [Tooltip("Whether click-to-move is enabled.")]
        public bool clickToMoveEnabled = false;

        [Tooltip("Which mouse button triggers click-to-move.")]
        public ClickToMoveButton clickToMoveButton = ClickToMoveButton.None;

        [Tooltip("Whether click-to-move uses pathfinding.")]
        public bool usePathfinding = false;

        [Tooltip("How character facing is determined.")]
        public MovementFacingMode facingMode = MovementFacingMode.CameraForward;

        [Tooltip("If true, A/D turn the character. If false, A/D strafe.")]
        public bool adTurnsCharacter = false;
        
        [Tooltip("If true, WASD moves in fixed screen directions (isometric). If false, moves relative to camera (TPS/FPS).")]
        public bool useScreenRelativeMovement = false;

        // ============================================================
        // CAMERA
        // ============================================================

        [Header("Camera")]
        [Tooltip("Whether Q/E camera rotation is enabled.")]
        public bool qeRotationEnabled = false;

        [Tooltip("Whether edge-pan camera movement is enabled.")]
        public bool edgePanEnabled = false;

        [Tooltip("Whether scroll wheel zoom is enabled.")]
        public bool scrollZoomEnabled = true;

        // ============================================================
        // CAMERA COMPATIBILITY
        // ============================================================

        [Header("Camera Compatibility")]
        [Tooltip("Camera modes compatible with this paradigm.")]
        public CameraMode[] compatibleCameraModes = { CameraMode.ThirdPersonFollow };

        // ============================================================
        // TARGETING (EPIC 18.19)
        // ============================================================

        [Header("Targeting")]
        [Tooltip("Default targeting mode for this paradigm.")]
        public TargetingMode defaultTargetingMode = TargetingMode.CameraRaycast;

        [Tooltip("Default lock-on behavior for this paradigm.")]
        public LockBehaviorType defaultLockBehavior = LockBehaviorType.HardLock;

        // ============================================================
        // PUBLIC API
        // ============================================================

        /// <summary>
        /// Check if this paradigm is compatible with a camera mode.
        /// </summary>
        public bool IsCompatibleWith(CameraMode cameraMode)
        {
            if (compatibleCameraModes == null || compatibleCameraModes.Length == 0)
                return true; // No restrictions

            foreach (var mode in compatibleCameraModes)
            {
                if (mode == cameraMode)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Maps this paradigm to the legacy InputScheme enum for backwards compatibility.
        /// Note: The actual cursor state is controlled by CursorController (paradigm system).
        /// This mapping is only for systems that still check InputSchemeManager.ActiveScheme.
        /// </summary>
        public InputScheme ToInputScheme()
        {
            // For cursor-free paradigms, use TacticalCursor so legacy systems know cursor is available
            // The actual cursor behavior (MMO's RMB-to-orbit) is handled by CursorController
            return cursorFreeByDefault || cameraOrbitMode == CameraOrbitMode.ButtonHoldOrbit
                ? InputScheme.TacticalCursor
                : (temporaryCursorFreeKey != KeyCode.None ? InputScheme.HybridToggle : InputScheme.ShooterDirect);
        }

        // ============================================================
        // VALIDATION
        // ============================================================

        private void OnValidate()
        {
            // Auto-set compatible cameras based on paradigm
            if (compatibleCameraModes == null || compatibleCameraModes.Length == 0)
            {
                compatibleCameraModes = paradigm switch
                {
                    InputParadigm.Shooter => new[] { CameraMode.ThirdPersonFollow },
                    InputParadigm.MMO => new[] { CameraMode.ThirdPersonFollow },
                    InputParadigm.ARPG => new[] { CameraMode.IsometricFixed, CameraMode.IsometricRotatable },
                    InputParadigm.MOBA => new[] { CameraMode.TopDownFixed },
                    InputParadigm.TwinStick => new[] { CameraMode.IsometricFixed, CameraMode.IsometricRotatable },
                    _ => new[] { CameraMode.ThirdPersonFollow },
                };
            }

            // Auto-set targeting defaults based on paradigm when at default values
            if (defaultTargetingMode == TargetingMode.CameraRaycast && defaultLockBehavior == LockBehaviorType.HardLock)
            {
                switch (paradigm)
                {
                    case InputParadigm.Shooter:
                        defaultTargetingMode = TargetingMode.CameraRaycast;
                        defaultLockBehavior = LockBehaviorType.HardLock;
                        break;
                    case InputParadigm.MMO:
                        defaultTargetingMode = TargetingMode.ClickSelect;
                        defaultLockBehavior = LockBehaviorType.SoftLock;
                        break;
                    case InputParadigm.ARPG:
                        defaultTargetingMode = TargetingMode.CursorAim;
                        defaultLockBehavior = LockBehaviorType.IsometricLock;
                        break;
                    case InputParadigm.MOBA:
                        defaultTargetingMode = TargetingMode.CursorAim;
                        defaultLockBehavior = LockBehaviorType.IsometricLock;
                        break;
                    case InputParadigm.TwinStick:
                        defaultTargetingMode = TargetingMode.CursorAim;
                        defaultLockBehavior = LockBehaviorType.TwinStick;
                        break;
                }
            }

            // Validate click-to-move settings
            if (clickToMoveEnabled && clickToMoveButton == ClickToMoveButton.None)
            {
                Debug.LogWarning($"[InputParadigmProfile] {name}: clickToMoveEnabled but no button set.");
            }
        }
    }
}
