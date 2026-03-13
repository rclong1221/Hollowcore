# EPIC 2.6: Death UI & Feedback

**Status**: Planning
**Epic**: EPIC 2 — Soul Chip, Death & Revival
**Dependencies**: EPIC 2.1 (SoulChip), EPIC 2.3 (Revival); EPIC 2.4 (Reanimation, optional); EPIC 2.5 (Death Spiral, optional); EPIC 12 (Scar Map, optional)

---

## Overview

Death is a narrative beat, not a loading screen. The death UI shows the player where they died, what they lost, and what comes next. The revival selection screen presents available bodies as a meaningful choice — quality versus distance versus danger. A brief soul chip transfer cinematic bridges the gap between death and rebirth. Chip degradation warnings escalate with visual and audio cues. In co-op, the death screen shifts to show the teammate carrying your chip and their path to revival. The Scar Map integrates skull icons, body locations, and reanimation status as persistent expedition scars.

---

## Component Definitions

```csharp
// File: Assets/Scripts/SoulChip/UI/DeathUIComponents.cs
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Hollowcore.SoulChip.UI
{
    /// <summary>
    /// Bridge component for death UI state. Managed system reads this
    /// to drive MonoBehaviour UI panels. Follows CombatUIBridgeSystem pattern.
    /// </summary>
    public struct DeathUIState : IComponentData
    {
        /// <summary>Current phase of the death UI flow.</summary>
        public DeathUIPhase Phase;

        /// <summary>SoulId of the dead player.</summary>
        public int SoulId;

        /// <summary>District where death occurred.</summary>
        public int DeathDistrictId;

        /// <summary>World position of death.</summary>
        public float3 DeathPosition;

        /// <summary>Number of available revival bodies.</summary>
        public int AvailableBodies;

        /// <summary>Whether drone recovery is available.</summary>
        public bool DroneAvailable;

        /// <summary>Current chip degradation tier.</summary>
        public byte DegradationTier;

        /// <summary>Total expedition deaths so far.</summary>
        public int TotalDeaths;

        /// <summary>Whether this is a full wipe (no options).</summary>
        public bool IsWipe;
    }

    /// <summary>
    /// Phases of the death UI flow. Sequential progression.
    /// </summary>
    public enum DeathUIPhase : byte
    {
        /// <summary>Not in death UI.</summary>
        None = 0,
        /// <summary>Death moment: camera, sound, brief pause.</summary>
        DeathMoment = 1,
        /// <summary>Death summary: where, what you had, what happened.</summary>
        DeathSummary = 2,
        /// <summary>Revival selection: choose a body or wait for co-op carry.</summary>
        RevivalSelection = 3,
        /// <summary>Chip transfer cinematic: consciousness moving to new body.</summary>
        ChipTransfer = 4,
        /// <summary>Reawakening: fading into new body, controls return.</summary>
        Reawakening = 5,
        /// <summary>Full wipe screen: expedition over.</summary>
        WipeScreen = 6,
    }

    /// <summary>
    /// Data passed to the revival selection UI for each available body.
    /// Populated by DeathUIBridgeSystem from RevivalBodyState entities.
    /// </summary>
    public struct RevivalBodyUIEntry
    {
        public Entity BodyEntity;
        public int BodyDefinitionId;
        public RevivalBodyTier Tier;
        public int DistrictId;
        public float Distance;       // From death location
        public float LocationDanger;  // 0.0-1.0
        public int Cost;
        public int AvailableLimbSlots;
        public float HealthMultiplier;
        public float SpeedMultiplier;
    }

    /// <summary>
    /// Co-op death screen state. Tracks teammate chip carrier status
    /// for the dead player's UI.
    /// </summary>
    public struct CoopDeathUIState : IComponentData
    {
        /// <summary>Entity of the teammate carrying the chip (Entity.Null if nobody).</summary>
        public Entity CarrierEntity;

        /// <summary>Whether the carrier is currently channeling revival.</summary>
        public bool CarrierChanneling;

        /// <summary>Channel progress (0.0-1.0).</summary>
        public float ChannelProgress;

        /// <summary>Name hash of the carrier (for display).</summary>
        public int CarrierNameHash;

        /// <summary>Distance from carrier to nearest revival body.</summary>
        public float CarrierToBodyDistance;
    }
}
```

---

## UI Panels

### DeathMomentOverlay

