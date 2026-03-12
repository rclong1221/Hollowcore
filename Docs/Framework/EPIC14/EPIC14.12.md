# EPIC14.12 - Opsive Agility Pack Animation Integration

**Status:** All Phases Complete (Manual Setup Remaining)
**Dependencies:** EPIC14.9 (Camera System), ClimbingDemo.controller (copied from AgilityDemo)
**Goal:** Integrate Opsive Agility Pack animations into DIG's hybrid ECS/MonoBehaviour animation system.

---

## Overview

The Opsive Agility Pack provides advanced movement animations (dodge, roll, vault, balance, ledge strafe, hang, crawl). DIG already uses Opsive's animation parameter system (`AbilityIndex`, `AbilityIntData`, `AbilityFloatData`) but drives it from our own ECS systems rather than Opsive's character controller.

**Source Controller:** `Assets/Art/Animations/Opsive/AddOns/Agility/AgilityDemo.controller`
**Target Controller:** `Assets/Art/Animations/Opsive/AddOns/Climbing/ClimbingDemo.controller`

---

## Current Architecture

DIG's animation system is an **ECS extension of Opsive's animation parameters**:

```
┌─────────────────────────────────────────────────────────────────┐
│ ECS (Authoritative State)                                       │
├─────────────────────────────────────────────────────────────────┤
│ FreeClimbState, SwimmingState, PlayerState, PlayerInput         │
│                           │                                     │
│                           ▼                                     │
│ PlayerAnimationStateSystem (Burst, runs on Server + Client)     │
│   - Reads ECS state components                                  │
│   - Writes PlayerAnimationState (AbilityIndex, IntData, etc.)   │
│   - Priority: Jump(1) > Fall(2) > Crouch(3) > Climb(503/104)    │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼ (GhostField replication)
┌─────────────────────────────────────────────────────────────────┐
│ MonoBehaviour (Visual Layer - Client Only)                      │
├─────────────────────────────────────────────────────────────────┤
│ ClimbAnimatorBridge.ApplyAnimationState()                       │
│   - Reads PlayerAnimationState                                  │
│   - Writes to Unity Animator (AbilityIndex, AbilityChange, etc.)│
│   - Handles animation events (OnAnimatorXxxComplete)            │
│   - Opsive's AnimatorMonitor is DISABLED                        │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│ ClimbingDemo.controller (Opsive Animator Controller)            │
│   - Base Layer transitions on AbilityIndex + AbilityChange      │
│   - AbilityIntData selects sub-state/direction                  │
│   - AbilityFloatData for blend trees                            │
└─────────────────────────────────────────────────────────────────┘
```

**Key Points:**
- Opsive's `AnimatorMonitor` is disabled in `ClimbAnimatorBridge.Awake()` (line 224-237)
- Opsive's `ChildAnimatorMonitor` is also disabled
- Only our ECS systems control `AbilityIndex`
- Animation events call back to bridges to signal completion

**AnimatorMonitor Disable Code (ClimbAnimatorBridge.cs:220-237):**
```csharp
void DisableOpsiveAnimatorMonitor()
{
    var opsiveMonitor = animator.GetComponent<Opsive.UltimateCharacterController.Character.AnimatorMonitor>();
    if (opsiveMonitor != null)
    {
        opsiveMonitor.enabled = false;
        Debug.Log($"[ClimbAnimatorBridge] DISABLED Opsive AnimatorMonitor");
    }

    var childMonitor = animator.GetComponent<Opsive.UltimateCharacterController.Character.ChildAnimatorMonitor>();
    if (childMonitor != null)
    {
        childMonitor.enabled = false;
        Debug.Log($"[ClimbAnimatorBridge] DISABLED Opsive ChildAnimatorMonitor");
    }
}
```

---

## Relationship to Other EPICs

| EPIC | Connection |
|------|------------|
| **14.9** | Camera system affects input transformation for directional abilities |
| **13.26** | ClimbAnimatorBridge already handles AbilityIndex for climbing |
| **1.9** | Climbing system integration (FreeClimb = 503, Hang = 104) |

---

## Agility Abilities Summary

| Ability | AbilityIndex | Description | Priority |
|---------|--------------|-------------|----------|
| **Dodge** | 101 | Quick directional dodge (L/R/F/B) while aiming | High |
| **Roll** | 102 | Rolling evasion with directional variants | High |
| **Crawl** | 103 | Low crawling movement (trigger-based) | Medium |
| **Hang** | 104 | Hanging from ledges, shimmying | Already Integrated |
| **Vault** | 105 | Climbing over obstacles | High |
| **LedgeStrafe** | 106 | Narrow ledge movement | Medium |
| **Balance** | 107 | Beam/narrow platform walking | Low |

