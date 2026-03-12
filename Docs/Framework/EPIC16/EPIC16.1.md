# EPIC 16: Universal Interaction Framework

**Status:** Planning
**Priority:** High (Core Infrastructure)
**Dependencies:**
- `DIG.Interaction` (EPIC 13.17 — Existing)
- `InputSystem`
- `Unity.NetCode`
- `Unity.Physics`

**Feature:** A composable, high-performance interaction framework that covers every interaction archetype across game genres — from instant pickups to lockpicking minigames, Helldivers stratagems, crafting stations, vehicle mounting, NPC dialogue, and cooperative interactions.

**Supersedes:** EPIC 15.23 (Advanced Interaction Framework — spatial hashing absorbed into Phase 1)

---

## Overview

### Problem

The existing `DIG.Interaction` system (EPIC 13.17) is mature and production-ready for **basic interactions** — pressing F to open a door, holding E to loot, toggling a lever. It covers 4 of the 14 mechanically distinct interaction archetypes found across AAA games.

The remaining 10 archetypes — crafting stations, sequential puzzles, input combos, vehicle mounting, proximity effects, minigames, async processing, and cooperative actions — require new infrastructure that the current `InteractableType` enum cannot express.

### Solution

Rather than expanding `InteractableType` from 5 values to 15+, we use **composable ECS dimension components**. Each interaction dimension (how it activates, how long it lasts, what input it needs, how it locks the player) is an **optional ECS component** that can be mixed and matched.

**Examples:**
- **Crafting Station** = `Interactable` + `InteractionSession` + `AsyncProcessingState`
- **Lockpick** = `Interactable(Timed)` + `InputSequenceState` + `MinigameSession`
- **Helldivers Stratagem** = `Interactable(Instant)` + `InputSequenceState`
- **Co-op Breach** = `Interactable(Timed)` + `CoopInteraction(RequiredPlayers=2)`
- **Turret Seat** = `Interactable(Instant)` + `MountPoint` + `MountState`
- **Campfire Heal** = `ProximityZone(Radius=3, Effect=Heal)`
- **Bomb Defusal** = `Interactable(Timed)` + `InteractionPhaseSequence(3 steps)` + `InputSequenceState`

### Principles

1. **Extend, don't replace** — All existing `DIG.Interaction` code stays untouched. New archetypes compose alongside existing components.
2. **Dimensions > Enums** — Orthogonal component composition instead of combinatorial enum explosion.
3. **BlobAssets for definitions** — Sequence patterns and phase definitions stored as BlobAssets (Burst-friendly, no `IBufferElementData` on ghost entities).
4. **Managed bridges for UI** — Minigames and station UIs follow the existing `InteractableHybridBridgeSystem` static-registry pattern.
5. **Server-authoritative** — All state transitions validated on server. Clients predict where safe.

---

## The 14 Interaction Archetypes

| # | Archetype | Game Examples | Status | Phase |
|---|-----------|--------------|--------|-------|
| 1 | **Instant Trigger** | Pick up item, flip switch, press button | COVERED | — |
| 2 | **Timed Channel** | Revive teammate, hack terminal, drink potion | COVERED | — |
| 3 | **Toggle State** | Light switch, valve, generator on/off | COVERED | — |
| 4 | **Continuous Channel** | Mining drill, healing beam, drinking fountain | PARTIAL | — |
| 5 | **Station Session** | Crafting bench, vendor shop, computer terminal, cockpit UI | GAP | 2 |
| 6 | **Multi-Phase Sequential** | Bomb defusal (cut wire → enter code → flip switch) | GAP | 3 |
| 7 | **Input Sequence** | Helldivers stratagems, lockpick combos, QTE, rhythm inputs | GAP | 3 |
| 8 | **Spatial Placement** | Place turret, deploy trap, build wall segment | GAP | 6 |
| 9 | **Mount/Seat** | Vehicle driver/gunner, turret, chair, ladder, zipline | GAP | 4 |
| 10 | **Proximity Zone** | Campfire heal, shop aura, buff zone, radiation area | GAP | 5 |
| 11 | **Ranged Initiation** | Fishing cast, grappling hook, thrown rope | GAP | 6 |
| 12 | **Minigame** | Hacking puzzle, lockpick skill game, wire matching, pipe puzzle | GAP | 5 |
| 13 | **Async Processing** | Smelting furnace, fermenting barrel, crafting queue | GAP | 2 |
| 14 | **Cooperative/Synchronized** | 2-player doors, team revive, synchronized levers | GAP | 7 |

---

## Architecture: Composable Dimensions

### The 7 Interaction Dimensions

Each dimension is an **optional ECS component**. An interactable's behavior emerges from which dimension components it has.

| Dimension | Component(s) | Values / Range |
|-----------|-------------|----------------|
| **Activation** | `Interactable.Type` (existing) | Instant, Timed, Toggle, Animated, Continuous |
| **Session** | `InteractionSession` | None (default), UI Panel, Full Screen, World-Space |
| **Input Complexity** | `InputSequenceState` | None (default), Button Sequence, Directional Combo, Rhythm |
| **Player Locking** | `SessionLockState` | Free (default), Position-Locked, Camera-Locked, Fully-Seated |
| **Phase Sequencing** | `InteractionPhaseSequence` | Single-phase (default), Multi-phase with BlobAsset definition |
| **Multiplayer** | `CoopInteraction` | Solo (default), Required N players, Competitive |
| **Persistence** | `AsyncProcessingState` | Ephemeral (default), Timed Processing, Persistent State |

### Data Flow

```
[Interactable Entities]
        |
        v
[InteractableSpatialMapSystem] ──> [NativeMultiHashMap<GridID, Entity>]
        |
        v
[InteractableDetectionSystem] ──> Query 9 Grid Cells ──> Scoring ──> InteractAbility.TargetEntity
        |
        v
[InteractAbilitySystem] ──> Start/Cancel/Progress (existing)
        |
        ├──> [PhaseSequenceSystem] ──> Advance through multi-phase definitions
        ├──> [InputSequenceSystem] ──> Validate button/directional inputs
        ├──> [StationSessionSystem] ──> Enter/Exit UI sessions
        ├──> [MountSystem] ──> Seat player, transfer control
        ├──> [AsyncProcessingSystem] ──> Queue/Timer management
        ├──> [CoopInteractionSystem] ──> Multi-player synchronization
        |
        v
[InteractableHybridBridgeSystem] ──> Managed UI / Audio / Minigames
```

