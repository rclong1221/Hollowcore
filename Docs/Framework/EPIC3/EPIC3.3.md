# EPIC 3.3: Ship Local Space (Moving Interior) and Relative Player Motion

**Priority**: MEDIUM  
**Goal**: Players remain stable inside a moving/rotating ship without jitter, drift, or physics explosions.  
**Dependencies**: Epic 3.1 (enter/exit), Epic 1.7/7.x (physics/collision stability)

## Design Notes (Match EPIC7 Level of Detail)
- **Core risk**: “parenting” dynamic physics bodies to moving transforms often causes jitter and non-determinism under NetCode prediction.
- **Two viable approaches**:
  - **A) Ship-local space**: store player local pose relative to ship and derive world pose each tick.
  - **B) Inertial correction**: apply ship delta transform to player each fixed tick, then run player movement in world space.
- **MVP recommendation**: start with inertial correction (less intrusive), then move to true ship-local space if needed.
- **Ordering matters**: ship movement → interior correction → player movement → physics step.

## Outstanding Tasks (Post-Review)
- [ ] **Task 3.3.A (Execution Order)**: Ensure `ShipInertialCorrectionSystem` updates before `CharacterControllerSystem` to prevent clipping.
- [ ] **Task 3.3.B (Input Targeting)**: Fix `ShipMovementSystem` to verify Helm Station ownership (prevent one helm piloting all ships).
- [ ] **Task 3.3.C (Physics Integration)**: Update `ShipMovementSystem` to support both `LocalTransform` (Kinematic) and `PhysicsVelocity` (Dynamic) movement.

## Problem Statement
Ships move in world space. Players inside the ship must feel like they’re on a stable interior, not sliding due to numerical error or network interpolation differences.

## Recommended Approach (Local Space)
Store player position/rotation in ship-local space while `PlayerMode.InShip` or `PlayerMode.Piloting`, and derive world transforms each tick from the ship transform.

## Components

**ShipRoot** (IComponentData, on ship entity)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `ShipId` | int | Yes | Stable ID for debugging/ownership |

**ShipKinematics** (IComponentData, on ship entity)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `LinearVelocity` | float3 | Quantization=100 | World-space velocity |
| `AngularVelocity` | float3 | Quantization=100 | World-space angular vel |

**InShipLocalSpace** (IComponentData, on player)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `ShipEntity` | Entity | Yes | The ship the player is “attached” to |
| `LocalPosition` | float3 | Quantization=100 | Position in ship-local coordinates |
| `LocalRotation` | quaternion | Quantization=1000 | Rotation in ship-local coordinates |

## Systems

**EnterShipLocalSpaceSystem** (SimulationSystemGroup, ServerWorld)
- On entering ship (Epic 3.1 completion), initializes `InShipLocalSpace` from current world transform and ship transform.

**ShipLocalSpaceTransformSystem** (PredictedFixedStepSimulationSystemGroup, ServerWorld + ClientWorld)
- For players with `InShipLocalSpace`:
  - compute world transform from ship transform × local transform
  - writes to `LocalTransform` (or equivalent)
- For on-foot logic (input, movement constraints), operate in ship-local axes when appropriate.

**ExitShipLocalSpaceSystem** (SimulationSystemGroup, ServerWorld)
- Removes `InShipLocalSpace` when leaving ship, restoring world-space movement.

## Physics Considerations
- If using Unity Physics dynamic bodies for players, avoid directly parenting rigid bodies to moving ships.
- Prefer a kinematic correction approach:
  - compute the delta transform from last ship transform to current
  - apply that delta to player transform before running movement
- Run ordering matters (Ship movement → ship-local correction → player movement → physics).

## Sub-Epics / Tasks

### Sub-Epic 3.3.1: Track Ship Transform Delta ✅
**Goal**: Deterministic ship motion delta per fixed tick.
**Tasks**:
- [x] Add `PreviousShipTransform` (or cached last-frame transform) on ship
- [x] Compute `delta = inverse(prev) * current` each fixed tick
- [x] Apply delta to all “in ship” occupants before movement runs

