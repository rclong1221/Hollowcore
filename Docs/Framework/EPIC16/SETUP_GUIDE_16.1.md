# SETUP GUIDE 16.1: Universal Interaction Framework

**Status:** Implemented
**Last Updated:** February 14, 2026
**Requires:** EPIC 13.17 interaction system (existing base)

This guide covers Unity Editor setup for the composable interaction framework. It explains how to set up each interaction archetype, what Inspector fields are available, and how to use the editor tools. After setup, any interaction archetype can be created by combining authoring components on a single GameObject.

---

## What's Automatic (No Setup Required)

Existing EPIC 13.17 interactables (doors, levers, switches) continue working unchanged. The framework extends the existing system — it does not replace it.

| Feature | How It Works |
|---------|-------------|
| Spatial detection | InteractableSpatialMapSystem auto-indexes all Interactable entities into a spatial hash grid |
| Detection scoring | InteractableDetectionSystem uses distance + priority + sticky-target hysteresis |
| Interaction prompt | InteractionPromptSystem reads InteractableContext.Verb for contextual prompts |
| Ghost replication | All interaction state components are ghost-replicated automatically |
| Server authority | State transitions are server-authoritative; clients predict where safe |

---

## Quick Start: Setup Wizard

The fastest way to create an interactable is the **Setup Wizard**.

1. Select a GameObject in your subscene
2. Open **DIG > Interaction > Setup Wizard**
3. Click an archetype preset (e.g., "Crafting Station", "Lockpick", "Coop Door")
4. The wizard adds the required authoring components with sensible defaults
5. Fine-tune values in the Inspector
6. Run **DIG > Interaction > Validate Scene** to check for configuration errors

Available presets:

| Preset | Components Added | Interaction Type |
|--------|-----------------|-----------------|
| Simple Door | InteractableAuthoring + DoorAuthoring | Toggle |
| Crafting Station | InteractableAuthoring + StationAuthoring | Instant |
| Lockpick | InteractableAuthoring + MinigameAuthoring | Timed |
| Coop Door | InteractableAuthoring + CoopInteractableAuthoring | Instant |
| Turret Seat | InteractableAuthoring + MountPointAuthoring | Instant |
| Resource Node | InteractableAuthoring + ResourceAuthoring | Timed |
| Proximity Heal | ProximityZoneAuthoring | (standalone zone) |
| Placeable Item | PlaceableAuthoring | (on item/tool prefab) |

---

## Component Reference Card

Which authoring components to combine for each archetype:

| Archetype | InteractableAuthoring | Additional Authoring | Type Setting |
|-----------|:--------------------:|---------------------|:----------:|
| Instant Trigger | Required | (none) | Instant |
| Timed Channel | Required | (none) | Timed |
| Toggle Switch | Required | DoorAuthoring or LeverAuthoring | Toggle |
| Station Session | Required | StationAuthoring | Instant |
| Multi-Phase Sequence | Required | MultiPhaseAuthoring | MultiPhase |
| Mount/Seat | Required | MountPointAuthoring | Instant |
| Minigame Gate | Required | MinigameAuthoring | Timed |
| Cooperative | Required | CoopInteractableAuthoring | Instant |
| Ranged Initiation | Required | RangedInteractableAuthoring | Instant or Timed |
| Resource Node | Required | ResourceAuthoring | Timed |
| Proximity Zone | Not needed | ProximityZoneAuthoring | (standalone) |
| Placeable Item | Not needed | PlaceableAuthoring | (on item/tool) |

Multiple addons can be combined on the same entity. For example, a lockpick door could have `InteractableAuthoring(Timed)` + `MinigameAuthoring` + `CoopInteractableAuthoring`.

---

## 1. Base Interactable Setup

Every interactable (except standalone ProximityZones and PlaceableItems) needs an **InteractableAuthoring** component.

### 1.1 Add the Component

1. Select your GameObject in a **subscene**
2. **Add Component > InteractableAuthoring**

### 1.2 Inspector Fields

#### Interaction Type

