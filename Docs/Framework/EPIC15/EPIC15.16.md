# EPIC 15.16: Target Locking System (Switchable)

**Status:** ✅ IMPLEMENTED
**Dependencies:** `LockOnTargeting.cs`, `TargetingSystemBase.cs`, `CameraLockOnSystem.cs`
**Feature:** Configurable and Switchable Target Locking

## Overview
Target locking can now be **switched on/off** at runtime via `TargetLockSettingsManager`. Both the MonoBehaviour-based `LockOnTargeting` (Tab key) and ECS-based `CameraLockOnSystem` (Grab input) respect this setting.

## Architecture

### Settings Flow
```
┌──────────────────────────────────┐
│  TargetLockSettingsManager       │ ◄── Managed singleton, PlayerPrefs persistence
│  - AllowTargetLock               │
│  - AllowAimAssist                │
│  - ShowIndicator                 │
│  - OnSettingsChanged event       │
└──────────────┬───────────────────┘
               │
    ┌──────────┴──────────┐
    ▼                     ▼
┌────────────────┐  ┌────────────────────────┐
│ LockOnTargeting│  │ TargetLockSettingsSyncSystem │
│ (MonoBehaviour)│  │ (syncs to ECS singleton)     │
│ checks directly│  └──────────────┬───────────────┘
└────────────────┘                 ▼
                          ┌────────────────────┐
                          │ TargetLockSettings │
                          │ (ECS component)    │
                          └────────────┬───────┘
                                       ▼
                          ┌────────────────────┐
                          │ CameraLockOnSystem │
                          │ reads ECS settings │
                          └────────────────────┘
```

## Files Created/Modified

### New Files
| File | Purpose |
|------|---------|
| `Targeting/Settings/TargetLockSettingsManager.cs` | Managed singleton with events, PlayerPrefs |
| `Targeting/Components/TargetLockSettings.cs` | ECS singleton for Burst systems |
| `Targeting/Systems/TargetLockSettingsSyncSystem.cs` | Bridges managed → ECS |
| `Targeting/Debug/TargetLockTester.cs` | Inspector-editable debug component |

### Modified Files
| File | Change |
|------|--------|
| `LockOnTargeting.cs` | Checks `TargetLockSettingsManager.AllowTargetLock` in Update |
| `CameraLockOnSystem.cs` | Checks `TargetLockSettings.AllowTargetLock` ECS singleton |

## Usage

### For Designers (Debug Testing)
1. Add `TargetLockTester` component to any GameObject in scene
2. Toggle checkboxes in Inspector at runtime:
   - **Allow Target Lock** - enables/disables Tab/Grab lock-on
   - **Allow Aim Assist** - (for future use)
   - **Show Indicator** - (for future use with UI)
3. Use preset dropdown for quick configurations (Normal, Hardcore, etc.)

### For Programmers (UI Integration)
```csharp
// Get/Set directly
TargetLockSettingsManager.Instance.AllowTargetLock = false;

// Subscribe to changes
TargetLockSettingsManager.Instance.OnSettingsChanged += () => {
    UpdateSettingsUI();
};

// Apply preset
TargetLockSettingsManager.Instance.ApplyPreset(TargetLockPreset.Hardcore);

// Save to PlayerPrefs
TargetLockSettingsManager.Instance.Save();
```

### For ECS Systems
```csharp
if (SystemAPI.TryGetSingleton<TargetLockSettings>(out var settings))
{
    if (!settings.AllowTargetLock)
    {
        // Skip lock-on logic
    }
}
```

## Verification ✅
1. Start Game. Press Tab → Lock on works ✅
2. Toggle `AllowTargetLock = false` in TargetLockTester Inspector ✅
3. Press Tab → Nothing happens ✅
4. If already locked on → Lock breaks immediately ✅

---

## Task 15.16.13: Health Bar Visibility Integration ✅
**Status:** Complete
**Priority:** High (UI feedback)
**Files:**
- `Combat/Bridges/EnemyHealthBarBridgeSystem.cs` (modified)
- `Combat/UI/WorldSpace/EnemyHealthBarPool.cs` (modified)

**Description:**
Integrate target locking with the health bar visibility system so that `WhenTargeted` and `WhenTargetedOrDamaged` modes work correctly. The locked-on enemy's health bar should appear when using these visibility modes.

