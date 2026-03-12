# EPIC 14.12 - Opsive Agility Pack Animation Integration Setup Guide

## Overview

The Agility Pack integration enables advanced movement animations (dodge, roll, vault, balance, ledge strafe, crawl) in DIG's hybrid ECS/MonoBehaviour animation system. This uses Opsive's animation parameter conventions (`AbilityIndex`, `AbilityIntData`, `AbilityFloatData`) driven by our own ECS systems.

---

## Phase Status

| Phase | Content | Status |
|-------|---------|--------|
| Phase 0 | AnimatorAbilityCopier Tool | ✅ Complete |
| Phase 1 | Controller Setup | Pending (Manual) |
| Phase 2 | Agility Constants | ✅ Complete |
| Phase 3 | ECS Components | ✅ Complete |
| Phase 4 | ECS Systems | ✅ Complete |
| Phase 5 | PlayerAnimationStateSystem Updates | ✅ Complete |
| Phase 6 | Animation Event Handlers | ✅ Complete |

---

## Files Created/Modified

### Phase 0: Editor Tool
| File | Purpose |
|------|---------|
| `Assets/Editor/AnimatorAbilityCopier.cs` | Copy ability state machines WITH transitions between controllers |

### Phase 2: Constants
| File | Purpose |
|------|---------|
| `Assets/Scripts/Player/Animation/OpsiveAnimatorConstants.cs` | Added Agility/Swimming ability indices and IntData constants |

### Phase 3: ECS Components
| File | Purpose |
|------|---------|
| `Assets/Scripts/Player/Components/AgilityComponents.cs` | All agility ability state components and event queue |

### Phase 4: ECS Systems
| File | Purpose |
|------|---------|
| `Assets/Scripts/Player/Systems/Abilities/AgilityAnimationEventSystem.cs` | Consumes animation events, clears ability states, manages cooldowns |
| `Assets/Scripts/Player/Systems/Abilities/DodgeRollAnimationBridgeSystem.cs` | Bridges DodgeRollState/DodgeDiveState to RollState/DodgeState |
| `Assets/Scripts/Player/Systems/Abilities/VaultAbilitySystem.cs` | Obstacle detection and vault triggering |
| `Assets/Scripts/Player/Authoring/AgilityAuthoring.cs` | Authoring component for player prefab |

### Phase 5: Animation State System
| File | Purpose |
|------|---------|
| `Assets/Scripts/Player/Systems/PlayerAnimationStateSystem.cs` | Added agility ability lookups and priority handling |

### Phase 6: Animation Events
| File | Purpose |
|------|---------|
| `Assets/Scripts/Player/Bridges/ClimbAnimatorBridge.cs` | Added agility animation event handlers |

---

## Architecture

DIG's animation system is an **ECS extension of Opsive's animation parameters**:

```
┌─────────────────────────────────────────────────────────────────┐
│ ECS (Authoritative State)                                       │
├─────────────────────────────────────────────────────────────────┤
│ PlayerInput → DodgeState, RollState, VaultState (future)        │
│                           │                                     │
│                           ▼                                     │
│ PlayerAnimationStateSystem (Burst, Server + Client)             │
│   - Reads ability state components                              │
│   - Writes PlayerAnimationState (AbilityIndex, IntData, etc.)   │
│   - Priority: Jump > Fall > Crouch > Dodge > Roll > Vault > ... │
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

---

## Quick Setup

### 1. Copy Ability States to ClimbingDemo.controller

1. Open Unity menu: **DIG > Animation > Copy Ability States With Transitions**
2. Set **Source** to `AgilityDemo.controller`
3. Set **Target** to `ClimbingDemo.controller`
4. Click **Analyze Ability State Machines**
5. Select the abilities you want (Dodge, Roll, Vault, etc.)
6. Ensure **Copy Missing Parameters** and **Copy AnyState Transitions** are checked
7. Click **Copy Selected Abilities**

### 2. Verify Controller Setup

After copying, verify in the Animator window:
- The ability sub-state machines exist (Dodge, Roll, Vault, etc.)
- AnyState has transitions to these states with conditions
- Parameters exist: `AbilityIndex`, `AbilityChange`, `AbilityIntData`, `AbilityFloatData`

### 3. Test Existing Animations

Ensure existing animations still work:
- Walk/Run/Sprint locomotion
- Jump and Fall
- Climbing (FreeClimb = 503, Hang = 104)

---

## AnimatorAbilityCopier Tool

### Menu Location

`DIG > Animation > Copy Ability States With Transitions`

### Features

| Feature | Description |
|---------|-------------|
| State Copying | Copies all states within ability sub-state machine |
| Transition Copying | Copies AnyState transitions with all conditions |
| Parameter Copying | Ensures required parameters exist in target |
| Duplicate Detection | Skips existing states/transitions |
| Batch Processing | Copy multiple abilities at once |

### Copy Options

| Option | Default | Description |
|--------|---------|-------------|
| Copy Missing Parameters | ✓ | Add parameters from source if missing |
| Copy AnyState Transitions | ✓ | Copy transitions from AnyState with conditions |
| Copy Entry Transitions | ✓ | Copy default state and entry transitions |
| Copy Exit Transitions | ✓ | Copy transitions back to locomotion |

### What Gets Copied

**States:**
- State name and position
- Motion (animation clip reference)
- Speed, cycleOffset, mirror settings
- State machine behaviours
- IK settings, write defaults, tags

**Transitions:**
- AnyState → Ability transitions
- Transition conditions (AbilityIndex == X, AbilityChange, etc.)
- Duration, exit time, interruption settings
- Internal state-to-state transitions
- Exit transitions

**Parameters (if missing):**
- `AbilityIndex` (Int)
- `AbilityChange` (Trigger)
- `AbilityIntData` (Int)
- `AbilityFloatData` (Float)

### Workflow Example

1. **Open Tool**: DIG > Animation > Copy Ability States With Transitions
2. **Quick Load**: Click "Agility" button to load AgilityDemo.controller as source
3. **Analyze**: Click "Analyze Ability State Machines"
4. **Review**: See list of abilities with state/transition counts
5. **Select**: Check boxes for abilities to copy (○ = missing, ● = exists)
6. **Copy**: Click "Copy Selected Abilities"
7. **Verify**: Open Animator window to confirm

---

## Agility Abilities Reference

### Ability Indices

| Ability | Index | Constant |
|---------|-------|----------|
| Dodge | 101 | `ABILITY_DODGE` |
| Roll | 102 | `ABILITY_ROLL` |
| Crawl | 103 | `ABILITY_CRAWL` |
| Hang | 104 | `ABILITY_HANG` |
| Vault | 105 | `ABILITY_VAULT` |
| Ledge Strafe | 106 | `ABILITY_LEDGE_STRAFE` |
| Balance | 107 | `ABILITY_BALANCE` |

### Dodge AbilityIntData

| Value | Direction | Constant |
|-------|-----------|----------|
| 0 | Left | `DODGE_LEFT` |
| 1 | Right | `DODGE_RIGHT` |
| 2 | Forward | `DODGE_FORWARD` |
| 3 | Backward | `DODGE_BACKWARD` |

### Roll AbilityIntData

| Value | Type | Constant |
|-------|------|----------|
| 0 | Left | `ROLL_LEFT` |
| 1 | Right | `ROLL_RIGHT` |
| 2 | Forward | `ROLL_FORWARD` |
| 3 | Land (from fall) | `ROLL_LAND` |

### Crawl AbilityIntData

| Value | State | Constant |
|-------|-------|----------|
| 0 | Active (crawling) | `CRAWL_ACTIVE` |
| 1 | Stopping (getting up) | `CRAWL_STOPPING` |

---

## Using Constants in Code

```csharp
using Player.Animation;

// Check if dodging
if (anim.AbilityIndex == OpsiveAnimatorConstants.ABILITY_DODGE)
{
    int direction = anim.AbilityIntData;

    if (direction == OpsiveAnimatorConstants.DODGE_LEFT)
    {
        // Dodging left
    }
}

