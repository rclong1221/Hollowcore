# EPIC 18.15: Click-to-Move & WASD Gating for MOBA/ARPG Paradigms

**Status:** PLANNED
**Priority:** High (core gameplay — MOBA/ARPG paradigms are non-functional without this)
**Dependencies:**
- EPIC 15.20: Input Paradigm Framework (ParadigmStateMachine, profiles, IParadigmConfigurable)
- EPIC 15.21: Input Action Layer (ParadigmInputManager, action maps)
- `ClickToMoveHandler` (existing — `Assets/Scripts/Core/Input/Paradigm/Pathfinding/ClickToMoveHandler.cs`)
- `ParadigmStateMachine` (existing — `Assets/Scripts/Core/Input/Paradigm/ParadigmStateMachine.cs`)
- `PlayerMovementSystem` (existing — `Assets/Scripts/Player/Systems/PlayerMovementSystem.cs`)
- `PlayerInputSystem` (existing — `Assets/Scripts/Player/Systems/PlayerInputSystem.cs`)
- `MovementRouter` (existing — `Assets/Scripts/Core/Input/Paradigm/Subsystems/MovementRouter.cs`)
- `ParadigmSettings` ECS singleton (existing — `Assets/Scripts/Core/Input/Paradigm/Components/ParadigmSettings.cs`)
- A* Pathfinding Project (third-party — `Pathfinding` namespace)

**Feature:** Fix click-to-move for MOBA and ARPG paradigms. When a player switches to a paradigm with `wasdEnabled: false` and `clickToMoveEnabled: true`, WASD keys must stop producing movement and click-to-move must become the sole movement input. Currently WASD always flows through the entire input pipeline unconditionally, making MOBA/ARPG paradigms behave identically to Shooter/TwinStick.

---

## Problem

When switching to MOBA or ARPG paradigm via ParadigmDemoUI, the player still moves with WASD as if in Shooter mode. Click-to-move never activates as the primary movement method. The symptom: "it just moves like a twin stick when I choose MOBA or ARPG."

---

## Root Cause Analysis

The paradigm transition chain works correctly — `ClickToMoveHandler.Configure()` IS called and `_clickToMoveEnabled` IS set to `true`. The `MovementRouter.IsWASDEnabled` IS set to `false`. The `ParadigmSettings.IsWASDEnabled` ECS singleton IS synced to `false`. Despite all of this, WASD input still produces movement because **no system in the movement pipeline actually reads the WASD-enabled flag**.

### The Full Input Pipeline

```
1. HARDWARE: WASD key pressed
   ↓
2. INPUT SYSTEM: Core action map "Move" action fires callback
   (Core map is ALWAYS enabled — ParadigmInputManager only toggles Combat maps)
   ↓
3. PlayerInputReader.OnMove() → writes to PlayerInputState.Move (float2)
   (Unconditional — no paradigm check)
   ↓
4. PlayerInputSystem.SampleInput() → copies PlayerInputState.Move to input.Horizontal/Vertical
   (Unconditional — lines 179-180, no IsWASDEnabled check)
   ↓
5. PlayerMovementSystem.OnUpdate() → reads input.Horizontal/Vertical
   (lines 275-276: effectiveHorizontal = input.Horizontal, effectiveVertical = input.Vertical)
   (No IsWASDEnabled check — ParadigmSettings is read but IsWASDEnabled is NEVER consumed)
   ↓
6. Character moves via WASD regardless of paradigm
```

### Bug 1 (PRIMARY): PlayerMovementSystem Never Gates WASD

`PlayerMovementSystem.cs` line 275-276:
```csharp
int effectiveVertical = isAutoRunning ? 1 : input.Vertical;
int effectiveHorizontal = input.Horizontal;
```

The `ParadigmSettings` singleton is successfully read at line 52-57, and `IsWASDEnabled` exists on it, but **it is never used to zero out effectiveHorizontal/effectiveVertical**. The field was written during EPIC 15.20 but the consumption point was never implemented.

### Bug 2 (SECONDARY): PlayerInputSystem Unconditionally Samples WASD

`PlayerInputSystem.cs` line 179-180:
```csharp
input.Horizontal = (sbyte)math.round(PlayerInputState.Move.x);
input.Vertical = (sbyte)math.round(PlayerInputState.Move.y);
```

