# EPIC 15.21: Input Action Layer

## Overview

A comprehensive input architecture that decouples raw hardware input from game actions, enabling paradigm-aware input routing where the same physical input triggers different actions based on context.

## Problem Statement

Currently, input handling is scattered across individual systems:
- `PlayerMovementSystem` checks `input.Use.IsSet` (LMB) and `input.AltUse.IsSet` (RMB) directly
- Each system must know about all paradigms and their input semantics
- No centralized place for paradigm-specific input remapping
- Keybind customization would require changes across many systems

## Goals

1. **Centralized Action Layer**: Single source of truth for "what action does this input mean in this paradigm"
2. **Paradigm-Aware Routing**: Same physical input → different semantic actions based on active paradigm
3. **Keybind Support**: Players can remap inputs without breaking paradigm-specific behavior
4. **Clean System Code**: Movement/combat systems read semantic actions, not raw inputs

## Input Semantic Differences by Paradigm

| Physical Input | Shooter | MMO/RPG | ARPG | Twin-Stick |
|----------------|---------|---------|------|------------|
| LMB | Fire | Interact/Turn | Attack at Cursor | Fire Direction |
| RMB | Aim | Camera Orbit | Move to Cursor | N/A |
| LMB+RMB | - | Auto-Run Forward | - | - |
| A/D | Strafe | Turn (Tank Mode) | Strafe | Strafe |
| A/D + RMB | Strafe | Strafe | Strafe | - |

## Chosen Approach: Unity Action Maps

> [!IMPORTANT]
> After researching alternatives, **Unity Action Maps** is the chosen long-term solution.
> This is the industry-standard approach with built-in keybind support, save/load, and Unity maintenance.

### Why Action Maps

| Factor | Benefit |
|--------|--------|
| Unity maintains it | Bug fixes and updates from Unity |
| Built-in keybinding | `SaveBindingOverridesAsJson()` / `LoadBindingOverridesFromJson()` |
| Composite bindings | Auto-run (LMB+RMB) via "TwoModifiers" composite |
| Industry standard | New team members understand it immediately |
| Editor-configurable | Input logic lives in `.inputactions` asset, not code |

### Target Architecture

```
DIGInputActions.inputactions
│
├── Core (Always Enabled)
│   ├── Move, Look, Zoom
│   ├── Jump, Crouch, Sprint
│   └── Interact, Reload
│
├── Combat_Shooter (Enabled when Shooter paradigm)
│   ├── Fire → Attack
│   ├── Aim → AimDownSights
│   ├── LeanLeft, LeanRight
│   └── Prone, Slide
│
├── Combat_MMO (Enabled when MMO paradigm)
│   ├── Fire → SelectTarget
│   ├── Aim → CameraOrbit
│   ├── AutoRun (Composite: Fire+Aim)
│   └── DodgeRoll
│
├── Combat_ARPG (Enabled when ARPG paradigm)
│   ├── Fire → AttackAtCursor
│   ├── Aim → MoveToClick
│   └── DodgeRoll
│
├── Vehicle (future)
├── EVA (future)
└── UI
```

### Data Flow

```
DIGInputActions.inputactions
        ↓
ParadigmInputManager (switches maps based on ParadigmStateMachine)
        ↓
PlayerInputReader (subscribes to active map's semantic actions)
        ↓
PlayerInputState → PlayerInputSystem → PlayerInput (ECS)
        ↓
Game Systems (read semantic actions, no paradigm checks needed)
```

### Map Switching

```csharp
void OnParadigmChanged(InputParadigm newParadigm)
{
    // Disable all paradigm-specific maps
    inputActions.Combat_Shooter.Disable();
    inputActions.Combat_MMO.Disable();
    inputActions.Combat_ARPG.Disable();
    
    // Enable the correct one
    switch (newParadigm)
    {
        case InputParadigm.Shooter: inputActions.Combat_Shooter.Enable(); break;
        case InputParadigm.MMO: inputActions.Combat_MMO.Enable(); break;
        case InputParadigm.ARPG: inputActions.Combat_ARPG.Enable(); break;
    }
}
```

### Player Keybind Customization

```csharp
// Save player's custom bindings
string json = inputActions.SaveBindingOverridesAsJson();
PlayerPrefs.SetString("keybinds", json);

// Load on game start
string saved = PlayerPrefs.GetString("keybinds");
inputActions.LoadBindingOverridesFromJson(saved);
```

