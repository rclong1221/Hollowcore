# EPIC 3.2: Ship Stations (Helm / Drill / Weapons) and Control Handoff

**Priority**: HIGH  
**Goal**: Players can operate ship stations with clear ownership rules, input routing, and camera behavior (pilot vs on-foot).  
**Dependencies**: Epic 1.2 (Camera), Epic 1.4 (`PlayerState.Mode`), Epic 2.x (tools/resources as station outputs)

## Design Notes (Match EPIC7 Level of Detail)
- **Occupancy is a lock**: each station has one authoritative operator (`CurrentOperator`), set/cleared by server only.
- **Input routing**: when operating, player input no longer drives on-foot movement; it drives `StationInput`.
- **Camera is client presentation**: authoritative state is “operating station X”; camera swaps are client-only reactions.
- **Failure handling**: on disconnect/death/airlock exit, server must clear station operator to avoid stuck stations.
- **NetCode**: treat station enter/exit as request-buffer actions (same pattern as tools/throwables/explosives).

## Scope
- Interaction + occupy/leave flow for stations
- Server-authoritative operator assignment
- Input routing: player input → station input while occupied
- Camera: switch to ship/pilot camera target when piloting

## Components

**OperableStation** (IComponentData, on station entity)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `Type` | StationType | Yes | Helm / DrillControl / WeaponStation / SystemsPanel |
| `InteractionPoint` | float3 | No | Where the player snaps to |
| `InteractionForward` | float3 | No | Facing direction while operating |
| `Range` | float | No | Max distance to operate |
| `CurrentOperator` | Entity | Yes | Player operating (Entity.Null if none) |

**OperatingStation** (IComponentData, on player)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `StationEntity` | Entity | Yes | Station being operated |
| `StationType` | StationType | Yes | Cached type |
| `IsOperating` | bool | Yes | Convenience flag |

**StationUseRequest** (IBufferElementData, on player; server-consumed)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `StationEntity` | Entity | Yes | Target station |
| `Action` | StationUseAction | Yes | Enter / Exit |
| `ClientTick` | uint | Yes | Ordering/anti-spam |

**StationInput** (IComponentData, on station; predicted or server-only depending on architecture)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `Move` | float2 | Quantization=100 | Stick/wasd (helm) |
| `Look` | float2 | Quantization=100 | Aim (weapons) |
| `Primary` | byte | Yes | Fire/drill |
| `Secondary` | byte | Yes | Alt |

## Systems

**StationPromptSystem** (PresentationSystemGroup, ClientWorld)
- Shows prompt when near station and not occupied (or “Press E to leave” when operating)

**StationUseRequestSystem** (PredictedSimulationSystemGroup, ClientWorld)
- Appends `StationUseRequest` on interact

**StationOccupancySystem** (SimulationSystemGroup, ServerWorld)
- Validates range + station availability
- Assigns/removes `OperableStation.CurrentOperator` and `OperatingStation`
- Updates `PlayerState.Mode` to `Piloting` (or `InShip`) while operating

**StationInputRoutingSystem** (PredictedSimulationSystemGroup, ClientWorld + ServerWorld)
- When `OperatingStation.IsOperating`:
  - consume player input and write to `StationInput`
  - optionally suppress on-foot movement input

## Camera Notes
- While operating helm/weapons: set camera target to ship/pilot target (Epic 1.2 camera system already supports arbitrary targets).
- Keep gameplay authority in ECS; camera smoothing can stay hybrid on client.

## Sub-Epics / Tasks

### Sub-Epic 3.2.1: Station Authoring + Data Model ✅
**Goal**: Stations are easy to place and configure in scenes/prefabs.
**Tasks**:
- [x] `StationAuthoring` with:
  - [x] `StationType`
  - [x] `InteractionPoint` + `InteractionForward` (local-space authoring converted to world/ship-space)
  - [x] `Range`
  - [x] optional camera target reference (for client camera)
- [x] Decide which fields replicate (operator + type) vs remain static (points/range baked)

### Sub-Epic 3.2.2: Occupancy + Enter/Exit Rules (Server) ✅
**Goal**: Deterministic behavior under contention and bad states.
**Tasks**:
- [x] Validation on enter:
  - [x] in range
  - [x] station is not occupied
  - [x] player is `InShip` and alive
- [x] Validation on exit:
  - [x] player is current operator OR station is invalid (failsafe)
- [x] Failsafes:
  - [x] if operator entity becomes invalid → clear `CurrentOperator`
  - [x] if operator leaves ship (Epic 3.1) → auto-exit station

### Sub-Epic 3.2.3: Input Routing (Client Predicted + Server Authoritative) ✅
**Goal**: Local feels responsive; server stays authoritative.
**Tasks**:
- [x] When operating:
  - [x] suppress on-foot movement systems (or gate them on `OperatingStation.IsOperating == false`)
  - [x] write `StationInput` from `PlayerInput`
- [x] Ensure remote observers see correct station outputs via replicated ship/station state (not via raw input replication)
- [x] Add rate limiting / deadzone for `StationInput` fields to avoid noisy snapshots

### Sub-Epic 3.2.4: Station Types (MVP vs Expansion) ⚠️ Framework Only
**Goal**: Define "what ships can do" through stations.
**Tasks**:
- [x] Helm:
  - [x] translate `StationInput.Move` to ship thrust/yaw requests (input mapping complete)
  - [ ] Ship physics response → **Epic 3.3** (Ship Local Space) - ship kinematics must exist before helm can drive movement
- [ ] Drill control:
  - [ ] hook to voxel/extraction pipeline → **Epic 2.5** (Explosives, voxel damage already implemented) + future Voxel Epic for continuous drilling
  - [ ] Resource yield from drilling → **Epic 2.6** (Resource Collection) - already implemented, needs integration