| Field | Description | Default |
|-------|-------------|---------|
| **Type** | How the interaction activates (see table below) | Instant |
| **Can Interact** | Whether this object can currently be interacted with | true |
| **Interaction Radius** | Maximum detection distance in meters | 2 |
| **Priority** | Higher priority wins when multiple interactables overlap | 0 |

**Interaction Types:**

| Type | Behavior |
|------|----------|
| Instant | Triggers immediately on press |
| Timed | Hold for HoldDuration seconds |
| Toggle | Switches between on/off states |
| Animated | Driven by animation events (requires InteractableAnimationConfigAuthoring) |
| Continuous | Hold to use, releases on button up |
| MultiPhase | Multi-step sequence (requires MultiPhaseAuthoring) |

#### Timed Interaction

| Field | Description | Default |
|-------|-------------|---------|
| **Requires Hold** | Player must hold the button | false |
| **Hold Duration** | Seconds to hold for completion | 1 |

#### ID Filtering

| Field | Description | Default |
|-------|-------------|---------|
| **Interactable ID** | Unique filter ID. 0 = universal (any ability). Non-zero requires matching ability ID | 0 |

#### Context

| Field | Description | Default |
|-------|-------------|---------|
| **Verb** | Contextual action verb (Open, Use, Loot, Mount, etc.) | Interact |
| **Action Name Key** | Localization key override. Empty = use verb name | "" |
| **Require Line Of Sight** | Player must see the interactable for detection | true |

#### UI

| Field | Description | Default |
|-------|-------------|---------|
| **Message** | Prompt text shown to the player | "Press E to Interact" |

---

## 2. Station Session (Crafting Bench, Terminal, Vendor)

Stations put the player into a UI session (crafting menu, vendor shop, terminal interface).

### 2.1 Setup

1. Add **InteractableAuthoring** (Type = Instant, Verb = Use or Craft)
2. Add **StationAuthoring**
3. Create a **StationUILink** MonoBehaviour on a scene GameObject
4. Assign a UI prefab to the StationUILink

### 2.2 StationAuthoring Inspector

| Field | Description | Default |
|-------|-------------|---------|
| **Session Type** | How the UI is presented (UIPanel, FullScreen, WorldSpace) | UIPanel |
| **Session ID** | Must match the StationUILink.SessionID in the scene | 0 |
| **Allow Concurrent Users** | Multiple players can use this station at once | false |
| **Lock Position** | Freeze the player's position while in session | true |
| **Lock Abilities** | Disable combat and movement while in session | true |
| **Max Distance** | Auto-exit if player walks farther than this (0 = disabled) | 5 |

### 2.3 Async Processing (Optional)

For stations that process items over time (smelting, fermenting):

1. Check **Enable Async Processing** on the StationAuthoring
2. The `AsyncProcessingSystem` handles timer ticks server-side
3. Players can walk away and return to collect completed items

---

## 3. Multi-Phase Sequences (Bomb Defusal, Puzzles)

Multi-phase interactions require the player to complete a series of steps in order.

### 3.1 Setup

1. Add **InteractableAuthoring** (Type = MultiPhase)
2. Add **MultiPhaseAuthoring**
3. Configure the phase list and any input sequences

### 3.2 MultiPhaseAuthoring Inspector

| Field | Description | Default |
|-------|-------------|---------|
| **Phases** | Ordered list of phase entries (see below) | Empty |
| **Reset On Fail** | Failing any phase resets to phase 0 instead of cancelling | false |
| **Total Time Limit** | Max seconds for entire sequence. 0 = no limit | 0 |

#### Phase Entry Fields

| Field | Description |
|-------|-------------|
| **Type** | Instant, Timed, or InputSequence |
| **Duration** | Seconds for Timed phases |
| **Input Sequence Index** | Which InputSequences entry to use (InputSequence type only) |

#### Input Sequences

| Field | Description |
|-------|-------------|
| **Inputs** | Array of directional/button inputs (Up, Down, Left, Right, Use, AltUse) |
| **Timeout Per Input** | Seconds allowed between each input press |

### 3.3 Example: Bomb Defusal

| Phase | Type | Config |
|-------|------|--------|
| 0 | Instant | Cut the wire (just press interact) |
| 1 | InputSequence | Enter code: Up, Down, Left, Right (index 0) |
| 2 | Timed | Hold to flip the switch (3 seconds) |

