# EPIC 18.4: Tutorial & Onboarding System — Setup Guide

**Status:** Implemented
**Last Updated:** March 4, 2026
**Requires:** EPIC 18.1 (IUIService, UIServiceBootstrap, UIToolkitService)

---

## Overview

The Tutorial system provides a data-driven step sequencing framework for player onboarding. Designers define sequences as ScriptableObject graphs with multiple presentation modes and completion conditions.

| Step Type | Behavior | Use Case |
|-----------|----------|----------|
| **Tooltip** | Small bubble near a UI element | Point out a specific button |
| **Highlight** | Spotlight mask (4-rect cutout) + tooltip | Draw attention to a UI element |
| **ForcedAction** | Tooltip without continue button, waits for input | "Press Jump to continue" |
| **Popup** | Centered modal with continue button | Welcome messages, lore text |
| **WorldMarker** | Screen-space arrow tracking a world position | "Go to the waypoint" |
| **Delay** | Invisible pause before next step | Pacing between steps |
| **Branch** | Conditional fork based on TutorialConditionSO | Branching sequences |

Key behaviors:
- **Completion detection** — Steps can auto-complete on input performed, UI screen opened, custom event, timer, or manual continue
- **Persistence** — Completed tutorials saved to PlayerPrefs; won't repeat
- **Auto-start** — Sequences with prerequisites can trigger automatically when conditions are met
- **Input glyphs** — Message text supports `{input:ActionName}` tokens that render device-appropriate icons (keyboard vs gamepad)
- **ECS bridge** — `TutorialVisualQueue` for ECS systems to trigger tutorials; `TutorialTriggerEventSystem` reads trigger volumes from subscenes

---

## Step 1: Create TutorialConfig Asset

1. Right-click in Project → **Create > DIG > Tutorial > Config**
2. Name it `TutorialConfig`
3. Move to `Assets/Resources/TutorialConfig.asset`

| Field | Default | Description |
|-------|---------|-------------|
| Spotlight Color | (0, 0, 0, 0.7) | Semi-transparent mask overlay color |
| Tooltip Offset | 12 | Pixels between target element and tooltip bubble |
| Step Transition Delay | 0.3 | Seconds to wait between steps |
| Marker Pool Size | 3 | Pre-warmed world marker instances |
| Screen Edge Margin | 40 | Pixels from screen edge for off-screen arrow clamping |
| Default Step Sound | (optional) | AudioClip played on each step (overridable per-step) |
| Sound Volume | 0.5 | Volume for step sounds [0.0–1.0] |

---

## Step 2: Add TutorialServiceBootstrap to Scene

1. Find the persistent/boot scene (same one that has `UIServiceBootstrap`)
2. Add a `TutorialServiceBootstrap` component to a persistent GameObject
   - Can be on the same GameObject as other bootstraps or a new one
3. The bootstrap:
   - Runs at execution order `-240` (after `UIServiceBootstrap` at `-300`)
   - Loads `TutorialConfig` from Resources automatically
   - Loads `Tutorial.uxml` and `Tutorial.uss` from `Resources/UI/Tutorial/`
   - Injects the overlay into UIToolkitService's Modal layer
   - Creates a child `TutorialService` singleton GameObject

No serialized fields — the bootstrap is fully automatic.

---

## Step 3: Create Tutorial Steps

1. Right-click in Project → **Create > DIG > Tutorial > Step**
2. Create one asset per step in the sequence
3. Configure the step fields:

| Field | Description |
|-------|-------------|
| Step Id | Unique ID within the sequence (used for branching/navigation) |
| Step Type | Tooltip, Highlight, ForcedAction, Popup, WorldMarker, Delay, or Branch |
| Title | Header text displayed to the player (optional) |
| Message | Body text. Supports `{input:Move}` tokens for input glyphs |
| Target Element Name | UI Toolkit element name to target (for Tooltip/Highlight steps) |
| World Target Tag | GameObject tag to track (for WorldMarker steps) |
| Completion Condition | How the step completes (see table below) |
| Completion Param | Parameter for the completion detector (see table below) |
| Timeout Seconds | Auto-advance after N seconds; 0 = no timeout |
| Highlight Padding | Pixels of padding around highlighted element (default 20) |
| Next Step Id | Override next step instead of array order (optional) |
| Branch Condition | TutorialConditionSO to evaluate (for Branch steps) |
| True Step Id | Next step if branch condition is true |
| False Step Id | Next step if branch condition is false |
| Sound | Per-step AudioClip override (optional) |

### Completion Condition Reference