Even if `PlayerMovementSystem` were gated, the networked `PlayerInput` component still carries WASD data to the server. This means a remote client could observe WASD movement from a MOBA-paradigm player if the server doesn't also gate it.

### Bug 3 (DESIGN GAP): Core Action Map Always Fires Move Callbacks

`ParadigmInputManager.ApplyParadigmMaps()` (line 175-208) only toggles Combat maps (`Combat_Shooter`, `Combat_MMO`, `Combat_ARPG`, `Combat_MOBA`). The `Core` action map — which contains the `Move` action bound to WASD — is **always enabled** (line 126: `_coreMap?.Enable()`). For MOBA/ARPG where `wasdEnabled: false`, the Move callbacks in `PlayerInputReader.OnMove()` still fire and populate `PlayerInputState.Move`.

### Bug 4 (INTERACTION): WASD Input Cancels Active Paths

`ClickToMoveHandler.Update()` line 185:
```csharp
if (_hasActivePath && math.lengthsq(PlayerInputState.Move) > 0.01f)
{
    CancelPath();
    return;
}
```

Even if click-to-move successfully starts a path, any WASD input (even brief key taps) immediately cancels it. Since the Core map's Move action is always enabled, any accidental WASD press while click-to-moving will kill the path. This is intentional for paradigms that have BOTH WASD and click-to-move enabled, but for MOBA/ARPG where `wasdEnabled: false`, it should be suppressed.

---

## Codebase Audit

### Files That Read `IsWASDEnabled` or `wasdEnabled`

| File | Reads | Actually Gates Input? |
|------|-------|-----------------------|
| `MovementRouter.cs` | `profile.wasdEnabled` → stores `_wasdEnabled` | No — only stores the value, exposes via `IsWASDEnabled` property |
| `ParadigmSettings.cs` | `IsWASDEnabled` field on ECS singleton | No — field exists but no system reads it for gating |
| `ParadigmSettingsSyncSystem.cs` | Syncs `MovementRouter.IsWASDEnabled` → `ParadigmSettings.IsWASDEnabled` | No — just syncs the value |
| `PlayerMovementSystem.cs` | Reads `ParadigmSettings` singleton | **NO** — never checks `IsWASDEnabled` |
| `PlayerInputSystem.cs` | Never reads `IsWASDEnabled` at all | **NO** |

**Conclusion**: The `IsWASDEnabled` flag is correctly propagated through the entire paradigm framework but never consumed at the point where it matters — the movement pipeline.

### Profile Configurations (Verified Correct)

| Profile | wasdEnabled | clickToMoveEnabled | clickToMoveButton | usePathfinding |
|---------|-------------|--------------------|--------------------|----------------|
| Profile_Shooter | 1 | 0 | None | 0 |
| Profile_MMO | 1 | 0 | None | 0 |
| Profile_ARPG_Classic | 0 | 1 | LeftButton | 1 |
| Profile_MOBA | 0 | 1 | RightButton | 1 |
| Profile_TwinStick | 1 | 0 | None | 0 |

### ClickToMoveHandler Registration Chain (Verified Working)

```
ClickToMoveHandler.Start()
  → ParadigmStateMachine.Instance.RegisterConfigurable(this)
    → RegisterConfigurable() checks if profile already active
      → If active: calls handler.Configure(activeProfile) immediately
      → Sets _clickToMoveEnabled, _activeButton, _usePathfinding
```

This works. The handler IS configured. The problem is downstream.

---

## Architecture: Proposed Solution

### Strategy: Gate WASD at Two Points

The minimal fix requires gating WASD at the input sampling level (prevents WASD from entering the networked pipeline) AND at the movement system level (defense-in-depth for server authority).