---

## Phase 1: Spatial Hashing (Performance Foundation)

*Absorbs EPIC 15.23 — Advanced Interaction Framework*

### Problem
The current `InteractableDetectionSystem` iterates **all** interactable entities (O(N)) per player per frame. This fails at >1,000 objects. Dense environments (loot rooms, cities) need O(1) detection.

### Components

```
InteractableSpatialIdx : IComponentData
    int LastGridID          // Cached grid cell to minimize hash map updates
    bool IsDirty            // Needs re-hash (moved since last frame)
```

### Systems

- **`InteractableSpatialMapSystem`** (SimulationSystemGroup, before DetectionSystem)
    - Maintains `NativeMultiHashMap<int, Entity>` singleton
    - Grid size: 2m (tunable via config)
    - Key = `floor(Pos.x / CellSize) + floor(Pos.z / CellSize) * GridWidth`
    - Only re-hashes entities whose `IsDirty` flag is set (most interactables are static)
    - Handles entity creation/destruction via structural change detection

- **`InteractableDetectionSystem`** (Rewrite)
    - Replace `SystemAPI.Query<Interactable>` full scan
    - Query player's grid cell + 8 neighbors (9 cells total)
    - Score: `(1.0 - Distance/Range) * 0.4 + (1.0 - Angle/Cone) * 0.6 + PriorityBonus`
    - **Sticky Target:** Current target gets +10% score bonus to prevent flickering
    - Cone + Raycast LOS check (existing logic, just on fewer candidates)
    - Burst-compatible, Jobs-friendly

### Interaction Context (Rich Prompts)

```
InteractionVerb : byte enum
    Interact, Loot, Open, Close, Revive, Breach, Talk, Use, Craft,
    Mount, Dismount, Place, Pickup, Activate, Deactivate

InteractableContext : IComponentData
    InteractionVerb Verb
    FixedString32Bytes ActionNameKey  // Localization key
    bool RequireLineOfSight           // Override per-interactable (default: true)
```

### UI & Feel

- **Prompt Clamping:** Clamp UI prompt position to 90% screen bounds (title-safe area)
- **Audio Stages:** OnHover (subtle click), OnStart (button press), OnComplete (chime), OnFail (locked rattle)
- **Haptics:** Hover = weak low-freq 1 frame, Hold = increasing intensity, Complete = sharp high-freq pop
- **Visual Polish:** Prompt completion animation: Scale Up → Flash White → Scale Down (EaseInBack)

### Implementation Tasks

- [ ] Create `InteractableSpatialIdx` component
- [ ] Implement `InteractableSpatialMapSystem` with `NativeMultiHashMap`
- [ ] Rewrite `InteractableDetectionSystem` to use spatial hash + 9-cell query
- [ ] Add sticky target hysteresis scoring
- [ ] Create `InteractionVerb` enum and `InteractableContext` component
- [ ] Add `InteractionContextAuthoring` MonoBehaviour
- [ ] Update `InteractionPromptSystem` to read `InteractableContext`
- [ ] Implement prompt screen-space clamping
- [ ] Add OnHover/OnStart/OnComplete/OnFail audio hooks
- [ ] **Test:** 10,000 interactables, verify < 0.1ms detection update
- [ ] **Test:** Verify LOS blocking through thin walls (raycast center + eye)

---

## Phase 2: Station Sessions & Async Processing

### Problem
Games need "enter a UI" interactions — crafting benches, vendor shops, computer terminals, cockpit controls. The player walks up, presses interact, and a full UI panel opens. The player is locked in place (or can walk away to cancel). Some stations also process items over time (smelting, fermenting).

### Components

```
InteractionSession : IComponentData [Ghost: All]
    SessionType SessionType         // UIPanel, FullScreen, WorldSpace
    bool IsOccupied                 // Someone is using this station (ghosted)
    Entity OccupantEntity           // Who is using it (ghosted)
    int SessionID                   // Links to managed UI prefab registry
    bool AllowConcurrentUsers       // Multiple players can use simultaneously

SessionType : byte enum
    UIPanel         // Opens a panel overlay (crafting bench)
    FullScreen      // Takes over the screen (computer terminal)
    WorldSpace      // UI appears in world space near the object (vending machine)

SessionLockState : IComponentData [Ghost: AllPredicted]
    bool LockPosition               // Player can't move
    bool LockCamera                 // Camera fixed on station
    bool LockAbilities              // Disable combat/abilities
    bool ShowPlayerModel            // Keep player visible or hide
    float3 SeatPosition             // Where to place the player
    quaternion SeatRotation         // Player facing direction

AsyncProcessingState : IComponentData [Ghost: All]
    float ProcessingTimeTotal       // Total time for current batch (ghosted)
    float ProcessingTimeElapsed     // Time elapsed (ghosted)
    bool IsProcessing               // Active processing (ghosted)
    int RecipeIndex                 // Links to RecipeDatabaseBlob (EPIC 11.7)
    int InputItemCount              // Items deposited
    int OutputItemCount             // Items ready for collection
    float FuelRemaining             // Optional fuel mechanic
```

### Systems

- **`StationSessionSystem`** (SimulationSystemGroup, Client|Server)
    - **Enter:** On interaction complete → set `IsOccupied`, `OccupantEntity`, apply `SessionLockState` to player
    - **Exit:** On cancel input or walk-away distance → clear occupant, release locks
    - **Validation:** Server checks: station not occupied (or AllowConcurrent), player in range
    - **UI Trigger:** Sets enableable `SessionUIActive` tag → managed bridge opens UI prefab

- **`AsyncProcessingSystem`** (SimulationSystemGroup, Server)
    - Ticks `ProcessingTimeElapsed` each frame for active processors
    - When `Elapsed >= Total`: mark output ready, fire `ProcessingCompleteEvent`
    - Continues when player walks away (server-side only)
    - Integration: reads `RecipeDatabaseBlob` from EPIC 11.7 `RecipeDatabaseSystem`

- **`StationSessionBridgeSystem`** (Managed, PresentationSystemGroup, ClientSimulation)
    - Static registry: `SessionID` → `GameObject` UI prefab
    - Opens/closes UI panels based on `SessionUIActive` tag
    - Passes `AsyncProcessingState` data to UI for progress display