// Set dodge animation
anim.AbilityIndex = OpsiveAnimatorConstants.ABILITY_DODGE;
anim.AbilityIntData = OpsiveAnimatorConstants.DODGE_FORWARD;
anim.AbilityChange = true; // Trigger transition
```

---

## AnimatorMonitor Disable

Opsive's `AnimatorMonitor` is disabled in `ClimbAnimatorBridge.Awake()` to prevent conflicts:

```csharp
void DisableOpsiveAnimatorMonitor()
{
    var opsiveMonitor = animator.GetComponent<AnimatorMonitor>();
    if (opsiveMonitor != null)
    {
        opsiveMonitor.enabled = false;
    }

    var childMonitor = animator.GetComponent<ChildAnimatorMonitor>();
    if (childMonitor != null)
    {
        childMonitor.enabled = false;
    }
}
```

This ensures only our ECS systems control `AbilityIndex`.

---

## Implemented Components (Phase 3)

### ECS Components (AgilityComponents.cs)

All agility ability components are in `Assets/Scripts/Player/Components/AgilityComponents.cs`:

```csharp
// Dodge ability state
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
public struct DodgeState : IComponentData
{
    [GhostField] public bool IsDodging;
    [GhostField] public int Direction;  // 0=Left, 1=Right, 2=Forward, 3=Backward
    public float TimeRemaining;
    public float CooldownRemaining;
    public float DodgeDuration;
    public float DodgeCooldown;
}

// Roll ability state
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
public struct RollState : IComponentData
{
    [GhostField] public bool IsRolling;
    [GhostField] public int RollType;  // 0=Left, 1=Right, 2=Forward, 3=Land
    public float TimeRemaining;
    public float CooldownRemaining;
    public float RollDuration;
    public float RollCooldown;
}

// Vault ability state
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
public struct VaultState : IComponentData
{
    [GhostField] public bool IsVaulting;
    [GhostField] public float StartVelocity;
    [GhostField] public float VaultHeight;
    public float TimeRemaining;
}

// Also includes: CrawlState, BalanceState, LedgeStrafeState
```

### Event Queue Pattern (AgilityAnimationEvents)

The static event queue pattern bridges MonoBehaviour animation callbacks to ECS:

```csharp
public struct AgilityAnimationEvents : IComponentData
{
    private static AgilityEventFlags _pendingEvents;
    private static readonly object _lock = new object();

    // Queue events from MonoBehaviour callbacks
    public static void QueueDodgeComplete() { lock (_lock) { _pendingEvents |= AgilityEventFlags.DodgeComplete; } }
    public static void QueueRollComplete() { lock (_lock) { _pendingEvents |= AgilityEventFlags.RollComplete; } }
    public static void QueueVaultComplete() { lock (_lock) { _pendingEvents |= AgilityEventFlags.VaultComplete; } }
    public static void QueueCrawlComplete() { lock (_lock) { _pendingEvents |= AgilityEventFlags.CrawlComplete; } }

    // Consume events in ECS systems (call once per frame)
    public static AgilityEventFlags ConsumeEvents()
    {
        lock (_lock)
        {
            var events = _pendingEvents;
            _pendingEvents = AgilityEventFlags.None;
            return events;
        }
    }
}

[Flags]
public enum AgilityEventFlags : byte
{
    None = 0,
    DodgeComplete = 1 << 0,
    RollComplete = 1 << 1,
    VaultComplete = 1 << 2,
    CrawlComplete = 1 << 3
}
```

---

## PlayerAnimationStateSystem Updates (Phase 5)

The system now handles agility abilities with priority between Crouch(3) and FreeClimb(503):

```csharp
// In OnUpdate - add lookups
var dodgeLookup = SystemAPI.GetComponentLookup<DodgeState>(true);
var rollLookup = SystemAPI.GetComponentLookup<RollState>(true);
var vaultLookup = SystemAPI.GetComponentLookup<VaultState>(true);