```
┌──────────────────────────────────────────────────────────────┐
│                    Fixed Input Pipeline                       │
│                                                              │
│  PlayerInputSystem.SampleInput():                            │
│    if (ParadigmSettings.IsWASDEnabled)                       │
│        input.Horizontal = round(PlayerInputState.Move.x)     │
│        input.Vertical = round(PlayerInputState.Move.y)       │
│    else                                                      │
│        input.Horizontal = 0                                  │
│        input.Vertical = 0                                    │
│                                                              │
│  (Path following is unaffected — PathMoveX/Y always sampled) │
│                                                              │
│  PlayerMovementSystem.OnUpdate():                            │
│    if (!paradigmSettings.IsWASDEnabled)                       │
│        effectiveHorizontal = 0                               │
│        effectiveVertical = 0                                 │
│    (Defense-in-depth — server may not have paradigm sync)    │
│                                                              │
│  ClickToMoveHandler.Update():                                │
│    WASD interruption check reads PlayerInputState.Move       │
│    → With SampleInput zeroing, this is still float2 from     │
│      Input System callbacks. Must also gate the interruption │
│      check with IsWASDEnabled.                               │
└──────────────────────────────────────────────────────────────┘
```

### Why NOT Disable the Core Action Map?

Disabling the `Core` action map's `Move` action for MOBA/ARPG would be the cleanest approach but is dangerous:
- Other actions in Core (Jump, Crouch, Sprint, Interact, Reload, etc.) must remain active
- Selectively disabling the `Move` action within Core requires action-level enable/disable, which Input System supports but adds complexity
- The Move action is also used by other systems (camera input, UI navigation) that shouldn't be affected

The safer approach is to let the Move action fire but ignore its output when `IsWASDEnabled == false`.

---

## Tasks

### Checklist
- [ ] **18.15.1**: Gate WASD in `PlayerInputSystem.SampleInput()`
- [ ] **18.15.2**: Gate WASD in `PlayerMovementSystem` (defense-in-depth)
- [ ] **18.15.3**: Gate WASD interruption in `ClickToMoveHandler`
- [ ] **18.15.4**: Verify `ClickToMoveHandler` registration timing
- [ ] **18.15.5**: Test all paradigm transitions (no cross-contamination)
- [ ] **18.15.6**: Verify cursor state for click-to-move raycasts

---

### Task 18.15.1: Gate WASD in PlayerInputSystem.SampleInput()

**File**: `Assets/Scripts/Player/Systems/PlayerInputSystem.cs`

This is the primary fix. `SampleInput()` is where managed-side input (from `PlayerInputState`) enters the ECS pipeline via the `PlayerInput` ghost component. Gating here prevents WASD from reaching the server.

**Change at lines 178-180** (inside `SampleInput()`):

Before:
```csharp
// ===== MOVEMENT (from PlayerInputState, populated by PlayerInputReader) =====
input.Horizontal = (sbyte)math.round(PlayerInputState.Move.x);
input.Vertical = (sbyte)math.round(PlayerInputState.Move.y);
```

After:
```csharp
// ===== MOVEMENT (from PlayerInputState, populated by PlayerInputReader) =====
// EPIC 18.15: Gate WASD input based on paradigm settings.
// MOBA/ARPG profiles have wasdEnabled=false — only path following moves the character.
bool wasdEnabled = MovementRouter.Instance == null || MovementRouter.Instance.IsWASDEnabled;
if (wasdEnabled)
{
    input.Horizontal = (sbyte)math.round(PlayerInputState.Move.x);
    input.Vertical = (sbyte)math.round(PlayerInputState.Move.y);
}
else
{
    input.Horizontal = 0;
    input.Vertical = 0;
}
```

**Why `MovementRouter.Instance` instead of ECS singleton?**: `SampleInput()` runs in managed code (`IInputComponentData.InternalSampleInput`), not Burst. It already accesses `PlayerInputState` (static managed fields). `MovementRouter.Instance` is the managed-side source of truth and updates synchronously during paradigm transitions. The ECS `ParadigmSettings` singleton syncs one frame later.

**Note**: The same logic must be applied to the second `SampleInput` method (the `BackupInput` overload at ~line 295) which has an identical copy of the sampling code.

**Acceptance**: Pressing WASD in MOBA/ARPG paradigm produces zero `Horizontal`/`Vertical` in the networked `PlayerInput` component. Movement only occurs via `PathMoveX`/`PathMoveY` (from click-to-move).

---

### Task 18.15.2: Gate WASD in PlayerMovementSystem (Defense-in-Depth)