## Complete Input Action Inventory

This section documents ALL input actions in the game, organized by domain. Each domain may require different treatment during migration.

---

### Domain 1: Movement (Priority: High)
**Current State:** Routed through `DIGInputActions.Gameplay` → `PlayerInputReader` → `PlayerInputState` → ECS

| Action | Default Key | ECS Field | Paradigm Behavior | Migration Notes |
|--------|-------------|-----------|-------------------|-----------------|
| Move | WASD/LeftStick | `Horizontal`, `Vertical` | All | Direction meaning differs by paradigm |
| Jump | Space/SouthBtn | `Jump` | All | Same across paradigms |
| Crouch | LeftCtrl/EastBtn | `Crouch` | All | Same across paradigms |
| Sprint | LeftShift/L3 | `Sprint` | All | Same across paradigms |
| Prone | Z | `Prone` | Shooter/Tactical | May be disabled in ARPG/MMO |
| Slide | X | `Slide` | Shooter | May be disabled in ARPG/MMO |
| DodgeRoll | B | `DodgeRoll` | ARPG/MMO | May be disabled in Shooter |
| DodgeDive | V | `DodgeDive` | Shooter | May be disabled in ARPG/MMO |

**Derived Actions (Need Router):**
- `AutoRunForward` (LMB+RMB in MMO)
- `TurnLeft/Right` (A/D in MMO tank mode)
- `StrafeLeft/Right` (A/D in Shooter, A/D+RMB in MMO)

---

### Domain 2: Combat (Priority: High)
**Current State:** Partially routed, some direct `Mouse.current` checks in `WeaponEquipVisualBridge`

| Action | Default Key | ECS Field | Paradigm Behavior | Migration Notes |
|--------|-------------|-----------|-------------------|-----------------|
| Fire | LMB/RT | `Use` | All | Semantics differ (attack vs click-to-move) |
| Aim | RMB/LT | `AltUse` | Shooter only | Suppressed in MMO (camera orbit) |
| Reload | R/WestBtn | `Reload` | All | Same across paradigms |
| Grab | Tab | `Grab` | All | Needs context (grab object vs grab ledge) |
| Tackle | - | `Tackle` | Tactical | Not bound |

**Direct Input Usages (Need Migration):**
- `WeaponEquipVisualBridge.cs` lines 753-758: Direct `Mouse.current.leftButton/rightButton`
- `WeaponEquipVisualBridge.cs` line 2643: Direct `Mouse.current`
- `DIGEquipmentProvider.cs` line 733: Direct `Mouse.current`

---

### Domain 3: Camera (Priority: High)
**Current State:** Mixed - some via `DIGInputActions`, some via paradigm subsystems

| Action | Default Key | ECS Field | Paradigm Behavior | Migration Notes |
|--------|-------------|-----------|-------------------|-----------------|
| Look | MouseDelta/RightStick | `LookDelta` | All | Sensitivity differs by paradigm |
| Zoom | ScrollWheel | `ZoomDelta` | All | Same across paradigms |
| FreeLook | LeftAlt | `FreeLook` | Shooter | Decouples camera from character |

**Derived Actions (Need Router):**
- `CameraOrbit` (RMB in MMO - currently conflicts with Aim)

**Direct Input Usages (Need Migration):**
- `CameraOrbitController.cs` line 193: Direct `Mouse.current`
- `CursorController.cs` line 220: Direct `Mouse.current.rightButton`

---

### Domain 4: Equipment (Priority: Medium)
**Current State:** Routed through `DIGInputActions` → `PlayerInputReader` → ECS

| Action | Default Key | ECS Field | Paradigm Behavior | Migration Notes |
|--------|-------------|-----------|-------------------|-----------------|
| EquipSlot1-9 | 1-9 | `EquipSlotId`, `EquipQuickSlot` | All | Same across paradigms |
| ToggleFlashlight | F | `ToggleFlashlight` | All | Same across paradigms |
| LeanLeft | Q | `LeanLeft` | Tactical/Shooter | May be disabled in ARPG |
| LeanRight | E | `LeanRight` | Tactical/Shooter | May be disabled in ARPG |

**Direct Input Usages (Need Migration):**
- `DIGEquipmentProvider.cs` lines 587, 631, 702: Direct `Keyboard.current`
- `EquipmentSlotDefinition.cs` lines 112, 157: Direct `Keyboard.current`