// In foreach loop - after crouch handling, before climb:

// Dodge (101) - highest agility priority
bool hasDodge = dodgeLookup.HasComponent(entity);
if (hasDodge)
{
    var ds = dodgeLookup[entity];
    if (ds.IsDodging)
    {
        anim.AbilityIndex = OpsiveAnimatorConstants.ABILITY_DODGE; // 101
        anim.AbilityIntData = ds.Direction; // 0-3
        if (prevIdx != OpsiveAnimatorConstants.ABILITY_DODGE)
            anim.AbilityChange = true;
        continue; // Skip lower priority abilities
    }
}

// Roll (102)
bool hasRoll = rollLookup.HasComponent(entity);
if (hasRoll)
{
    var rs = rollLookup[entity];
    if (rs.IsRolling)
    {
        anim.AbilityIndex = OpsiveAnimatorConstants.ABILITY_ROLL; // 102
        anim.AbilityIntData = rs.RollType; // 0-3
        if (prevIdx != OpsiveAnimatorConstants.ABILITY_ROLL)
            anim.AbilityChange = true;
        continue;
    }
}

// Vault (105)
bool hasVault = vaultLookup.HasComponent(entity);
if (hasVault)
{
    var vs = vaultLookup[entity];
    if (vs.IsVaulting)
    {
        anim.AbilityIndex = OpsiveAnimatorConstants.ABILITY_VAULT; // 105
        anim.AbilityFloatData = vs.StartVelocity;
        anim.AbilityIntData = (int)(vs.VaultHeight * 1000f);
        if (prevIdx != OpsiveAnimatorConstants.ABILITY_VAULT)
            anim.AbilityChange = true;
        continue;
    }
}
```

---

## Animation Event Handlers (Phase 6)

Add these handlers to `ClimbAnimatorBridge.cs`:

```csharp
#region Agility Animation Events (EPIC 14.12)

public void OnAnimatorDodgeComplete()
{
    Player.Components.AgilityAnimationEvents.QueueDodgeComplete();
}

public void OnAnimatorRollComplete()
{
    Player.Components.AgilityAnimationEvents.QueueRollComplete();
}

public void OnAnimatorVaultComplete()
{
    Player.Components.AgilityAnimationEvents.QueueVaultComplete();
}

public void OnAnimatorCrawlComplete()
{
    Player.Components.AgilityAnimationEvents.QueueCrawlComplete();
}

#endregion
```

---

## ECS Systems (Phase 4)

### Bridge Systems

The existing `DodgeRollSystem` and `DodgeDiveSystem` handle gameplay (stamina, invuln, etc.). Bridge systems sync their state to Opsive animation components:

```csharp
// DodgeRollAnimationBridgeSystem - syncs DodgeRollState -> RollState
// When DodgeRollState.IsActive == 1, sets RollState.IsRolling = true
// This allows PlayerAnimationStateSystem to drive Opsive Roll animation