### Authoring

- **`StationAuthoring`** — Session type, lock settings, seat position, UI prefab reference
- **`AsyncProcessorAuthoring`** — Processing time, fuel requirement, recipe filter

### Implementation Tasks

- [ ] Create `InteractionSession`, `SessionLockState`, `AsyncProcessingState` components
- [ ] Implement `StationSessionSystem` (enter/exit, occupancy, locks)
- [ ] Implement `AsyncProcessingSystem` (timer, completion, server-only)
- [ ] Create `StationSessionBridgeSystem` (managed UI bridge)
- [ ] Create `StationAuthoring` and `AsyncProcessorAuthoring` MonoBehaviours + Bakers
- [ ] Wire `AsyncProcessingSystem` to EPIC 11.7 `RecipeDatabaseBlob`
- [ ] **Test:** Crafting bench — enter session, deposit items, start processing, walk away, return, collect
- [ ] **Test:** Two players cannot use same station (unless AllowConcurrent)
- [ ] **Test:** Server-authoritative: client can't fake crafting completion

---

## Phase 3: Multi-Phase Sequences & Input Sequences

### Problem
Complex interactions require **multiple steps** (bomb defusal: cut wire → enter code → flip switch) or **specific input patterns** (Helldivers stratagems: ↑↓←→↑, lockpick combos, QTE button prompts).

### Components

```
// ─── Multi-Phase ───

InteractionPhaseConfig : IComponentData
    BlobAssetReference<PhaseSequenceBlob> Phases  // Burst-friendly phase definitions
    int CurrentPhase                               // Active phase index (ghosted)
    int TotalPhases                                // Cached from blob
    bool ResetOnFail                               // Restart from phase 0 on failure
    float PhaseTimeout                             // Max time per phase (0 = no limit)
    float PhaseTimeElapsed                         // Timer for current phase

PhaseSequenceBlob : BlobAsset
    BlobArray<PhaseDefinition> Phases

PhaseDefinition : struct
    InteractableType PhaseType      // Instant, Timed, or InputSequence for this step
    float Duration                  // For Timed phases
    FixedString32Bytes PromptKey    // Localization key for this step's prompt
    int InputSequenceIndex          // If this phase uses input sequence, index into sequence blob

// ─── Input Sequences ───

InputSequenceConfig : IComponentData
    BlobAssetReference<InputSequenceBlob> Sequences  // All sequence definitions
    int ActiveSequenceIndex                           // Which sequence is active (-1 = none)

InputSequenceState : IComponentData [Ghost: AllPredicted]
    int CurrentInputIndex           // Progress through sequence (ghosted)
    int TotalInputs                 // Length of active sequence
    float InputTimeout              // Max time between inputs (0 = no limit)
    float TimeSinceLastInput        // Timer
    bool SequenceComplete           // All inputs matched (ghosted)
    bool SequenceFailed             // Wrong input / timeout (ghosted)
    InputSequenceMode Mode          // ButtonSequence, DirectionalCombo, Rhythm, QTE

InputSequenceMode : byte enum
    ButtonSequence      // Press specific buttons in order (door codes)
    DirectionalCombo    // D-pad/stick directions (Helldivers stratagems)
    Rhythm              // Timed inputs to a beat (rhythm minigames)
    QTE                 // Random prompts with time windows (QTE events)

InputSequenceBlob : BlobAsset
    BlobArray<SequenceDefinition> Sequences

SequenceDefinition : struct
    BlobArray<InputStep> Steps
    float BaseTimeout               // Default time between inputs
    bool AllowPartialCredit         // Some games give partial success

InputStep : struct
    InputAction RequiredInput       // Which button/direction
    float TimeWindow                // For Rhythm/QTE: acceptable timing window
    float BeatTime                  // For Rhythm: when this beat should land
```

### Systems

- **`PhaseSequenceSystem`** (PredictedSimulationSystemGroup, Client|Server)
    - Reads `InteractionPhaseConfig` on active interactions
    - Advances `CurrentPhase` when phase completion conditions met
    - Each phase may delegate to `InputSequenceSystem` or use existing Timed/Instant logic
    - Fires `PhaseAdvanceEvent` for UI/audio feedback
    - On final phase complete: triggers normal interaction completion

- **`InputSequenceSystem`** (PredictedSimulationSystemGroup, Client|Server)
    - Reads player input each tick, compares to expected `InputStep`
    - Advances `CurrentInputIndex` on correct input
    - Sets `SequenceFailed` on wrong input or timeout
    - Sets `SequenceComplete` when all inputs matched
    - Predicted on client for responsive feel, validated on server

### Authoring

- **`MultiPhaseAuthoring`** — Define phases as a list in inspector (type, duration, prompt per phase)
- **`InputSequenceAuthoring`** — Define button/direction sequences, timeout, mode
- Baker creates `BlobAsset` at bake time from inspector lists

### Implementation Tasks

- [ ] Define `PhaseSequenceBlob` and `InputSequenceBlob` BlobAsset structures
- [ ] Create `InteractionPhaseConfig`, `InputSequenceConfig`, `InputSequenceState` components
- [ ] Implement `PhaseSequenceSystem` (phase advancement, timeout, reset)
- [ ] Implement `InputSequenceSystem` (input matching, timeout, prediction)
- [ ] Create `MultiPhaseAuthoring` with inspector phase list → BlobAsset baker
- [ ] Create `InputSequenceAuthoring` with inspector sequence list → BlobAsset baker
- [ ] Create UI bridge for sequence prompts (directional arrows, button icons, progress)
- [ ] **Test:** 3-phase bomb defusal (cut → code → flip)
- [ ] **Test:** Helldivers-style stratagem (↑↓←→↑) with timeout
- [ ] **Test:** Failed sequence resets to phase 0 when `ResetOnFail` enabled
- [ ] **Test:** Predicted input feels instant, server validates

---

## Phase 4: Mount/Seat

### Problem
Players need to sit in vehicles, man turrets, sit on chairs, ride ladders, and use ziplines. The player avatar is repositioned (or hidden), camera may change, and the player's input now controls the mounted object.

### Components