---

## 4. Mount/Seat (Turrets, Vehicles, Ladders)

Mount points let players sit in seats, man turrets, climb ladders, or ride ziplines.

### 4.1 Setup

1. Add **InteractableAuthoring** (Type = Instant, Verb = Mount)
2. Add **MountPointAuthoring**
3. Position the seat offset gizmo in the Scene view

### 4.2 MountPointAuthoring Inspector

| Field | Description | Default |
|-------|-------------|---------|
| **Type** | Seat, Turret, Ladder, Zipline, or Passenger | Seat |
| **Seat Offset** | Local-space position of the seat | (0, 0.5, 0) |
| **Seat Rotation Euler** | Facing direction in local space | (0, 0, 0) |
| **Dismount Offset** | Where the player appears on dismount (local) | (1, 0, 0) |
| **Hide Player Model** | Hide the player avatar while mounted | false |
| **Transfer Input To Mount** | Forward player input to the mount entity (turrets, vehicles) | false |
| **Mount Transition Duration** | Seconds for mount/dismount blend | 0.5 |

#### Ladder-Specific

| Field | Description | Default |
|-------|-------------|---------|
| **Ladder Speed** | Climb speed in units/sec | 2 |
| **Ladder Min Y** | Bottom of ladder in local Y | 0 |
| **Ladder Max Y** | Top of ladder in local Y | 5 |

#### Zipline-Specific

| Field | Description | Default |
|-------|-------------|---------|
| **Zipline Speed** | Travel speed in units/sec | 8 |
| **Max Zipline Distance** | Maximum travel distance | 50 |

### 4.3 Turret Setup

For a turret where the player's aim controls the turret:

1. Set Type = **Turret**
2. Enable **Transfer Input To Mount**
3. Add your turret aiming/firing system that reads `MountInput` on this entity
4. Player input (look, fire) is automatically redirected to the mount's `MountInput` component

---

## 5. Proximity Zones (Campfire Heal, Buff Area, Radiation)

Proximity zones apply effects to entities inside a radius. They do NOT require InteractableAuthoring.

### 5.1 Setup

1. Create an empty GameObject in your subscene
2. Add **ProximityZoneAuthoring**
3. Set the radius and effect type
4. A game-specific system in `DIG.Player` reads `ProximityZone.EffectTickReady` and the `ProximityZoneOccupant` buffer to apply actual health/damage/buff effects

### 5.2 ProximityZoneAuthoring Inspector

| Field | Description | Default |
|-------|-------------|---------|
| **Radius** | Detection radius | 5 |
| **Effect** | None, Heal, Damage, Buff, Debuff, Shop, Dialogue, Custom | Heal |
| **Effect Interval** | Seconds between effect ticks. 0 = every frame | 1 |
| **Effect Value** | Magnitude (heal amount, damage per tick, etc.) | 10 |
| **Max Occupants** | Maximum entities in zone. 0 = unlimited | 0 |
| **Requires Line Of Sight** | Must see zone center | false |
| **Show World Space UI** | Display a world-space indicator | false |

### 5.3 Gizmos

The scene gizmo shows a color-coded sphere matching the effect type:
- **Green** = Heal
- **Red** = Damage
- **Blue** = Buff
- **Purple** = Debuff
- **Yellow** = Shop
- **Cyan** = Dialogue

---

## 6. Minigames (Lockpick, Hacking Puzzle)

Minigames gate a Timed interaction on a player-skill UI challenge.

### 6.1 Setup

1. Add **InteractableAuthoring** (Type = Timed)
2. Add **MinigameAuthoring**
3. Create a **MinigameLink** MonoBehaviour on a scene GameObject
4. Assign a minigame UI prefab to the MinigameLink
5. The UI prefab should implement `IMinigameUI` and call `link.ReportResult()` when done

### 6.2 MinigameAuthoring Inspector

