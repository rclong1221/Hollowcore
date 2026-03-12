# EPIC 3.1: Airlocks, Pressurization, and EVA Transitions

**Priority**: HIGH  
**Goal**: Seamless transitions between ship interior (pressurized) and EVA (vacuum), with clear interaction prompts and authoritative NetCode behavior.  
**Dependencies**: Epic 1.4 (`PlayerState`), Epic 2.1 (Oxygen), Survival `EnvironmentZone` / `CurrentEnvironmentZone`

## Design Notes (Match EPIC7 Level of Detail)
- **Truth source**: oxygen drain and “safe vs unsafe” comes from `CurrentEnvironmentZone` (`OxygenRequired`, `ZoneType`), not from ad-hoc flags toggled by airlock code.
- **Authority**: server is authoritative for cycle start/end, door lock state, teleports, and `PlayerState.Mode`.
- **Prediction**: clients can predict *intent* (request + UI) but must reconcile if server rejects (range/lock/occupied).
- **Safety invariant**: interior + exterior doors cannot both be open while cycling (unless explicitly configured).
- **Anti-spam**: requests include `ClientTick` and are rate-limited server-side (1 active request per player per airlock).
- **Future-proofing**: allow failure modes (no power, breached hull) to block cycling or force unsafe outcomes (ties into Epic 3.5/3.6).

## Scope
- Airlock interaction + cycling
- Door state + safety rules (no “both doors open” unless explicitly allowed)
- Player mode transition (`PlayerMode.EVA` ↔ `PlayerMode.InShip`)
- Environment zone handoff (pressurized ↔ vacuum) as the authoritative driver for oxygen drain

## Components

**Airlock** (IComponentData, on airlock entity)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `InteriorSpawn` | float3 | No | Teleport target inside ship |
| `ExteriorSpawn` | float3 | No | Teleport target outside ship |
| `InteriorForward` | float3 | No | Spawn orientation forward |
| `ExteriorForward` | float3 | No | Spawn orientation forward |
| `State` | AirlockState | Yes | Idle / CyclingToInterior / CyclingToExterior |
| `CycleTime` | float | No | Total time for a cycle |
| `CycleProgress` | float | Quantization=100 | 0..CycleTime |
| `CurrentUser` | Entity | Yes | Player currently cycling (Entity.Null if none) |

**AirlockDoor** (IComponentData, on door entities; optional if doors are just visuals)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `DoorSide` | DoorSide | Yes | Interior / Exterior |
| `IsOpen` | bool | Yes | Door open state |
| `IsLocked` | bool | Yes | Locked during cycle, damage, etc. |

**AirlockUseRequest** (IBufferElementData, on player; server-consumed)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `AirlockEntity` | Entity | Yes | Airlock being used |
| `Direction` | AirlockDirection | Yes | EnterShip / ExitShip |
| `ClientTick` | uint | Yes | Ordering/anti-spam |

**AirlockInteractable** (IComponentData, on airlock entity)
| Field | Type | Description |
|---|---|---|
| `Range` | float | Interaction distance |
| `PromptEnter` | FixedString64Bytes | “Press E: Enter Ship” |
| `PromptExit` | FixedString64Bytes | “Press E: Exit Ship” |

## Systems

**AirlockPromptSystem** (PresentationSystemGroup, ClientWorld)
- Query: local player, nearby `AirlockInteractable` + `Airlock`
- Chooses prompt based on `PlayerState.Mode` and airlock availability
- Drives UI only (no gameplay authority)

**AirlockUseRequestSystem** (PredictedSimulationSystemGroup, ClientWorld)
- When interact pressed and prompt-valid, appends `AirlockUseRequest`
- Local prediction: may start “cycle” feedback, but teleport/state change remains server-authoritative

**AirlockCycleSystem** (SimulationSystemGroup, ServerWorld)
- Consumes `AirlockUseRequest`, validates:
  - player is in range (server-side distance check)
  - airlock not in use / not locked
  - direction allowed (enter vs exit)
- Executes cycle:
  - Locks doors, updates `Airlock.State` + progress
  - On completion: teleports player to target spawn, updates `PlayerState.Mode`
  - Ensures environment zone outcome is correct (player ends in pressurized or vacuum zone)

## NetCode & Authority Notes
- Server is authoritative for teleport + `PlayerState.Mode` changes.
- Client may predict UI/animation, but must reconcile cleanly when server denies or delays.
- Use a request-buffer pattern (already used elsewhere in Survival systems) to avoid client-side spawns/state mutation.