```
MountPoint : IComponentData [Ghost: All]
    MountType Type                  // Seat, Turret, Ladder, Zipline
    float3 SeatOffset               // Local-space offset from interactable
    quaternion SeatRotation         // Facing direction
    float3 DismountOffset           // Where player appears on exit
    Entity OccupantEntity           // Who is mounted (ghosted)
    bool IsOccupied                 // Seat taken (ghosted)
    bool HidePlayerModel            // Hide avatar while mounted
    bool TransferInputToMount       // Player input drives the mount entity
    int MountAnimationHash          // Mount enter animation trigger
    int DismountAnimationHash       // Mount exit animation trigger

MountType : byte enum
    Seat            // Vehicle seat, chair (fixed position, free look)
    Turret          // Turret seat (position fixed, aim controls turret)
    Ladder          // Ladder (up/down movement, fixed facing)
    Zipline         // Zipline (forward movement along spline)
    Passenger       // Passenger seat (no control, free look)

MountState : IComponentData [Ghost: AllPredicted]
    Entity MountedOn                // What entity the player is mounted on (ghosted)
    bool IsMounted                  // Active mount state (ghosted)
    bool IsTransitioning            // Mount/dismount animation playing (ghosted)
    float TransitionProgress        // 0-1 animation progress (ghosted)
    MountType ActiveMountType       // Cached type for quick checks
```

### Systems

- **`MountSystem`** (PredictedSimulationSystemGroup, Client|Server)
    - **Mount:** On interact → validate seat available → set `MountState.IsMounted`, apply seat position/rotation
    - **During:** If `TransferInputToMount`, redirect player movement input to mount entity
    - **Dismount:** On cancel/interact again → play exit animation → teleport to `DismountOffset`
    - **Visibility:** Toggle player model rendering based on `HidePlayerModel`
    - **Ladder/Zipline:** Special movement modes — translate along predefined axis/spline

### Integration

- Links to EPIC 3.2 Ship Stations for cockpit/turret seats
- Uses existing `MoveTowardsLocation` for smooth mount enter/exit positioning
- Uses existing `InteractAbility.BlockedAbilitiesMask` (EPIC 13.17.5) to disable combat while mounted

### Authoring

- **`MountPointAuthoring`** — Mount type, seat offset, dismount point, animation references, input transfer toggle
- Gizmos: visualize seat position and dismount point in Scene view

### Implementation Tasks

- [ ] Create `MountPoint` and `MountState` components
- [ ] Implement `MountSystem` (mount/dismount/transitions)
- [ ] Implement input redirection for `TransferInputToMount`
- [ ] Handle player model visibility toggling
- [ ] Implement ladder movement mode (up/down on axis)
- [ ] Implement zipline movement mode (forward along spline)
- [ ] Create `MountPointAuthoring` MonoBehaviour + Baker with gizmos
- [ ] Wire to EPIC 3.2 Ship Station seats
- [ ] **Test:** Mount turret, aim/fire redirected to turret entity
- [ ] **Test:** Two players cannot mount same single seat
- [ ] **Test:** Dismount places player at correct offset, not inside geometry

---

## Phase 5: Proximity Zones & Minigames

### Problem
Some interactions are passive — standing near a campfire heals you, entering a shop zone opens a vendor UI. Others require a dedicated UI minigame — hacking puzzles, lockpicking skill games, pipe routing.

### Components

```
// ─── Proximity Zones ───

ProximityZone : IComponentData [Ghost: All]
    float Radius                    // Detection radius
    ProximityEffect Effect          // What happens when inside
    float EffectInterval            // How often effect ticks (seconds)
    float EffectValue               // Magnitude (heal amount, damage, etc.)
    int MaxOccupants                // 0 = unlimited
    bool RequiresLineOfSight        // Must see the zone center
    bool ShowWorldSpaceUI           // Display radius indicator

ProximityEffect : byte enum
    None, Heal, Damage, Buff, Debuff, Shop, Dialogue, Custom

ProximityZoneOccupant : IBufferElementData  // On the zone entity (NOT ghost-replicated)
    Entity OccupantEntity
    float TimeInZone

// ─── Minigames ───

MinigameConfig : IComponentData [Ghost: All]
    int MinigameTypeID              // Links to managed minigame prefab registry
    float DifficultyLevel           // 0-1 difficulty scalar
    float TimeLimit                 // Max time to complete (0 = no limit)
    bool FailEndsInteraction        // Failing the minigame cancels the interaction
    int RewardTier                  // Quality of loot/result on success

MinigameState : IComponentData [Ghost: AllPredicted]
    bool IsActive                   // Minigame currently running (ghosted)
    bool Succeeded                  // Player completed successfully (ghosted)
    bool Failed                     // Player failed (ghosted)
    float TimeRemaining             // Countdown (ghosted)
    float Score                     // Optional score for graded minigames (ghosted)
```

### Systems

- **`ProximityZoneSystem`** (SimulationSystemGroup, Client|Server)
    - Spatial query: find players within `Radius` of each zone
    - Manage `ProximityZoneOccupant` buffer (add/remove on enter/exit)
    - Tick effects at `EffectInterval` (heal, damage, buff application)
    - Fire `ProximityZoneEnterEvent` / `ProximityZoneExitEvent` for UI/audio

- **`MinigameBridgeSystem`** (Managed, PresentationSystemGroup, ClientSimulation)
    - Static registry: `MinigameTypeID` → `GameObject` minigame prefab
    - When `MinigameState.IsActive` becomes true: instantiate minigame UI
    - Minigame UI writes results back to `MinigameState` via bridge
    - On success/fail: `InteractAbilitySystem` reads state and completes/cancels interaction
    - Server validates: client claims success → server checks if conditions actually met (for deterministic minigames)

### Authoring

- **`ProximityZoneAuthoring`** — Radius, effect type, interval, value, max occupants
    - Gizmos: wire sphere showing zone radius in Scene view
- **`MinigameAuthoring`** — Minigame type (prefab reference), difficulty, time limit, fail behavior

### Implementation Tasks

