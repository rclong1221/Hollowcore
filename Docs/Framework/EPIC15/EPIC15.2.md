# EPIC 15.2: Input System Modernization

## Goal
To replace the rigid, hardcoded input checks in `PlayerInputSystem` with a robust, data-driven architecture using **Unity's Input Action Assets**. This is a prerequisite for console support, user rebinding, and proper UI navigation.

---

## 1. Input Architecture Overhaul

### Current State (Completed ✅)
*   `PlayerInputSystem.cs` now reads from `PlayerInputState` exclusively.
*   All input flows through `DIGInputActions.inputactions` → `PlayerInputReader` → `PlayerInputState` → ECS.
*   Full gamepad support with configurable aim acceleration.
*   Context switching via `InputContextManager` for UI/Gameplay modes.

### Action Maps
1.  **Gameplay (OnFoot)**
    *   `Move` (Vector2): WASD / Left Stick
    *   `Look` (Vector2): Mouse Delta / Right Stick
    *   `Fire`: LMB / RT
    *   `Aim`: RMB / LT
    *   `Jump`, `Crouch`, `Sprint`, `Interact`, `Reload`.
    *   `EquipSlot1-9`: Numeric keys 1-9 for quick slot weapon selection.
2.  **Gameplay (Vehicle)**
    *   `Throttle` (Axis)
    *   `Steer` (Vector2)
3.  **UI**
    *   `Navigate` (Vector2)
    *   `Submit`, `Cancel`
    *   `Point` (Mouse Position)

---

## 2. Infrastructure Changes

### `PlayerInputReader.cs`
*   **Refactor:** Remove all direct hardware calls.
*   **Implement:** `DIGInputActions.IGameplayActions` interface.
*   **Logic:**
    *   On `Enable`: `_inputActions.Gameplay.Enable()`
    *   On `Disable`: `_inputActions.Gameplay.Disable()`
    *   Cache values into `PlayerInputState` (static) or a new `InputBlob`.

### `PlayerInputSystem.cs` (ECS)
*   **Update:** Read from the `PlayerInputState` (which is now populated by the Action Events) instead of hardware.
*   **Benefit:** The system logic remains "Pure" and agnostic to *what* triggered the input (Keyboard vs Controller).

---

## 3. Gamepad & Haptics Support

### Control Scheme
*   Implement a standard "FPS Console" layout (e.g., Call of Duty / Halo style).
*   **Look Acceleration:** Apply a custom curve to Right Stick input to allow precision aiming + fast turning.
*   **Deadzone:** 0.15 radial deadzone.

### Feedback (FEEL Integration)
*   Leverage FEEL for Haptics.
*   Trigger `MMF_Player` Rumble feedbacks on Fire, Damage, and Land events.

---

## 4. Context Management
*   **Problem:** Opening Inventory currently conflicts with "Look" or "Fire" if not manually blocked.
*   **Solution:** `InputContextManager.Push(Context.UI)`
    *   Disables `Gameplay` Action Map.
    *   Enables `UI` Action Map.
    *   Unlocks Cursor.
*   **Pop:** `InputContextManager.Pop()` restores previous state.

---

## Implementation Tasks
- [x] Create `DIGInputActions.inputactions` asset with Gameplay/UI maps.
- [x] Create `InputContextManager` to handle map switching.
- [x] Refactor `PlayerInputReader` to use C# Events from the Asset.
- [x] Update `PlayerInputSystem` to read all input from `PlayerInputState` (no direct hardware access).
- [x] Implement Gamepad Aim Acceleration curve.
- [x] Create `GameplayFeedbackManager` for FEEL haptics integration.
- [x] Migrate equipment slot input to Input Actions (EquipSlot1-9).
- [ ] (Future) Vehicle action map.