## Sub-Epics / Tasks

### Sub-Epic 3.1.1: Interaction + Prompting ✅
**Goal**: Consistent, non-jittery prompt behavior for local player.
**Tasks**:
- [x] Add a single “best airlock” selection rule (closest + in-view + usable) to avoid prompt flicker
- [x] Show prompt variants:
  - [x] EVA → “Press E: Enter Ship”
  - [x] InShip → “Press E: Exit Ship”
  - [x] Busy/Locked → “Airlock Busy” / “Airlock Locked”
- [x] Add client-side “held interact” debounce (avoid repeated requests per frame)

### Sub-Epic 3.1.2: Request Buffer + Server Validation ✅
**Goal**: Avoid prediction exploits and keep server authoritative.
**Tasks**:
- [x] `AirlockUseRequest` buffer on player with `[InternalBufferCapacity]` (small, e.g. 2–4)
- [x] Server validation checks:
  - [x] distance/range (server-side)
  - [x] `Airlock.CurrentUser == Entity.Null`
  - [x] door lock state / state machine legality
  - [x] player is alive and not already transitioning
- [x] Rejection path is silent but deterministic (no partial state changes)

### Sub-Epic 3.1.3: Cycle State Machine (Doors + Timing) ✅
**Goal**: Door safety rules and deterministic cycle timing.
**Tasks**:
- [x] Implement `AirlockState` with explicit transitions and a single writer system on server
- [x] Lock both doors at cycle start; open only the destination side at cycle end
- [x] Replicate `Airlock.State` + `CycleProgress` to drive client visuals
- [ ] Optional: add “manual override” flag for debug/dev (ties into Epic 7.9 debug tooling style)

### Sub-Epic 3.1.4: Teleport + Player Mode + Zone Outcome ✅
**Goal**: Player ends in the right mode and oxygen logic follows automatically.
**Tasks**:
- [x] On cycle completion (server):
  - [x] teleport player to target spawn
  - [x] set `PlayerState.Mode` to `InShip` or `EVA`
  - [x] ensure their `CurrentEnvironmentZone` resolves correctly next tick (zone volumes/overlaps)
- [x] Handle edge cases:
  - [x] player dies mid-cycle → abort and clear `CurrentUser`
  - [x] player disconnects mid-cycle → clear `CurrentUser`
  - [x] airlock entity despawns mid-cycle → fail safe (unlock doors, clear user)

### Sub-Epic 3.1.5: Presentation (Client-Only) ✅
**Goal**: Make cycling feel “real” without adding network load.
**Tasks**:
- [x] Client-side door animation system driven by replicated `Airlock`/`AirlockDoor` state
- [x] Audio cues (seal, vent, hiss) + UI progress indicator during cycle
- [x] Optional screen FX when transitioning to/from vacuum (helmet HUD)

### Sub-Epic 3.1.6: QA Checklist
**Tasks**:
- [ ] Try enter/exit spam; verify only one request is accepted
- [ ] Two players attempt same airlock; verify deterministic winner and no stuck `CurrentUser`
- [ ] Simulated latency: 50ms/100ms/200ms; verify no rubber-banding beyond teleport snap
- [ ] Verify oxygen drain flips only when zone says `OxygenRequired == true`

## Files (Planned)
- `Assets/Scripts/Runtime/Ship/Airlocks/Components/AirlockComponents.cs`
- `Assets/Scripts/Runtime/Ship/Airlocks/Systems/AirlockUseRequestSystem.cs`
- `Assets/Scripts/Runtime/Ship/Airlocks/Systems/AirlockCycleSystem.cs`
- `Assets/Scripts/Runtime/Ship/Airlocks/Systems/AirlockPromptSystem.cs`
- `Assets/Scripts/Runtime/Ship/Airlocks/Authoring/AirlockAuthoring.cs`

## Acceptance Criteria
- [x] Enter/exit prompt appears only when in range and airlock is usable
- [x] Server prevents opening both doors simultaneously during a cycle
- [x] Player ends in correct `PlayerMode` and corresponding environment zone type
- [x] Oxygen drain is driven by `CurrentEnvironmentZone.OxygenRequired` (vacuum/toxic/underwater), not by hand-toggled flags
- [x] Denied requests (out of range / already in use) reconcile without desync or rubber-banding