- [ ] Create `ProximityZone`, `ProximityZoneOccupant`, `MinigameConfig`, `MinigameState` components
- [ ] Implement `ProximityZoneSystem` (enter/exit detection, effect ticking)
- [ ] Implement `MinigameBridgeSystem` (managed UI lifecycle, result bridging)
- [ ] Create `ProximityZoneAuthoring` + Baker with radius gizmo
- [ ] Create `MinigameAuthoring` + Baker
- [ ] Create sample minigame: lockpicking (rotate pick + apply tension)
- [ ] Create sample minigame: hacking (match symbols / code breaking)
- [ ] **Test:** Campfire heals 5 HP/sec for all players within radius
- [ ] **Test:** Minigame failure cancels interaction when `FailEndsInteraction` enabled
- [ ] **Test:** Server validates minigame success (no client-side cheating)

### Note on `ProximityZoneOccupant`

This is an `IBufferElementData` on the **zone entity**, which is a scene-placed interactable — NOT a ghost-replicated player entity. This is safe because:
- Zone entities are server-owned scene objects
- The occupant buffer is server-only tracking (not replicated to clients)
- Zone effects are applied via existing systems (Health modification, buff application)

---

## Phase 6: Spatial Placement & Ranged Initiation

### Problem
Players need to place objects in the world (turrets, traps, building pieces) with a preview ghost and validation. Some interactions start from range — fishing, grappling hooks, thrown ropes.

### Components

```
// ─── Spatial Placement ───

PlaceableConfig : IComponentData
    Entity PreviewPrefab            // Ghost preview entity to instantiate
    float MaxPlacementRange         // How far from player
    float GridSnap                  // Snap-to-grid size (0 = free placement)
    bool RequireFlat                // Surface must be within angle tolerance
    float MaxSurfaceAngle           // Max degrees from flat (default: 30)
    bool RequireFoundation          // Must be placed on valid foundation
    PlacementValidation Validation  // How to validate placement

PlacementValidation : byte enum
    None            // Always valid
    NoOverlap       // Physics overlap check
    Foundation      // Must be on specific surface tag
    LineOfSight     // Must see placement point from player
    Custom          // Delegated to game-specific system

PlacementState : IComponentData [Ghost: AllPredicted]
    float3 PreviewPosition          // Where the preview is shown (ghosted)
    quaternion PreviewRotation      // Preview orientation (ghosted)
    bool IsValid                    // Current placement is valid (ghosted)
    bool IsPlacing                  // Player is in placement mode (ghosted)
    Entity PreviewEntity            // The preview ghost entity

// ─── Ranged Initiation ───

RangedInteraction : IComponentData [Ghost: All]
    float MaxRange                  // Maximum initiation range
    float ProjectileSpeed           // Speed of the initiation projectile (0 = instant raycast)
    Entity ProjectilePrefab         // Visual projectile (fishing line, grapple hook)
    bool RequireHit                 // Must hit target to initiate (fishing: hit water)
    RangedInitType InitType         // How the ranged interaction starts

RangedInitType : byte enum
    Raycast         // Instant raycast (grapple aim)
    Projectile      // Thrown projectile (fishing cast, rope)
    ArcProjectile   // Arced throw (grenade-style placement)
```

### Systems

- **`PlacementSystem`** (PredictedSimulationSystemGroup, Client|Server)
    - **Enter:** Player activates placement mode → spawn preview entity
    - **Update:** Raycast from camera → surface hit → snap to grid → validate (overlap, surface angle, foundation)
    - **Confirm:** Player confirms → destroy preview → spawn real entity at position
    - **Cancel:** Player cancels → destroy preview → exit placement mode
    - Preview rendering: translucent material, green=valid / red=invalid

- **`RangedInteractionSystem`** (PredictedSimulationSystemGroup, Client|Server)
    - **Aim:** Player aims at target, shows trajectory/line preview
    - **Fire:** Raycast or spawn projectile toward target
    - **Connect:** On hit → begin interaction at range (or reel in for grapple)
    - Falls through to standard `InteractAbilitySystem` once connected

### Authoring

- **`PlaceableAuthoring`** — Preview prefab, range, grid snap, surface requirements
- **`RangedInteractableAuthoring`** — Range, projectile prefab, init type

### Implementation Tasks

- [ ] Create `PlaceableConfig`, `PlacementState`, `RangedInteraction` components
- [ ] Implement `PlacementSystem` (preview lifecycle, validation, confirmation)
- [ ] Implement grid snapping and surface angle validation
- [ ] Implement placement preview rendering (valid/invalid color feedback)
- [ ] Implement `RangedInteractionSystem` (aim, fire, connect)
- [ ] Create `PlaceableAuthoring` and `RangedInteractableAuthoring` + Bakers
- [ ] **Test:** Place turret with grid snap, verify no overlap validation
- [ ] **Test:** Fishing cast → hit water → begin fishing channel interaction
- [ ] **Test:** Invalid placement (too steep, overlapping) shows red preview

---

## Phase 7: Cooperative Interactions

### Problem
Some interactions require multiple players acting together — two players turning keys simultaneously, team revive where one player channels while another defends, synchronized lever pulls.

### Components

```
CoopInteraction : IComponentData [Ghost: All]
    int RequiredPlayers             // How many players needed (ghosted)
    int CurrentPlayers              // How many have joined (ghosted)
    float SyncTolerance             // Max time between players' inputs (seconds)
    CoopMode Mode                   // How cooperation works
    bool AllPlayersReady            // All slots filled (ghosted)

CoopMode : byte enum
    Simultaneous    // All players must act at the same time (key turn)
    Sequential      // Players take turns (relay race)
    Parallel        // All players channel simultaneously (team revive)
    Asymmetric      // Different players do different things (one hacks, one defends)

CoopSlot : IBufferElementData       // On the interactable entity (NOT ghost-replicated)
    Entity PlayerEntity
    int SlotIndex                   // Which role/position
    float3 SlotPosition             // Where this player stands
    quaternion SlotRotation         // Which way this player faces
    bool IsReady                    // Player has confirmed/started their part
```

### Systems

- **`CoopInteractionSystem`** (SimulationSystemGroup, Server)
    - **Join:** Player interacts → assigned to next open `CoopSlot`
    - **Wait:** Display "Waiting for X more players" UI
    - **Ready Check:** When all slots filled, check `SyncTolerance` window
    - **Execute:** All players perform interaction simultaneously
    - **Cancel:** Any player leaving cancels for all (or just that slot, mode-dependent)
    - Server-authoritative: only server manages slot assignment and ready state

### Authoring

- **`CoopInteractableAuthoring`** — Required players, mode, sync tolerance, slot positions (transforms in inspector)
    - Gizmos: visualize each slot position with player silhouette