---

## AbilityIntData Values Per Ability

### Dodge (101)
| IntData | Direction |
|---------|-----------|
| 0 | Left |
| 1 | Right |
| 2 | Forward |
| 3 | Backward |

### Roll (102)
| IntData | Type |
|---------|------|
| 0 | Left |
| 1 | Right |
| 2 | Forward |
| 3 | Land (from fall) |

### Crawl (103)
| IntData | State |
|---------|-------|
| 0 | Active (crawling) |
| 1 | Stopping (getting up) |

### Vault (105)
- `AbilityFloatData` = starting velocity (for animation speed)
- `AbilityIntData` = height * 1000 (for vault height adjustment)

### LedgeStrafe (106)
- Movement controlled by `HorizontalMovement` parameter
- Uses root motion for positioning

### Balance (107)
- Movement controlled by `ForwardMovement` parameter
- Uses root motion for positioning

---

## Animation State Machine Structure

The AgilityDemo.controller uses Opsive's standard layer structure:

### Base Layer
Contains ability states triggered by `AbilityIndex`:
- `Dodge/*` - Directional dodge states
- `Roll/*` - Directional roll states
- `Crawl/*` - Crawl start/loop/stop
- `Vault/*` - Vault over obstacle
- `LedgeStrafe/*` - Narrow ledge movement
- `Balance/*` - Beam walking

### Transition Conditions
All ability states use:
- `AbilityIndex == [ability_id]` to enter
- `AbilityChange` trigger to initiate transition
- `AbilityIntData` for sub-state selection

---

## Conflicts and Disable Requirements

### Known Conflicts

| Conflict | Description | Resolution |
|----------|-------------|------------|
| **Run Overwrite** | Agility controller has locomotion states that override existing run animations | Disable/remove redundant locomotion states from copied controller |
| **AnimatorMonitor** | Opsive's AnimatorMonitor writes AbilityIndex directly | Already disabled in ClimbAnimatorBridge.Awake() |
| **Duplicate Ability Indices** | Some abilities share indices with other packs | Use unique indices per pack (101-107 for Agility) |

### States to Disable/Remove

When copying AgilityDemo.controller to ClimbingDemo.controller, these states may conflict:

1. **Base Layer > Movement** - May override existing locomotion
2. **Base Layer > Idle** - May conflict with weapon idle states
3. **Any SpeedChange states** - Handled by existing sprint system

### Required Disables

```
Disable these in the copied controller if they cause issues:
- Movement blend tree (if duplicating locomotion)
- Any states with AbilityIndex < 100 (core abilities already handled)
```

---

## Integration Architecture

Following the existing pattern (FreeClimb, Swimming), each agility ability needs:

### Per-Ability Components

```
┌─────────────────────────────────────────────────────────────────┐
│ New ECS Components (per ability)                                │
├─────────────────────────────────────────────────────────────────┤
│ DodgeState : IComponentData                                     │
│   - bool IsDodging                                              │
│   - int Direction (0=Left, 1=Right, 2=Forward, 3=Back)          │
│   - float TimeRemaining                                         │
│                                                                 │
│ RollState : IComponentData                                      │
│   - bool IsRolling                                              │
│   - int RollType (0=Left, 1=Right, 2=Forward, 3=Land)           │
│   - float TimeRemaining                                         │
│                                                                 │
│ VaultState : IComponentData                                     │
│   - bool IsVaulting                                             │
│   - float StartVelocity                                         │
│   - float VaultHeight                                           │
└─────────────────────────────────────────────────────────────────┘
```

### Per-Ability Systems

```
┌─────────────────────────────────────────────────────────────────┐
│ New ECS Systems (per ability)                                   │
├─────────────────────────────────────────────────────────────────┤
│ DodgeAbilitySystem                                              │
│   - Detects: Aim + Direction + Action button                    │
│   - Checks: IsGrounded, not already dodging, cooldown           │
│   - Sets: DodgeState.IsDodging = true, Direction                │
│                                                                 │
│ RollAbilitySystem                                               │
│   - Detects: Direction + Action button (or auto on land)        │
│   - Checks: IsGrounded, space ahead, slope limit                │
│   - Sets: RollState.IsRolling = true, RollType                  │
│                                                                 │
│ VaultAbilitySystem                                              │
│   - Detects: Near obstacle + Jump/Action                        │
│   - Checks: Obstacle height < MaxHeight, space beyond           │
│   - Sets: VaultState.IsVaulting = true, height/velocity         │
└─────────────────────────────────────────────────────────────────┘
```