```
// File: Assets/Scripts/SoulChip/UI/Panels/DeathMomentOverlay.cs
// MonoBehaviour on Canvas — triggered by DeathUIPhase.DeathMoment
//
// Brief cinematic pause on death:
//   1. Time scale: lerp to 0.2 over 0.3 seconds (brief slow-mo)
//   2. Camera: slight zoom on death position, desaturation filter
//   3. Audio: impact sound + low drone, ambient cuts out
//   4. HUD: all HUD elements fade out except health bar (which empties)
//   5. Duration: 1.5 seconds real-time
//   6. Transition: fade to black → DeathSummary
//
// If co-op: other players see a brief flash at the death location
// and hear a comms-static sound effect.
```

### DeathSummaryPanel

```
// File: Assets/Scripts/SoulChip/UI/Panels/DeathSummaryPanel.cs
// MonoBehaviour on Canvas — triggered by DeathUIPhase.DeathSummary
//
// Layout:
//   ┌──────────────────────────────────────────┐
//   │  SIGNAL LOST                             │
//   │  District: [DistrictName]                │
//   │  Zone: [ZoneName]                        │
//   │  Cause: [DamageSource / Enemy Name]      │
//   ├──────────────────────────────────────────┤
//   │  INVENTORY AT DEATH                      │
//   │  [Weapon Icon] [Weapon Name]      x1     │
//   │  [Limb Icon]   [Limb Name]        x4     │
//   │  [Currency]     [Amount]                  │
//   │  [Consumable]   [Name]            x2     │
//   ├──────────────────────────────────────────┤
//   │  SOUL CHIP STATUS                        │
//   │  Transfers: [Count]  Degradation: [Tier] │
//   │  [Degradation bar visual]                │
//   ├──────────────────────────────────────────┤
//   │  [Find a Body]              [Give Up]    │
//   └──────────────────────────────────────────┘
//
// "Find a Body" → transitions to RevivalSelection
// "Give Up" → only shown if wipe conditions met → WipeScreen
// Auto-advances to RevivalSelection after 5 seconds if player doesn't interact
```

### RevivalSelectionPanel

```csharp
// File: Assets/Scripts/SoulChip/UI/Panels/RevivalSelectionPanel.cs
// MonoBehaviour on Canvas — triggered by DeathUIPhase.RevivalSelection
//
// Displays available revival bodies as selectable cards.
// Data sourced from DeathUIBridgeSystem → RevivalBodyUIEntry list.
//
// Layout per body card:
//   ┌─────────────────────────┐
//   │ [Tier Color Border]     │
//   │ [Body Silhouette]       │
//   │ Tier: CHEAP / MID / PREMIUM
//   │ District: [Name]        │
//   │ Distance: [X]m          │
//   │ Danger: [■■■□□]         │
//   │ Limb Slots: [N]         │
//   │ Stats: HP [X] SPD [X]   │
//   │ Cost: [Amount / FREE]   │
//   │ [SELECT]                │
//   └─────────────────────────┘
//
// Sorting options: Quality (tier desc), Distance (nearest first), Danger (safest first)
// Default sort: Quality
//
// Body cards color-coded:
//   Cheap: grey/brown border
//   Mid: blue border
//   Premium: gold border
//
// If drone insurance available: "DRONE RECOVERY" button (auto-selects nearest Cheap)
// If continuity cache available: "CONTINUITY CACHE" button (activates pre-placed body)
//
// Co-op mode additions:
//   - "WAITING FOR RESCUE" banner if teammate has chip
//   - Shows teammate position + distance to nearest body
//   - Dead player can ping preferred body for teammate
//
// If no bodies available and no other options: panel shows "NO OPTIONS REMAINING"
//   → auto-transition to WipeScreen after 3 seconds
```

### ChipTransferCinematic

