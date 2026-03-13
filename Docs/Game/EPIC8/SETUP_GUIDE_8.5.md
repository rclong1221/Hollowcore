# EPIC 8.5 Setup Guide: Trace UI

**Status:** Planned
**Requires:** EPIC 8.1 (TraceState), EPIC 8.2 (TraceSourceEvent), EPIC 8.3 (TraceSinkEvent), Framework Combat/UI/ (CombatUIBridgeSystem pattern)

---

## Overview

Trace must be visible at all times. The HUD displays an always-on meter color-coded by threshold tier. Threshold crossings trigger pulse animations and audio stings. Every Trace change shows a brief popup notification with source or sink context. A screen-edge vignette effect intensifies as the player approaches the next threshold. The gate selection screen integrates Trace warnings on dirty contracts and sink perks.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| EPIC 8.1 | TraceState, TraceConfig | Source data for meter display |
| EPIC 8.2 | TraceSourceEvent | Triggers +Trace popups |
| EPIC 8.3 | TraceSinkEvent | Triggers -Trace popups |
| Framework UI | CombatUIBridgeSystem pattern | Architecture for ECS-to-UI bridge |

### New Setup Required
1. Create `TraceMeterUI` prefab in the HUD canvas
2. Create `TraceNotificationPool` with 4 popup objects
3. Create `TraceVignetteEffect` full-screen overlay
4. Create Trace audio assets
5. Initialize `TraceUINotificationQueue.Initialize()` in TraceBootstrapSystem
6. Add `TraceGateWarning` component to gate option widgets

---

## 1. TraceMeterUI Prefab

**Create:** `Assets/Prefabs/UI/HUD/TraceMeter.prefab`
**Place:** Main HUD canvas, anchored top-center or top-right

### 1.1 Prefab Structure
```
TraceMeter (RectTransform)
  +-- MeterBackground (Image)
  +-- MeterFill (Image, Filled type, segmented by thresholds)
  +-- ThresholdMarker_2 (Image, vertical pip at Trace 2)
  +-- ThresholdMarker_3 (Image, vertical pip at Trace 3)
  +-- ThresholdMarker_4 (Image, vertical pip at Trace 4)
  +-- NumericDisplay (TextMeshProUGUI, "TRACE: 3 / 6")
  +-- TierIcon (Image, changes per threshold tier)
  +-- PulseOverlay (Image, white flash, hidden by default)
```

### 1.2 Color Zones
| Trace Range | Tier | Color | Hex | Icon |
|-------------|------|-------|-----|------|
| 0-1 | Baseline | Green | #4ADE80 | Shield |
| 2 | Hunters | Yellow | #FBBF24 | Crosshair |
| 3 | Gates | Orange | #FB923C | Lock |
| 4+ | Escalation | Red | #EF4444 | Skull |

### 1.3 TraceMeterUI Component Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `MeterFillImage` | Reference to the fill Image | null | Required |
| `NumericText` | Reference to the TMP text | null | Required |
| `TierIcon` | Reference to the tier icon Image | null | Required |
| `PulseOverlay` | Reference to the pulse flash Image | null | Required |
| `PulseDuration` | Seconds for pulse animation | 0.5 | 0.2-1.0 |
| `ThresholdMarkers` | Array of threshold pip Images | [] | -- |

### 1.4 Meter Fill Material
**Create:** `Assets/Materials/UI/TraceMeterFill.mat`

Segmented gradient shader with color stops at threshold boundaries:
- 0% to 33%: Green gradient
- 33% to 50%: Yellow gradient
- 50% to 75%: Orange gradient
- 75% to 100%: Red gradient

**Tuning tip:** The fill should lerp smoothly when Trace changes (0.3s interpolation). Instant jumps feel jarring.

---

## 2. Trace Notification Popups

**Create:** `Assets/Prefabs/UI/HUD/TraceNotificationPopup.prefab`
**Place:** Pool of 4 instances in the HUD canvas near the Trace meter

### 2.1 Popup Structure
```
TraceNotificationPopup (RectTransform, VerticalLayoutGroup)
  +-- AmountText (TextMeshProUGUI, "+1 TRACE" or "-1 TRACE")
  +-- CategoryIcon (Image, icon per source/sink type)
  +-- ContextLabel (TextMeshProUGUI, "Echo: Memory Fragment")
```