| Field | Description | Default |
|-------|-------------|---------|
| **Minigame Type ID** | Links to a MinigameLink with matching ID | 1 |
| **Difficulty Level** | Passed to the minigame UI (0 = easy, 1 = hard) | 0.5 |
| **Time Limit** | Seconds before auto-fail. 0 = no limit | 30 |
| **Fail Ends Interaction** | If true, failing the minigame cancels the entire interaction | true |
| **Reward Tier** | Loot/quality tier on success | 0 |

### 6.3 MinigameLink Setup

1. Create a new GameObject in the scene (NOT in a subscene)
2. Add **MinigameLink** component
3. Set **Minigame Type ID** to match the MinigameAuthoring
4. Assign **Minigame Prefab** (a UI prefab that implements `IMinigameUI`)
5. Optionally assign **UI Parent** (a Transform where the prefab spawns)

### 6.4 Creating a Minigame UI Prefab

Your minigame prefab needs a MonoBehaviour that implements `IMinigameUI`:

- `Initialize(Entity targetEntity, float difficulty, float timeLimit)` — called when the minigame opens
- When the player completes or fails, call `MinigameLink.ReportResult(bool succeeded, float score)` on the link

---

## 7. Spatial Placement (Turret Deploy, Trap Placement)

Placeable items let the player position objects in the world with a preview ghost.

### 7.1 Setup (On the Item/Tool Prefab)

1. Add **PlaceableAuthoring** to your item or tool prefab
2. Assign the **Placeable Prefab** (what gets spawned when the player confirms)
3. A game system sets `PlacementState.IsPlacing = true` on the player to enter placement mode

### 7.2 PlaceableAuthoring Inspector

| Field | Description | Default |
|-------|-------------|---------|
| **Placeable Prefab** | What gets spawned on confirm | (required) |
| **Max Placement Range** | Maximum raycast distance from player eye | 10 |
| **Grid Snap** | Snap-to-grid size. 0 = free placement | 0 |
| **Max Surface Angle** | Maximum surface angle from flat (degrees) | 45 |
| **Validation** | None, NoOverlap, FlatSurface, Foundation, Custom | FlatSurface |
| **Overlap Check Radius** | Physics overlap radius for NoOverlap validation | 0.5 |

### 7.3 Preview Visuals

1. Create a **PlacementPreviewLink** MonoBehaviour on a scene GameObject
2. Assign **Preview Prefab** (a mesh that represents the placement ghost)
3. Assign **Valid Material** (green/transparent) and **Invalid Material** (red/transparent)
4. The system automatically shows/hides the preview and swaps materials based on validation

---

## 8. Ranged Interaction (Fishing, Grapple Hook)

Ranged interactions let players initiate from a distance via raycast or projectile.

### 8.1 Setup

1. Add **InteractableAuthoring** (Type = Instant or Timed)
2. Add **RangedInteractableAuthoring**

### 8.2 RangedInteractableAuthoring Inspector

| Field | Description | Default |
|-------|-------------|---------|
| **Max Range** | Maximum initiation distance | 20 |
| **Init Type** | Raycast, Projectile, or ArcProjectile | Raycast |
| **Projectile Speed** | Travel speed for Projectile types (units/sec) | 15 |
| **Require Aim At Target** | Player must aim within a cone to initiate | true |

### 8.3 How It Works

- **Raycast:** Instant line-of-sight check. If clear, interaction starts immediately.
- **Projectile:** Fires a virtual projectile. Interaction starts when it arrives at the target. The InteractAbilitySystem gates completion until the projectile connects.
- **ArcProjectile:** Same as Projectile but with arc trajectory.

---

## 9. Cooperative Interactions (Dual Key Turn, Team Revive)

Cooperative interactions require multiple players to work together.

### 9.1 Setup

1. Add **InteractableAuthoring** (Type = Instant)
2. Add **CoopInteractableAuthoring**
3. Position slot gizmos in the Scene view for each player position

### 9.2 CoopInteractableAuthoring Inspector

| Field | Description | Default |
|-------|-------------|---------|
| **Required Players** | How many players needed to complete | 2 |
| **Mode** | How cooperation works (see table below) | Simultaneous |
| **Sync Tolerance** | Max seconds between players' inputs (Simultaneous only) | 2 |
| **Channel Duration** | How long all must channel (Parallel/Asymmetric, 0 = instant) | 5 |

