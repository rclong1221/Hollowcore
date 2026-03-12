using DIG.Accessibility.Motor;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Mathematics;
using Player.Systems;

namespace DIG.Core.Input
{
    /// <summary>
    /// Bridges Unity Input System Actions to the static PlayerInputState used by ECS.
    /// 
    /// EPIC 15.21: Redesigned to work with paradigm-based action maps using runtime lookup:
    /// - Core: Always-on shared actions (Move, Look, Jump, etc.)
    /// - Combat_Shooter: LMB→Attack, RMB→AimDownSights
    /// - Combat_MMO: LMB→SelectTarget, RMB→CameraOrbit, LMB+RMB→AutoRun
    /// - Combat_ARPG: LMB→AttackAtCursor, RMB→MoveToClick
    /// 
    /// Uses InputActionMap.FindAction() runtime API for robust action access
    /// regardless of generated code state.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        public static PlayerInputReader Instance { get; private set; }
        
        [Header("Gamepad Settings")]
        [Tooltip("Curve for gamepad aim acceleration. X = input magnitude, Y = output multiplier")]
        [SerializeField] private AnimationCurve _aimAccelerationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        [Tooltip("Radial deadzone for stick inputs (0.15 = 15%)")]
        [SerializeField] private float _stickDeadzone = 0.15f;
        
        [Tooltip("Sensitivity multiplier for gamepad look")]
        [SerializeField] private float _gamepadLookSensitivity = 150f;

        private DIGInputActions _inputActions;
        private bool _isGamepad = false;
        
        // Cached action map references (runtime lookup)
        private InputActionMap _coreMap;
        private InputActionMap _shooterMap;
        private InputActionMap _mmoMap;
        private InputActionMap _arpgMap;
        private InputActionMap _mobaMap;

        /// <summary>
        /// Auto-create at runtime so input works without manual scene setup.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance != null) return;
            
