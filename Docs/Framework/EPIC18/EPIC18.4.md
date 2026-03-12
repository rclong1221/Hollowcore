# EPIC 18.4: Tutorial & Onboarding System

**Status:** IMPLEMENTED
**Priority:** High (First-time user experience — player retention critical)
**Dependencies:**
- `TutorialTriggerAuthoring` / `TutorialTriggerComponent` (existing — `DIG.UI.Tutorial`, `Assets/Scripts/Core/Tutorial/TutorialTriggerAuthoring.cs`, ECS component with Header/Message/OneTime)
- `TutorialTriggerSystem` (existing — `DIG.UI.Tutorial`, `Assets/Scripts/Core/Tutorial/TutorialTriggerSystem.cs`, Burst-compiled trigger detection, sets `Triggered = true` but has no UI hookup)
- `ZoneTriggerAuthoring` (existing — `DIG.Core.Zones`, `Assets/Scripts/Core/Zones/ZoneTriggerAuthoring.cs`)
- `InteractionPromptView` / `InteractionPromptViewModel` (existing — `DIG.UI.Views`, `Assets/Scripts/UI/Views/InteractionPromptView.cs`)
- `NotificationService` (EPIC 18.3 — for tooltip/hint display)
- `IUIService` (EPIC 18.1 — screen overlay management)
- `InputGlyphProvider` (existing — `DIG.UI.Core.Input`, input glyph swapping)
- `PlayerTag` (existing — ECS player identification)

**Feature:** A data-driven tutorial and onboarding framework where designers define tutorial sequences as ScriptableObject graphs, with step types including Highlight (spotlight a UI element), Tooltip (point at world/UI target), ForcedAction (wait for player to perform action), Popup (information modal), and WorldMarker (3D waypoint). Steps can be triggered by zone entry, input events, game state conditions, or quest progression. Supports branching, skip/dismiss, persistence of completed tutorials, and a visual graph editor for authoring sequences.

---

## Codebase Audit Findings

### What Already Exists

| System | File | Status | Notes |
|--------|------|--------|-------|
| `TutorialTriggerAuthoring` | `Assets/Scripts/Core/Tutorial/TutorialTriggerAuthoring.cs` | Minimal | ECS component with Header (FixedString64), Message (FixedString512), OneTime bool. Baker creates entity |
| `TutorialTriggerSystem` | `Assets/Scripts/Core/Tutorial/TutorialTriggerSystem.cs` | Minimal | Burst-compiled ITriggerEventsJob that detects player collision with trigger volumes. Sets `Triggered = true` but **does nothing with the data** — commented-out Debug.Log, says "in future generic UI event" |
| `InteractionPromptView` | `Assets/Scripts/UI/Views/InteractionPromptView.cs` | Implemented | UI Toolkit-based prompt for "Press E to interact" — could be extended for tutorial prompts |
| `ZoneTriggerAuthoring` | `Assets/Scripts/Core/Zones/ZoneTriggerAuthoring.cs` | Implemented | Generic zone trigger volumes |

### What's Missing

- **No tutorial step sequencing** — `TutorialTriggerSystem` sets a bool but has no concept of step chains, prerequisites, or branching
- **No UI spotlight/highlight** — no way to draw attention to a specific UI element (darken background, highlight button)
- **No world-space tutorial markers** — no 3D waypoint arrows pointing to objectives
- **No forced action detection** — no way to wait for player to perform a specific action (open inventory, equip weapon, etc.)
- **No tutorial progress persistence** — `Triggered` flag is ECS-only (reset on scene reload), no PlayerPrefs/save integration
- **No skip/dismiss** — no way for experienced players to skip tutorials
- **No visual authoring** — tutorial sequences must be defined in code or basic inspector fields
- **No conditional triggers** — no "show this step only if player has never opened inventory"

---

## Problem

The existing `TutorialTriggerSystem` is a skeleton — it detects when a player enters a trigger volume and sets a flag, but nothing reads that flag to show UI. There is no way to create a multi-step tutorial sequence like:

1. "Press W to move forward" (wait for movement)
2. Spotlight the inventory button → "Press I to open your inventory" (wait for inventory open)
3. "Drag a weapon to your hotbar" (wait for equip)
4. Arrow pointing to enemy → "Approach the training dummy" (wait for proximity)
5. "Click to attack!" (wait for attack input)