## Suggested File Structure
```
Assets/Scripts/Runtime/Ship/Airlocks/
├── Components/
│   └── AirlockComponents.cs
├── Systems/
│   ├── AirlockUseRequestSystem.cs
│   ├── AirlockCycleSystem.cs
│   ├── AirlockPromptSystem.cs
│   └── AirlockDoorAnimationSystem.cs
└── Authoring/
    └── AirlockAuthoring.cs
```

---

# Implementation Guide

> **Status**: ✅ IMPLEMENTED  
> **Last Updated**: December 2024

This section provides detailed guidance for developers and designers working with the airlock system.

---

## Architecture Overview

The airlock system follows a **client-server authority model** using Unity NetCode:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           CLIENT                                         │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌──────────────────┐    ┌─────────────────────┐    ┌────────────────┐  │
│  │ AirlockPrompt    │    │ AirlockUseRequest   │    │ AirlockDoor    │  │
│  │ System           │    │ System              │    │ AnimationSystem│  │
│  │ (UI prompts)     │    │ (sends requests)    │    │ (visuals)      │  │
│  └────────┬─────────┘    └──────────┬──────────┘    └────────────────┘  │
│           │                         │                                    │
│           │                         │ AirlockUseRequest buffer           │
│           │                         ▼                                    │
├───────────┼─────────────────────────┼────────────────────────────────────┤
│           │                         │           NETWORK                  │
├───────────┼─────────────────────────┼────────────────────────────────────┤
│           │                         │                                    │
│           │                         ▼                                    │
│           │              ┌─────────────────────┐                         │
│           │              │ AirlockCycleSystem  │                         │
│           │              │ (validates, cycles, │                         │
│           │              │  teleports)         │                         │
│           │              └──────────┬──────────┘                         │
│           │                         │                                    │
│           │                         │ Replicates Airlock.State,          │
│           │                         │ AirlockDoor.IsOpen, etc.           │
│           ◄─────────────────────────┘                                    │
│                                                                          │
│                           SERVER                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Key Principles

1. **Server Authority**: The server makes all gameplay decisions (teleport, mode change, door state)
2. **Client Prediction**: Client can show UI and predict intent, but never mutates authoritative state
3. **Request-Buffer Pattern**: Client requests go into a buffer; server consumes and validates
4. **Replicated State**: Door open/closed, cycle progress are replicated for client visuals

---

## For Designers: Setting Up Airlocks in Unity

### Step 1: Create the Airlock GameObject

1. In your scene hierarchy, create an empty GameObject where you want the airlock
2. Name it descriptively, e.g., `Airlock_MainBay` or `Airlock_Engineering`
3. Add a **Collider** component (Box, Sphere, or Capsule) and set it as a **Trigger**
   - This collider defines the interaction area
   - Size it to cover the "control panel" or activation area

### Step 2: Add the Airlock Authoring Component

1. Select your airlock GameObject
2. Click **Add Component** → search for `AirlockAuthoring`
3. You'll see several fields to configure:

| Field | What It Does | Recommended Value |
|-------|--------------|-------------------|
| **Interior Spawn Point** | Where players appear INSIDE the ship | Create a child Transform, position it in pressurized zone |
| **Exterior Spawn Point** | Where players appear OUTSIDE the ship | Create a child Transform, position it in vacuum zone |
| **Cycle Time** | How long the airlock takes to cycle (seconds) | 2-5 seconds for gameplay feel |
| **Interaction Range** | How close players must be to interact | 2-3 meters typical |
| **Prompt Enter** | Text shown when in EVA mode | "Press E: Enter Ship" |
| **Prompt Exit** | Text shown when inside ship | "Press E: Exit Ship" |
| **Prompt Busy** | Text shown when airlock is in use | "Airlock Busy" |
| **Prompt Locked** | Text shown when airlock is locked | "Airlock Locked" |

### Step 3: Create Spawn Point Transforms

1. Create two child GameObjects under your airlock:
   - `SpawnPoint_Interior` - position this where players should appear inside the ship
   - `SpawnPoint_Exterior` - position this where players should appear in space/outside
2. **Important**: The **forward direction (blue arrow)** of each spawn point determines which way the player faces after teleporting
3. Drag these into the `Interior Spawn Point` and `Exterior Spawn Point` fields

### Step 4: Add Doors (Optional)

If you want animated doors:

1. Create door GameObjects (with meshes/colliders)
2. Add `AirlockDoorAuthoring` component to each door
3. Configure:
   - **Door Side**: `Interior` or `Exterior`
   - **Parent Airlock**: Drag your airlock GameObject here
   - **Animation Type**: `Slide` (sci-fi sliding doors) or `Rotate` (swing doors)
   - **Open Direction**: Which way the door slides (for sliding type)
   - **Open Distance**: How far it slides open
   - **Open Angle**: How far it rotates (for rotating type)

### Step 5: Set Up Environment Zones

For oxygen to work correctly, you need `EnvironmentZone` triggers on both sides:

1. Inside the ship: An `EnvironmentZoneAuthoring` set to `Pressurized` (OxygenRequired = false)
2. Outside the ship: An `EnvironmentZoneAuthoring` set to `Vacuum` (OxygenRequired = true)

The airlock teleports players between these zones, and the survival systems automatically detect the zone change.

### Visual Checklist in Editor

When you select your airlock in the Scene view, you should see:
- 🔵 **Blue wire sphere**: Interaction range
- 🟢 **Green sphere + ray**: Interior spawn point and facing direction
- 🔴 **Red sphere + ray**: Exterior spawn point and facing direction
- 🟡 **Yellow line**: Connection between spawn points

---

## For Developers: Working with the Code

### File Locations

```
Assets/Scripts/Runtime/Ship/Airlocks/
├── Components/
│   └── AirlockComponents.cs      # All component definitions
├── Systems/
│   ├── AirlockPromptSystem.cs    # Client: UI prompts
│   ├── AirlockUseRequestSystem.cs # Client: Input handling + requests
│   ├── AirlockCycleSystem.cs     # Server: State machine + teleport
│   └── AirlockDoorAnimationSystem.cs # Client: Door visuals
└── Authoring/
    └── AirlockAuthoring.cs       # Unity editor components
```

### Component Reference

| Component | Lives On | Purpose |
|-----------|----------|---------|
| `Airlock` | Airlock entity | Main state machine, spawn points, cycle progress |
| `AirlockDoor` | Door entities | Individual door state (open/closed/locked) |
| `AirlockInteractable` | Airlock entity | Interaction range and prompt text |
| `AirlockUseRequest` | Player entity (buffer) | Client requests to use an airlock |
| `AirlockTransitionPending` | Player entity | Added during cycle, removed on completion |
| `AirlockPromptState` | Player entity | Client-side UI state |
| `AirlockLocked` | Airlock entity | Tag component to lock an airlock |
| `AirlockInteractDebounce` | Player entity | Prevents input spam |
| `AirlockDoorAnimation` | Door entities | Animation state for visuals |

### Player Prefab Setup

Your player prefab needs these components to interact with airlocks:

1. Add `AirlockPlayerAuthoring` component to your player prefab
2. This adds:
   - `AirlockUseRequest` buffer (for sending requests)
   - `AirlockPromptState` (for UI)
   - `AirlockInteractDebounce` (for input handling)

### How to Lock an Airlock (Code Example)

To lock an airlock (e.g., due to damage or power failure):

```csharp
// Add the AirlockLocked component to prevent use
EntityManager.AddComponentData(airlockEntity, new AirlockLocked
{
    LockReason = "No Power"
});
```

To unlock:
```csharp
// Remove the component to allow use again
EntityManager.RemoveComponent<AirlockLocked>(airlockEntity);
```

### How to Force an Airlock Open (Debug/Cheat)

```csharp
// Get the airlock component
var airlock = EntityManager.GetComponentData<Airlock>(airlockEntity);

// Set doors to open state directly (server-side only!)
foreach (var (door, entity) in 
         SystemAPI.Query<RefRW<AirlockDoor>>()
         .WithEntityAccess())
{
    if (door.ValueRO.AirlockEntity == airlockEntity)
    {
        door.ValueRW.IsOpen = true;
        door.ValueRW.IsLocked = false;
    }
}
```

### State Machine Flow

```
       Player presses E           Server validates
              │                         │
              ▼                         ▼
    ┌─────────────────┐      ┌─────────────────────┐
    │ AirlockUseRequest│────►│ AirlockCycleSystem  │
    │ (client creates) │      │ (server processes)  │
    └─────────────────┘      └──────────┬──────────┘
                                        │
                        ┌───────────────┼───────────────┐
                        ▼               ▼               ▼
                   Rejected        Accepted         Edge Case
                   (silent)            │           (abort)
                                       ▼
                              ┌─────────────────┐
                              │ State = Cycling │
                              │ Lock both doors │
                              │ Set CurrentUser │
                              └────────┬────────┘
                                       │
                                       │ CycleProgress += dt
                                       │
                                       ▼
                              ┌─────────────────┐
                              │ Progress >= Time│
                              └────────┬────────┘
                                       │
                                       ▼
                              ┌─────────────────┐
                              │ Teleport player │
                              │ Set PlayerMode  │
                              │ Open dest door  │
                              │ State = Idle    │
                              └─────────────────┘
```