            var go = new GameObject("[PlayerInputReader]");
            go.AddComponent<PlayerInputReader>();
            DontDestroyOnLoad(go);
            Debug.Log("[PlayerInputReader] Auto-initialized");
        }

        private void OnEnable()
        {
            // Singleton assignment
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Get or create input actions from ParadigmInputManager or InputContextManager
            if (ParadigmInputManager.Instance != null)
            {
                _inputActions = ParadigmInputManager.Instance.InputActions;
            }
            else if (InputContextManager.Instance != null)
            {
                _inputActions = InputContextManager.Instance.InputActions;
            }
            else
            {
                _inputActions = new DIGInputActions();
            }

            // EPIC 15.21 Phase 6: Initialize KeybindService for rebinding support
            Keybinds.KeybindService.Initialize(_inputActions.asset);

            // Cache action maps using runtime lookup
            CacheActionMaps();
            
            // Subscribe to all actions
            SubscribeToCoreActions();
            SubscribeToCombatActions();
        }

        private void CacheActionMaps()
        {
            if (_inputActions?.asset == null)
            {
                Debug.LogError("[PlayerInputReader] InputActionAsset is null!");
                return;
            }
            
            _coreMap = _inputActions.asset.FindActionMap("Core", throwIfNotFound: false);
            _shooterMap = _inputActions.asset.FindActionMap("Combat_Shooter", throwIfNotFound: false);
            _mmoMap = _inputActions.asset.FindActionMap("Combat_MMO", throwIfNotFound: false);
            _arpgMap = _inputActions.asset.FindActionMap("Combat_ARPG", throwIfNotFound: false);
            _mobaMap = _inputActions.asset.FindActionMap("Combat_MOBA", throwIfNotFound: false);

            if (_coreMap == null)
                Debug.LogWarning("[PlayerInputReader] Core action map not found!");
        }

        private void Update()
        {
            // Clear edge detection flags at frame start
            PlayerInputState.ClearEdgeFlags();
            UpdateCursorState();
        }

        private void UpdateCursorState()
        {
            // Always track cursor screen position for hover systems (EPIC 15.18)
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var mousePos = mouse.position.ReadValue();
                PlayerInputState.CursorScreenPosition = new float2(mousePos.x, mousePos.y);
            }

            // EPIC 15.20: CursorController (paradigm system) is the authority for cursor state
            if (CursorController.Instance != null)
            {
                return; // CursorController handles cursor state
            }

            if (DIG.UI.MenuState.IsAnyMenuOpen())
            {
                if (Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                return;
            }

            // EPIC 15.18: Defer to InputSchemeManager for cursor state during gameplay
            var scheme = InputSchemeManager.Instance;
            if (scheme != null && scheme.IsCursorFree)
            {
                Cursor.lockState = CursorLockMode.Confined;
                Cursor.visible = true;
                return;
            }

            // Default: locked cursor
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromActions();
            
            // Don't dispose if using shared instance from managers
            if (ParadigmInputManager.Instance == null && InputContextManager.Instance == null && _inputActions != null)
            {
                _inputActions.Dispose();
            }
        }

        #region ===== Action Subscriptions using Runtime Lookup =====
        
        private void SubscribeToCoreActions()
        {
            if (_coreMap == null) return;
            
            SubscribeAction(_coreMap, "Move", OnMove);
            SubscribeAction(_coreMap, "Look", OnLook);
            SubscribeAction(_coreMap, "Jump", OnJump);
            SubscribeAction(_coreMap, "Crouch", OnCrouch);
            SubscribeAction(_coreMap, "Sprint", OnSprint);
            SubscribeAction(_coreMap, "Interact", OnInteract);
            SubscribeAction(_coreMap, "Reload", OnReload);
            SubscribeAction(_coreMap, "ToggleFlashlight", OnToggleFlashlight);
            SubscribeAction(_coreMap, "Grab", OnGrab);
            SubscribeAction(_coreMap, "Zoom", OnZoom);
            
            // Equipment slots — latch on performed, don't clear on canceled.
            // Quick taps can fire performed+canceled in the same frame; clearing on canceled
            // would lose the input before DIGEquipmentProvider reads it.
            SubscribeAction(_coreMap, "EquipSlot1", ctx => { if (ctx.performed) PlayerInputState.EquipSlot1 = true; });
            SubscribeAction(_coreMap, "EquipSlot2", ctx => { if (ctx.performed) PlayerInputState.EquipSlot2 = true; });
            SubscribeAction(_coreMap, "EquipSlot3", ctx => { if (ctx.performed) PlayerInputState.EquipSlot3 = true; });
            SubscribeAction(_coreMap, "EquipSlot4", ctx => { if (ctx.performed) PlayerInputState.EquipSlot4 = true; });
            SubscribeAction(_coreMap, "EquipSlot5", ctx => { if (ctx.performed) PlayerInputState.EquipSlot5 = true; });
            SubscribeAction(_coreMap, "EquipSlot6", ctx => { if (ctx.performed) PlayerInputState.EquipSlot6 = true; });
            SubscribeAction(_coreMap, "EquipSlot7", ctx => { if (ctx.performed) PlayerInputState.EquipSlot7 = true; });
            SubscribeAction(_coreMap, "EquipSlot8", ctx => { if (ctx.performed) PlayerInputState.EquipSlot8 = true; });
            SubscribeAction(_coreMap, "EquipSlot9", ctx => { if (ctx.performed) PlayerInputState.EquipSlot9 = true; });
            
            // Modifiers
            SubscribeAction(_coreMap, "ModShift", OnModShift);
            SubscribeAction(_coreMap, "ModCtrl", OnModCtrl);
            SubscribeAction(_coreMap, "ModAlt", OnModAlt);
        }
        
        private void SubscribeToCombatActions()
        {
            // Combat_Shooter actions
            if (_shooterMap != null)
            {
                SubscribeAction(_shooterMap, "Attack", OnAttack);
                SubscribeAction(_shooterMap, "AimDownSights", OnAimDownSights);
                SubscribeAction(_shooterMap, "LeanLeft", OnLeanLeft);
                SubscribeAction(_shooterMap, "LeanRight", OnLeanRight);
                SubscribeAction(_shooterMap, "Prone", OnProne);
                SubscribeAction(_shooterMap, "Slide", OnSlide);
                SubscribeAction(_shooterMap, "DodgeDive", OnDodgeDive);
                SubscribeAction(_shooterMap, "FreeLook", OnFreeLook);
            }
            
            // Combat_MMO actions
            if (_mmoMap != null)
            {
                SubscribeAction(_mmoMap, "SelectTarget", OnSelectTarget);
                SubscribeAction(_mmoMap, "CameraOrbit", OnCameraOrbit);
                SubscribeAction(_mmoMap, "AutoRun", OnAutoRun);
                SubscribeAction(_mmoMap, "DodgeRoll", OnDodgeRoll);
                SubscribeAction(_mmoMap, "StrafeLeft", OnStrafeLeft);
                SubscribeAction(_mmoMap, "StrafeRight", OnStrafeRight);
            }
            
            // Combat_ARPG actions
            if (_arpgMap != null)
            {
                SubscribeAction(_arpgMap, "AttackAtCursor", OnAttackAtCursor);
                SubscribeAction(_arpgMap, "MoveToClick", OnMoveToClick);
                SubscribeAction(_arpgMap, "DodgeRoll", OnDodgeRoll);
            }

            // Combat_MOBA actions (EPIC 15.20 Phase 4a)
            if (_mobaMap != null)
            {
                SubscribeAction(_mobaMap, "AttackMove", OnAttackMove);
                SubscribeAction(_mobaMap, "Stop", OnStop);
                SubscribeAction(_mobaMap, "HoldPosition", OnHoldPosition);
                SubscribeAction(_mobaMap, "CameraLockToggle", OnCameraLockToggle);
                SubscribeAction(_mobaMap, "AttackAtCursor", OnAttackAtCursor);
            }
        }
        
        /// <summary>
        /// Helper to subscribe to an action using runtime lookup.
        /// </summary>
        private void SubscribeAction(InputActionMap map, string actionName, System.Action<InputAction.CallbackContext> callback)
        {
            var action = map?.FindAction(actionName, throwIfNotFound: false);
            if (action != null)
            {
                action.performed += callback;
                action.canceled += callback;
            }
        }
        
        private void UnsubscribeFromActions()
        {
            // Core unsubscribe
            UnsubscribeAction(_coreMap, "Move", OnMove);
            UnsubscribeAction(_coreMap, "Look", OnLook);
            UnsubscribeAction(_coreMap, "Jump", OnJump);
            UnsubscribeAction(_coreMap, "Crouch", OnCrouch);
            UnsubscribeAction(_coreMap, "Sprint", OnSprint);
            UnsubscribeAction(_coreMap, "Interact", OnInteract);
            UnsubscribeAction(_coreMap, "Reload", OnReload);
            UnsubscribeAction(_coreMap, "ToggleFlashlight", OnToggleFlashlight);
            UnsubscribeAction(_coreMap, "Grab", OnGrab);
            UnsubscribeAction(_coreMap, "Zoom", OnZoom);
            
            // Modifiers unsubscribe
            // Note: Lambdas are hard to unsubscribe from if not stored, 
            // but since we destroy the object on disable/destroy usually, it might be ok?
            // Actually, SubscribeAction adds to the Action event. 
            // C# require the SAME delegate instance to unsubscribe.
            // The current implementation of SubscribeAction takes a callback.
            // If I pass a lambda `ctx => ...`, a NEW delegate is created each time.
            // So UnsubscribeAction with a new lambda `ctx => ...` does NOTHING.
            // I must use named methods for Clean Unsubscribing!
            UnsubscribeAction(_coreMap, "ModShift", OnModShift);
            UnsubscribeAction(_coreMap, "ModCtrl", OnModCtrl);
            UnsubscribeAction(_coreMap, "ModAlt", OnModAlt);
            
            // Combat_Shooter unsubscribe
            UnsubscribeAction(_shooterMap, "Attack", OnAttack);
            UnsubscribeAction(_shooterMap, "AimDownSights", OnAimDownSights);
            UnsubscribeAction(_shooterMap, "LeanLeft", OnLeanLeft);
            UnsubscribeAction(_shooterMap, "LeanRight", OnLeanRight);
            UnsubscribeAction(_shooterMap, "Prone", OnProne);
            UnsubscribeAction(_shooterMap, "Slide", OnSlide);
            UnsubscribeAction(_shooterMap, "DodgeDive", OnDodgeDive);
            UnsubscribeAction(_shooterMap, "FreeLook", OnFreeLook);
            
            // Combat_MMO unsubscribe
            UnsubscribeAction(_mmoMap, "SelectTarget", OnSelectTarget);
            UnsubscribeAction(_mmoMap, "CameraOrbit", OnCameraOrbit);
            UnsubscribeAction(_mmoMap, "AutoRun", OnAutoRun);
            UnsubscribeAction(_mmoMap, "DodgeRoll", OnDodgeRoll);
            UnsubscribeAction(_mmoMap, "StrafeLeft", OnStrafeLeft);
            UnsubscribeAction(_mmoMap, "StrafeRight", OnStrafeRight);
            
            // Combat_ARPG unsubscribe
            UnsubscribeAction(_arpgMap, "AttackAtCursor", OnAttackAtCursor);
            UnsubscribeAction(_arpgMap, "MoveToClick", OnMoveToClick);
            UnsubscribeAction(_arpgMap, "DodgeRoll", OnDodgeRoll);

            // Combat_MOBA unsubscribe
            UnsubscribeAction(_mobaMap, "AttackMove", OnAttackMove);
            UnsubscribeAction(_mobaMap, "Stop", OnStop);
            UnsubscribeAction(_mobaMap, "HoldPosition", OnHoldPosition);
            UnsubscribeAction(_mobaMap, "CameraLockToggle", OnCameraLockToggle);
            UnsubscribeAction(_mobaMap, "AttackAtCursor", OnAttackAtCursor);
        }
        
        /// <summary>
        /// Helper to unsubscribe from an action using runtime lookup.
        /// </summary>
        private void UnsubscribeAction(InputActionMap map, string actionName, System.Action<InputAction.CallbackContext> callback)
        {
            var action = map?.FindAction(actionName, throwIfNotFound: false);
            if (action != null)
            {
                action.performed -= callback;
                action.canceled -= callback;
            }
        }
        
        #endregion

        #region ===== Core Action Handlers =====

        private void OnMove(InputAction.CallbackContext context)
        {
            var value = context.ReadValue<Vector2>();
            PlayerInputState.Move = new float2(value.x, value.y);
        }

        private void OnLook(InputAction.CallbackContext context)
        {
            var raw = context.ReadValue<Vector2>();
            _isGamepad = context.control?.device is Gamepad;

            if (_isGamepad)
            {
                raw = ApplyAimAcceleration(raw) * _gamepadLookSensitivity * Time.deltaTime;
            }

            var delta = new float2(raw.x, raw.y);

            // EPIC 15.18: Always store raw delta
            PlayerInputState.RawLookDelta = delta;

            // EPIC 15.20: Use CameraOrbitController for paradigm-aware suppression
            bool suppressLook = false;
            
            var orbitController = CameraOrbitController.Instance;
            if (orbitController != null)
            {
                suppressLook = !orbitController.ShouldApplyLookDelta();
            }
            else if (InputSchemeManager.Instance != null)
            {
                suppressLook = InputSchemeManager.Instance.ShouldSuppressLookDelta();
            }

            PlayerInputState.LookDelta = suppressLook ? float2.zero : delta;
        }

        private void OnJump(InputAction.CallbackContext context)
        {
            PlayerInputState.Jump = context.performed || context.ReadValueAsButton();
        }

        private void OnCrouch(InputAction.CallbackContext context)
        {
            if (HoldToToggleService.IsToggleEnabled("Crouch"))
            {
                if (context.performed)
                    PlayerInputState.Crouch = HoldToToggleService.OnActionPerformed("Crouch");
                // On canceled: keep toggle state (don't clear)
            }
            else
            {
                PlayerInputState.Crouch = context.performed || context.ReadValueAsButton();
            }
        }

        private void OnSprint(InputAction.CallbackContext context)
        {
            if (HoldToToggleService.IsToggleEnabled("Sprint"))
            {
                if (context.performed)
                    PlayerInputState.Sprint = HoldToToggleService.OnActionPerformed("Sprint");
            }
            else
            {
                PlayerInputState.Sprint = context.performed || context.ReadValueAsButton();
            }
        }

        private void OnInteract(InputAction.CallbackContext context)
        {
            PlayerInputState.Interact = context.performed || context.ReadValueAsButton();
        }

        private void OnReload(InputAction.CallbackContext context)
        {
            PlayerInputState.Reload = context.performed || context.ReadValueAsButton();
            if (context.started || context.performed)
                PlayerInputState.ReloadPressed = true;
        }

        private void OnToggleFlashlight(InputAction.CallbackContext context)
        {
            // Latch on performed, don't clear on canceled (same-frame drop fix).
            // PlayerInputSystem consumes after reading.
            if (context.performed)
                PlayerInputState.ToggleFlashlight = true;
        }

        private void OnGrab(InputAction.CallbackContext context)
        {
            PlayerInputState.Grab = context.performed || context.ReadValueAsButton();
        }

        private void OnZoom(InputAction.CallbackContext context)
        {
            PlayerInputState.ZoomDelta = context.ReadValue<float>();
        }

        private void OnModShift(InputAction.CallbackContext context)
        {
            PlayerInputState.ModShift = context.ReadValueAsButton();
        }

        private void OnModCtrl(InputAction.CallbackContext context)
        {
            PlayerInputState.ModCtrl = context.ReadValueAsButton();
        }

        private void OnModAlt(InputAction.CallbackContext context)
        {
            PlayerInputState.ModAlt = context.ReadValueAsButton();
        }
        
        #endregion

        #region ===== Combat_Shooter Action Handlers =====
        
        private void OnAttack(InputAction.CallbackContext context)
        {
            bool wasPressed = PlayerInputState.Fire;
            PlayerInputState.Fire = context.performed || context.ReadValueAsButton();
            if (!wasPressed && PlayerInputState.Fire)
                PlayerInputState.FirePressed = true;
            if (wasPressed && !PlayerInputState.Fire)
                PlayerInputState.FireReleased = true;
        }
        
        private void OnAimDownSights(InputAction.CallbackContext context)
        {
            if (HoldToToggleService.IsToggleEnabled("Aim"))
            {
                if (context.performed)
                {
                    bool wasPressed = PlayerInputState.Aim;
                    PlayerInputState.Aim = HoldToToggleService.OnActionPerformed("Aim");
                    if (!wasPressed && PlayerInputState.Aim)
                        PlayerInputState.AimPressed = true;
                    if (wasPressed && !PlayerInputState.Aim)
                        PlayerInputState.AimReleased = true;
                }
            }
            else
            {
                bool wasPressed = PlayerInputState.Aim;
                PlayerInputState.Aim = context.performed || context.ReadValueAsButton();
                if (!wasPressed && PlayerInputState.Aim)
                    PlayerInputState.AimPressed = true;
                if (wasPressed && !PlayerInputState.Aim)
                    PlayerInputState.AimReleased = true;
            }
        }

        private void OnLeanLeft(InputAction.CallbackContext context)
        {
            PlayerInputState.LeanLeft = context.performed || context.ReadValueAsButton();
        }

        private void OnLeanRight(InputAction.CallbackContext context)
        {
            PlayerInputState.LeanRight = context.performed || context.ReadValueAsButton();
        }

        private void OnProne(InputAction.CallbackContext context)
        {
            // Latch on performed, don't clear on canceled (same-frame drop fix).
            // PlayerInputSystem consumes after reading.
            if (context.performed)
                PlayerInputState.Prone = true;
        }

        private void OnSlide(InputAction.CallbackContext context)
        {
            // Latch on performed, don't clear on canceled (same-frame drop fix).
            // PlayerInputSystem consumes after reading.
            if (context.performed)
                PlayerInputState.Slide = true;
        }

        private void OnDodgeDive(InputAction.CallbackContext context)
        {
            // Latch on performed, don't clear on canceled. Quick taps can fire
            // performed+canceled in the same frame, losing the input before ECS reads it.
            // PlayerInputSystem consumes (clears) after reading.
            if (context.performed)
                PlayerInputState.DodgeDive = true;
        }

        private void OnFreeLook(InputAction.CallbackContext context)
        {
            PlayerInputState.FreeLook = context.performed || context.ReadValueAsButton();
        }
        
        #endregion

        #region ===== Combat_MMO Action Handlers =====
        
        private void OnSelectTarget(InputAction.CallbackContext context)
        {
            PlayerInputState.Select = context.performed || context.ReadValueAsButton();
        }
        
        private void OnCameraOrbit(InputAction.CallbackContext context)
        {
            PlayerInputState.CameraOrbit = context.performed || context.ReadValueAsButton();
        }
        
        private void OnAutoRun(InputAction.CallbackContext context)
        {
            // Latch on performed, don't clear on canceled (same-frame drop fix).
            // PlayerInputSystem consumes after reading.
            if (context.performed)
                PlayerInputState.AutoRun = true;
        }

        private void OnDodgeRoll(InputAction.CallbackContext context)
        {
            // Latch on performed, don't clear on canceled. Quick taps can fire
            // performed+canceled in the same frame, losing the input before ECS reads it.
            // PlayerInputSystem consumes (clears) after reading.
            if (context.performed)
                PlayerInputState.DodgeRoll = true;
        }

        private void OnStrafeLeft(InputAction.CallbackContext context)
        {
            PlayerInputState.MMOStrafeLeft = context.performed || context.ReadValueAsButton();
        }

        private void OnStrafeRight(InputAction.CallbackContext context)
        {
            PlayerInputState.MMOStrafeRight = context.performed || context.ReadValueAsButton();
        }
        
        #endregion

        #region ===== Combat_ARPG Action Handlers =====
        
        private void OnAttackAtCursor(InputAction.CallbackContext context)
        {
            PlayerInputState.Fire = context.performed || context.ReadValueAsButton();
        }
        
        private void OnMoveToClick(InputAction.CallbackContext context)
        {
            PlayerInputState.Aim = context.performed || context.ReadValueAsButton();
        }
        
        #endregion

        #region ===== Combat_MOBA Action Handlers (EPIC 15.20 Phase 4a) =====

        private void OnAttackMove(InputAction.CallbackContext context)
        {
            // Latch on performed, don't clear on canceled (same-frame drop fix).
            if (context.performed)
                PlayerInputState.AttackMove = true;
        }

        private void OnStop(InputAction.CallbackContext context)
        {
            // Latch on performed, don't clear on canceled (same-frame drop fix).
            if (context.performed)
                PlayerInputState.Stop = true;
        }

        private void OnHoldPosition(InputAction.CallbackContext context)
        {
            // Latch on performed, don't clear on canceled (same-frame drop fix).
            if (context.performed)
                PlayerInputState.HoldPosition = true;
        }

        private void OnCameraLockToggle(InputAction.CallbackContext context)
        {
            if (context.performed)
                PlayerInputState.CameraLockToggle = true;
        }

        #endregion

        #region ===== Utility =====

        private Vector2 ApplyAimAcceleration(Vector2 input)
        {
            float magnitude = input.magnitude;
            
            if (magnitude < _stickDeadzone)
                return Vector2.zero;

            float normalizedMagnitude = (magnitude - _stickDeadzone) / (1f - _stickDeadzone);
            normalizedMagnitude = Mathf.Clamp01(normalizedMagnitude);

            float acceleratedMagnitude = _aimAccelerationCurve.Evaluate(normalizedMagnitude);
            
            return input.normalized * acceleratedMagnitude;
        }

        #endregion
    }
}