Each of these steps requires different UI treatments (tooltip, spotlight, world marker, forced action) and different completion conditions. Without a framework, every tutorial interaction must be hardcoded, making it impossible for designers to iterate on onboarding flow.

---

## Architecture Overview

```
                    DESIGNER DATA LAYER
  TutorialSequenceSO           TutorialStepSO            TutorialConditionSO
  (ordered list of steps,     (step type, target,        (condition type,
   prerequisites, auto-start,  message, completion        parameter, comparator,
   can-skip, priority)         condition, timeout)        value — for branching)
        |                         |                           |
        └──── TutorialService (MonoBehaviour singleton) ──────┘
              (loads sequences, manages active tutorial,
               step progression, persistence, skip logic)
                         |
        ┌────────────────┼────────────────────┐
        |                |                    |
  TutorialOverlayView  WorldMarkerPool    CompletionDetector
  (UI Toolkit overlay    (3D waypoint         (listens for input
   with spotlight mask,   arrows, screen-      events, ECS state
   tooltip bubbles,       space indicators,    changes, UI events
   message panels)        distance labels)     to detect step
                                               completion)
                         |
              ECS Integration
                         |
  TutorialStateComponent (on player entity)
  (tracks active tutorial ID, current step,
   completed tutorial bitmask — replicated
   for server-aware tutorials)
                         |
  TutorialTriggerSystem (existing, enhanced)
  (now fires TutorialStartRequest event when
   Triggered flag is set — consumed by
   TutorialBridgeSystem to start sequences)
                         |
                 EDITOR TOOLING
                         |
  TutorialGraphEditor ── visual node graph
  (drag/drop step creation, preview in Scene view,
   condition wiring, test playback)
```

---

## Core Types

### TutorialStepType

```csharp
public enum TutorialStepType
{
    Tooltip,        // Arrow + message pointing at UI element or world position
    Highlight,      // Darken screen, spotlight a UI element with cutout mask
    ForcedAction,   // Wait for player to perform a specific input/action
    Popup,          // Information panel with continue button
    WorldMarker,    // 3D waypoint arrow in world space
    Delay,          // Wait N seconds before advancing
    Branch          // Evaluate condition, go to step A or B
}
```

### CompletionCondition

```csharp
public enum CompletionCondition
{
    ManualContinue,     // Player clicks "Next" / "Got it"
    InputPerformed,     // Specific InputAction triggered (e.g., "Move", "Jump")
    UIScreenOpened,     // Specific screen opened via IUIService
    ItemEquipped,       // Item placed in equipment slot
    EnemyKilled,        // Any enemy killed
    ZoneEntered,        // Player enters specified zone
    QuestAccepted,      // Quest accepted
    CustomEvent,        // String event key fired via TutorialService.FireEvent()
    Timer,              // Auto-advance after duration
    Expression          // Evaluate scripted condition
}
```

---

## ScriptableObjects

### TutorialSequenceSO

**File:** `Assets/Scripts/Tutorial/Config/TutorialSequenceSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| SequenceId | string | "" | Unique identifier |
| DisplayName | string | "" | Editor label |
| Steps | TutorialStepSO[] | empty | Ordered steps |
| Prerequisite | TutorialConditionSO | null | Must be satisfied to auto-start |
| AutoStart | bool | false | Start automatically when prerequisite met |
| CanSkip | bool | true | Allow player to skip entire sequence |
| Priority | int | 0 | If multiple tutorials trigger, highest priority wins |
| SaveKey | string | "" | PlayerPrefs key for completion persistence |

### TutorialStepSO

**File:** `Assets/Scripts/Tutorial/Config/TutorialStepSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| StepId | string | "" | Unique within sequence |
| StepType | TutorialStepType | Tooltip | How this step is presented |
| Title | string | "" | Optional header text |
| Message | string | "" | Body text (supports rich text + {input:ActionName} glyph tokens) |
| TargetElementName | string | "" | UI element name for Highlight/Tooltip |
| WorldTargetTag | string | "" | Tag for finding world-space target |
| CompletionCondition | CompletionCondition | ManualContinue | How this step completes |
| CompletionParam | string | "" | Parameter for condition (action name, screen ID, etc.) |
| TimeoutSeconds | float | 0 | Auto-advance after timeout (0 = no timeout) |
| ArrowDirection | Direction | Auto | Tooltip arrow direction |
| HighlightPadding | float | 20 | Pixels of padding around highlighted element |
| NextStepId | string | "" | Override next step (for branching) |
| BranchCondition | TutorialConditionSO | null | For Branch type steps |
| TrueStepId | string | "" | Step if condition true |
| FalseStepId | string | "" | Step if condition false |
| Sound | AudioClip | null | Play on step show |