| Condition | Completion Param | Behavior |
|-----------|-----------------|----------|
| **ManualContinue** | (ignored) | Player clicks "Got it" / "Continue" button |
| **InputPerformed** | Input action name (e.g., `"Jump"`, `"Move"`) | Completes when the named InputAction fires |
| **UIScreenOpened** | Screen name (e.g., `"InventoryScreen"`) | Completes when `NotifyScreenChanged()` matches |
| **CustomEvent** | Event key (e.g., `"picked_up_sword"`) | Completes when `FireEvent(key)` is called |
| **Timer** | (ignored) | Auto-completes after `TimeoutSeconds` (defaults to 3s if 0) |

### Input Glyph Tokens

Messages support device-aware input icons via `{input:ActionName}` syntax:

```
Press {input:Jump} to jump!
Use {input:Move} to walk around.
```

These are processed through `InputGlyphProvider.ProcessText()` and render the correct icon for the active input device (keyboard key, gamepad button, etc.).

---

## Step 4: Create Tutorial Conditions (Optional)

Conditions are used for **auto-start prerequisites** and **branch steps**.

1. Right-click in Project → **Create > DIG > Tutorial > Condition**
2. Configure the condition:

| Field | Description |
|-------|-------------|
| Type | `TutorialCompleted`, `SettingEquals`, or `PlayerLevelAbove` |
| Tutorial Id | For `TutorialCompleted` — checks if that tutorial was completed |
| Setting Key | For `SettingEquals` — PlayerPrefs key to check |
| String Value | For `SettingEquals` — value to compare against |
| Int Value | For `PlayerLevelAbove` — minimum player level |
| Compare | `Equals`, `NotEquals`, `GreaterThan`, `LessThan` |
| Invert | Negate the result |

### Examples

- **"Only if movement tutorial is done"**: Type=`TutorialCompleted`, TutorialId=`"movement_basics"`, Invert=`false`
- **"Only if NOT completed"**: Same as above but Invert=`true`
- **"Player is level 5+"**: Type=`PlayerLevelAbove`, IntValue=`5`

---

## Step 5: Create Tutorial Sequences

1. Right-click in Project → **Create > DIG > Tutorial > Sequence**
2. Configure the sequence:

| Field | Default | Description |
|-------|---------|-------------|
| Sequence Id | | Unique identifier (used by API and persistence) |
| Display Name | | Human-readable name for editor tooling |
| Steps | | Ordered array of `TutorialStepSO` assets |
| Prerequisite | (none) | `TutorialConditionSO` that must be true for auto-start |
| Auto Start | false | Automatically start when prerequisite is met and not completed |
| Can Skip | true | Show "Skip" button to player |
| Priority | 0 | Higher priority wins when multiple auto-starts are eligible |
| Save Key | (auto) | PlayerPrefs key; auto-generates as `Tutorial_{SequenceId}` if empty |

### Assigning Steps

Drag `TutorialStepSO` assets into the **Steps** array in order. Steps execute sequentially unless overridden by `NextStepId` or branching.

### Auto-Start Behavior