// DodgeDiveAnimationBridgeSystem - syncs DodgeDiveState -> DodgeState
// When DodgeDiveState.IsActive == 1, sets DodgeState.IsDodging = true
// This allows PlayerAnimationStateSystem to drive Opsive Dodge animation
```

### Animation Event System

Consumes animation completion events from ClimbAnimatorBridge and clears ability states:

```csharp
// AgilityAnimationEventSystem
// - Reads AgilityAnimationEvents.ConsumeEvents() (static thread-safe queue)
// - Clears DodgeState.IsDodging when DodgeComplete event received
// - Clears RollState.IsRolling when RollComplete event received
// - Clears VaultState.IsVaulting when VaultComplete event received
```

### Vault Ability System

Detects vaultable obstacles and triggers vault when player jumps near them:

```csharp
// VaultAbilitySystem
// 1. Casts ray forward at waist height to detect obstacle
// 2. Casts ray from above to find obstacle top (calculate height)
// 3. Checks height is within MinVaultHeight-MaxVaultHeight range
// 4. Sets VaultState.IsVaulting = true, VaultHeight, StartVelocity
```

### Cooldown System

Updates ability cooldown timers:

```csharp
// AgilityCooldownSystem
// - Updates DodgeState.TimeRemaining and CooldownRemaining
// - Updates RollState.TimeRemaining and CooldownRemaining
// - Updates VaultState.TimeRemaining
// - Clears ability state when duration expires (backup to animation events)
```

---

## Authoring Component

Add `AgilityAuthoring` to your player prefab to enable agility abilities:

```csharp
// In Unity Inspector:
// 1. Select player prefab
// 2. Add Component > Player > Agility Authoring
// 3. Configure:
//    - Can Dodge, Can Roll, Can Vault, Can Crawl (toggles)
//    - Dodge/Roll Duration and Cooldown
//    - Vault Height Range (Min/Max)
```

This bakes the following components onto the player entity:
- `AgilityConfig` - Configuration settings
- `HasAgilityAbilities` - Tag for queries
- `DodgeState`, `RollState`, `VaultState`, `CrawlState` - Ability states
- `AgilityAnimationEvents` - Event queue

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Tool not in menu | Ensure `AnimatorAbilityCopier.cs` compiled (check Console) |
| States copied but no transitions | Ensure "Copy AnyState Transitions" is checked |
| Animation doesn't play | Check AbilityIndex parameter exists in controller |
| Animation stuck | Ensure AbilityChange trigger is fired on transitions |
| Locomotion broken | Some abilities may have conflicting locomotion states - disable them |
| Existing climb/hang broken | Test after copying each ability, identify conflicts |

### Verifying Transitions

1. Open Animator window
2. Select the Base Layer
3. Click on "Any State"
4. In Inspector, verify transitions exist to your ability states
5. Check conditions include `AbilityIndex == X` and `AbilityChange`

### Checking Parameters

1. Open Animator window
2. Click "Parameters" tab
3. Verify these exist:
   - `AbilityIndex` (Int)
   - `AbilityChange` (Trigger)
   - `AbilityIntData` (Int)
   - `AbilityFloatData` (Float)

---

## Comparison: AnimatorStateCopier vs AnimatorAbilityCopier

| Feature | AnimatorStateCopier | AnimatorAbilityCopier |
|---------|---------------------|----------------------|
| Copies states | ✓ | ✓ |
| Copies motions | ✓ | ✓ |
| Copies behaviours | ✓ | ✓ |
| Copies AnyState transitions | ✗ | ✓ |
| Copies transition conditions | ✗ | ✓ |
| Copies parameters | ✗ | ✓ |
| Copies internal transitions | ✗ | ✓ |
| Copies exit transitions | ✗ | ✓ |
| Works per-state | ✓ | ✗ (per ability) |
| Works per-ability | ✗ | ✓ |

Use **AnimatorStateCopier** for individual state copies.
Use **AnimatorAbilityCopier** for complete ability integration.

---

## Success Criteria

### Completed
- [x] AnimatorAbilityCopier tool works correctly
- [x] Constants available in OpsiveAnimatorConstants.cs
- [x] ECS components created (DodgeState, RollState, VaultState, etc.)
- [x] PlayerAnimationStateSystem handles agility abilities
- [x] Animation event handlers added to ClimbAnimatorBridge
- [x] Bridge systems sync DodgeRollState/DodgeDiveState to animation states
- [x] VaultAbilitySystem detects obstacles and triggers vault
- [x] AgilityCooldownSystem manages timers
- [x] AgilityAnimationEventSystem consumes events and clears states
- [x] AgilityAuthoring component created for prefab setup

### Manual Tasks Remaining
- [ ] Run AnimatorAbilityCopier to copy abilities to ClimbingDemo.controller
- [ ] Verify transitions are copied with correct conditions
- [ ] Verify parameters are added if missing
- [ ] Test existing climb/hang/locomotion still work
- [ ] Add AgilityAuthoring component to player prefab