```
// File: Assets/Scripts/SoulChip/UI/Cinematics/ChipTransferCinematic.cs
// MonoBehaviour — triggered by DeathUIPhase.ChipTransfer
//
// Brief visual sequence (3-4 seconds) showing consciousness transfer:
//   1. Frame 0-1s: Glowing chip particle exits death body
//      - Camera: third person view of death location
//      - VFX: soul chip energy orb rises from body
//      - Audio: ascending electronic hum
//
//   2. Frame 1-2.5s: Chip travels to new body
//      - Camera: follows chip along path (straight line or along district geometry)
//      - VFX: trail of data fragments, district-colored particle stream
//      - Audio: whooshing + digital data transfer sounds
//      - If cross-district: brief flash/warp transition at gate
//
//   3. Frame 2.5-4s: Chip enters new body
//      - Camera: arrives at new body location
//      - VFX: chip energy absorbs into body, body activates (lights turn on)
//      - Audio: boot-up sequence, system initialization sounds
//      - Screen: brief static/glitch (more intense at higher degradation)
//
// Degradation effects during cinematic:
//   Tier 0: clean, smooth transfer
//   Tier 1: minor static, brief color shift
//   Tier 2: noticeable glitches, audio distortion, longer static
//   Tier 3: heavy corruption, visual tearing, audio scramble, brief blackout
//
// Skip: player can hold [Skip] to reduce to 1-second version
```

### ChipDegradationWarning

```
// File: Assets/Scripts/SoulChip/UI/Panels/ChipDegradationWarning.cs
// MonoBehaviour — persistent HUD element after first chip transfer
//
// Visual states:
//   Tier 0: Hidden (no warning needed)
//   Tier 1: Small chip icon, amber glow, "CHIP INTEGRITY: 95%"
//     - Subtle pulse every 10 seconds
//   Tier 2: Chip icon with crack, orange glow, "CHIP INTEGRITY: 90%"
//     - Periodic screen-edge chromatic aberration (every 30s, 0.5s duration)
//     - Brief input delay indicator when delay triggers
//   Tier 3: Chip icon heavily cracked, red glow, "CHIP INTEGRITY: 85%"
//     - Frequent visual glitches (every 15s, 1s duration)
//     - Memory glitch: brief flash of random scene/dialogue fragment
//     - Audio warping: ambient sounds distort periodically
//     - Compendium loss notification on each new page lost
//
// Audio cues:
//   Tier 1: quiet electronic ping on damage taken
//   Tier 2: distorted ping + brief static burst
//   Tier 3: corruption sound + heartbeat layer in ambient mix
```

### CoopDeathScreen

```
// File: Assets/Scripts/SoulChip/UI/Panels/CoopDeathScreen.cs
// MonoBehaviour on Canvas — shown to dead player in co-op instead of RevivalSelection
//
// Layout:
//   ┌──────────────────────────────────────────┐
//   │  AWAITING RECOVERY                       │
//   │                                          │
//   │  [Teammate Avatar]                       │
//   │  [TeammateName] has your chip            │
//   │  Distance to nearest body: [X]m          │
//   │                                          │
//   │  ┌─ TEAMMATE VIEW ─────────────────┐    │
//   │  │ [Minimap showing carrier +       │    │
//   │  │  available bodies + enemies]     │    │
//   │  └──────────────────────────────────┘    │
//   │                                          │
//   │  Available Bodies:                       │
//   │  [Cheap - 120m] [Mid - 340m]            │
//   │                                          │
//   │  [PING BODY]  — mark preferred body      │
//   │                                          │
//   │  Revival Progress: [████░░░░] 55%        │
//   │  (shown when carrier is channeling)      │
//   └──────────────────────────────────────────┘
//
// If no teammate has chip yet:
//   "YOUR CHIP IS AT [Location] — WAITING FOR RECOVERY"
//   Shows chip location on minimap
//
// If teammate dies while carrying:
//   "CHIP DROPPED — [Location]"
//   Updates minimap with new chip position
//
// Dead player can spectate carrier (hold [Spectate] button)
```

### WipeScreen

```
// File: Assets/Scripts/SoulChip/UI/Panels/WipeScreen.cs
// MonoBehaviour on Canvas — triggered by DeathUIPhase.WipeScreen
//
// Layout:
//   ┌──────────────────────────────────────────┐
//   │              CONNECTION SEVERED           │
//   │                                          │
//   │  Expedition Summary                      │
//   │  ─────────────────                       │
//   │  Districts Cleared: [N] / [Total]        │
//   │  Total Deaths: [N]                       │
//   │  Bodies Reanimated: [N]                  │
//   │  Enemies Defeated: [N]                   │
//   │  Time Elapsed: [HH:MM:SS]               │
//   │                                          │
//   │  PRESERVED:                              │
//   │  ✓ [N] Compendium entries                │
//   │  ✓ Account progression                   │
//   │                                          │
//   │  LOST:                                   │
//   │  ✗ Carried inventory                     │
//   │  ✗ Currency on hand                      │
//   │  ✗ Unfinished district progress          │
//   │                                          │
//   │  [RETURN TO HUB]                         │
//   └──────────────────────────────────────────┘
//
// Wipe screen has somber audio: low drone, system shutdown sounds
// Visual: slow static overlay, screen dims at edges
// Co-op: all party members see same screen simultaneously
```