---

### Domain 5: Interaction (Priority: Medium)
**Current State:** Routed via ECS

| Action | Default Key | ECS Field | Paradigm Behavior | Migration Notes |
|--------|-------------|-----------|-------------------|-----------------|
| Interact | T/NorthBtn | `Interact` | All | Same across paradigms |

---

### Domain 6: UI (Priority: Low)
**Current State:** Separate action map in `DIGInputActions`, handled by UI Event System

| Action | Default Key | Notes |
|--------|-------------|-------|
| Navigate | WASD/DPad/LeftStick | UI navigation |
| Submit | Enter/SouthBtn | Confirm selection |
| Cancel | Escape/EastBtn | Close/back |
| Point | MousePosition | Cursor position |
| Click | LMB | UI click |

**Legacy Input Usages (Need Migration):**
- `NavigationManager.cs` line 114: `UnityEngine.Input.GetKeyDown(KeyCode.Escape)`

---

### Domain 7: Special Modes (Priority: Low)
**Current State:** Partially implemented on ECS, partially unbound

| Action | Default Key | ECS Field | Notes |
|--------|-------------|-----------|-------|
| ToggleMagneticBoots | - | `ToggleMagneticBoots` | EVA mode |

---

### Domain 8: Debug (Priority: Low - Keep Separate)
**Current State:** Direct keyboard access, intentionally not routed

| File | Usage | Notes |
|------|-------|-------|
| `DamageDebugSystem.cs` | `Keyboard.current` | Debug only |
| `RespawnDebugSystem.cs` | `Keyboard.current` | Debug only |
| `StatusEffectDebugSystem.cs` | `Keyboard.current` | Debug only |
| `CargoDebugSystem.cs` | `Keyboard.current` | Debug only |
| `PowerDebugSystem.cs` | `Keyboard.current` | Debug only |
| `StreamingVisualizer.cs` | `Keyboard.current` | Debug only |
| `InputSchemeDebugOverlay.cs` | `Keyboard.current` | Debug only |

> [!NOTE]
> Debug inputs intentionally bypass the Input Action Layer for simplicity.

---

### Domain 9: Targeting (Priority: Medium)
**Current State:** Direct mouse access for cursor position

| File | Usage | Migration Notes |
|------|-------|-----------------|
| `CursorHoverSystem.cs` line 102 | `Mouse.current` | Cursor position |
| `CursorClickTargetSystem.cs` line 90 | `Mouse.current` | Cursor position + click |

---

## Migration Summary

| Domain | Priority | Files to Migrate | Complexity |
|--------|----------|------------------|------------|
| Movement | High | `PlayerMovementSystem.cs` | Medium - paradigm logic |
| Combat | High | `WeaponEquipVisualBridge.cs`, `DIGEquipmentProvider.cs`, `PlayerToItemInputSystem.cs` | High - many direct usages |
| Camera | High | `CameraOrbitController.cs`, `CursorController.cs` | Medium |
| Equipment | Medium | `DIGEquipmentProvider.cs`, `EquipmentSlotDefinition.cs` | Low |
| Interaction | Medium | Already routed | Low |
| UI | Low | `NavigationManager.cs` | Low - legacy Input API |
| Targeting | Medium | `CursorHoverSystem.cs`, `CursorClickTargetSystem.cs` | Low |
| Debug | Skip | N/A | Keep as-is |

## Implementation Phases

### Phase 1: Redesign Input Asset
- [x] Create `Core` action map (shared actions that work in all paradigms)
- [x] Create `Combat_Shooter` action map with Shooter-specific semantics
- [x] Create `Combat_MMO` action map with MMO semantics + AutoRun composite
- [x] Create `Combat_ARPG` action map with ARPG semantics
- [x] Verify all actions have Keyboard+Mouse and Gamepad bindings

### Phase 2: Create ParadigmInputManager
- [x] Create `ParadigmInputManager.cs` MonoBehaviour
- [x] Subscribe to `ParadigmStateMachine.OnParadigmChanged`
- [x] Implement map enable/disable switching logic
- [x] Ensure `Core` map stays enabled at all times

### Phase 3: Rewrite PlayerInputReader
- [x] Subscribe to semantic action callbacks from active maps
- [x] Remove raw button interpretation (let action maps define semantics)
- [x] Update `PlayerInputState` fields to match semantic actions

