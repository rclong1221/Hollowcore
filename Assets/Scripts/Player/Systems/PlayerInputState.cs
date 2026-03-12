using Unity.Mathematics;

namespace Player.Systems
{
    // Small shared runtime input state used by both MonoBehaviours and the ECS writer system.
    public static class PlayerInputState
    {
        public static float2 Move = float2.zero;
        public static float2 LookDelta = float2.zero;
        public static float ZoomDelta = 0f;
        public static bool Jump = false;
        public static bool Crouch = false;
        public static bool Sprint = false;
        // Lean inputs (peek left/right)
        public static bool LeanLeft = false;
        public static bool LeanRight = false;
        public static bool DodgeRoll = false;
        public static bool DodgeDive = false;
        public static bool Prone = false;
        public static bool Slide = false;
        
        // === COMBAT/INTERACTION (from Input Actions) ===
        public static bool Fire = false;
        public static bool Aim = false;
        public static bool Interact = false;
        public static bool Reload = false;
        public static bool ToggleFlashlight = false;
        public static bool Grab = false;
        public static bool FreeLook = false;
        
        // === EDGE DETECTION (EPIC 15.21 Phase 7) ===
        /// <summary>Fire was just pressed this frame.</summary>
        public static bool FirePressed = false;
        /// <summary>Fire was just released this frame.</summary>
        public static bool FireReleased = false;
        /// <summary>Aim was just pressed this frame.</summary>
        public static bool AimPressed = false;
        /// <summary>Aim was just released this frame.</summary>
        public static bool AimReleased = false;
        /// <summary>Reload was just pressed this frame.</summary>
        public static bool ReloadPressed = false;
        
        // === MODIFIERS (Core) ===
        public static bool ModShift = false;
        public static bool ModCtrl = false;
        public static bool ModAlt = false;
        
        // === MMO AUTO-RUN (EPIC 15.21) ===
        /// <summary>MMO auto-run active (LMB+RMB held in MMO mode via composite binding).</summary>
        public static bool AutoRun = false;
        
        // === MMO SPECIFIC (Decoupled from Fire/Aim) ===
        public static bool Select = false;       // Was Fire
        public static bool CameraOrbit = false;  // Was Aim
        public static bool MMOStrafeLeft = false;
        public static bool MMOStrafeRight = false;
        
        // === EQUIPMENT QUICK SLOTS (EPIC 15.21) ===
        public static bool EquipSlot1 = false;
        public static bool EquipSlot2 = false;
        public static bool EquipSlot3 = false;
        public static bool EquipSlot4 = false;
        public static bool EquipSlot5 = false;
        public static bool EquipSlot6 = false;
        public static bool EquipSlot7 = false;
        public static bool EquipSlot8 = false;
        public static bool EquipSlot9 = false;

        // === CLICK-TO-MOVE PATHFINDING (EPIC 15.20 Phase 3) ===
        /// <summary>
        /// Pathfinding-computed movement direction in camera-relative space.
        /// Written by ClickToMoveHandler each frame when following a path.
        /// X = camera-right component, Y = camera-forward component.
        /// Continuous float2 (not quantized to -1/0/1 like WASD).
        /// </summary>
        public static float2 PathMoveDirection = float2.zero;
        /// <summary>Whether the player is currently following a click-to-move path.</summary>
        public static bool IsPathFollowing = false;

        // === MOBA ACTIONS (EPIC 15.20 Phase 4a) ===
        /// <summary>Attack-move mode toggled (press A then click).</summary>
        public static bool AttackMove = false;
        /// <summary>Stop all movement and actions.</summary>
        public static bool Stop = false;
        /// <summary>Hold position (attack in range only, no movement).</summary>
        public static bool HoldPosition = false;
        /// <summary>Toggle camera lock on player (consumed on read).</summary>
        public static bool CameraLockToggle = false;

        // === INPUT SCHEME (EPIC 15.18) ===
        /// <summary>Unfiltered mouse delta — preserved even when LookDelta is suppressed by InputSchemeManager.</summary>
        public static float2 RawLookDelta = float2.zero;
        /// <summary>Current cursor screen position in pixels. Updated by PlayerInputReader each frame.</summary>
        public static float2 CursorScreenPosition = float2.zero;