---

## Scar Map Integration

```
// File: Assets/Scripts/SoulChip/UI/ScarMapDeathOverlay.cs
// MonoBehaviour — overlay layer on the Scar Map (EPIC 12)
//
// Death markers on expedition map:
//
// SKULL ICONS:
//   White skull: lootable body (items still present)
//   Red skull: reanimated body (hostile mini-boss)
//   Grey skull: looted/empty body
//   Gold body icon: available revival body
//   Blue body icon: revival terminal (activated)
//
// HOVER BEHAVIOR:
//   Hovering over a skull shows tooltip:
//   ┌──────────────────────────┐
//   │ Death #[N] — [District]  │
//   │ [TimeAgo] ago            │
//   │ Cause: [Source]          │
//   │ Status: [Lootable/Reanimated/Looted]
//   │ Items: [weapon icon]x1 [limb icon]x3
//   │ Currency: [amount]       │
//   └──────────────────────────┘
//
// Hovering over a gold body icon:
//   ┌──────────────────────────┐
//   │ Revival Body — [Tier]    │
//   │ District: [Name]         │
//   │ Danger: [Rating]         │
//   │ Limbs: [N] slots         │
//   │ Cost: [Amount / FREE]    │
//   └──────────────────────────┘
//
// CONNECTIONS:
//   Dashed line from dead player's current chip to available bodies
//   Line color = danger gradient (green → yellow → red)
//   Line thickness = body quality (thicker = better tier)
```

---

## Systems

### DeathUIBridgeSystem

```csharp
// File: Assets/Scripts/SoulChip/UI/Systems/DeathUIBridgeSystem.cs
// Managed SystemBase
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
//
// Bridges ECS death state to MonoBehaviour UI panels.
// Follows CombatUIBridgeSystem pattern from DIG framework.
//
// Each frame:
//   1. Query for DeathUIState changes on local player entity
//   2. On Phase transition:
//      - DeathMoment: activate DeathMomentOverlay, start timer
//      - DeathSummary: populate DeathSummaryPanel with death data
//      - RevivalSelection: query RevivalBodyState entities,
//        build RevivalBodyUIEntry list sorted by quality,
//        pass to RevivalSelectionPanel
//      - ChipTransfer: trigger ChipTransferCinematic with endpoints
//      - Reawakening: fade in new body view, return controls
//      - WipeScreen: populate WipeScreen with expedition stats
//   3. Co-op: query ChipCarrier entities to populate CoopDeathUIState
//   4. Forward player inputs from RevivalSelectionPanel as RevivalRequest entities
//   5. Update Scar Map overlay with current death/body markers
```

### DeathUIPhaseSystem

```csharp
// File: Assets/Scripts/SoulChip/UI/Systems/DeathUIPhaseSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: RevivalSelectionSystem
//
// Drives the DeathUIState.Phase state machine:
//   1. On player death detected:
//      Phase = DeathMoment
//      Start 1.5s timer
//   2. After DeathMoment timer:
//      Phase = DeathSummary
//      Start 5s auto-advance timer (or wait for input)
//   3. On "Find a Body" input or auto-advance:
//      If drone recovery active: Phase = ChipTransfer (skip selection)
//      Else: Phase = RevivalSelection
//   4. On RevivalRequest created (body selected):
//      Phase = ChipTransfer
//   5. After ChipTransferCinematic complete (3-4s):
//      Phase = Reawakening
//   6. After reawakening fade-in (1s):
//      Phase = None (player has control)
//
// Wipe path:
//   If WipeEvent exists during RevivalSelection:
//      Phase = WipeScreen
//   WipeScreen → "Return to Hub" input → hub transition
```

---

## Setup Guide