### PlayerAnimationStateSystem Updates

Add priority handling for agility abilities (after existing abilities):

```csharp
// Existing priorities:
// 1. Jump (AbilityIndex = 1)
// 2. Fall (AbilityIndex = 2)
// 3. Crouch (AbilityIndex = 3)
// 4. FreeClimb (AbilityIndex = 503)
// 5. Hang (AbilityIndex = 104)

// NEW priorities (insert between Crouch and Climb):
// 4. Dodge (AbilityIndex = 101) - if DodgeState.IsDodging
// 5. Roll (AbilityIndex = 102) - if RollState.IsRolling
// 6. Vault (AbilityIndex = 105) - if VaultState.IsVaulting
// 7. FreeClimb (503)
// 8. Hang (104)
```

---

## Implementation Plan

### Phase 0: Create Full State+Transition Copier Tool (BLOCKING)
- [x] Create `Assets/Editor/AnimatorAbilityCopier.cs`
- [x] Implement state copying (reuse existing logic)
- [x] Implement AnyState transition copying with conditions
- [x] Implement exit transition copying
- [x] Implement parameter verification/creation
- [x] Test on single ability (Dodge) before full run
- [x] Menu: `DIG > Animation > Copy Ability States With Transitions`

**Tool Complete:** `AnimatorAbilityCopier.cs` copies states, AnyState transitions with conditions, internal transitions, and ensures parameters exist.

### Phase 1: Controller Setup
- [x] Copy AgilityDemo.controller to ClimbingDemo.controller (already done)
- [ ] Run new Ability Copier tool to add missing states + transitions
- [ ] Verify parameters exist: AbilityIndex, AbilityChange, AbilityIntData, AbilityFloatData
- [ ] Identify and disable conflicting locomotion states (if any)
- [ ] Test that existing climbing (503) and hang (104) still function

### Phase 2: Add Agility Constants
- [x] Add Agility ability indices to `OpsiveAnimatorConstants.cs`:
  ```csharp
  // Agility Pack abilities (101-107)
  public const int ABILITY_DODGE = 101;
  public const int ABILITY_ROLL = 102;
  public const int ABILITY_CRAWL = 103;
  // ABILITY_HANG = 104 already exists
  public const int ABILITY_VAULT = 105;
  public const int ABILITY_LEDGE_STRAFE = 106;
  public const int ABILITY_BALANCE = 107;

  // Dodge IntData
  public const int DODGE_LEFT = 0;
  public const int DODGE_RIGHT = 1;
  public const int DODGE_FORWARD = 2;
  public const int DODGE_BACKWARD = 3;

  // Roll IntData
  public const int ROLL_LEFT = 0;
  public const int ROLL_RIGHT = 1;
  public const int ROLL_FORWARD = 2;
  public const int ROLL_LAND = 3;

  // Crawl IntData
  public const int CRAWL_ACTIVE = 0;
  public const int CRAWL_STOPPING = 1;
  ```

### Phase 3: ECS Components
- [x] Create `DodgeState` component (`Assets/Scripts/Player/Components/AgilityComponents.cs`)
- [x] Create `RollState` component
- [x] Create `VaultState` component
- [x] Create `CrawlState`, `BalanceState`, `LedgeStrafeState` components
- [x] Create `AgilityAnimationEvents` component with static event queue
- [x] Create `AgilityConfig` component
- [x] Create `AgilityAuthoring` component
- [x] Add AgilityAuthoring to player server prefab (manual setup in Unity)

### Phase 4: ECS Systems
- [x] Create `AgilityAnimationEventSystem` - Consumes animation events, clears ability states
- [x] Create `AgilityCooldownSystem` - Updates cooldown timers
- [x] Create `DodgeRollAnimationBridgeSystem` - Bridges DodgeRollState -> RollState for animation
- [x] Create `DodgeDiveAnimationBridgeSystem` - Bridges DodgeDiveState -> DodgeState for animation
- [x] Create `VaultAbilitySystem` - Detects obstacles and triggers vault on jump input
- [x] Create `VaultMovementSystem` - Handles movement during vault (root motion)
- [x] Create `AgilityAuthoring` - Authoring component for player prefab

**Files Created:**
- `Assets/Scripts/Player/Systems/Abilities/AgilityAnimationEventSystem.cs`
- `Assets/Scripts/Player/Systems/Abilities/DodgeRollAnimationBridgeSystem.cs`
- `Assets/Scripts/Player/Systems/Abilities/VaultAbilitySystem.cs`
- `Assets/Scripts/Player/Authoring/AgilityAuthoring.cs`

