# EPIC 8.5: Trace UI

**Status**: Planning
**Epic**: EPIC 8 — Trace (Global Pressure Meter)
**Priority**: Medium — Players must see pressure to feel it
**Dependencies**: EPIC 8.1 (TraceState), EPIC 8.2 (TraceSourceEvent), EPIC 8.3 (TraceSinkEvent); Framework: Combat/UI/CombatUIBridgeSystem pattern, EPIC 6 (Gate Selection UI)

---

## Overview

Trace must be visible at all times. The HUD displays an always-on meter color-coded by threshold: green at baseline, yellow when hunters are active, orange when gates are reduced, red at full escalation. Threshold crossings trigger a pulse animation and audio sting. Every Trace change produces a brief popup notification showing the source or sink. The gate selection screen integrates Trace warnings on dirty contracts. A screen-edge vignette effect intensifies as the player approaches the next threshold.

---

## Component Definitions

### TraceUIState (IComponentData)

```csharp
// File: Assets/Scripts/Trace/Components/TraceUIComponents.cs
using Unity.Entities;

namespace Hollowcore.Trace
{
    /// <summary>
    /// Client-side singleton tracking UI animation state for Trace display.
    /// Separate from TraceState to keep presentation decoupled from simulation.
    /// </summary>
    public struct TraceUIState : IComponentData
    {
        /// <summary>Displayed Trace value (lerps toward TraceState.CurrentTrace for smooth animation).</summary>
        public float DisplayedTrace;

        /// <summary>Previous frame's CurrentTrace (for detecting changes).</summary>
        public int PreviousTrace;

        /// <summary>Timer for threshold crossing pulse animation (counts down to 0).</summary>
        public float PulseTimer;

        /// <summary>The threshold level that was just crossed (for pulse color selection).</summary>
        public byte PulseThresholdLevel;

        /// <summary>Current vignette intensity (0-1, driven by proximity to next threshold).</summary>
        public float VignetteIntensity;
    }
}
```

### TraceUINotification

```csharp
// File: Assets/Scripts/Trace/Components/TraceUIComponents.cs (continued)
using Unity.Collections;

namespace Hollowcore.Trace
{
    /// <summary>
    /// Queued notification for Trace change popups.
    /// Follows the DamageVisualQueue static pattern from the framework.
    /// </summary>
    public struct TraceUINotification
    {
        /// <summary>Positive = Trace gained, negative = Trace lost.</summary>
        public int Amount;

        /// <summary>Source or sink category for icon/color selection.</summary>
        public byte CategoryId;

        /// <summary>Display text (e.g., "Echo: Memory Fragment" or "Side Goal: Kill Witness").</summary>
        public FixedString64Bytes Label;

        /// <summary>True if this is a sink (Trace reduced).</summary>
        public bool IsReduction;
    }
}
```

---

## Static Notification Queue

### TraceUINotificationQueue

```csharp
// File: Assets/Scripts/Trace/UI/TraceUINotificationQueue.cs
using Unity.Collections;

namespace Hollowcore.Trace.UI
{
    /// <summary>
    /// Static bridge between ECS Trace systems and MonoBehaviour UI.
    /// Follows the DamageVisualQueue pattern from the framework.
    /// </summary>
    public static class TraceUINotificationQueue
    {
        public static NativeQueue<TraceUINotification> Pending;

        public static void Initialize()
        {
            if (!Pending.IsCreated)
                Pending = new NativeQueue<TraceUINotification>(Allocator.Persistent);
        }

        public static void Dispose()
        {
            if (Pending.IsCreated)
                Pending.Dispose();
        }

        public static void Enqueue(int amount, byte categoryId,
            FixedString64Bytes label, bool isReduction)
        {
            if (!Pending.IsCreated) return;
            Pending.Enqueue(new TraceUINotification
            {
                Amount = amount,
                CategoryId = categoryId,
                Label = label,
                IsReduction = isReduction
            });
        }
    }
}
```

---

## Systems

### TraceUIBridgeSystem