1. Create Canvas prefabs for each UI panel: DeathMomentOverlay, DeathSummaryPanel, RevivalSelectionPanel, ChipTransferCinematic, ChipDegradationWarning, CoopDeathScreen, WipeScreen
2. Add `DeathUIState` to player entity baker (Phase = None)
3. Add `CoopDeathUIState` to player entity baker (CarrierEntity = Entity.Null)
4. Register panels with a `DeathUIRegistry` MonoBehaviour (follows CombatUIRegistry pattern)
5. Create chip transfer VFX: soul orb particle, data fragment trail, body activation glow
6. Create degradation post-processing effects: chromatic aberration, static overlay, screen tear
7. Configure audio: death impact, transfer hum, degradation corruption, wipe shutdown
8. Add Scar Map overlay layer for death markers (requires EPIC 12 map foundation)
9. Create revival body card prefab for RevivalSelectionPanel grid
10. Configure co-op spectate camera for dead player watching carrier
11. Add skip input binding for ChipTransferCinematic (hold to skip)

---

## Verification

- [ ] Death moment: slow-mo, desaturation, ambient cut — 1.5s duration
- [ ] Death summary: displays district, zone, cause of death correctly
- [ ] Death summary: inventory snapshot matches what player had at death
- [ ] Death summary: chip status shows correct transfer count and tier
- [ ] Death summary: auto-advances to revival selection after 5 seconds
- [ ] Revival selection: body cards show tier, distance, danger, cost, stats
- [ ] Revival selection: cards sortable by quality, distance, danger
- [ ] Revival selection: drone recovery button appears when charges available
- [ ] Revival selection: continuity cache button appears when cache active
- [ ] Revival selection: selecting a body creates RevivalRequest
- [ ] Revival selection: insufficient currency prevents Premium selection
- [ ] Chip transfer cinematic: 3-4 seconds, skippable
- [ ] Chip transfer cinematic: degradation Tier 0 — clean transfer
- [ ] Chip transfer cinematic: degradation Tier 3 — heavy glitches
- [ ] Chip degradation warning: hidden at Tier 0
- [ ] Chip degradation warning: amber glow at Tier 1
- [ ] Chip degradation warning: chromatic aberration at Tier 2
- [ ] Chip degradation warning: visual glitches + audio warp at Tier 3
- [ ] Co-op: dead player sees "Awaiting Recovery" with carrier info
- [ ] Co-op: dead player can ping preferred body for carrier
- [ ] Co-op: channel progress bar shown during revival
- [ ] Co-op: chip drop notification when carrier dies
- [ ] Co-op: dead player can spectate carrier
- [ ] Wipe screen: shows expedition summary with correct stats
- [ ] Wipe screen: shows preserved vs lost items correctly
- [ ] Wipe screen: "Return to Hub" transitions to hub scene
- [ ] Scar Map: white skull for lootable body
- [ ] Scar Map: red skull for reanimated body
- [ ] Scar Map: grey skull for looted body
- [ ] Scar Map: gold icon for revival body
- [ ] Scar Map: hover tooltip shows body details
- [ ] Scar Map: dashed lines from chip to available bodies

---

## Editor Tooling

**Death UI Preview Window** (`Window > Hollowcore > Death UI Preview`):
- Standalone EditorWindow that renders each death UI panel in isolation without play mode
- Tab per panel: DeathMoment, DeathSummary, RevivalSelection, ChipTransfer, Degradation, CoopDeath, WipeScreen
- Mock data injection: configurable SoulId, TotalDeaths, DegradationTier, available body count, co-op state
- Degradation visual preview: slider for tier 0-3 showing post-processing effects at each level
- Cinematic timeline scrubber: preview ChipTransferCinematic keyframes at arbitrary time positions

**Scar Map Overlay Editor**:
- Scene view gizmo overlay: place mock skull icons and revival body markers in the editor scene
- Preview hover tooltips with sample data
- Validate dashed-line rendering between chip and body positions

---

## Debug Visualization

**Death UI Phase State Machine** (toggle via debug menu):
- Overlay showing current `DeathUIPhase` as a state diagram with the active state highlighted
- Phase transition log: timestamped entries for each phase change
- Timer display: current countdown for DeathMoment (1.5s), DeathSummary (5s auto-advance), ChipTransfer (3-4s)

**Co-op Death Debug**:
- In-game overlay showing all `ChipCarrier` entities with carried SoulId, speed multiplier, channel progress
- Line renderer from carrier to nearest revival body

**Activation**: Debug menu toggle `Death/UI/ShowPhaseState`