### Phase 5: PlayerAnimationStateSystem Updates
- [x] Add `DodgeState` lookup and handling
- [x] Add `RollState` lookup and handling
- [x] Add `VaultState` lookup and handling
- [x] Insert priority between Crouch(3) and FreeClimb(503)
- [x] Agility abilities use `continue` to skip other ability handling

### Phase 6: Animation Event Handlers
- [x] Add to `ClimbAnimatorBridge.cs`:
  ```csharp
  public void OnAnimatorDodgeComplete() { /* Queue event to clear DodgeState */ }
  public void OnAnimatorRollComplete() { /* Queue event to clear RollState */ }
  public void OnAnimatorVaultComplete() { /* Queue event to clear VaultState */ }
  ```
- [x] Create event queue system (like `FreeClimbAnimationEvents`) - `AgilityAnimationEvents` in `AgilityComponents.cs`
- [x] ECS systems read events to clear state - `AgilityAnimationEventSystem`

---

## Code Modifications Required

### OpsiveAnimatorConstants.cs
Add agility ability indices and IntData values (see Phase 2).

### PlayerAnimationStateSystem.cs
Add agility ability handling. Example for Dodge:

```csharp
// In OnUpdate, add lookup:
var dodgeLookup = SystemAPI.GetComponentLookup<DodgeState>(true);

// In foreach loop, after crouch handling:
bool hasDodge = dodgeLookup.HasComponent(entity);
if (hasDodge)
{
    var ds = dodgeLookup[entity];
    if (ds.IsDodging)
    {
        anim.AbilityIndex = OpsiveAnimatorConstants.ABILITY_DODGE; // 101
        anim.AbilityIntData = ds.Direction; // 0-3
        if (prevAbilityIndex != OpsiveAnimatorConstants.ABILITY_DODGE)
        {
            anim.AbilityChange = true;
        }
        return; // Skip lower priority abilities
    }
}
```

### ClimbAnimatorBridge.cs
Add animation event handlers:

```csharp
// Animation event callbacks (called by Opsive animation clips)
public void OnAnimatorDodgeComplete()
{
    Player.Components.AgilityAnimationEvents.QueueEvent(
        Player.Components.AgilityAnimationEvents.EventType.DodgeComplete);
}

public void OnAnimatorRollComplete()
{
    Player.Components.AgilityAnimationEvents.QueueEvent(
        Player.Components.AgilityAnimationEvents.EventType.RollComplete);
}

public void OnAnimatorVaultComplete()
{
    Player.Components.AgilityAnimationEvents.QueueEvent(
        Player.Components.AgilityAnimationEvents.EventType.VaultComplete);
}
```

### New Files Required

| File | Purpose |
|------|---------|
| `DodgeState.cs` | ECS component for dodge state |
| `RollState.cs` | ECS component for roll state |
| `VaultState.cs` | ECS component for vault state |
| `DodgeAbilitySystem.cs` | Detects input, triggers dodge |
| `RollAbilitySystem.cs` | Detects input, triggers roll |
| `VaultAbilitySystem.cs` | Detects obstacle, triggers vault |
| `AgilityAnimationEvents.cs` | Event queue for animation callbacks |
| `AgilityEventSystem.cs` | Reads events, clears ability states |

---

## Controller State Analysis

### Missing States Check (from Analyzer)

The Animator Analyzer shows **32 missing states** in ClimbingDemo.controller vs AgilityDemo.controller:

**Missing Dodge States:**
- Dodge/Melee Dodge/* (6 directional variants)
- Dodge/Aim Dodge/* (6 directional variants)
- Dodge/Bow Dodge/* (6 directional variants)

**Missing Roll States:**
- Roll/Roll, Roll/Roll Left, Roll/Roll Right
- Roll/Roll Walk, Roll/Roll Run
- Roll/Aim Roll Left, Roll/Aim Roll Right
- Roll/Falling Roll

**Missing Balance States:**
- Balance/Balance Movement
- Balance/Balance Idle Left, Balance/Balance Idle Right

**Missing Ledge Strafe States:**
- Ledge Strafe/Ledge Idle, Ledge Strafe/Ledge Strafe

**Missing Vault State:**
- Vault/Vault

---

## State Copier Tool Limitations

### Current Tool: `DIG > Animation > Copy Missing States to ClimbingDemo`

**Location:** `Assets/Editor/AnimatorStateCopier.cs`

**What it DOES copy:**
- ✅ State name and sub-state machine hierarchy
- ✅ Motion (animation clip reference)
- ✅ Speed, cycleOffset, mirror parameters
- ✅ IK on feet, write default values, tag
- ✅ StateMachineBehaviours

**What it does NOT copy:**
- ❌ **Transitions** (entry/exit/AnyState transitions)
- ❌ **Transition conditions** (AbilityIndex == 101, AbilityChange trigger, etc.)
- ❌ **Parameters** (AbilityIndex, AbilityIntData, etc. - must exist in target)
- ❌ **Blend trees** (only references Motion, doesn't deep copy blend tree structure)

### Impact

Without transitions and conditions, copied states will be **orphaned** - they exist but have no way to be entered. The Opsive ability system relies on:

```
AnyState → [AbilityIndex == 101 && AbilityChange] → Dodge/Melee Dodge/Forward
```

These transition conditions must be manually recreated or a new tool must be created.

---

## Required: New Editor Tool

### Phase 0: Create Full State+Transition Copier Tool

**Menu Location:** `DIG > Animation > Copy States With Transitions`

**Requirements:**
1. Copy states (existing functionality)
2. Copy transitions from AnyState to ability states
3. Copy transition conditions (AbilityIndex == X, AbilityChange, AbilityIntData == Y)
4. Ensure required parameters exist in target controller
5. Copy exit transitions back to locomotion

**Tool Specification:**

```csharp
// What to copy per ability state machine (e.g., "Dodge")
CopyAbilityStateMachine(
    source: AgilityDemo.controller,
    target: ClimbingDemo.controller,
    stateMachinePath: "Dodge",
    copyStates: true,
    copyAnyStateTransitions: true,
    copyExitTransitions: true,
    ensureParameters: true  // Add missing params to target
);
```

**Parameters to ensure exist:**
- `AbilityIndex` (int)
- `AbilityChange` (trigger)
- `AbilityIntData` (int)
- `AbilityFloatData` (float)

These should already exist in ClimbingDemo.controller from the Climbing addon.

---

## Verification Checklist

### Controller Setup
- [ ] ClimbingDemo.controller has all 32 missing agility states
- [ ] Existing climbing animations still play correctly
- [ ] Existing hang animations still play correctly
- [ ] Locomotion not broken by agility states

### Dodge (101)
- [ ] Left dodge plays when AbilityIndex=101, IntData=0
- [ ] Right dodge plays when AbilityIndex=101, IntData=1
- [ ] Forward dodge plays when AbilityIndex=101, IntData=2
- [ ] Backward dodge plays when AbilityIndex=101, IntData=3
- [ ] Animation completes and returns to locomotion

### Roll (102)
- [ ] Directional roll variants play correctly
- [ ] Landing roll plays after fall (if configured)
- [ ] Root motion moves character appropriately

### Vault (105)
- [ ] Vault triggers near valid obstacles
- [ ] Height affects animation variant
- [ ] Character moves over obstacle during animation

### Balance (107) / LedgeStrafe (106)
- [ ] Movement input drives blend trees
- [ ] Character stays on narrow surface
- [ ] Exit transitions work correctly

---

## Animation Events Reference

| Event Name | Called When | Handler |
|------------|-------------|---------|
| `OnAnimatorDodgeComplete` | Dodge animation ends | Clear AbilityIndex to 0 |
| `OnAnimatorRollComplete` | Roll animation ends | Clear AbilityIndex to 0 |
| `OnAnimatorVaultComplete` | Vault animation ends | Clear AbilityIndex to 0 |
| `OnAnimatorCrawlComplete` | Crawl stop animation ends | Clear AbilityIndex to 0 |
| `OnAnimatorHangStartInPosition` | Hang ready | Already implemented |
| `OnAnimatorHangComplete` | Hang exit | Already implemented |

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Missing transitions** | **Critical** | Create new editor tool (Phase 0) before any other work |
| Locomotion conflicts | High | Careful state pruning, test extensively |
| Animation event missing | Medium | Add events to animation clips |
| Root motion issues | Medium | Configure per-ability in controller |
| Priority conflicts | Low | Clear priority order in system |
| Parameter mismatch | Medium | Tool verifies/creates parameters automatically |

---

## Success Criteria

- [ ] All 7 agility abilities play correct animations
- [ ] Existing climbing/hang animations unaffected
- [ ] Locomotion (walk/run/sprint) unaffected
- [ ] Weapon animations unaffected
- [ ] Smooth transitions between abilities
- [ ] No animation stuck states
- [ ] Performance acceptable (no GC spikes)