### Sub-Epic 3.3.2: Occupant Attachment Rules ✅
**Goal**: Correctly decide when player is attached vs free-floating.
**Tasks**:
- [x] Attach when `PlayerState.Mode == InShip || Piloting`
- [x] Detach when:
  - [x] leaving ship (Epic 3.1)
  - [x] knocked down and ejected (future - handled via PlayerMode)
  - [x] severe breach / decompression (Epic 3.6 - handled via PlayerMode)

### Sub-Epic 3.3.3: NetCode Reconciliation Strategy ✅
**Goal**: Minimize visible snapping for remote players.
**Tasks**:
- [x] Ensure the correction runs in `PredictedFixedStepSimulationSystemGroup`
- [x] Quantize replicated local-space fields if replicating them (`LocalPosition`, `LocalRotation`)
- [x] Add a small client-side smoothing window for mispredictions (pattern similar to Epic 7.5)

### Sub-Epic 3.3.4: QA Checklist
**Tasks**:
- [ ] Ship accelerates/rotates; idle player does not drift
- [ ] Player walks inside ship while ship rotates; movement stays stable
- [ ] High latency (100ms+): remote players don’t jitter excessively

## Acceptance Criteria
- [x] Standing still inside a moving ship does not drift over time
- [x] Rotating ship does not cause player jitter or violent impulses
- [x] Server/client remain visually consistent (no “teleport wobble” on remote clients)

## Files (Implemented)
```
Assets/Scripts/Runtime/Ship/LocalSpace/
├── Components/
│   └── ShipLocalSpaceComponents.cs
├── Systems/
│   ├── ShipLocalSpaceAttachmentSystem.cs
│   ├── ShipLocalSpaceTransformSystem.cs
│   ├── ShipMovementSystem.cs
│   ├── PlayerModeLocalSpaceSystem.cs
│   └── LocalSpaceSmoothingSystem.cs
└── Authoring/
    └── ShipLocalSpaceAuthoring.cs
```

---

# Implementation Guide