When `AutoStart` is enabled:
- The service evaluates all auto-start sequences every 0.5 seconds while idle
- The highest-priority sequence whose prerequisite passes (and hasn't been completed) starts automatically
- Prerequisite is skipped if the field is left empty (always eligible)

---

## Step 6: Add In-World Trigger Volumes (Optional)

For tutorials triggered by player movement through the world:

1. In a subscene, create a GameObject with a trigger collider
2. Add the `TutorialTriggerAuthoring` component

| Field | Default | Description |
|-------|---------|-------------|
| Header | "Tutorial Header" | Display header text |
| Message | "Tutorial Message" | Display body text |
| Sequence Id | | Must match a `TutorialSequenceSO.SequenceId` |
| One Time | true | Only trigger once per entity lifetime |

When the player enters the trigger volume, `TutorialTriggerEventSystem` reads the triggered flag and calls `TutorialService.StartTutorial(sequenceId)`.

**Note:** The existing Burst-compiled `TutorialTriggerSystem` handles collision detection. The managed `TutorialTriggerEventSystem` reads its output — no modifications to the original system are needed.

---

## Step 7: Calling the Tutorial API from Game Code

### Starting a Tutorial Manually

```csharp
TutorialService.Instance.StartTutorial("onboarding_basics");
```

### Firing Custom Events (for CustomEvent completion)

```csharp
// When the player opens inventory for the first time
TutorialService.Instance.FireEvent("inventory_opened");
```

### Notifying Screen Changes (for UIScreenOpened completion)

```csharp
// Call when a UI screen changes
TutorialService.Instance.NotifyScreenChanged("MapScreen");
```

### Checking Completion Status

```csharp
if (TutorialService.Instance.IsTutorialCompleted("onboarding_basics"))
{
    // Skip intro screens
}
```

### Subscribing to Events

```csharp
TutorialService.Instance.OnTutorialStarted += seq => Debug.Log($"Started: {seq.DisplayName}");
TutorialService.Instance.OnStepShown += (step, index, total) => Debug.Log($"Step {index+1}/{total}");
TutorialService.Instance.OnTutorialCompleted += seq => Debug.Log($"Completed: {seq.SequenceId}");
TutorialService.Instance.OnTutorialSkipped += seq => Debug.Log($"Skipped: {seq.SequenceId}");
```

### Triggering from ECS Systems

```csharp
using DIG.Tutorial.Bridge;

TutorialVisualQueue.Enqueue(new TutorialVisualEvent
{
    SequenceId = "boss_intro"
});
```

The `TutorialTriggerEventSystem` drains this queue each frame in `PresentationSystemGroup`.

### Resetting Progress (Debug)

```csharp
TutorialService.Instance.ResetTutorial("onboarding_basics"); // Reset one
TutorialService.Instance.ResetAll();                          // Reset all
```

---

## Step 8: Editor Tooling

### Tutorial Workstation

Open via **DIG > Tutorial Workstation** in the menu bar.

| Tab | What It Does | Requires Play Mode? |
|-----|-------------|---------------------|
| **Sequence Browser** | Browse all `TutorialSequenceSO` assets, view steps, check completion status, open in Graph Editor | No |
| **Trigger Map** | List all `TutorialTriggerAuthoring` in the current scene, ping GameObjects, view SequenceId bindings | No |

**Sequence Browser features:**
- Select a sequence to see its steps listed with type, title, and completion condition
- "Open in Graph Editor" button opens the visual graph view
- "Reset All Progress" button clears all tutorial PlayerPrefs (debug/testing)

**Trigger Map features:**
- Lists all trigger authoring components in the active scene
- "Ping" button selects and highlights the trigger GameObject in the Hierarchy
- Shows Header, Sequence ID, OneTime flag, and world position for each trigger

### Tutorial Graph Editor

Open via **DIG > Tutorial > Graph Editor** in the menu bar, or from the Sequence Browser's "Open in Graph Editor" button.

**Features:**
- Visual node graph showing step flow
- Nodes are color-coded by step type
- Edges show sequential connections, NextStepId overrides, and branch true/false paths
- Load a `TutorialSequenceSO` via the object field in the toolbar
- **Save** — persists node layout to the sequence asset
- **Auto-Layout** — arranges nodes left-to-right
- **Clear** — removes all nodes from the view
- Double-click a node to select its `TutorialStepSO` in the Inspector

---

## Step Type Quick Reference

### Tooltip

Points a bubble at a UI element. Best for "Click this button" instructions.

- Set **Target Element Name** to the UI Toolkit element's `name` attribute
- Set **Completion Condition** (usually `ManualContinue` or `InputPerformed`)

### Highlight

Same as Tooltip but adds a darkened spotlight mask around the target element. Four semi-transparent rectangles form a cutout, updated per-frame as the target moves.

- Set **Target Element Name** to the element to highlight
- Set **Highlight Padding** to control cutout size (default 20px)

### ForcedAction

A Tooltip without a continue button — the player must perform the action to proceed.

- Set **Completion Condition** to `InputPerformed` or `CustomEvent`
- Set **Completion Param** to the action name or event key

### Popup

Centered modal dialog. Good for welcome messages, lore, or multi-paragraph text.

- Set **Title** and **Message**
- Usually uses `ManualContinue` (player clicks "Continue")

### WorldMarker

Screen-space arrow tracking a world position. Clamps to screen edges when the target is off-screen, with distance label.

- Set **World Target Tag** to the tag on the target GameObject
- Arrow projects via `Camera.main.WorldToScreenPoint()` and edge-clamps following the `OffScreenIndicatorRenderer` pattern

### Delay

Invisible pause. Does not show any UI.

- Set **Timeout Seconds** for how long to wait
- Completion Condition should be `Timer`

### Branch

Conditional fork. Evaluates a `TutorialConditionSO` and jumps to **True Step Id** or **False Step Id**.

- Set **Branch Condition** to a `TutorialConditionSO` asset
- Set **True Step Id** and **False Step Id** to valid Step IDs within the sequence

---

## Customizing the UI

The tutorial overlay UI is defined in two files loaded from Resources:

- **UXML**: `Assets/Resources/UI/Tutorial/Tutorial.uxml`
- **USS**: `Assets/Resources/UI/Tutorial/Tutorial.uss`

### Key USS Classes

| Class | Element | What to Customize |
|-------|---------|-------------------|
| `.tutorial-spotlight__mask` | Spotlight rectangles | Background color, opacity |
| `.tutorial-tooltip` | Tooltip container | Background, border, border-radius, padding |
| `.tutorial-tooltip--visible` | Tooltip fade-in state | Opacity transition timing |
| `.tutorial-popup` | Popup container | Background, max-width, padding |
| `.tutorial-popup--visible` | Popup fade-in state | Opacity, translate transitions |
| `.tutorial-popup__backdrop` | Popup backdrop | Background color/opacity |
| `.tutorial-world-marker` | World arrow container | Size, color |
| `.tutorial-world-marker--pulse` | Arrow pulse animation | Scale animation keyframes |
| `.tutorial-skip-btn` | Skip button | Position, styling |
| `.tutorial-step-counter` | Step counter label | Position, font size, color |

### UXML Element Names

These names are queried by the overlay controller. Do not rename them:

```
spotlight-container, spotlight-top, spotlight-bottom, spotlight-left, spotlight-right
tooltip-container, tooltip-arrow, tooltip-title, tooltip-message, tooltip-continue-btn
popup-container, popup-backdrop, popup-title, popup-message, popup-continue-btn
world-marker-container, marker-arrow, marker-distance
skip-btn, step-counter
```

---

## Troubleshooting

### Tutorial doesn't start

- Check console for `[TutorialServiceBootstrap]` warnings about missing resources
- Ensure `TutorialConfig.asset` is in `Assets/Resources/`
- Ensure `Tutorial.uxml` and `Tutorial.uss` are in `Assets/Resources/UI/Tutorial/`
- Verify the `SequenceId` matches exactly between `TutorialSequenceSO` and the `StartTutorial()` call or trigger authoring
- Check `TutorialService.Instance` is not null (bootstrap must be in scene)

### Auto-start sequence never fires

- Ensure `AutoStart` is checked on the sequence
- Check that the `Prerequisite` condition evaluates to true (or leave it empty)
- Confirm the sequence hasn't already been completed — use Reset in the Workstation
- Auto-start evaluates every 0.5s and only while the service is idle (no active tutorial)

### Spotlight/Highlight doesn't appear around the right element

- Verify **Target Element Name** matches the `name` attribute in the UXML (case-sensitive)
- The element must be in the same UI Toolkit panel tree as the overlay
- If the element is inside a scroll view or dynamic list, the name query may not find it until it's rendered

### World marker doesn't appear

- Ensure the target GameObject has the correct tag matching **World Target Tag**
- `Camera.main` must exist (the marker needs a camera to project world→screen)
- Check that the target object is active in the scene

### Input completion doesn't fire

- The InputAction must be **enabled** at the time the step starts
- **Completion Param** must match the action name exactly (case-insensitive)
- If using Input System action maps, ensure the correct map is active

### ECS trigger volume doesn't start tutorial

- The trigger authoring must be in a **subscene** (it bakes to an ECS entity)
- **Sequence Id** on the authoring must match a `TutorialSequenceSO.SequenceId`
- Reimport the subscene after modifying the authoring component
- `TutorialTriggerEventSystem` runs in `PresentationSystemGroup` on `ClientSimulation | LocalSimulation` worlds only

### No sound plays

- Assign an AudioClip to `TutorialConfigSO.DefaultStepSound` or `TutorialStepSO.Sound`
- Ensure there is an AudioSource available (add one to the TutorialService GameObject if needed)

---

## Verification Checklist

- [ ] `TutorialConfig.asset` exists in `Assets/Resources/`
- [ ] `Tutorial.uxml` exists in `Assets/Resources/UI/Tutorial/`
- [ ] `Tutorial.uss` exists in `Assets/Resources/UI/Tutorial/`
- [ ] `TutorialServiceBootstrap` component on a persistent scene GameObject
- [ ] In Play Mode: `TutorialService.Instance` is not null
- [ ] Create a 3-step sequence: Popup → ForcedAction (InputPerformed) → Popup
- [ ] Call `TutorialService.Instance.StartTutorial("test-sequence")` → first popup appears
- [ ] Click Continue → ForcedAction step waits for input → perform input → auto-advances
- [ ] Final popup → click Continue → tutorial completes, PlayerPrefs persisted
- [ ] Restart Play Mode → `IsTutorialCompleted("test-sequence")` returns true
- [ ] Highlight step → spotlight mask appears around target UI element
- [ ] WorldMarker step → arrow tracks world position, edge-clamps when off-screen
- [ ] Skip button visible when `CanSkip = true`; clicking it ends the tutorial
- [ ] Auto-start: sequence with met prerequisite starts automatically
- [ ] Trigger volume: player enters trigger → tutorial starts
- [ ] Editor: **DIG > Tutorial Workstation** opens with Sequence Browser and Trigger Map
- [ ] Editor: **DIG > Tutorial > Graph Editor** loads a sequence and shows nodes/edges
- [ ] Reset: Workstation's "Reset All Progress" clears completion state