### Phase 4: Migrate Direct Input Usages
- [x] `WeaponEquipVisualBridge.cs` - Replace `Mouse.current` with action callbacks
- [x] `DIGEquipmentProvider.cs` - Replace `Keyboard.current` with action callbacks
- [x] `CameraOrbitController.cs` - Replace `Mouse.current` with action callbacks
- [x] `CursorController.cs` - Replace `Mouse.current` with action callbacks
- [x] `EquipmentSlotDefinition.cs` - Replace `Keyboard.current` with action callbacks
- [x] `CursorHoverSystem.cs` - Replace `Mouse.current` with action callbacks
- [x] `CursorClickTargetSystem.cs` - Replace `Mouse.current` with action callbacks
- [x] `NavigationManager.cs` - Replace legacy `Input.GetKeyDown` with action callbacks

### Phase 5: Update ECS Pipeline
- [x] Update `PlayerInput` (ECS struct) with semantic action fields
- [x] Update `PlayerInputSystem` to read semantic state from `PlayerInputState`
- [x] Remove paradigm checks from `PlayerMovementSystem`
- [x] Remove paradigm checks from `PlayerToItemInputSystem`

### Phase 6: Keybind UI
- [x] Create keybind settings UI panel
- [x] Implement interactive rebinding with `PerformInteractiveRebinding()`
- [x] Save/load bindings with `SaveBindingOverridesAsJson()` / `LoadBindingOverridesFromJson()`
- [x] Add "Reset to Defaults" functionality
- [x] Per-paradigm keybind sections in UI

### Phase 7: Cleanup
- [ ] Remove legacy paradigm checks scattered across systems
- [ ] Delete unused input handling code
- [ ] Update documentation
- [ ] Testing all paradigms

## Dependencies

- EPIC 15.20: Input Paradigm Framework (provides `ParadigmStateMachine`)
- Unity Input System package (already installed)

## Success Criteria

1. Same physical input produces correct semantic action per paradigm
2. MMO auto-run (LMB+RMB) works via composite binding
3. Game systems read semantic actions only, no raw input or paradigm checks
4. Players can fully customize keybinds per paradigm
5. Keybinds persist across sessions
6. New paradigms can be added by creating new action maps (no code changes)

---

## Extensibility

This section documents how new features, systems, or third-party assets can integrate with the Input Action Layer without modifying core input code.

### Core Principles

1. **Registration over Modification**: New actions are registered, not hardcoded
2. **Data-Driven**: Action definitions via ScriptableObjects where possible
3. **Paradigm-Aware by Default**: All registered actions declare their paradigm behavior
4. **Subscription Model**: Features subscribe to actions rather than polling

### Action Registration API

New features register their semantic actions at runtime:

```csharp
// Third-party asset registers its custom action
InputActionRouter.RegisterAction(new InputActionDefinition
{
    ActionId = "CustomAbility1",
    DisplayName = "Use Special Ability",
    DefaultBinding = "<Keyboard>/q",
    Category = InputCategory.Combat,
    
    // Define behavior per paradigm
    ParadigmBehavior = new Dictionary<InputParadigm, ActionBehavior>
    {
        { InputParadigm.Shooter, ActionBehavior.Enabled },
        { InputParadigm.MMO, ActionBehavior.Enabled },
        { InputParadigm.ARPG, ActionBehavior.Disabled }, // Not available in ARPG
    }
});
```

### Action Listener Interface

Features subscribe to actions rather than polling raw input:

```csharp
public class MyCustomAbility : MonoBehaviour, IInputActionListener
{
    void OnEnable()
    {
        InputActionRouter.Subscribe("CustomAbility1", OnAbilityTriggered);
    }
    
    void OnDisable()
    {
        InputActionRouter.Unsubscribe("CustomAbility1", OnAbilityTriggered);
    }
    
    void OnAbilityTriggered(InputActionContext ctx)
    {
        // Only called when action is valid for current paradigm
        // ctx contains: IsPressed, WasJustPressed, WasJustReleased, etc.
        if (ctx.WasJustPressed)
        {
            ActivateAbility();
        }
    }
}
```

### ScriptableObject Action Definitions

Assets can define their input needs via data files:

```
Assets/
  MyAsset/
    InputActions/
      CustomAbilityActions.asset  ← ScriptableObject
```