### TutorialConditionSO

**File:** `Assets/Scripts/Tutorial/Config/TutorialConditionSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| ConditionType | ConditionType enum | TutorialCompleted | What to check |
| TutorialId | string | "" | For TutorialCompleted condition |
| SettingKey | string | "" | For SettingEquals condition |
| IntValue | int | 0 | Comparison value |
| StringValue | string | "" | Comparison value |
| Comparator | Comparator enum | Equals | Equals, NotEquals, GreaterThan, etc. |
| Invert | bool | false | Negate the result |

---

## TutorialService

**File:** `Assets/Scripts/Tutorial/TutorialService.cs`

- MonoBehaviour singleton, `DontDestroyOnLoad`
- API:
  - `StartTutorial(string sequenceId)` — begin a sequence
  - `AdvanceStep()` — manually advance to next step
  - `SkipTutorial()` — skip current sequence
  - `FireEvent(string eventKey)` — fire custom completion event
  - `bool IsTutorialCompleted(string sequenceId)` — check persistence
  - `void ResetTutorial(string sequenceId)` — clear completion for replay
  - `void ResetAll()` — clear all tutorial progress
- Loads all `TutorialSequenceSO` from Resources at startup
- Evaluates prerequisites each frame for auto-start sequences
- Manages active tutorial state machine: Idle → StepActive → WaitingCompletion → Advancing → StepActive | Complete
- Persists completion to PlayerPrefs (bitmask per sequence)
- Fires `OnTutorialStarted`, `OnStepShown`, `OnStepCompleted`, `OnTutorialCompleted` events

---

## UI Components

### TutorialOverlayView

**File:** `Assets/Scripts/Tutorial/UI/TutorialOverlayView.cs`

- Permanent UI Toolkit overlay layer (above gameplay, below tooltips)
- **Spotlight Mode:** Full-screen semi-transparent mask with rectangular cutout around target element. Cutout position updated per-frame to track moving elements
- **Tooltip Mode:** Arrow-pointed bubble positioned relative to target with auto-flip if near screen edge
- **Popup Mode:** Centered modal with title, message, optional image, continue button
- **Input Glyph Injection:** Replaces `{input:Jump}` tokens in message text with `InputGlyphProvider` sprites for current device
- **Skip Button:** Persistent "Skip Tutorial" button in corner when `CanSkip = true`
- **Step Counter:** "Step 2 of 5" indicator

### WorldTutorialMarker

**File:** `Assets/Scripts/Tutorial/WorldMarkers/WorldTutorialMarker.cs`

- MonoBehaviour pooled instance
- 3D arrow/beacon pointing at world target
- Screen-edge indicator when target is off-screen
- Distance label
- Pulsing animation via shader

---

## ECS Integration

### Enhanced TutorialTriggerSystem

- Existing system modified minimally: when `Triggered` flag is set, also creates a `TutorialStartRequest` event entity via ECB
- `TutorialStartRequest` carries the `SequenceId` from the trigger component
- New `TutorialBridgeSystem` (PresentationSystemGroup) reads `TutorialStartRequest` and calls `TutorialService.StartTutorial()`

### TutorialPlayerState (optional ECS component)

```
TutorialPlayerState (IComponentData, on player entity)
  ActiveTutorialHash  : int      // Hash of active sequence ID (0 = none)
  CurrentStepIndex    : byte     // Current step index
  CompletedMask       : ulong    // Bitmask of completed tutorials (up to 64)