**Cooperation Modes:**

| Mode | Behavior | Example |
|------|----------|---------|
| Simultaneous | All players must press Use within the sync window | Dual key turn, synchronized levers |
| Sequential | Players confirm in slot order, one at a time | Relay puzzle, ordered sequence |
| Parallel | All players channel at the same time until duration elapses | Team revive, group ritual |
| Asymmetric | Like Parallel, but different roles per slot | One hacks, another defends |

### 9.3 Slot Configuration

The **Slots** array defines where each player stands and faces:

| Field | Description |
|-------|-------------|
| **Position** | Local-space offset from the interactable |
| **Rotation** | Local-space facing direction |

Each slot shows in the Scene view as a numbered blue circle with a direction arrow. If you define fewer slots than RequiredPlayers, extra slots get auto-generated at default positions.

### 9.4 Lifecycle

1. First player interacts -> assigned to slot 0, sees "Waiting for 1 more player"
2. Second player interacts -> assigned to slot 1
3. Both players are moved to their slot positions
4. Mode-specific logic runs (sync check, channel timer, sequence order)
5. On success: triggers the interaction effect
6. If a player cancels or disconnects: their slot is cleared, state resets

---

## 10. Editor Tools

### 10.1 Scene Validator

**Menu:** DIG > Interaction > Validate Scene

Scans all GameObjects in the current scene for interaction configuration errors.

**What it checks:**
- Addon components without InteractableAuthoring (e.g., StationAuthoring alone)
- Type mismatches (e.g., MinigameAuthoring on an Instant interaction)
- Coop slot count less than required players
- Timed interaction with zero hold duration
- MultiPhase type without MultiPhaseAuthoring
- Proximity zone with zero radius
- Placeable without prefab assigned
- Ranged interaction with zero range
- Duplicate non-zero InteractableIDs

Click the **Select** button next to any result to highlight the problematic GameObject. The validator auto-refreshes when the hierarchy changes.

### 10.2 Setup Wizard

**Menu:** DIG > Interaction > Setup Wizard

Step-by-step component setup:
1. Select a GameObject in the scene
2. Open the wizard and pick an archetype preset
3. Components are added with undo support
4. Fine-tune in the Inspector
5. Click "Validate" to check for issues

### 10.3 Interaction Debugger

**Menu:** Window > DIG > Interaction Debugger

Live play-mode inspector for ECS interaction state. Three tabs:

| Tab | Shows |
|-----|-------|
| **Interactables** | All entities with Interactable component, their type/radius/priority, and addon badges |
| **Active Interactions** | Currently active interactions with progress, multi-phase state, station/mount/minigame details |
| **Zones & Coop** | Proximity zones with occupant lists, cooperative interactions with slot status and channel progress |

The debugger auto-refreshes at 10fps during play mode. It requires the ECS world to be active.

---

## Troubleshooting

| Problem | Cause | Fix |
|---------|-------|-----|
| Player doesn't detect interactable | InteractionRadius too small or RequireLineOfSight blocked | Increase radius, check for walls between player and object |
| Timed interaction completes instantly | HoldDuration is 0 | Set HoldDuration > 0 on InteractableAuthoring |
| Minigame doesn't open | MinigameTypeID mismatch | Ensure MinigameAuthoring.MinigameTypeID matches MinigameLink.MinigameTypeID |
| Station UI doesn't appear | SessionID mismatch or missing StationUILink | Ensure StationAuthoring.SessionID matches StationUILink.SessionID |
| Coop never completes | Not enough slots defined | Ensure Slots array length >= RequiredPlayers |
| Mount puts player at wrong position | Seat offset incorrect | Adjust SeatOffset in MountPointAuthoring, use the scene gizmo |
| Placement preview stays red | Surface angle or overlap fail | Increase MaxSurfaceAngle or reduce OverlapCheckRadius |
| Validator shows "missing InteractableAuthoring" | Addon component without base | Add InteractableAuthoring to the same GameObject |
| Changes not visible in play mode | Subscene not reimported | Re-open the subscene to trigger reimport |