### 2.2 Popup Behavior
| Property | Value |
|----------|-------|
| Max visible | 4 (oldest recycled) |
| Display duration | 3 seconds |
| Fade out | 0.5 second alpha fade |
| Slide animation | Slides up 30px over lifetime |
| +Trace color | Red text |
| -Trace color | Green text |

### 2.3 Category Icons
| Category | Icon Description |
|----------|-----------------|
| EchoMission | Spiral symbol |
| AlarmExtraction | Bell/alarm icon |
| DirtyContract | Handshake/contract icon |
| LateBossExtraction | Clock/skull icon |
| TimePassed | Hourglass icon |
| Backtracking | Arrow-return icon |
| SideGoal | Checkmark/eraser icon |
| GatePerk | Gate/shield icon |
| Bribe | Coin/key icon |
| HunterLoot | Hunter skull icon |

---

## 3. TraceVignetteEffect

**Create:** `Assets/Prefabs/UI/HUD/TraceVignette.prefab`
**Place:** Full-screen UI Image behind all HUD elements

### 3.1 Vignette Material
**Create:** `Assets/Materials/UI/TraceVignette.mat`

Radial gradient shader with properties:
| Property | Description | Default | Range |
|----------|-------------|---------|-------|
| `_VignetteColor` | Edge color matching NEXT threshold tier | -- | Color |
| `_VignetteIntensity` | Opacity/strength of effect | 0.0 | 0.0-1.0 |

### 3.2 Vignette Behavior
- Intensity ramps from 0 (just crossed threshold) to max (about to cross next)
- Color matches the NEXT threshold tier (warning of what is coming)
- Only visible when intensity > 0.05 (avoids constant subtle overlay at low Trace)
- Disabled entirely at Trace 0

**Tuning tip:** The vignette should be subtle -- players should notice it subconsciously. If testers report feeling "something is wrong" without being able to articulate it, the vignette intensity is calibrated correctly.

---

## 4. TraceGateWarning

**Create:** Add `TraceGateWarning` MonoBehaviour to each gate option widget in the gate screen prefab.

### 4.1 Display Rules
| Condition | Display |
|-----------|---------|
| `GateTraceModifier.TraceChange > 0` | Red badge: "+1 TRACE" |
| `GateTraceModifier.TraceChange < 0, IsPaidOption=false` | Green badge: "-1 TRACE" |
| `GateTraceModifier.TraceChange < 0, IsPaidOption=true` | Green badge: "-1 TRACE (200 Currency)" |
| No GateTraceModifier | No badge |

---

## 5. Audio Assets

**Create at:** `Assets/Audio/SFX/Trace/`

| File | Trigger | Description |
|------|---------|-------------|
| `trace_gained.wav` | Any +Trace event | Short warning tone, pitch increases with Trace level |
| `trace_reduced.wav` | Any -Trace event | Relief tone (descending) |
| `threshold_up_2.wav` | Crossing into Trace 2 (Hunters) | Escalating sting, tension |
| `threshold_up_3.wav` | Crossing into Trace 3 (Gates) | More urgent sting |
| `threshold_up_4.wav` | Crossing into Trace 4 (Escalation) | Full danger sting |
| `threshold_down.wav` | Dropping below any threshold | De-escalation sting |
| `hunter_warning.wav` | First hunter activation at Trace 2 | Distinct hunter horn/siren |

---

## 6. TraceUINotificationQueue

**File:** `Assets/Scripts/Trace/UI/TraceUINotificationQueue.cs`

Static NativeQueue bridge following the `DamageVisualQueue` pattern.

### 6.1 Initialization
Call `TraceUINotificationQueue.Initialize()` in `TraceBootstrapSystem.OnCreate()`.

### 6.2 Enqueue Flow
- `TraceSourceSystem` calls `TraceUINotificationQueue.Enqueue(amount, categoryId, label, isReduction=false)` for each processed source event
- `TraceSinkSystem` calls `TraceUINotificationQueue.Enqueue(amount, categoryId, label, isReduction=true)` for each processed sink event