**InputActionDefinitionAsset.cs**:
```csharp
[CreateAssetMenu(menuName = "DIG/Input/Action Definition")]
public class InputActionDefinitionAsset : ScriptableObject
{
    public string ActionId;
    public string DisplayName;
    public string DefaultBinding;
    public InputCategory Category;
    public ParadigmBehaviorEntry[] ParadigmBehaviors;
    
    void OnEnable()
    {
        // Auto-register when asset loads
        InputActionRouter.RegisterAction(ToDefinition());
    }
}
```

### Paradigm Override Points

Assets can extend paradigm behavior without modifying core profiles:

```csharp
// Asset adds paradigm-specific rules
ParadigmStateMachine.RegisterOverride(InputParadigm.MMO, new ParadigmOverride
{
    // Add new actions available in this paradigm
    AdditionalActions = new[] { "CustomAbility1", "CustomAbility2" },
    
    // Remap existing actions (e.g., replace default attack)
    ActionRemaps = new Dictionary<string, string>
    {
        { "Attack", "CustomAttack" }
    },
    
    // Suppress actions in this paradigm
    SuppressedActions = new[] { "DefaultBlock" }
});
```

### ECS Integration

For ECS systems that need to read custom actions:

```csharp
// Option 1: Query the router directly (main thread only)
bool isAbilityActive = InputActionRouter.GetAction("CustomAbility1").IsPressed;

// Option 2: Register a semantic field on PlayerInput struct
// (Requires extending the struct - use sparingly)
public struct PlayerInput : IInputComponentData
{
    // ... existing fields ...
    
    // Custom action fields (registered dynamically)
    public InputEvent CustomAbility1;
}

// Option 3: Use a separate component for custom actions
public struct CustomInputState : IComponentData
{
    public bool Ability1Pressed;
    public bool Ability2Pressed;
}
```

### Keybind Persistence

Custom actions participate in the keybind save/load system:

```csharp
// Keybinds auto-save to PlayerPrefs or custom backend
InputActionRouter.SetCustomBinding("CustomAbility1", "<Keyboard>/e");

// Load persisted keybinds on startup
InputActionRouter.LoadPersistedBindings();
```

### Integration Checklist for New Features

When adding a new feature or asset that requires input:

1. **Define Actions**: Create `InputActionDefinitionAsset` for each action
2. **Declare Paradigm Behavior**: Specify which paradigms support each action
3. **Subscribe to Actions**: Use `IInputActionListener` or poll `InputActionRouter`
4. **Handle Paradigm Changes**: Subscribe to `OnParadigmChanged` if behavior changes
5. **Test All Paradigms**: Verify action behaves correctly in each supported mode
6. **Document Keybinds**: Add to settings UI if player-configurable

### Avoiding Common Mistakes

| ❌ Don't | ✅ Do |
|----------|-------|
| Check `Input.GetKeyDown(KeyCode.Q)` | Subscribe to `"MyAction"` |
| Hardcode paradigm checks in feature code | Declare paradigm behavior in registration |
| Modify `PlayerInput` struct for every feature | Use separate components or router queries |
| Assume action is always available | Check `InputActionRouter.IsActionEnabled("MyAction")` |

## Completion Status

**Status**: [X] Complete
**Date**: 2026-02-06
**Pull Request**: [#392](https://github.com/rclong1221/DIG/pull/392)

### Deliverables Achieved

1.  **Input Architecture**: Implemented paradigm-aware `DIGInputActions` with Core, Shooter, MMO, and ARPG maps.
2.  **Manager System**: Created `ParadigmInputManager` to switch maps dynamically based on `ParadigmStateMachine`.
3.  **Semantic Input**: Rewrote `PlayerInputReader` to map raw inputs to semantic actions (`Use`, `AltUse`, `AutoRun`), removing distinct hardware checks from gameplay systems.
4.  **Keybind System**: Implemented `KeybindService` for JSON persistent bindings and a premium UI for user customization.
5.  **MMO Refinements**:
    - Implemented composite `AutoRun` (LMB+RMB).
    - Refactored `PlayerMovementSystem` and `PlayerAnimationStateSystem` to standardize strafing logic (Q/E now shares physics/animation with A/D).
6.  **Cleanup**: Removed legacy input code and migrated all direct `Mouse.current` / `Keyboard.current` usages to the new system.