- [ ] Weapons:
  - [ ] aim + fire; spawn/projectiles server-side → **No current Epic** - requires new Epic 5.x (Ship Combat) to be defined
  - [ ] Projectile damage → **Epic 4.1** (Health, Damage Events) - damage pipeline ready
- [ ] Systems panel:
  - [ ] toggles for power/life support → **Epic 3.5** (Ship Power, Life Support)

### Sub-Epic 3.2.5: QA Checklist
**Tasks**:
- [ ] Two players spam same station; only one is accepted; no stuck operator
- [ ] Operator dies/disconnects; station clears within 1 tick (or fixed timeout)
- [ ] Camera switches for local operator only; remote clients do not see camera changes
- [ ] Input suppression: on-foot movement never runs while operating

## Files (Implemented)
- `Assets/Scripts/Runtime/Ship/Stations/Components/StationComponents.cs`
- `Assets/Scripts/Runtime/Ship/Stations/Systems/StationUseRequestSystem.cs`
- `Assets/Scripts/Runtime/Ship/Stations/Systems/StationOccupancySystem.cs`
- `Assets/Scripts/Runtime/Ship/Stations/Systems/StationInputRoutingSystem.cs`
- `Assets/Scripts/Runtime/Ship/Stations/Systems/StationPromptSystem.cs`
- `Assets/Scripts/Runtime/Ship/Stations/Systems/StationCameraSystem.cs`
- `Assets/Scripts/Runtime/Ship/Stations/Camera/StationCameraController.cs`
- `Assets/Scripts/Runtime/Ship/Stations/Authoring/StationAuthoring.cs`

## Acceptance Criteria
- [x] Only one operator per station; server rejects contested requests deterministically
- [x] Enter/exit station feels immediate for local player and stable for remote observers
- [x] While operating, on-foot movement is suppressed and input drives station behavior
- [x] Camera switches to appropriate target while piloting and returns on exit

## Suggested File Structure
```
Assets/Scripts/Runtime/Ship/Stations/
├── Components/
│   └── StationComponents.cs
├── Systems/
│   ├── StationUseRequestSystem.cs
│   ├── StationOccupancySystem.cs
│   ├── StationInputRoutingSystem.cs
│   ├── StationPromptSystem.cs
│   └── StationCameraSystem.cs
├── Camera/
│   └── StationCameraController.cs
└── Authoring/
    └── StationAuthoring.cs
```

---

# Implementation Guide

> **Status**: ✅ IMPLEMENTED (Core Framework)  
> **Last Updated**: December 2024

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           CLIENT                                         │
├─────────────────────────────────────────────────────────────────────────┤
│  StationPromptSystem     StationUseRequestSystem    StationCameraSystem  │
│  (UI prompts)            (sends requests)           (camera control)     │
│                                                                          │
│  StationInputRoutingSystem (writes to StationInput from PlayerInput)     │
├─────────────────────────────────────────────────────────────────────────┤
│                           NETWORK                                        │
├─────────────────────────────────────────────────────────────────────────┤
│                    StationOccupancySystem (ServerWorld)                  │
│                    - Validates requests                                  │
│                    - Assigns CurrentOperator                             │
│                    - Updates PlayerState.Mode                            │
│                    - Handles edge cases                                  │
└─────────────────────────────────────────────────────────────────────────┘
```

## For Designers: Setting Up Stations

### Step 1: Create Station GameObject

1. Create empty GameObject where station should be
2. Name descriptively: `Helm_Main`, `DrillControl_Port`, `Weapons_Turret1`
3. Position at the station's interaction area

### Step 2: Add Station Authoring

1. Add `StationAuthoring` component
2. Configure:
   - **Type**: `Helm`, `DrillControl`, `WeaponStation`, `SystemsPanel`
   - **Interaction Point**: Child transform where player stands/sits
   - **Interaction Range**: 2-3 meters typical
   - **Prompts**: Customize text for each state

### Step 3: Create Interaction Point

1. Create child GameObject: `InteractionPoint`
2. Position where player should stand/sit
3. Rotate to face the correct direction
4. Assign to `Interaction Point` field

### Step 4: Camera Target (Optional)

1. Create child GameObject: `CameraTarget`
2. Position for ideal operating view
3. Add `StationCameraTargetAuthoring` component
4. Assign to station's `Camera Target` field

### Player Setup

1. Add `StationPlayerAuthoring` to player prefab
2. Configure debounce tick count

## Component Reference

| Component | Entity | Purpose |
|-----------|--------|---------|
| `OperableStation` | Station | Main station data, current operator |
| `OperatingStation` | Player | Added when operating a station |
| `StationUseRequest` | Player (buffer) | Client requests to enter/exit |
| `StationInput` | Station | Input state from operator |
| `StationInteractable` | Station | Prompt configuration |
| `StationPromptState` | Player | Client UI state |
| `StationDisabled` | Station | Tag for disabled stations |
| `StationCameraOverride` | Player | Camera settings backup |

## Station Type Input Mapping

| Station Type | Move Input | Look Input | Primary | Secondary |
|--------------|------------|------------|---------|-----------|
| **Helm** | Thrust/Yaw | Pitch/Roll | Boost | Brake |
| **DrillControl** | Aim | Fine Aim | Drill | Toggle Mode |
| **WeaponStation** | - | Aim | Fire | Alt-Fire |
| **SystemsPanel** | Navigate | - | Select | Back |

## Extending with New Station Types

1. Add new value to `StationType` enum
2. Add input mapping case in `StationInputRoutingSystem`
3. Add camera handling in `StationCameraSystem`
4. Add UI in `StationCameraController`
5. Create behavior system (e.g., `ShipMovementSystem` for Helm)