```csharp
// File: Assets/Scripts/Trace/Systems/TraceUIBridgeSystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
//
// Managed SystemBase — bridges ECS TraceState to MonoBehaviour UI.
//
// Each frame:
//   1. Read TraceState singleton (CurrentTrace, MaxTrace, PeakTrace)
//   2. Read TraceUIState singleton
//   3. Detect change: if CurrentTrace != PreviousTrace:
//      a. If crossed a threshold boundary: trigger pulse
//         - Set PulseTimer = PulseDuration (config)
//         - Set PulseThresholdLevel to the crossed threshold
//         - Fire audio sting via TraceAudioBridge
//      b. Update PreviousTrace
//   4. Animate DisplayedTrace toward CurrentTrace (lerp, ~0.3s)
//   5. Calculate VignetteIntensity:
//      - NextThreshold = smallest threshold > CurrentTrace
//      - If no time accumulator data: intensity = 0
//      - Else: intensity = AccumulatedTime / TimePerTracePoint (0-1 ramp toward next point)
//      - Scale by proximity factor (only visible above 60% progress)
//   6. Decrement PulseTimer
//   7. Push to UI registry:
//      - TraceMeterUI.SetTrace(DisplayedTrace, MaxTrace, thresholdColor)
//      - TraceMeterUI.SetPulse(PulseTimer > 0, PulseThresholdLevel)
//      - TraceVignetteUI.SetIntensity(VignetteIntensity)
//   8. Drain TraceUINotificationQueue → spawn popup widgets
```

---

## MonoBehaviour UI Components

### TraceMeterUI

```csharp
// File: Assets/Scripts/Trace/UI/TraceMeterUI.cs
// MonoBehaviour on the always-visible HUD Trace meter.
//
// Visual elements:
//   - Fill bar: normalized 0-MaxTrace, segmented by thresholds
//   - Threshold markers: vertical pips at 2, 3, 4 on the bar
//   - Color zones:
//     * 0-1 (Baseline): green (#4ADE80)
//     * 2   (Hunters):  yellow (#FBBF24)
//     * 3   (Gates):    orange (#FB923C)
//     * 4+  (Escalation): red (#EF4444)
//   - Numeric display: "TRACE: 3 / 6"
//   - Pulse overlay: white flash on threshold crossing, decays over 0.5s
//   - Icon: changes per current threshold tier (shield, crosshair, lock, skull)
//
// Methods:
//   void SetTrace(float displayedTrace, int maxTrace, Color thresholdColor)
//   void SetPulse(bool active, byte thresholdLevel)
//   void SetSegmentHighlight(int segmentIndex, bool active)
```

### TraceNotificationPopup

```csharp
// File: Assets/Scripts/Trace/UI/TraceNotificationPopup.cs
// MonoBehaviour for individual Trace change popups.
//
// Spawned by TraceMeterUI when TraceUIBridgeSystem delivers notifications.
// Pooled (max 4 visible, oldest recycled).
//
// Visual:
//   - "+1 TRACE" (red, slides up) or "-1 TRACE" (green, slides up)
//   - Category icon (echo, alarm, contract, timer, backtrack, side goal, gate, bribe, hunter)
//   - Context label below: "Echo: Memory Fragment" or "Side Goal: Kill Witness"
//   - Auto-dismiss after 3 seconds with fade
//
// Methods:
//   void Show(TraceUINotification notification)
//   void Dismiss()
```

### TraceVignetteEffect

```csharp
// File: Assets/Scripts/Trace/UI/TraceVignetteEffect.cs
// MonoBehaviour controlling a screen-edge vignette overlay.
//
// Uses a full-screen UI Image with a radial gradient material.
// Color matches the NEXT threshold tier (warning of what's coming).
// Intensity ramps from 0 (just crossed threshold) to max (about to cross next).
//
// Properties:
//   - Material: _VignetteColor (Color), _VignetteIntensity (float 0-1)
//   - Only visible when intensity > 0.05 (avoids constant subtle overlay at low Trace)
//   - Disabled entirely at Trace 0
//
// Methods:
//   void SetIntensity(float intensity)
//   void SetColor(Color warningColor)
```

### TraceGateWarning

```csharp
// File: Assets/Scripts/Trace/UI/TraceGateWarning.cs
// MonoBehaviour added to gate selection screen UI elements.
//
// When a gate option has GateTraceModifier:
//   - If TraceChange > 0 (dirty contract): show red "+1 TRACE" warning badge
//   - If TraceChange < 0 (sink gate): show green "-1 TRACE" reward badge
//   - If IsPaidOption: show currency cost alongside Trace change
//
// Reads GateTraceModifier from the gate option entity via a managed bridge.
//
// Methods:
//   void Configure(int traceChange, int bribeCost, bool isPaidOption)
//   void Hide()
```