### Implementation Tasks

- [ ] Create `CoopInteraction` and `CoopSlot` components
- [ ] Implement `CoopInteractionSystem` (join, wait, ready check, execute, cancel)
- [ ] Create UI for "Waiting for N players" and per-slot status
- [ ] Create `CoopInteractableAuthoring` + Baker with slot position gizmos
- [ ] Handle player disconnect during coop interaction (graceful cleanup)
- [ ] **Test:** 2-player door — both players interact within sync window → door opens
- [ ] **Test:** Player leaves mid-wait → other player notified, interaction cancels
- [ ] **Test:** Asymmetric mode — player 1 hacks terminal while player 2 holds position

### Note on `CoopSlot`

This is an `IBufferElementData` on the **interactable entity** (scene-placed, server-owned). It is NOT on a ghost-replicated player entity. The slot buffer is server-only state for managing which players occupy which positions. Safe to use.

---

## Phase 8: Designer Tooling & Polish

### Editor Tools

- [ ] **Interaction Debugger Window** (`Window > DIG > Interaction Debugger`)
    - Live view of spatial hash grid (color-coded cells)
    - Current detection target + score breakdown
    - Active interaction state (phase, sequence progress, session)
    - All proximity zones with occupant counts

- [ ] **Interaction Setup Wizard** (`DIG > Interaction > Setup Wizard`)
    - Step-by-step: pick archetype → auto-add required components
    - Presets: "Simple Door", "Crafting Station", "Lockpick", "Coop Door", "Turret Seat"
    - Validates component combination (warns if conflicting settings)

- [ ] **Interaction Validator** (`DIG > Interaction > Validate Scene`)
    - Checks all interactables for missing components
    - Warns about unreachable interactables (inside walls, underground)
    - Validates BlobAsset references (sequences, phases)

### Sample Scenes

- [ ] `Scenes/Samples/InteractionShowcase` — One of each archetype, playable demo
- [ ] Each sample includes inspector annotations explaining the setup

### Documentation

- [ ] Per-phase setup guide in `Docs/EPIC16/`
- [ ] Component reference card (which components for which archetype)

---

## Phase 9: AI Workstation (Developer Tooling)

### Problem
Debugging 2,000+ AI entities at runtime requires scattered tools — `AggroDebugTester` for threats, `VisionDebugTester` for detection, console logs for state transitions. There's no unified "brain debugger" showing HFSM state, threat table, ability cooldowns, target info, and leash distance in one place. Designers can't see aggregate state distribution (how many Idle vs Combat vs Patrol) or tune encounters without pausing.

### AI Workstation Window (`DIG/AI Workstation`)

Single `EditorWindow` with sidebar tabs. All tabs share a persistent **Entity Selector** toolbar — click an enemy in the Scene view, pick by entity index, or filter by behavior state.

#### Tab 1: Brain Inspector
Unified per-entity debug view, vertically stacked:
- **HFSM State** — Current state + sub-state, state timer, transition guard status
- **Threat Table** — Horizontal bars per threat source (red = leader, yellow = visible, grey = hidden) with threat values and decay rates
- **Ability Cooldowns** — Progress bars per ability slot, charge pips, GCD indicator, active cast phase
- **Target Info** — Target entity, distance, last known position
- **Leash Gauge** — Distance from spawn / max leash as filled bar with red zone warning
- **Config Summary** — AIBrain archetype, melee range, chase speed, patrol radius (read-only reference)

#### Tab 2: Dashboard
Aggregate live stats across ALL AI entities:
- **State Distribution** — Color-coded counts: Idle / Patrol / Combat / ReturnHome / Investigate / Flee
- **Combat Stats** — Aggroed count, average threat, abilities cast/sec
- **Ability Usage** — Which abilities fire most across all entities
- **Performance** — Per-system ms timing for AI/Aggro systems

#### Tab 3: Overlay
Configuration for runtime world-space debug labels above enemies:
- **Master Toggle** + hotkey binding
- **Display Options** — Checkboxes: State, Sub-state, Threat value, Target name, Active ability, Health %
- **Filters** — Only show for Combat state, only aggroed, max camera distance
- **Visual** — Font size, background alpha, state-based color coding

#### Tab 4: Scene Tools
Toggle interactive Scene view gizmos:
- **Patrol Radius** — Wire circle at spawn position per selected entity
- **Leash Radius** — Wire sphere showing leash boundary
- **Detection Range** — Vision cone from VisionSettings
- **Melee Range** — Small circle showing attack reach
- **Threat Lines** — Lines from all aggroed enemies to their current targets

### File Structure

```
Assets/Editor/AIWorkstation/
├── AIWorkstationWindow.cs
├── IAIWorkstationModule.cs
├── AIWorkstationStyles.cs
├── Modules/
│   ├── BrainInspectorModule.cs
│   ├── DashboardModule.cs
│   ├── OverlaySettingsModule.cs
│   └── SceneToolsModule.cs

Assets/Scripts/AI/Debug/
└── AIDebugOverlaySystem.cs
```

### Implementation Tasks

- [ ] Create `AIWorkstationWindow` EditorWindow with sidebar tab layout (matches Combat Workstation pattern)
- [ ] Create `IAIWorkstationModule` interface with `OnGUI()`, `OnSceneGUI()`, `OnEntityChanged(Entity)`
- [ ] Create `AIWorkstationStyles` — shared GUIStyles, state-color mapping, stat box helpers
- [ ] Implement `BrainInspectorModule` — reads AIState, AggroState, ThreatEntry buffer, AbilityCooldownState buffer, AbilityExecutionState, AggroConfig, AIBrain, SpawnPosition from EntityManager
- [ ] Implement `DashboardModule` — queries all entities with AIState, computes distribution counts, system timing
- [ ] Implement `OverlaySettingsModule` — static config for overlay toggle, display options, filters
- [ ] Implement `SceneToolsModule` — SceneView.duringSceneGui gizmo rendering with toggle checkboxes
- [ ] Create `AIDebugOverlaySystem` (managed, PresentationSystemGroup, editor-only) — reads AIState/AggroState per entity, renders world-space labels via Camera.WorldToScreenPoint + GUI
- [ ] **Test:** Open workstation in play mode, click enemy in scene → Brain Inspector shows live state
- [ ] **Test:** Dashboard shows correct state distribution for 100+ enemies
- [ ] **Test:** Overlay labels visible above enemies, toggle on/off with hotkey