> **Status**: ✅ IMPLEMENTED  
> **Last Updated**: December 2024

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        TICK START                                        │
├─────────────────────────────────────────────────────────────────────────┤
│  ShipTransformTrackingSystem    - Capture "before movement" state        │
├─────────────────────────────────────────────────────────────────────────┤
│                     PREDICTED FIXED STEP                                 │
├─────────────────────────────────────────────────────────────────────────┤
│  ShipMovementSystem             - Apply helm input to ship kinematics   │
│           ↓                                                              │
│  ShipInertialCorrectionSystem   - Convert local→world for occupants     │
│           ↓                                                              │
│  [Player Movement Systems]      - Normal movement in world space        │
│           ↓                                                              │
│  [Physics Step]                                                          │
│           ↓                                                              │
│  ShipLocalSpaceCaptureSystem    - Convert world→local after movement    │
├─────────────────────────────────────────────────────────────────────────┤
│                        TICK END                                          │
├─────────────────────────────────────────────────────────────────────────┤
│  ShipTransformStoreSystem       - Store for next tick's delta           │
└─────────────────────────────────────────────────────────────────────────┘
```

## System Execution Order

1. **ShipTransformTrackingSystem** (OrderFirst) - Capture previous state
2. **ShipMovementSystem** - Apply helm input to ship
3. **ShipInertialCorrectionSystem** - Apply ship transform to occupants
4. **[Player Movement]** - Normal movement systems
5. **[Physics]** - Unity Physics step
6. **ShipLocalSpaceCaptureSystem** - Capture local position after movement
7. **ShipTransformStoreSystem** (OrderLast) - Store for next tick

## For Designers: Setting Up Ships

### Step 1: Create Ship Root GameObject

1. Create empty GameObject as ship root
2. Add `ShipRootAuthoring` component
3. Configure:
   - **Ship ID**: Unique identifier
   - **Ship Name**: Display name
   - **Max Linear Speed**: Default 50 m/s
   - **Max Angular Speed**: Default 2 rad/s

### Step 2: Add Helm Station

1. Create helm station as child of ship
2. Add `StationAuthoring` with Type = Helm
3. Configure interaction point

### Step 3: Add Networking Components (REQUIRED)

For the ship visual to sync with the ECS entity position, add these components to the ship root:

1. **GhostAuthoringComponent** (Unity.NetCode)
   - Makes the ship a networked entity
   - Enables replication across server/clients
   
2. **GhostPresentationGameObjectAuthoring** (Unity.NetCode.Hybrid)
   - Links the ECS entity to the visual GameObject
   - Required for `ShipVisualSyncSystem` to update the visual
   - Set the `Presentation` field to the ship's visual mesh GameObject

**Without these components, the ship entity will move in ECS but the visual GameObject will remain stationary.**

### Player Setup

1. Add `ShipLocalSpacePlayerAuthoring` to player prefab
2. Configure smoothing duration (default: 0.1s)

## Component Reference

| Component | Entity | Purpose |
|-----------|--------|---------|
| `ShipRoot` | Ship | Identifies ship root entity |
| `ShipKinematics` | Ship | Velocity state |
| `ShipPreviousTransform` | Ship | For delta calculation |
| `ShipOccupant` | Ship (buffer) | List of occupants |
| `InShipLocalSpace` | Player | Local position/rotation |
| `LocalSpaceSmoothing` | Player | Misprediction smoothing |
| `AttachToShipRequest` | Player | Request to attach |
| `DetachFromShipRequest` | Player | Request to detach |

## How Local Space Works

1. **Attach**: When player enters ship (Mode = InShip/Piloting)
   - World position converted to ship-local coordinates
   - `InShipLocalSpace` component added

2. **Each Tick**:
   - `ShipInertialCorrectionSystem` converts local → world
   - Player movement runs in world space
   - `ShipLocalSpaceCaptureSystem` converts world → local

3. **Detach**: When player exits ship (Mode = EVA/Dead)
   - World position preserved
   - `InShipLocalSpace` component removed

## Integration with Epic 3.2 (Stations)

The `ShipMovementSystem` reads `StationInput` from helm stations:

| Input | Ship Response |
|-------|---------------|
| Move.y | Forward/backward thrust |
| Move.x | Yaw (turn left/right) |
| Look.y | Pitch |
| Look.x | Roll (reduced) |
| Primary | Boost (2x thrust) |
| Secondary | Brake (reduce velocity) |
| Modifier | Vertical thrust (up) |

---

## Test Ship Creator

An editor utility is provided for quickly spawning test ships.

### Menu Location
```
GameObject > DIG - Test Objects > Ships
```

### Available Options

| Option | Description |
|--------|-------------|
| **Complete Test Ship** | Full ship with hull, helm, drill station, systems panel, and airlock |
| **Basic Ship (No Stations)** | Hull and interior only, no interactable stations |
| **Ship Hull Only** | Just the exterior walls for testing |

### Complete Test Ship Layout

```
     ┌────────────────────────────────────────┐
     │              Ship Interior              │
     │                                         │
     │    [Drill]            [Systems]         │
     │       ●                  ●              │
     │                                         │
     │              [Console]                  │
     │                                         │
     │                [Helm]                   │
     │                  ●                      │
     │                                         │
     └────────────────[Airlock]───────────────┘
                         ↓
                      [Space]
```

### What Gets Created

| Element | Components Added |
|---------|-----------------|
| Ship Root | `ShipRootAuthoring` (ID, name, max speeds) |
| Hull | Floor, walls, ceiling (Unity primitives) |
| Helm Station | `StationAuthoring` (Type=Helm), seat, interaction point |
| Drill Station | `StationAuthoring` (Type=DrillControl), control panel |
| Systems Panel | `StationAuthoring` (Type=SystemsPanel), buttons |
| Airlock | `AirlockAuthoring`, interior/exterior doors, spawn points |

**⚠️ Important:** After creating a test ship, you must manually add:
- `GhostAuthoringComponent` to the ship root
- `GhostPresentationGameObjectAuthoring` to the ship root (set Presentation field to the hull GameObject)

These are required for the ship visual to sync with the ECS entity position when piloting.

### How to Test

1. **Create Ship**: GameObject → DIG - Test Objects → Ships → Complete Test Ship
2. **Add Player**: Ensure player prefab has `ShipLocalSpacePlayerAuthoring` and `StationPlayerAuthoring`
3. **Enter Play Mode**
4. **Test Helm**: Walk to helm, press T to enter, use WASD to fly ship
5. **Test Airlock**: Walk to airlock, press T to cycle and exit to EVA