**Challenge Solved:**
Since enemies are not NetCode ghosts (they don't have `GhostInstance` component), we cannot use ghostId matching to find the corresponding server entity. Instead, the system uses **position-based matching** - comparing the `LastTargetPosition` from `CameraTargetLockState` against server entity positions.

**Implementation:**
1. `EnemyHealthBarBridgeSystem` reads `CameraTargetLockState` from local player on ClientWorld
2. Gets `LastTargetPosition` (which includes height offset) from lock state
3. Iterates server entities with `Health + ShowHealthBarTag + LocalTransform`
4. Finds closest server entity within 2m tolerance of target position
5. Passes matched server entity to `EnemyHealthBarPool.SetTargetedEntity()`
6. Pool's visibility evaluation checks `IsTargeted = entity == _targetedEntity`

**Acceptance Criteria:**
- [x] WhenTargeted mode shows health bar only for locked target
- [x] WhenTargetedOrDamaged shows bar for locked OR damaged enemies
- [x] Health bar appears immediately when Tab-locking
- [x] Health bar hides when unlocking (with fade if configured)
- [x] Works in listen server mode (separate client/server worlds)

---

## Future Extensions
- **Aim Assist:** Implement soft-targeting using `AllowAimAssist` flag
- **UI Indicator:** Use `ShowIndicator` to toggle reticle visibility
- **Per-Zone Settings:** Create override components for specific areas

---

# EPIC 15.16 Extended Tasks

> **Implementation Status:**  
> ✅ **Hard Lock** - Dark Souls style, fully working  
> ✅ **Soft Lock** - Partial bias (0.3 strength), fully working  
> ✅ **Over-the-Shoulder** - Shoulder offset + ADS zoom, fully working  
> 🔒 **Isometric Lock** - DEFERRED (requires top-down camera mode)  
> 🔒 **Twin-Stick** - DEFERRED (requires isometric camera + aim visualization)  
> 🔒 **First Person** - DEFERRED (requires FPS camera mode)

---

## Camera Mode Tasks

### Task 15.16.1: Over-the-Shoulder Mode
**Status:** ✅ IMPLEMENTED
**Priority:** Medium
**Files:** 
- `Targeting/Systems/OverTheShoulderSystem.cs`
- `Targeting/Core/TargetingState.cs` (OverTheShoulderState component)
- `Targeting/Authoring/TargetingModuleAuthoring.cs` (bakes component)

Camera offset left/right of character. Lock can swap shoulder for visibility. ADS brings camera closer.

**Implementation:**
- `OverTheShoulderSystem` runs after `CameraLockOnSystem` in SimulationSystemGroup
- Reads ADS from `PlayerInput.AltUse` (right-click / left trigger)
- Applies shoulder offset to `CameraViewConfig.CombatCameraOffset.x`
- Applies zoom offset to `CameraViewConfig.CombatCameraOffset.z`
- Auto-swaps shoulder when target moves to occluded side

**Components:**
- `OverTheShoulderState` - CurrentShoulderSide, DesiredShoulderSide, IsAiming, CurrentZoom, DesiredZoom

**Acceptance Criteria:**
- [x] Camera offset based on `CurrentShoulderSide` (-1 left, 1 right)
- [x] Shoulder swap via auto-detection (when target crosses center)
- [x] Smooth interpolation when swapping (lerp @ deltaTime * 5)
- [x] Tighter zoom when `IsAiming = true` (0.6 = zoomed in)
- [x] Auto-swap when target occluded by shoulder side

---

### Task 15.16.2: Twin-Stick Mode
**Status:** 🔒 DEFERRED (requires isometric camera)
**Priority:** Low
**File:** `Targeting/Systems/TwinStickAimSystem.cs`

Move with left stick, aim with right stick. Lock = sticky aim (aim slows near targets).

**What's Missing:**
- Currently uses Hard Lock camera tracking
- NO independent aim direction
- NO aim visualization
- NO sticky aim near targets

**Components Needed:**
- `TwinStickState` - Aim direction (float2), Is aiming flag
- Uses `AimAssistState` for sticky aim

**Acceptance Criteria:**
- [ ] Right stick controls aim direction independently of movement
- [ ] Aim direction visualized (line/laser/reticle)
- [ ] Sticky aim slows when aim passes over valid targets
- [ ] Auto-target nearest enemy in aim direction

---

### Task 15.16.3: First Person Aim Assist
**Status:** 🔒 DEFERRED (requires FPS camera mode)
**Priority:** Medium
**File:** `Targeting/Systems/FirstPersonAimAssistSystem.cs`

Camera IS the view. Lock = aim magnetism only (no character rotation concept).

**What's Missing:**
- Currently uses Hard Lock camera tracking (forces view to target)
- NO aim magnetism (subtle pull toward target)
- NO sticky aim (slowdown near target)
- NO distinction between mouse vs controller input

**Components Needed:**
- `AimAssistState` - Magnetism pull, sticky target

**Acceptance Criteria:**
- [ ] NO forced camera tracking (player retains full aim control)
- [ ] Aim slows when crosshair near target (Sticky Aim)
- [ ] Crosshair subtly pulled toward target center (Magnetism)
- [ ] Configurable strength (for accessibility options)
- [ ] Only active on controller input (not mouse)

---

### Task 15.16.4: Soft Lock Mode (God of War style)
**Status:** ✅ IMPLEMENTED
**Priority:** HIGH
**Files Modified:**
- `Player/Systems/CameraLockOnSystem.cs` - Break lock on mouse movement, per-entity cooldown
- `Camera/Cinemachine/CinemachineCameraController.cs` - Smooth transition on lock break, JustUnlocked detection
- `Player/Systems/PlayerMovementSystem.cs` - Reads static mode for synchronized behavior
- `Player/Components/CameraTargetLockState.cs` - Added SoftLockBreakCooldown, JustUnlocked fields
- `Targeting/Debug/TargetingModeTester.cs` - Static fields for cross-world mode sync

Camera tracks target (like Hard Lock) until player moves mouse - then lock breaks immediately.

**Implementation Details:**
1. `CameraLockOnSystem` detects mouse movement (`LookDelta`) in Soft Lock mode
2. Mouse movement above threshold (1.0) immediately breaks the lock
3. Per-entity `SoftLockBreakCooldown` prevents flicker from re-locking too quickly
4. `JustUnlocked` flag signals to camera controller for smooth transition
5. `CinemachineCameraController` applies 0.2s smoothstep blend on unlock
6. Static fields in `TargetingModeTester` bypass ECS world sync issues

**Flicker Prevention (EPIC 15.16 improvement):**
- Per-entity cooldown (0.5s) after soft lock break
- JustUnlocked flag for cross-system coordination
- Smooth camera transition instead of instant snap
- Consistent rotation format (yaw/pitch) on follow target
- Mouse threshold increased to 1.0 to filter micro-jitter

**Behavior Comparison:**
| Feature | Hard Lock | Soft Lock |
|---------|-----------|-----------|
| Camera tracks target | ✅ Always | ✅ Until mouse moves |
| Circle-strafe movement | ✅ Always | ✅ Until mouse moves |
| Character faces target | ✅ Always | ✅ Until mouse moves |
| Mouse breaks lock | ❌ No | ✅ Immediately |
| Tab breaks lock | ✅ Toggle | ✅ Toggle |
| Re-lock cooldown | None | 0.5s after break |

**Acceptance Criteria:**
- [x] Camera tracks target while locked (like Hard Lock)
- [x] Mouse movement immediately breaks the lock
- [x] Hard Lock is unaffected by mouse movement
- [x] Lock indicator shows target while locked
- [x] No screen flicker when breaking lock
- [x] Smooth camera transition on unlock

---

### Task 15.16.5: Isometric Lock Mode (Diablo style)
**Status:** 🔒 DEFERRED (requires isometric camera mode)
**Priority:** Medium
**File:** `Targeting/Systems/IsometricLockSystem.cs`

Camera is FIXED (top-down/isometric). Character faces target direction.

**What's Missing:**
- Currently uses Hard Lock camera tracking (forces camera to target)
- NO fixed overhead camera
- NO click-to-target or cursor-based targeting
- NO character facing toward target

**Components Needed:**
- `IsometricLockState` - Target entity, facing direction

**Acceptance Criteria:**
- [ ] Camera is FIXED overhead (no rotation)
- [ ] Character FACES target direction
- [ ] Click-to-target or auto-nearest-to-cursor targeting
- [ ] Lock indicator at target position

---

## Lock Variation Tasks

### Task 15.16.6: Multi-Lock System
**Status:** 🔮 FUTURE (not yet needed)
**Priority:** Low (specialty feature)
**File:** `Targeting/Systems/MultiLockSystem.cs`

Lock multiple targets simultaneously for missile salvos or chain attacks.

**Components:**
- `MultiLockState` - Locked count, accumulating flag, max targets
- `LockedTargetElement` - Buffer of locked targets

**Acceptance Criteria:**
- [ ] Hold lock button to accumulate targets
- [ ] Visual indicator on each locked target (numbered)
- [ ] Release to "fire" (dispatch event with all targets)
- [ ] Maximum target cap (configurable, default 6)
- [ ] Targets ordered by lock acquisition time

---

### Task 15.16.7: Part Targeting System
**Status:** 🔮 FUTURE (not yet needed)
**Priority:** Medium (boss fights)
**File:** `Targeting/Systems/PartTargetingSystem.cs`

Target specific body parts on enemies. Different damage multipliers per part.

**Components:**
- `TargetablePartElement` - Buffer on enemies (PartId, LocalOffset, DamageMultiplier, IsExposed)
- `PartTargetingState` - Current part index, part offset

**Acceptance Criteria:**
- [ ] Enemies can define multiple targetable parts
- [ ] UI shows available parts when locked
- [ ] Cycle between parts with input (Q/E or stick)
- [ ] Damage multiplier applied on hit
- [ ] Parts can be exposed/hidden based on enemy state

---

### Task 15.16.8: Predictive Aim System
**Status:** 🔮 FUTURE (not yet needed)
**Priority:** Low (ranged/projectile focus)
**File:** `Targeting/Systems/PredictiveAimSystem.cs`

Show lead indicator for moving targets. Calculate intercept point based on projectile speed.

**Components:**
- `PredictiveAimState` - Target velocity, predicted aim point, time to intercept

**Acceptance Criteria:**
- [ ] Track target velocity over time
- [ ] Calculate intercept point for current weapon projectile speed
- [ ] Display lead indicator at predicted position
- [ ] Indicator shows validity (in range, reachable)
- [ ] Works with both locked and soft-locked targets

---

### Task 15.16.9: Priority Auto-Switch
**Status:** ✅ IMPLEMENTED
**Priority:** High (quality of life)
**File:** `Targeting/Systems/PriorityAutoSwitchSystem.cs`

Automatically switch to next valid target when current target dies or goes out of range.

**Components:**
- Uses existing `TargetingState` + `LockOnTarget.Priority`

**Acceptance Criteria:**
- [x] Detect when current target becomes invalid (dead, out of range, destroyed)
- [x] Find next best target within range
- [x] Respect priority (boss > elite > normal)
- [ ] Smooth transition to new target (no jarring camera snap)
- [x] Configurable: on/off in settings

---

### Task 15.16.10: Sticky Aim System
**Status:** ✅ IMPLEMENTED
**Priority:** High (controller support)
**File:** `Targeting/Systems/StickyAimSystem.cs`

Aim movement slows when crosshair is near valid targets. Essential for controller aiming.

**Components:**
- `AimAssistState` - StickyTarget, CurrentStickyStrength, InStickyZone

**Acceptance Criteria:**
- [x] Define sticky radius around each target (screen space)
- [x] When aim enters sticky zone, reduce aim speed
- [x] Configurable strength (0 = none, 1 = full stop)
- [ ] Only active on controller input
- [x] Works in both 3rd person and 1st person

---

### Task 15.16.11: Snap Aim System
**Status:** ✅ IMPLEMENTED
**Priority:** Medium (accessibility)
**File:** `Targeting/Systems/SnapAimSystem.cs`

Quick snap to nearest target when ADS or lock button pressed.

**Components:**
- Uses `AimAssistState` + `TargetSelectionConfig.SnapAimAngle`

**Acceptance Criteria:**
- [x] On ADS press, find nearest target within snap angle
- [x] Instantly rotate aim toward target
- [x] Configurable max snap angle (default 30°)
- [x] Optional: only snap to targets in front of player
- [ ] Cooldown to prevent snap abuse

---

## Core System Tasks

### Task 15.16.12: Lock Input Mode System
**Status:** ✅ IMPLEMENTED
**Priority:** High (foundation)
**File:** `Targeting/Systems/LockInputModeSystem.cs`

Support different input modes: Toggle, Hold, ClickTarget, AutoNearest, HoverTarget.

**Components:**
- `LockInputMode` enum (already created)
- `LockInputHandler` enum (new) - selects which system handles input
- Modify `CameraLockOnSystem` to check `ActiveLockBehavior.InputMode`

**Acceptance Criteria:**
- [x] Toggle mode: Press to lock, press again to unlock
- [x] Hold mode: Hold to lock, release to unlock
- [ ] ClickTarget mode: Click on enemy to lock (for isometric)
- [x] AutoNearest mode: Always targets nearest (no input needed)
- [ ] HoverTarget mode: Target under cursor (FPS style)
- [x] Mode stored in `ActiveLockBehavior.InputMode`
- [x] Input handler preference: choose between CameraLockOnSystem (default) or LockInputModeSystem

---

### Task 15.16.12b: Lock Input Handler Selection
**Status:** ✅ IMPLEMENTED
**Priority:** Medium (user preference)
**Files:**
- `Targeting/Core/LockBehaviorType.cs` - Added `LockInputHandler` enum
- `Player/Systems/CameraLockOnSystem.cs` - Checks handler preference
- `Targeting/Systems/LockInputModeSystem.cs` - Checks handler preference

Two systems can process lock-on input. This task adds user control over which one is active.

**Input Handler Options:**
| Handler | World | Best For |
|---------|-------|----------|
| **CameraLockOnSystem** (default) | ClientWorld only | Soft Lock break detection, camera integration |
| **LockInputModeSystem** | Client + Server | Server-authoritative, simpler logic |

**Implementation:**
1. Added `LockInputHandler` enum with `CameraLockOnSystem` and `LockInputModeSystem` values
2. Added `InputHandler` field to `ActiveLockBehavior` component
3. Added `InputHandler` field to `TargetingModeTester` inspector
4. `CameraLockOnSystem` skips input handling if `InputHandler != CameraLockOnSystem`
5. `LockInputModeSystem` skips entirely if `InputHandler == CameraLockOnSystem`

**Acceptance Criteria:**
- [x] User can select input handler in TargetingModeTester
- [x] Default is CameraLockOnSystem (best Soft Lock support)
- [x] Only one system processes input at a time
- [x] Both systems preserved for different use cases

---

### Task 15.16.13: Lock Behavior Dispatcher
**Status:** ✅ IMPLEMENTED
**Priority:** HIGH (foundation)
**File:** `Player/Systems/CameraLockOnSystem.cs`

Routes targeting updates to the appropriate lock behavior based on `ActiveLockBehavior.BehaviorType`.

**Implementation:**
- `CameraLockOnSystem` switches `rotationStrength` based on mode
- Per-mode systems (`OverTheShoulderSystem`, etc.) run after and apply mode-specific logic

**Components:**
- `ActiveLockBehavior` singleton - stores current BehaviorType
- Mode-specific systems check `ActiveLockBehavior` before running

**Acceptance Criteria:**
- [x] Read `ActiveLockBehavior.BehaviorType`
- [x] HardLock → rotationStrength 1.0 (full camera control)
- [x] SoftLock → rotationStrength 0.3 (partial bias)
- [x] OverTheShoulder → rotationStrength 0.5 + OverTheShoulderSystem handles offset/zoom
- [ ] IsometricLock → DEFERRED (needs camera mode)
- [ ] TwinStick → DEFERRED (needs isometric camera)
- [ ] FirstPerson → DEFERRED (needs FPS camera)
- [ ] Handle mode transitions smoothly

---

### Task 15.16.14: Lock Feature Flags Integration
**Status:** ✅ IMPLEMENTED
**Priority:** Medium
**File:** `Targeting/Systems/LockFeatureFlagsSystem.cs`

Make `LockFeatureFlags` actually control feature activation.

**Components:**
- `LockFeatureFlags` (already created)
- Modify lock systems to check flags

**Acceptance Criteria:**
- [ ] MultiLock flag enables/disables multi-target locking
- [ ] PartTargeting flag enables/disables body part selection
- [ ] PredictiveAim flag enables/disables lead indicator
- [x] PriorityAutoSwitch flag enables/disables auto-retargeting
- [x] StickyAim flag enables/disables aim slowdown
- [x] SnapAim flag enables/disables snap-to-target

---

## Implementation Order

### ✅ Phase 0: COMPLETE (Hard Lock Foundation)
- Hard Lock (Dark Souls style) - **FULLY WORKING**
- Lock Input Modes (Toggle, Hold, AutoNearest) - **WORKING**
- Priority Auto-Switch - **WORKING**
- Sticky Aim System - **WORKING**
- Snap Aim System - **WORKING**
- Input Handler Selection - **WORKING** (CameraLockOnSystem or LockInputModeSystem)

### ✅ Phase 1: Soft Lock (COMPLETE)
- **Task 15.16.4: Soft Lock** (God of War) - Camera tracks until mouse moves, then breaks
- **Task 15.16.12b: Input Handler Selection** - User preference for which system handles input

### 🟡 Phase 2: Camera Mode Implementations
2. **Task 15.16.1: Over-the-Shoulder** (RE4) - Offset camera + shoulder swap
3. **Task 15.16.3: First Person Aim Assist** - Aim magnetism only
4. **Task 15.16.5: Isometric Lock** (Diablo) - Fixed camera + character facing
5. **Task 15.16.2: Twin-Stick** - Independent aim direction

### 🔮 Phase 3: Future Features
- Task 15.16.6: Multi-Lock System
- Task 15.16.7: Part Targeting System
- Task 15.16.8: Predictive Aim System

---

## Summary: What's Working vs What Needs Work

| Mode | Status | What Works | What's Missing |
|------|--------|------------|----------------|
| **Hard Lock** | ✅ DONE | Camera tracks target, strafe movement, persists through mouse input | - |
| **Soft Lock** | ✅ DONE | Camera tracks target, strafe movement, mouse breaks lock, smooth transition, no flicker | - |
| **Input Handler** | ✅ DONE | User selects CameraLockOnSystem (default) or LockInputModeSystem | - |
| **Isometric** | 🔒 DEFERRED | Menu option exists | Isometric camera, click-to-target input, world-relative movement |
| **Over Shoulder** | ✅ DONE | Shoulder offset, ADS zoom, auto-swap when target crosses center | - |
| **Twin Stick** | 🔒 DEFERRED | Menu option exists | Right stick input, independent aim, aim visualization |
| **First Person** | 🔒 DEFERRED | Menu option exists | FPS camera mode, aim magnetism |

> **Deferred** = Requires foundational systems (camera modes, input mappings) not yet implemented.

---

# Lock Mode Architecture (Phase 2 Planning)

This section documents the unified architecture for all lock modes, ensuring reusable systems and consistent behavior.

## Core Reusable Components

### 1. Lock Phase State Machine (Shared by ALL modes)

```
LockPhase: Unlocked → Locking → Locked → Unlocked
```

| Phase | Description |
|-------|-------------|
| **Unlocked** | Free camera, no target, normal movement |
| **Locking** | Target acquired, camera/body en route to target, break detection DISABLED |
| **Locked** | Arrival condition met, break detection ENABLED (varies by mode) |

**Transitions:**
- `Unlocked → Locking`: Target acquired (Tab press, Hold start, etc.)
- `Locking → Locked`: Arrival condition met (camera/body within 5° threshold)
- `Locked → Unlocked`: Break condition met (varies by mode)

**Implementation:** `CameraLockOnSystem` (SimulationSystemGroup) queries `CinemachineCameraController.HasCameraArrivedAtTarget` to detect arrival. Single writer for Phase state ensures consistency.

---

### 2. Target Acquisition System (Shared by ALL modes)

Finds valid targets based on range, angle, and priority.

**Responsibilities:**
- Find target in range/angle from crosshair
- Priority-based selection (Boss > Elite > Normal)
- Store: `TargetEntity`, `LastTargetPosition`

**Used by:** ALL modes

---

### 3. Camera Lock System

Rotates camera to face target.

| Mode | Uses Camera Lock? | Arrival Condition |
|------|-------------------|-------------------|
| Hard Lock | ✅ Yes | Camera yaw/pitch within 5° of target |
| Soft Lock | ✅ Yes | Camera yaw/pitch within 5° of target |
| Over-the-Shoulder | ✅ Yes | Camera yaw/pitch within 5° of target |
| First Person | ✅ Yes | Camera yaw/pitch within 5° of target |
| Isometric | ❌ No | N/A (camera is fixed) |
| Twin Stick | ❌ No | N/A (camera is fixed/loose follow) |

---

### 4. Body Facing System

Rotates character body to face target.

| Mode | Uses Body Facing? | Details |
|------|-------------------|---------|
| Hard Lock | ✅ Yes | Body snaps to face target |
| Soft Lock | ✅ Yes | Body snaps to face target |
| Over-the-Shoulder | ✅ Yes | Body faces target |
| First Person | ❌ No | Camera IS the view, no separate body rotation |
| Isometric | ✅ Yes | Body faces target, camera stays fixed overhead |
| Twin Stick | ✅ Yes | Body faces right stick aim direction |

---

### 5. Head Look System (IK)

Head turns toward target (cosmetic, uses Animation Rigging).

| Mode | Uses Head Look? | Details |
|------|-----------------|---------|
| Hard Lock | ⚪ Optional | Not needed, body already facing |
| Soft Lock | ⚪ Optional | Not needed, body already facing |
| Over-the-Shoulder | ✅ Yes | Head glances at target while traversing |
| First Person | ❌ No | No visible body |
| Isometric | ✅ Yes | Head looks at target while body rotates |
| Twin Stick | ⚪ Optional | Head could track nearest enemy |

---

### 6. Movement Relativity System

Determines what direction is "forward" for WASD input.

| Mode | Movement Relative To | Details |
|------|---------------------|---------|
| Hard Lock | Target direction | Circle-strafe around target |
| Soft Lock | Target direction | Circle-strafe while locked |
| Over-the-Shoulder | Camera direction | Standard TPS movement |
| First Person | Camera direction | Standard FPS movement |
| Isometric | **World/Camera (fixed)** | Up = North, Right = East, regardless of body facing |
| Twin Stick | Left stick = move direction | Decoupled from aim |

---

### 7. Lock Break Conditions

| Mode | What Breaks Lock? |
|------|-------------------|
| Hard Lock | Tab toggle only |
| Soft Lock | Mouse movement OR Tab toggle |
| Over-the-Shoulder | Tab toggle (or ADS release if Hold mode) |
| First Person | Tab toggle (or aim magnetism naturally decays) |
| Isometric | Tab toggle or click elsewhere |
| Twin Stick | Right stick overrides lock, Tab clears |

---

## System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                    TARGET ACQUISITION SYSTEM                         │
│         (Shared) Find target, priority, range, angle                │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    LOCK PHASE STATE MACHINE                          │
│         (Shared) Unlocked → Locking → Locked → Unlocked             │
│         Camera/Body signals arrival for Locking → Locked            │
└─────────────────────────────────────────────────────────────────────┘
                                   │
          ┌────────────────────────┼────────────────────────┐
          ▼                        ▼                        ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│  CAMERA LOCK     │    │  BODY FACING     │    │  HEAD LOOK (IK)  │
│  SYSTEM          │    │  SYSTEM          │    │  SYSTEM          │
│                  │    │                  │    │                  │
│ Hard Lock    ✅  │    │ Hard Lock    ✅  │    │ Hard Lock    ⚪  │
│ Soft Lock    ✅  │    │ Soft Lock    ✅  │    │ Soft Lock    ⚪  │
│ OTS          ✅  │    │ OTS          ✅  │    │ OTS          ✅  │
│ First Person ✅  │    │ First Person ❌  │    │ First Person ❌  │
│ Isometric    ❌  │    │ Isometric    ✅  │    │ Isometric    ✅  │
│ Twin Stick   ❌  │    │ Twin Stick   ✅  │    │ Twin Stick   ⚪  │
└──────────────────┘    └──────────────────┘    └──────────────────┘
          │                        │
          └────────────────────────┼────────────────────────┘
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    MOVEMENT RELATIVITY SYSTEM                        │
│         (Shared) Determines WASD meaning based on mode               │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    LOCK BREAK SYSTEM                                 │
│         (Per-mode logic) Mouse breaks Soft Lock, Tab breaks all     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Mode-by-Mode Specifications

### Hard Lock (Dark Souls, Elden Ring)

| Component | Enabled | Details |
|-----------|---------|---------|
| Camera Lock | ✅ | Camera orbits behind player, faces target |
| Body Facing | ✅ | Character faces target |
| Head Look IK | ⚪ | Optional (body already facing) |
| Movement | Target-relative | Circle-strafe around target |
| Break Condition | Tab only | Mouse input ignored |
| Arrival | Camera within 5° | Signals `Locking → Locked` |

---

### Soft Lock (God of War 2018)

| Component | Enabled | Details |
|-----------|---------|---------|
| Camera Lock | ✅ | Same as Hard Lock while locked |
| Body Facing | ✅ | Same as Hard Lock while locked |
| Head Look IK | ⚪ | Optional |
| Movement | Target-relative | Circle-strafe while locked |
| Break Condition | **Mouse movement** OR Tab | Any intentional mouse input breaks lock |
| Arrival | Camera within 5° | Break detection only after arrival |

---

### Isometric Lock (Diablo, Hades)

| Component | Enabled | Details |
|-----------|---------|---------|
| Camera Lock | ❌ | Camera is FIXED (top-down/isometric) |
| Body Facing | ✅ | Body rotates to face target |
| Head Look IK | ✅ | Head IK looks at target |
| Movement | **WORLD-relative** | WASD = cardinal directions (Up=North), NOT body-relative |
| Break Condition | Tab or click elsewhere | |
| Arrival | Body within 5° of target | Or instant (no camera wait) |

---

### Over-the-Shoulder (RE4, Gears of War, TLOU)

| Component | Enabled | Details |
|-----------|---------|---------|
| Camera Lock | ✅ | Camera aims at target with shoulder offset |
| Body Facing | ✅ | Body faces target (or camera direction in ADS) |
| Head Look IK | ✅ | Head glances at target during movement |
| Movement | Camera-relative | Standard TPS movement |
| Break Condition | Tab or ADS release | |
| Arrival | Camera within 3° | |
| Special | Shoulder swap | Can swap camera side for visibility |

---

### First Person (Halo, Destiny, CoD)

| Component | Enabled | Details |
|-----------|---------|---------|
| Camera Lock | ✅ | View pulls toward target (aim magnetism) |
| Body Facing | ❌ | No visible body |
| Head Look IK | ❌ | No visible head |
| Movement | Camera-relative | Standard FPS movement |
| Break Condition | Tab or natural decay | Magnetism fades over time |
| Arrival | Camera within 3° | |
| Special | Aim magnetism | Subtle pull, not forced snap |

---

### Twin Stick (Helldivers, Enter the Gungeon)

| Component | Enabled | Details |
|-----------|---------|---------|
| Camera Lock | ❌ | Camera is fixed or loose follow |
| Body Facing | ✅ | Body faces right stick (aim) direction |
| Head Look IK | ⚪ | Optional |
| Movement | Left stick = world-relative | Decoupled from aim direction |
| Break Condition | Right stick overrides | Any right stick input takes priority |
| Arrival | Instant | No camera to wait for |
| Special | Sticky aim | Aim slows near targets |

---

## Systems Implementation Plan

| System | Status | Shared? | Notes |
|--------|--------|---------|-------|
| `TargetAcquisitionSystem` | Existing | ✅ | Refactor out of CameraLockOnSystem |
| `LockPhaseSystem` | **NEW** | ✅ | State machine with camera callback |
| `CameraLockSystem` | Existing | ✅ | Refactor out of CameraLockOnSystem |
| `BodyFacingSystem` | Existing | ✅ | Refactor out of PlayerMovementSystem |
| `HeadLookIKSystem` | **NEW** | ✅ | Animation Rigging integration |
| `MovementRelativitySystem` | Existing | ✅ | Refactor out of PlayerMovementSystem |
| `LockBreakSystem` | **NEW** | Per-mode | Or integrate into LockPhaseSystem |

---

## Open Questions

1. **Isometric camera:** Is there an existing fixed isometric camera setup?
2. **Twin Stick input:** Is right stick aim input already mapped?
3. **Head Look IK:** Is Animation Rigging set up for head IK?
4. **Implementation priority:** Recommended order:
   - Fix Hard Lock + Soft Lock (current issues)
   - Over-the-Shoulder
   - Isometric
   - Twin Stick
   - First Person