**File**: `Assets/Scripts/Player/Systems/PlayerMovementSystem.cs`

Defense-in-depth: even if `PlayerInputSystem` doesn't gate (e.g., on dedicated server where `MovementRouter.Instance` might be null), the movement system should also respect `IsWASDEnabled`.

**Change at lines 275-276**:

Before:
```csharp
int effectiveVertical = isAutoRunning ? 1 : input.Vertical;
int effectiveHorizontal = input.Horizontal;
```

After:
```csharp
// EPIC 18.15: Gate WASD input based on paradigm settings.
// When wasdEnabled=false (MOBA/ARPG), only path-following input moves the character.
bool wasdGated = hasParadigmSettings && !paradigmSettings.IsWASDEnabled;
int effectiveVertical = wasdGated ? 0 : (isAutoRunning ? 1 : input.Vertical);
int effectiveHorizontal = wasdGated ? 0 : input.Horizontal;
```

**Note**: `hasParadigmSettings` and `paradigmSettings` are already available at this point (read at lines 52-57). No additional singleton lookups. This is Burst-compatible since `ParadigmSettings` is an ECS `IComponentData`.

**Acceptance**: Even if WASD data somehow reaches `PlayerInput.Horizontal/Vertical`, movement is still zero when `IsWASDEnabled == false`. Path-following via `IsPathFollowing`/`PathMoveX`/`PathMoveY` is unaffected (uses separate branches at lines 357-358, 420-421, 585-586).

---

### Task 18.15.3: Gate WASD Interruption in ClickToMoveHandler

**File**: `Assets/Scripts/Core/Input/Paradigm/Pathfinding/ClickToMoveHandler.cs`

The WASD interruption guard (line 185) cancels any active path when WASD input is detected. This is correct for paradigms where both WASD and click-to-move are enabled (e.g., a hypothetical hybrid mode), but wrong for MOBA/ARPG where WASD is disabled — the Move action still fires callbacks from the Core map, populating `PlayerInputState.Move` even though the movement pipeline now ignores it.

**Change at lines 184-189**:

Before:
```csharp
// WASD INTERRUPTION: If player presses any movement key, cancel path
if (_hasActivePath && math.lengthsq(PlayerInputState.Move) > 0.01f)
{
    if (_logPathEvents) Debug.Log("[ClickToMoveHandler] Path cancelled by WASD input");
    CancelPath();
    return;
}
```

After:
```csharp
// WASD INTERRUPTION: If player presses any movement key, cancel path.
// Only check if WASD is enabled — in MOBA/ARPG, Move callbacks still fire from Core map
// but should not interrupt click-to-move paths.
bool wasdEnabled = MovementRouter.Instance == null || MovementRouter.Instance.IsWASDEnabled;
if (wasdEnabled && _hasActivePath && math.lengthsq(PlayerInputState.Move) > 0.01f)
{
    if (_logPathEvents) Debug.Log("[ClickToMoveHandler] Path cancelled by WASD input");
    CancelPath();
    return;
}
```

**Acceptance**: In MOBA/ARPG mode, pressing WASD while click-to-move path is active does NOT cancel the path. In Shooter/MMO mode, WASD still cancels paths as before (since `wasdEnabled = true`).

---

### Task 18.15.4: Verify ClickToMoveHandler Registration Timing

**File**: `Assets/Scripts/Core/Input/Paradigm/Pathfinding/ClickToMoveHandler.cs`

Verify that `ClickToMoveHandler` is registered with `ParadigmStateMachine` before the paradigm transition fires. The current flow:

```
ClickToMoveHandler.Start() → ParadigmStateMachine.Instance.RegisterConfigurable(this)
```

`RegisterConfigurable()` in `ParadigmStateMachine` (line ~215) auto-configures late registrants:
```csharp
if (_activeProfile != null)
    configurable.Configure(_activeProfile);
```

**Potential issue**: If `ClickToMoveHandler` hasn't been instantiated when the paradigm switches, it never gets configured. The handler is created via `[RuntimeInitializeOnLoadMethod]` or scene placement. Verify:

1. Check if `ClickToMoveHandler` uses `[RuntimeInitializeOnLoadMethod]` for auto-creation (like `MovementRouter`)
2. If not, verify it's present in the scene or on a persistent GameObject
3. If timing is unreliable, add `[RuntimeInitializeOnLoadMethod]` auto-initialization

**Current state**: `ClickToMoveHandler` does NOT have `[RuntimeInitializeOnLoadMethod]`. It relies on being in a scene. If it's not in the starting scene, it won't exist when MOBA paradigm is selected.

**Fix if needed**: Add auto-initialization:
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void AutoInitialize()
{
    if (Instance != null) return;
    var go = new GameObject("[ClickToMoveHandler]");
    go.AddComponent<ClickToMoveHandler>();
    Debug.Log("[ClickToMoveHandler] Auto-initialized");
}
```

**Acceptance**: `ClickToMoveHandler.Instance` is non-null before any paradigm transition can occur. Switching to MOBA always calls `Configure()` on it.

---

### Task 18.15.5: Test All Paradigm Transitions

Verify no cross-contamination across all paradigm switches:

| From | To | Expected WASD | Expected Click-to-Move | Verify |
|------|----|---------------|------------------------|--------|
| Shooter | MOBA | Disabled | RMB → pathfind | WASD produces no movement; RMB click moves player along A* path |
| Shooter | ARPG | Disabled | LMB → pathfind | WASD produces no movement; LMB click moves player along A* path |
| MOBA | Shooter | Enabled | Disabled | WASD moves normally; clicking does nothing |
| ARPG | MMO | Enabled | Disabled | WASD moves normally; clicking does nothing |
| MOBA | ARPG | Disabled | LMB (was RMB) | Click button changes; WASD still disabled |
| ARPG | MOBA | Disabled | RMB (was LMB) | Click button changes; WASD still disabled |
| Shooter | TwinStick | Enabled | Disabled | WASD moves; mouse aims (facingMode changes) |

**Additional checks**:
- Path following crosses paradigm switch: start a path in MOBA, switch to Shooter mid-path → path should cancel (WASD re-enabled)
- WASD during path follow: in MOBA, press W while path is active → path should NOT cancel (Bug 4 fix)
- Hold-to-move: in MOBA, hold RMB → continuous repath at 0.2s interval → character follows cursor
- A* unavailable: click-to-move falls back to direct movement (no pathfinding graph)

**Acceptance**: All transitions behave correctly. No paradigm produces movement from a method that should be disabled.

---

### Task 18.15.6: Verify Cursor State for Click-to-Move Raycasts

**File**: `Assets/Scripts/Core/Input/Paradigm/Subsystems/CursorController.cs` (verify only)

Click-to-move requires a free cursor for accurate screen-to-world raycasts. MOBA/ARPG profiles have `cursorFreeByDefault: true`. Verify that `CursorController.Configure()`:

1. Sets `Cursor.lockState = CursorLockMode.None`
2. Sets `Cursor.visible = true`
3. Executes BEFORE `ClickToMoveHandler.Configure()` (via `ConfigurationOrder`)

**Expected order** (from `IParadigmConfigurable.ConfigurationOrder`):
- CursorController: order 50 (before movement)
- MovementRouter: order 100
- ClickToMoveHandler: order 110

This should be correct. Verify at runtime that cursor is actually free when clicking in MOBA mode.

**Acceptance**: Cursor is visible and unlocked in MOBA/ARPG. `Camera.ScreenPointToRay()` in `ClickToMoveHandler.TryRaycastAndMove()` uses the actual cursor position (not screen center).

---

## Files to Modify

| File | Change |
|------|--------|
| `Assets/Scripts/Player/Systems/PlayerInputSystem.cs` | Gate `Horizontal`/`Vertical` sampling with `MovementRouter.IsWASDEnabled` |
| `Assets/Scripts/Player/Systems/PlayerMovementSystem.cs` | Gate `effectiveHorizontal`/`effectiveVertical` with `ParadigmSettings.IsWASDEnabled` |
| `Assets/Scripts/Core/Input/Paradigm/Pathfinding/ClickToMoveHandler.cs` | Gate WASD interruption check + add `[RuntimeInitializeOnLoadMethod]` |

## Files Unchanged

| File | Reason |
|------|--------|
| `Assets/Scripts/Core/Input/Paradigm/Subsystems/MovementRouter.cs` | Already stores `IsWASDEnabled` correctly — no changes needed |
| `Assets/Scripts/Core/Input/Paradigm/Components/ParadigmSettings.cs` | Already has `IsWASDEnabled` field — no changes needed |
| `Assets/Scripts/Core/Input/Paradigm/ParadigmStateMachine.cs` | Transition chain works correctly — profiles are applied |
| `Assets/Scripts/Core/Input/Paradigm/ParadigmInputManager.cs` | Core action map intentionally stays enabled (other actions needed) |
| `Assets/Scripts/Core/Input/PlayerInputReader.cs` | Always writes to `PlayerInputState.Move` — gated downstream instead |
| `Assets/Data/Input/Profiles/*.asset` | Profile values are correct as-is |

---

## Verification

1. **MOBA mode**: Switch to MOBA via ParadigmDemoUI → WASD produces zero movement → RMB click on ground → A* path computed → character walks to destination → hold RMB → continuous follow → release → character stops at last target.

2. **ARPG mode**: Switch to ARPG → WASD produces zero movement → LMB click on ground → character walks to destination → LMB on enemy → character walks to enemy position (attack integration is future work).

3. **Shooter mode**: Switch to Shooter → WASD moves normally → clicking does nothing for movement → confirm no regression from WASD gating logic.

4. **MMO mode**: Switch to MMO → WASD moves normally → A/D turns character (not strafes) unless RMB held → confirm no regression.

5. **TwinStick mode**: Switch to TwinStick → WASD moves → mouse controls facing → confirm no regression.

6. **Round-trip**: Shooter → MOBA → Shooter → ARPG → Shooter → verify WASD works every time after returning from a click-to-move paradigm.

7. **Path interruption**: In Shooter mode, if click-to-move were somehow enabled, WASD still cancels paths. In MOBA mode, WASD does NOT cancel paths.

8. **No alive players / no A\* graph**: Click-to-move in MOBA falls back to direct movement when A* graph is not configured. Character still moves toward click point.

9. **Networked**: In a multiplayer session, remote clients see MOBA player moving only via click-to-move. No phantom WASD movement on the server from suppressed inputs.

---

## Architecture Decisions

### Why Gate at SampleInput Rather Than Disable the Move Action?

| Approach | Pros | Cons | Decision |
|----------|------|------|----------|
| Disable `Core/Move` action for MOBA/ARPG | Cleanest — no callbacks fire at all | Requires action-level enable/disable; other systems (camera) may read Move; risky to disable Core actions | **Rejected** |
| Gate in `PlayerInputReader.OnMove()` | Earliest possible gate | `PlayerInputReader` doesn't know about paradigm state; would need dependency on `MovementRouter` | **Rejected** |
| Gate in `PlayerInputSystem.SampleInput()` | Natural chokepoint; managed code can read `MovementRouter.Instance`; prevents WASD from entering networked pipeline | One frame of stale data possible if paradigm changes mid-frame | **Chosen** |
| Gate in `PlayerMovementSystem` only | Burst-compatible; reads ECS singleton | WASD still in `PlayerInput` component → server sees it → potential desync | Defense-in-depth only |
| Zero `PlayerInputState.Move` in `MovementRouter` | Would prevent all downstream issues | `MovementRouter` doesn't own `PlayerInputState`; breaks separation of concerns | **Rejected** |

### Why Defense-in-Depth in PlayerMovementSystem?

The server runs `PlayerMovementSystem` to validate client movement. If a client's `PlayerInputSystem` gates WASD but the server doesn't (e.g., paradigm sync delay), the server could interpret residual `Horizontal/Vertical` as movement and desync. The `ParadigmSettings` ECS singleton is synced by `ParadigmSettingsSyncSystem` and available on both client and server, making the `PlayerMovementSystem` gate authoritative.

### Future: Click-to-Move for Combat Targeting

This epic only covers movement. Click-to-move for combat (click enemy to attack, click ground to cast AoE) is a separate feature that builds on this foundation. The `ClickToMoveHandler` already raycasts and finds click targets — a future epic can extend `ProcessClickInput()` to detect enemy entities and feed attack commands instead of move commands.