---

## Common Patterns and Best Practices

### Pattern: Checking If Player Can Use Airlock

```csharp
bool CanPlayerUseAirlock(Entity playerEntity, Entity airlockEntity)
{
    // Check airlock is not locked
    if (EntityManager.HasComponent<AirlockLocked>(airlockEntity))
        return false;
    
    // Check airlock is idle
    var airlock = EntityManager.GetComponentData<Airlock>(airlockEntity);
    if (airlock.State != AirlockState.Idle)
        return false;
    
    // Check airlock has no current user
    if (airlock.CurrentUser != Entity.Null)
        return false;
    
    // Check player is in valid mode
    var playerState = EntityManager.GetComponentData<PlayerState>(playerEntity);
    if (playerState.Mode != PlayerMode.EVA && playerState.Mode != PlayerMode.InShip)
        return false;
    
    // Check player is not already transitioning
    if (EntityManager.HasComponent<AirlockTransitionPending>(playerEntity))
        return false;
    
    return true;
}
```

### Pattern: Listening for Airlock Events

To respond to airlock transitions (e.g., for audio/VFX):

```csharp
// Check for state changes by comparing previous and current state
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct AirlockEventSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (airlock, prevState) in 
                 SystemAPI.Query<RefRO<Airlock>, RefRW<AirlockPreviousState>>())
        {
            if (prevState.ValueRO.State != airlock.ValueRO.State)
            {
                // State changed! Fire events
                if (airlock.ValueRO.State == AirlockState.CyclingToExterior)
                    PlaySound("airlock_depressurize");
                else if (airlock.ValueRO.State == AirlockState.CyclingToInterior)
                    PlaySound("airlock_pressurize");
                    
                prevState.ValueRW.State = airlock.ValueRO.State;
            }
        }
    }
}
```

---

## Debugging Tips

### Problem: Player Not Seeing Prompts

1. Check player has `AirlockPromptState` component (add `AirlockPlayerAuthoring`)
2. Check player has `GhostOwnerIsLocal` tag (network setup issue if missing)
3. Check airlock has `AirlockInteractable` component
4. Check player is within `InteractionRange`
5. Check player `PlayerState.Mode` is `EVA` or `InShip`

### Problem: Airlock Not Responding to Input

1. Check player has `AirlockUseRequest` buffer
2. Check `PlayerInput.Interact` is being set (input system)
3. Check debounce isn't blocking (reduce `DebounceTickCount` for testing)
4. Check server is running and connected (requests need server to process)

### Problem: Player Teleports to Wrong Location

1. Check spawn point Transforms are at correct positions
2. Check spawn points are assigned to correct fields (Interior vs Exterior)
3. Check spawn point forward directions face the right way

### Problem: Doors Not Animating

1. Check doors have `AirlockDoorAuthoring` component
2. Check `ParentAirlock` is assigned
3. Check animation values are non-zero (`OpenDistance` or `OpenAngle`)
4. Check system is running (client-side only: `AirlockDoorAnimationSystem`)

### Debug Logging

Add this to `AirlockCycleSystem` for debugging:

```csharp
UnityEngine.Debug.Log($"[Airlock] Request from {playerEntity} for {request.AirlockEntity}, Direction={request.Direction}");
UnityEngine.Debug.Log($"[Airlock] Validation: range={distance:F2}/{interactable.Range}, state={airlock.State}, user={airlock.CurrentUser}");
```

---

## Extension Points

### Adding New Lock Reasons

The `AirlockLocked` component has a `LockReason` field. You can add locks from any system:

```csharp
// Power system locks airlocks when power is off
EntityManager.AddComponentData(airlockEntity, new AirlockLocked { LockReason = "No Power" });

// Damage system locks damaged airlocks
EntityManager.AddComponentData(airlockEntity, new AirlockLocked { LockReason = "Hull Breach" });

// Security system locks airlocks during lockdown
EntityManager.AddComponentData(airlockEntity, new AirlockLocked { LockReason = "Security Lockdown" });
```