---

## Design Considerations

### Why Composable Dimensions Over Enum Expansion

Adding 10 new values to `InteractableType` creates a combinatorial explosion:
- A "cooperative timed lockpick at a crafting station" would need its own enum value
- Every system needs a switch statement covering all combinations
- Adding a new dimension requires modifying all existing code

With composition:
- Add `CoopInteraction` + `Interactable(Timed)` + `InputSequenceState` + `InteractionSession`
- Each system only cares about its own component
- New dimensions are additive — zero changes to existing systems

### NetCode Safety Rules

1. **NEVER** add new `IBufferElementData` to **ghost-replicated player entities**
    - `CoopSlot` and `ProximityZoneOccupant` go on **interactable entities** (scene-placed, server-owned)
2. **BlobAssets** for read-only definitions (phase sequences, input patterns) — shared, immutable, Burst-friendly
3. **Ghost component types:**
    - `[Ghost: All]` for interactable state (visible to all clients): `InteractionSession`, `MountPoint`, `CoopInteraction`
    - `[Ghost: AllPredicted]` for player state (predicted): `InputSequenceState`, `PlacementState`, `MountState`, `MinigameState`
4. **Server-authoritative validation** for all state transitions that affect gameplay (crafting completion, minigame success, sequence validation)
5. All managed bridges in `PresentationSystemGroup` with `WorldSystemFilter(ClientSimulation)`

### Performance Budget

| System | Target | Notes |
|--------|--------|-------|
| Spatial Hash Update | < 0.05ms | Most interactables static, only dirty entities re-hashed |
| Detection (per player) | < 0.1ms | 9-cell query, max ~50 candidates per query |
| Phase/Sequence Processing | < 0.02ms | Per-player, simple state machine |
| Proximity Zone | < 0.1ms | Spatial query, effect ticking |
| Total Interaction Budget | < 0.5ms | All interaction systems combined |

### Managed Bridges Pattern

All new managed bridges follow the same pattern established by `InteractableHybridBridgeSystem`:

1. Static `Dictionary<Entity, ManagedComponent>` registry
2. MonoBehaviour registers itself via `Entity` lookup at spawn
3. Managed system queries ECS state, calls into MonoBehaviour methods
4. Runs in `PresentationSystemGroup`, `WorldSystemFilter(ClientSimulation)`
5. One-way data flow: ECS → Managed (never managed → ECS for state)

### Interrupt & Priority System

Interactions can be interrupted by gameplay events. Each interaction type has an **interrupt resistance level** and events have an **interrupt power**. Interruption only occurs when power >= resistance.

```
InteractionInterruptConfig : IComponentData
    InterruptResistance Resistance   // How hard to interrupt this interaction
    bool CancelOnDamage              // Taking damage interrupts (default: true)
    bool CancelOnMovement            // Player moving interrupts (default: false for stations)
    bool CancelOnCombat              // Entering combat interrupts (default: true)
    float DamageThreshold            // Minimum damage to trigger interrupt (0 = any)

InterruptResistance : byte enum
    None = 0            // Instant triggers — can't be interrupted (already done)
    Low = 1             // Most timed channels — any damage/movement cancels
    Medium = 2          // Stations — only combat or forced displacement cancels
    High = 3            // Co-op interactions — only hard CC (stun/knockback) cancels
    Uninterruptible = 4 // Cutscenes, forced sequences — nothing cancels
```

Systems that produce interrupts:
- `DamageApplySystem` → sets `InterruptRequest` on damaged player entities
- `MountSystem` → forced dismount on damage (if `CancelOnDamage`)
- `StationSessionSystem` → exit session on combat enter (if `CancelOnCombat`)
- `PhaseSequenceSystem` → resets phase or cancels based on `ResetOnFail`

### Input Device Abstraction

`InputSequenceSystem` must show the correct icons per device. Input steps reference abstract actions, not hardware buttons:

```
InputStep : struct
    InputActionReference RequiredAction   // Unity InputSystem action reference (not raw KeyCode)
    float TimeWindow
    float BeatTime

// UI resolves device-specific icons at display time:
// InputActionReference → current control scheme → icon sprite
// Example: "Interact_North" → Gamepad Y / Keyboard E / Touch top-button
```

The existing `PlayerInputReader` already abstracts input via Unity's InputSystem. `InputSequenceSystem` reads `InputAction.triggered` per step — device-agnostic by design. The UI prompt bridge resolves `InputActionReference` → current control scheme → icon sprite from a shared `InputIconDatabase` ScriptableObject.

### Target Selection & Cycling

When multiple interactables overlap (scoring difference < 5%), the player can cycle between them:

- **Cycle key:** Tab (keyboard) / D-pad Left-Right (gamepad) — configurable in InputSystem
- **Cycle behavior:** Rotates through all candidates within 5% of the top score
- **Visual:** Non-selected candidates show dimmed prompts at their world positions
- **Timeout:** If player doesn't interact within 2 seconds of cycling, reverts to highest-score target

Implementation: `InteractableDetectionSystem` stores a `NativeList<InteractionCandidate>` (top 4 candidates) per player. Cycle input increments `InteractAbility.CandidateIndex`. No extra component needed — uses existing `InteractAbility` fields.

### Save/Load for Async Processing

`AsyncProcessingState` must survive across sessions. Strategy:

1. `AsyncProcessingState` is an `IComponentData` on scene-placed station entities (persisted via subscene)
2. Runtime state (`ProcessingTimeElapsed`, `IsProcessing`, `InputItemCount`, `OutputItemCount`) serialized via the existing `WorldSaveSystem` component snapshot pipeline
3. On load: `AsyncProcessingSystem` resumes from saved `ProcessingTimeElapsed` — no special restore logic
4. `FuelRemaining` also serialized (consumable state)
5. Items deposited in stations are stored as `StationInventory` buffer on the station entity (not player entity), serialized alongside

If no save system exists yet, station state resets on world reload (acceptable for early implementation — furnaces restart).

### Station Queue System

When a station is occupied:

```
StationQueueState : IComponentData [Ghost: All]
    int QueueLength                  // How many players waiting (ghosted)
    int MaxQueueSize                 // 0 = no queue allowed (default: 0)
    float EstimatedWaitTime          // Computed from current processing + queue ahead (ghosted)
```