### 6.3 Drain Flow
`TraceUIBridgeSystem` (PresentationSystemGroup) drains the queue each frame and spawns popup widgets via `TraceMeterUI`.

---

## 7. TraceUIBridgeSystem

**File:** `Assets/Scripts/Trace/Systems/TraceUIBridgeSystem.cs`
**Filter:** ClientSimulation | LocalSimulation
**Group:** PresentationSystemGroup

Managed SystemBase that:
1. Reads TraceState singleton (CurrentTrace, MaxTrace, PeakTrace)
2. Reads TraceUIState singleton (animation state)
3. Detects threshold crossings and triggers pulses + audio
4. Animates DisplayedTrace via lerp (~0.3s)
5. Calculates vignette intensity from time accumulation progress
6. Pushes to TraceMeterUI and TraceVignetteEffect via cached references
7. Drains TraceUINotificationQueue and spawns popup widgets

**Tuning tip:** Cache `TraceMeterUI` and `TraceVignetteEffect` references using `Object.FindFirstObjectByType` on first frame. Do not search every frame.

---

## Scene & Subscene Checklist
| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| HUD Canvas | `TraceMeter` prefab (top-center or top-right) | Always visible during expedition |
| HUD Canvas | `TraceNotificationPool` (4 popup instances) | Near Trace meter |
| HUD Canvas | `TraceVignette` (full-screen Image, behind HUD elements) | Radial gradient material |
| Gate Screen prefab | `TraceGateWarning` on each gate option widget | Reads GateTraceModifier |
| `Assets/Audio/SFX/Trace/` | 7 audio files | Gained, reduced, threshold stings, hunter warning |
| `Assets/Materials/UI/` | `TraceMeterFill.mat`, `TraceVignette.mat` | Shader materials |
| TraceBootstrapSystem | `TraceUINotificationQueue.Initialize()` | Creates the notification NativeQueue |

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| TraceMeterUI not on HUD canvas | Trace meter invisible | Place prefab in active HUD canvas |
| TraceUINotificationQueue not initialized | Popups never appear, queue not created | Call Initialize() in TraceBootstrapSystem |
| Vignette behind HUD elements wrong | Vignette covers buttons/text | Set vignette sorting order behind all interactive HUD elements |
| Vignette visible at Trace 0 | Constant slight screen-edge tint | Check `if (intensity < 0.05) SetActive(false)` |
| Pulse animation fires every frame | Constant flashing | PulseTimer must count down and stop; only trigger on threshold CROSSING |
| Audio sting plays on load | Threshold "crossed" on initial TraceState read | Set PreviousTrace = CurrentTrace on first frame (skip initial detection) |
| DisplayedTrace jumps instead of lerping | Jarring visual on Trace change | Use `Mathf.Lerp(displayed, target, deltaTime / 0.3f)` |
| Gate warning badge always hidden | Players don't see Trace cost/reward on gates | Verify TraceGateWarning reads GateTraceModifier component |

---

## Verification

- [ ] Trace meter visible on HUD at all times during expedition
- [ ] Meter fill color matches current threshold tier (green/yellow/orange/red)
- [ ] Numeric display shows correct "TRACE: X / Y" values
- [ ] Threshold markers visible on meter bar at positions 2, 3, 4
- [ ] +1 Trace popup appears with correct category icon and red text
- [ ] -1 Trace popup appears with green text and sink category icon
- [ ] Popups stack correctly (max 4, oldest recycled)
- [ ] Popups auto-dismiss after 3 seconds with fade
- [ ] Pulse animation fires on threshold crossing (white flash, 0.5s decay)
- [ ] Audio sting plays on threshold crossing (correct variant per tier)
- [ ] Vignette intensifies as AccumulatedTime approaches next Trace point
- [ ] Vignette color matches the NEXT threshold tier
- [ ] Vignette invisible at Trace 0
- [ ] Gate screen shows "+1 TRACE" warning on dirty contract gates
- [ ] Gate screen shows "-1 TRACE" perk on sink gates
- [ ] Bribe gates show currency cost alongside Trace badge
- [ ] DisplayedTrace animates smoothly (no instant jumps)