---

## Audio Integration

### TraceAudioBridge

```csharp
// File: Assets/Scripts/Trace/UI/TraceAudioBridge.cs
// Static class for Trace-related audio events.
//
// Audio events:
//   - TraceGained: short warning tone, pitch increases with Trace level
//   - TraceReduced: relief tone (descending)
//   - ThresholdCrossed_Up: escalating sting (unique per threshold tier)
//   - ThresholdCrossed_Down: de-escalation sting
//   - HunterWarning: distinct hunter horn/siren at Trace 2+ first activation
//
// Implementation: calls into existing audio system (SoundEventRequest pattern)
```

---

## Setup Guide

1. **Create `Assets/Scripts/Trace/UI/` folder** for all MonoBehaviour UI components
2. **Create HUD prefab elements**:
   - `TraceMeter` object in the main HUD canvas, anchored top-center or top-right
   - `TraceNotificationPool` container with 4 pre-instantiated `TraceNotificationPopup` objects
   - `TraceVignette` full-screen overlay image behind all HUD elements
3. **Create materials**:
   - `TraceMeterFill.mat`: segmented gradient (green→yellow→orange→red)
   - `TraceVignette.mat`: radial gradient shader with `_VignetteColor` and `_VignetteIntensity` properties
4. **Create audio assets** at `Assets/Audio/SFX/Trace/`:
   - `trace_gained.wav`, `trace_reduced.wav`
   - `threshold_up_2.wav`, `threshold_up_3.wav`, `threshold_up_4.wav`
   - `threshold_down.wav`, `hunter_warning.wav`
5. Initialize `TraceUINotificationQueue.Initialize()` in TraceBootstrapSystem
6. Add `TraceMeterUI` component to the HUD Trace meter GameObject
7. Add `TraceVignetteEffect` component to the vignette overlay
8. Add `TraceGateWarning` component to each gate option widget in the gate selection screen prefab
9. Wire `TraceUIBridgeSystem` to find `TraceMeterUI` and `TraceVignetteEffect` via `Object.FindFirstObjectByType` (cached on first frame)

---

## Verification

- [ ] Trace meter visible on HUD at all times during expedition
- [ ] Meter fill color matches current threshold tier (green/yellow/orange/red)
- [ ] Numeric display shows correct "TRACE: X / Y" values
- [ ] Threshold markers visible on meter bar at positions 2, 3, 4
- [ ] +1 Trace popup appears with correct category icon and context label
- [ ] -1 Trace popup appears in green with sink category
- [ ] Popups stack correctly (max 4, oldest recycled)
- [ ] Popups auto-dismiss after 3 seconds with fade
- [ ] Pulse animation fires on threshold crossing (white flash, 0.5s decay)
- [ ] Audio sting plays on threshold crossing (correct variant per tier)
- [ ] Screen-edge vignette intensifies as AccumulatedTime approaches next Trace point
- [ ] Vignette color matches the NEXT threshold tier
- [ ] Vignette invisible at Trace 0 and immediately after a threshold crossing
- [ ] Gate selection screen shows "+1 TRACE" warning on dirty contract options
- [ ] Gate selection screen shows "-1 TRACE" perk on sink gate options
- [ ] Bribe gates show currency cost alongside Trace change
- [ ] DisplayedTrace animates smoothly (no instant jumps)

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Trace/Debug/TraceUIDebugOverlay.cs
namespace Hollowcore.Trace.Debug
{
    /// <summary>
    /// Extended debug mode for the Trace UI, toggled via debug console.
    /// Layers additional info over the production Trace meter.
    /// </summary>
    // Overlay elements:
    //   1. Source/sink event log: persistent scrollable log (last 50 events)
    //      showing every TraceUINotification with timestamps
    //   2. AccumulatedTime bar: progress toward next time-based +1, with countdown
    //   3. BacktrackAccumulatedTime bar: progress toward next backtrack +1 (if backtracking)
    //   4. Threshold effect list: all active RunModifierStack entries from TraceThresholdEffect
    //   5. Vignette intensity value: numeric display of current vignette strength
    //   6. Pulse state: whether pulse is active, remaining duration, threshold level
    //   7. PeakTrace display: highest Trace reached this run
    //
    // Toggle: debug console command "trace_debug" or TraceWorkstation debug tab
}
```