- `MaxQueueSize = 0` (default): no queue — player sees "Station in use" prompt and can't interact
- `MaxQueueSize > 0`: player joins queue, sees position and ETA
- Queue stored as `StationQueueSlot` buffer on the station entity (server-only, NOT ghost-replicated)
- On occupant exit: next queued player auto-enters (within range check)
- Player walking away from queue removes them

Most games don't need queues (MaxQueueSize=0 is the default). This is opt-in per station.

### Cooldown & Rate Limiting

```
InteractionCooldown : IComponentData
    float CooldownDuration           // Seconds before re-interaction (0 = no cooldown)
    float LastInteractionTime        // When last used
    bool PerPlayerCooldown           // true = each player has independent cooldown
```

- **Global cooldown:** Lever pulled → 2s before anyone can pull again (ghosted state)
- **Per-player cooldown:** Vendor visited → 30s before same player can open vendor again, others unaffected
- Per-player cooldown stored as `InteractionCooldownEntry` buffer on the interactable (Entity + Timestamp)
- Anti-spam: `InteractAbilitySystem` enforces 0.2s minimum between interact attempts (hardcoded floor)

### Multi-Phase Cancel & Step-Back

```
InteractionPhaseConfig additions:
    bool AllowStepBack               // Player can go back one phase (default: false)
    bool ConfirmBeforeCancel         // Show "Are you sure?" prompt on cancel (default: false)
    int CheckpointPhase              // On fail, reset to this phase instead of 0 (-1 = reset to 0)
```

- **Step-back:** Cancel key during a phase reverts to previous phase (if `AllowStepBack`). Cancel on phase 0 cancels entirely.
- **Checkpoint:** Failed phase resets to `CheckpointPhase` instead of phase 0 (e.g., bomb defusal: fail on phase 3 → reset to phase 2, not phase 1)
- **Confirm cancel:** For long interactions (crafting), prompt before discarding progress

### Accessibility

All interaction systems support these accessibility features:

1. **Hold vs Toggle:** `InteractAbility.HoldMode` — `Hold` (default, existing) or `Toggle` (press once to start, press again to cancel). Exposed in Settings → Accessibility.
2. **Input sequence assist:** `InputSequenceState.AssistMode` — `None` (default), `SlowMotion` (extends time windows by 2x), `AutoAdvance` (only requires first input, rest auto-succeed). For players who can't do rapid inputs.
3. **Visual indicators:** All audio-only feedback has a visual equivalent. OnHover sound → UI highlight pulse. OnComplete chime → screen-edge flash.
4. **Colorblind:** Interaction prompt colors (red/green for valid/invalid placement) have secondary indicators: checkmark/X icon overlay. Shape-coded, not just color-coded.
5. **Screen reader:** `InteractableContext.ActionNameKey` is a localization key that screen readers can vocalize. All prompts have text equivalents.
6. **Reduced motion:** Option to disable prompt scale/flash animations. Simple opacity fade instead.

### Ghost Replication Bandwidth Analysis

Per interactable entity, ghost component overhead:

| Component | Size | PrefabType | Notes |
|-----------|------|------------|-------|
| `InteractionSession` | 16 bytes | All | Only if station archetype |
| `SessionLockState` | 32 bytes | AllPredicted | Only while player is seated |
| `MountPoint` | 48 bytes | All | Only if mount archetype |
| `MountState` | 24 bytes | AllPredicted | Only on mounted player |
| `CoopInteraction` | 12 bytes | All | Only if co-op archetype |
| `InputSequenceState` | 20 bytes | AllPredicted | Only during active sequence |
| `PlacementState` | 36 bytes | AllPredicted | Only during placement mode |
| `MinigameState` | 16 bytes | AllPredicted | Only during minigame |
| `AsyncProcessingState` | 28 bytes | All | Only if async archetype |

**Worst case (a station with everything):** ~232 bytes per interactable entity per snapshot.
**1000 interactables:** ~232 KB per snapshot — well within NetCode's budget. Most interactables only have 1-2 of these components (16-48 bytes each).
**AllPredicted components** only replicate for the interacting player, not all clients — further reducing bandwidth.
**Static interactables** (unchanged state) are delta-compressed to near-zero by NetCode's snapshot diff.

### Error Recovery & Graceful Degradation

1. **Server crash during station session:** Client detects ghost despawn → `StationSessionBridgeSystem` auto-closes UI panel. Player's deposited items are on the station entity (server-side) — lost if not saved. Mitigation: auto-save station state every 30 seconds via `AsyncProcessingAutoSaveSystem`.
2. **Client disconnect while mounted:** `MountSystem` server-side timeout — if mounted player's connection drops for > 5 seconds, force dismount and release seat. `MountState` cleaned up by ghost despawn.
3. **Player disconnect during co-op interaction:** `CoopInteractionSystem` detects missing player entity → cancels interaction for all participants, returns them to pre-interaction state. CoopSlot buffer cleaned up.
4. **Entity destruction during interaction:** If the interactable entity is destroyed (dynamite blows up the crafting bench), all active sessions on that entity are force-cancelled. Players receive `InteractionCancelledEvent` with reason `TargetDestroyed`.
5. **Physics desync:** If `SessionLockState.SeatPosition` puts the player inside geometry (moved at runtime), `StationSessionSystem` validates position with a physics overlap check on enter. If invalid, cancels with reason `InvalidPosition`.

---

## Integration Points

| System | EPIC | Integration |
|--------|------|-------------|
| Crafting Backend | 11.7 | `AsyncProcessingSystem` reads `RecipeDatabaseBlob` for recipe validation and timing |
| Ship Stations | 3.2 | `MountSystem` provides seat/turret infrastructure for ship cockpits |
| Inventory | 11.x | Station sessions read/write inventory for crafting, vendors |
| Combat | 15.x | `SessionLockState` disables combat abilities during station use |
| Input System | Core | `InputSequenceSystem` reads from `PlayerInputReader` action maps |
| Animation | 13.17.1 | Existing `InteractionAnimatorBridge` works with all new archetypes |
| IK Targeting | 13.17.2 | Existing `InteractableIKTarget` works with mounts, stations |
| Audio | 13.17.6 | Existing audio pipeline extended with new interaction stages |