### Adding Emergency Override

To add an emergency override that lets players force doors open:

1. Add a new field to `Airlock`: `bool EmergencyOverrideActive`
2. Modify `AirlockCycleSystem` to check this flag
3. Create a new request type `AirlockEmergencyOverrideRequest`
4. Add cooldown/consequences (damage to doors, alarm, etc.)

### Adding Multi-Person Airlocks

The current system supports one person at a time. To support multiple:

1. Change `CurrentUser` (Entity) to `CurrentUsers` (NativeList or fixed buffer)
2. Modify validation to check capacity instead of single user
3. Track all players for teleport on completion
4. Consider group abort logic if one player dies

---

## Testing Checklist

Use this checklist when testing airlock functionality:

### Basic Functionality
- [ ] Player can see prompt when in range (EVA mode)
- [ ] Player can see prompt when in range (InShip mode)
- [ ] Pressing E starts cycle (visual feedback)
- [ ] Player teleports to correct location after cycle
- [ ] `PlayerState.Mode` changes correctly (EVA ↔ InShip)
- [ ] Doors animate open/closed correctly

### Edge Cases
- [ ] Spam E key: only one cycle starts
- [ ] Two players try same airlock: one wins, other sees "Busy"
- [ ] Player dies mid-cycle: cycle aborts, airlock resets
- [ ] Player disconnects mid-cycle: airlock resets
- [ ] Walk out of range while cycling: cycle continues (already started)

### Network
- [ ] 50ms latency: smooth experience
- [ ] 100ms latency: acceptable, no rubber-banding
- [ ] 200ms latency: playable, minor visual delay
- [ ] Packet loss: system recovers gracefully

### Integration
- [ ] Oxygen starts draining in vacuum zone after exit
- [ ] Oxygen stops draining in pressurized zone after enter
- [ ] EVA HUD appears after exiting to vacuum
- [ ] Ship HUD appears after entering ship

---

## Glossary

| Term | Definition |
|------|------------|
| **EVA** | Extra-Vehicular Activity - when player is outside the ship in vacuum |
| **InShip** | Player is inside the pressurized ship interior |
| **Cycle** | The process of transitioning through an airlock (pressurize/depressurize) |
| **CurrentUser** | The player entity currently using the airlock |
| **Spawn Point** | The position where players appear after teleporting |
| **Request Buffer** | A list of pending requests from client to server |
| **Ghost Field** | A field that gets replicated from server to clients |
| **Quantization** | Compression of float values for network efficiency |

---

## Related Epics

- **Epic 1.4**: `PlayerState` / `PlayerMode` definitions
- **Epic 2.1**: Oxygen system that reacts to `CurrentEnvironmentZone`
- **Epic 3.5**: Ship damage (can lock airlocks)
- **Epic 3.6**: Hull breaches (affects airlock safety)
- **Epic 7.9**: Debug tooling (airlock debug commands)

## Technical Implementation Notes

### Coordinate Spaces & Moving Ships
To ensure airlocks function correctly on moving ships (Epic 3.3 integration), the following coordinate space rules must be followed:

1.  **Spawn Points (Baking)**: `InteriorSpawn` and `ExteriorSpawn` in the `Airlock` component must be stored as **Local Coordinates** relative to the Airlock entity. This is handled by `AirlockBaker` converting the authoring transform positions using `InverseTransformPoint`.
    - *Reason*: If stored as World Coordinates, the spawn points would remain static in the world even as the ship moves, causing players to teleport to empty space.

2.  **Spawn Points (Runtime)**: When teleporting the player in `AirlockCycleSystem`, these local spawn points must be transformed to **World Space** using the Airlock entity's `LocalToWorld` matrix (`math.transform` and `math.rotate`).

3.  **Interaction Validation**: Distance checks on the server must use the Airlock's **World Position** (via `LocalToWorld` lookup), not its `LocalTransform.Position`.
    - *Reason*: `LocalTransform.Position` is relative to the ship (parent), whereas the player's position is typically absolute World Space. Comparing them directly leads to incorrect distance calculations.

4.  **Door Animation**: `AirlockDoor` entities must be children of the Airlock/Ship. Their closed state (`ClosedPosition`, `ClosedRotation`) is baked as **Local Coordinates** and applied to `LocalTransform`. This ensures doors stay attached to the moving ship while animating.