        // === EQUIPMENT (set by DIGEquipmentProvider, respects slot definitions) ===
        /// <summary>
        /// Pending equip slot index (0=MainHand, 1=OffHand, etc). -1 means no pending equip.
        /// Set by DIGEquipmentProvider based on EquipmentSlotDefinition.RequiredModifier.
        /// </summary>
        public static int PendingEquipSlot = -1;
        
        /// <summary>
        /// Pending equip quick slot (1-9). 0 means no pending equip.
        /// Set by DIGEquipmentProvider based on the numeric key pressed.
        /// </summary>
        public static int PendingEquipQuickSlot = 0;
        
        /// <summary>
        /// Consume the pending equip request (call after reading values).
        /// </summary>
        public static void ConsumePendingEquip()
        {
            PendingEquipSlot = -1;
            PendingEquipQuickSlot = 0;
        }
        
        /// <summary>
        /// Reset ALL input state to defaults. Call when transitioning to gameplay
        /// (e.g., going in-game) to prevent stale values from lobby/UI from
        /// triggering actions on the first frame.
        ///
        /// Without this, held-action flags (Jump, Sprint, Crouch, etc.) can persist
        /// across scene transitions because action map Disable() prevents the
        /// 'canceled' callback from firing, leaving the static field stuck at true.
        /// </summary>
        public static void ResetAll()
        {
            Move = float2.zero;
            LookDelta = float2.zero;
            RawLookDelta = float2.zero;
            ZoomDelta = 0f;
            Jump = false;
            Crouch = false;
            Sprint = false;
            LeanLeft = false;
            LeanRight = false;
            DodgeRoll = false;
            DodgeDive = false;
            Prone = false;
            Slide = false;
            Fire = false;
            Aim = false;
            Interact = false;
            Reload = false;
            ToggleFlashlight = false;
            Grab = false;
            FreeLook = false;
            FirePressed = false;
            FireReleased = false;
            AimPressed = false;
            AimReleased = false;
            ReloadPressed = false;
            ModShift = false;
            ModCtrl = false;
            ModAlt = false;
            AutoRun = false;
            Select = false;
            CameraOrbit = false;
            MMOStrafeLeft = false;
            MMOStrafeRight = false;
            EquipSlot1 = false;
            EquipSlot2 = false;
            EquipSlot3 = false;
            EquipSlot4 = false;
            EquipSlot5 = false;
            EquipSlot6 = false;
            EquipSlot7 = false;
            EquipSlot8 = false;
            EquipSlot9 = false;
            PathMoveDirection = float2.zero;
            IsPathFollowing = false;
            AttackMove = false;
            Stop = false;
            HoldPosition = false;
            CameraLockToggle = false;
            CursorScreenPosition = float2.zero;
            PendingEquipSlot = -1;
            PendingEquipQuickSlot = 0;
        }

        /// <summary>
        /// Clear edge detection flags. Call at the start of each frame before processing input.
        /// Also clears latched MOBA press-once flags (AttackMove, Stop, HoldPosition, CameraLockToggle)
        /// which are consumed by MonoBehaviours rather than PlayerInputSystem.
        /// </summary>
        public static void ClearEdgeFlags()
        {
            FirePressed = false;
            FireReleased = false;
            AimPressed = false;
            AimReleased = false;
            ReloadPressed = false;

            // MOBA press-once commands — latched by PlayerInputReader, consumed here.
            // Unlike ECS-path inputs (consumed by PlayerInputSystem.SampleInput), these
            // are read directly by MonoBehaviours during Update, so frame-start clearing works.
            AttackMove = false;
            Stop = false;
            HoldPosition = false;
            CameraLockToggle = false;
        }

        /// <summary>
        /// Clear equipment slot flags after they've been read by DIGEquipmentProvider.
        /// These are latched on performed (not cleared on canceled) to survive same-frame
        /// performed+canceled pairs from quick taps.
        /// </summary>
        public static void ConsumeEquipSlotFlags()
        {
            EquipSlot1 = false;
            EquipSlot2 = false;
            EquipSlot3 = false;
            EquipSlot4 = false;
            EquipSlot5 = false;
            EquipSlot6 = false;
            EquipSlot7 = false;
            EquipSlot8 = false;
            EquipSlot9 = false;
        }
    }
}