Total: 13 bytes
```

- Replicated via Ghost for server-aware tutorials (e.g., prevent combat during tutorial)
- Optional — managed-only mode works without this component

---

## Editor Tooling

### TutorialGraphEditor

**File:** `Assets/Editor/TutorialWorkstation/TutorialGraphEditor.cs`

- Visual node graph using `UnityEditor.Experimental.GraphView` (same tech as Shader Graph / VFX Graph)
- Nodes for each TutorialStepSO (color-coded by type)
- Edges for step connections and branch paths
- Condition nodes for prerequisites and branching
- Preview button: highlights target element in Scene/Game view
- Play button: step-through tutorial in Play mode with pause/next controls
- Auto-layout button for clean graph arrangement

### TutorialWorkstationModule

**File:** `Assets/Editor/TutorialWorkstation/Modules/TutorialWorkstationModule.cs`

- Registered in Workstation window
- **Sequence Browser:** Lists all TutorialSequenceSO with completion stats
- **Step Inspector:** Select a step to see its config, target preview, completion condition
- **Progress Reset:** Button to clear all tutorial progress for testing
- **Trigger Map:** Shows all TutorialTriggerAuthoring placements in scene

---

## File Manifest

| File | Type | Lines (est.) |
|------|------|-------------|
| `Assets/Scripts/Tutorial/TutorialService.cs` | MonoBehaviour | ~300 |
| `Assets/Scripts/Tutorial/Config/TutorialSequenceSO.cs` | ScriptableObject | ~40 |
| `Assets/Scripts/Tutorial/Config/TutorialStepSO.cs` | ScriptableObject | ~60 |
| `Assets/Scripts/Tutorial/Config/TutorialConditionSO.cs` | ScriptableObject | ~35 |
| `Assets/Scripts/Tutorial/UI/TutorialOverlayView.cs` | UIView | ~300 |
| `Assets/Scripts/Tutorial/UI/TutorialOverlayViewModel.cs` | ViewModel | ~120 |
| `Assets/Scripts/Tutorial/WorldMarkers/WorldTutorialMarker.cs` | MonoBehaviour | ~100 |
| `Assets/Scripts/Tutorial/WorldMarkers/WorldMarkerPool.cs` | Class | ~60 |
| `Assets/Scripts/Tutorial/Systems/TutorialBridgeSystem.cs` | SystemBase | ~50 |
| `Assets/Scripts/Tutorial/CompletionDetectors/ICompletionDetector.cs` | Interface | ~15 |
| `Assets/Scripts/Tutorial/CompletionDetectors/InputCompletionDetector.cs` | Class | ~40 |
| `Assets/Scripts/Tutorial/CompletionDetectors/UIScreenCompletionDetector.cs` | Class | ~30 |
| `Assets/Scripts/Tutorial/CompletionDetectors/CustomEventCompletionDetector.cs` | Class | ~25 |
| `Assets/Editor/TutorialWorkstation/TutorialGraphEditor.cs` | Editor | ~400 |
| `Assets/Editor/TutorialWorkstation/Modules/TutorialWorkstationModule.cs` | Editor | ~200 |

**Total estimated:** ~1,775 lines

---

## Performance Considerations

- `TutorialService` only evaluates prerequisites when no tutorial is active — skips evaluation entirely during active tutorial
- Spotlight mask uses a single-quad shader with UV-space cutout — no render texture, no stencil buffer
- `WorldTutorialMarker` pool is pre-warmed with 3 instances — no runtime instantiation
- Completion detection uses event subscription (not polling) — zero per-frame cost for InputPerformed, UIScreenOpened, etc.
- `TutorialTriggerSystem` remains Burst-compiled — the new `TutorialStartRequest` entity creation uses ECB (no managed code in job)

---

## Testing Strategy

- Unit test step progression: start sequence → complete each step → verify OnTutorialCompleted fires
- Unit test branching: Branch step with true condition → verify correct path taken
- Unit test persistence: complete tutorial → restart → verify IsTutorialCompleted returns true
- Unit test skip: skip tutorial → verify all steps skipped, completion marked
- Integration test: player enters trigger volume → TutorialBridgeSystem starts sequence → UI overlay appears
- Integration test: Highlight step → verify spotlight mask appears around target element
- Integration test: ForcedAction step → perform action → verify auto-advance
- Editor test: TutorialGraphEditor creates and wires steps correctly
