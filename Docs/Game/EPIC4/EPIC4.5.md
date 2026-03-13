# EPIC 4.5: The Three-Act Structure

**Status**: Planning
**Epic**: EPIC 4 — District Graph & Expedition Structure
**Priority**: High — Pacing driver for each district
**Dependencies**: EPIC 4.4 (Zone Structure), EPIC 3 (The Front — FrontState, FrontPhase)

---

## Overview

Each district's gameplay naturally divides into three acts based on the Front's advance and the player's progress. This is not a scripted sequence — it emerges from the interaction between FrontState phases, zone conversion states, and player completion metrics. Act 1 (Freedom) offers open exploration with most zones safe. Act 2 (Squeeze) applies pressure as the Front visibly advances and routes narrow. Act 3 (Intensity) confines the player to restricted combat survival. Returning to a previously cleared district always starts at Act 3 because the Front has advanced while the player was away. The per-district run loop targets 20-30 minutes.

---

## Component Definitions

### ActState (IComponentData)

```csharp
// File: Assets/Scripts/Expedition/Components/ActComponents.cs
using Unity.Entities;

namespace Hollowcore.Expedition
{
    public enum ActPhase : byte
    {
        /// <summary>Freedom — Front Phase 1, most zones Safe/Contested, multiple routes open.</summary>
        Act1_Freedom = 0,

        /// <summary>Squeeze — Front Phase 2-3, Front visibly advancing, routes narrowing.</summary>
        Act2_Squeeze = 1,

        /// <summary>Intensity — Front Phase 3-4, restricted zones, combat survival.</summary>
        Act3_Intensity = 2,
    }

    /// <summary>
    /// Per-district act state. Lives on the district entity (same entity as FrontState).
    /// Derived from FrontState + zone conversion + player progress — NOT directly set by designers.
    /// </summary>
    public struct ActState : IComponentData
    {
        /// <summary>Current act phase for this district.</summary>
        public ActPhase CurrentAct;

        /// <summary>Previous act phase (for detecting transitions).</summary>
        public ActPhase PreviousAct;

        /// <summary>Elapsed time in current act (seconds).</summary>
        public float ActElapsedTime;

        /// <summary>Total district time across all visits (seconds).</summary>
        public float TotalDistrictTime;

        /// <summary>True if this district was previously cleared and revisited (permanent Act 3).</summary>
        public bool IsRevisitedCleared;
    }
}
```

### ActThresholds (IComponentData)

```csharp
// File: Assets/Scripts/Expedition/Components/ActComponents.cs (continued)
namespace Hollowcore.Expedition
{
    /// <summary>
    /// Configurable thresholds that determine act transitions.
    /// Baked from DistrictDefinitionSO or global config. Per-district entity.
    /// </summary>
    public struct ActThresholds : IComponentData
    {
        // --- Zone Conversion Thresholds ---
        /// <summary>Fraction of zones that must be Contested+ to trigger Act 2 (0.0-1.0).</summary>
        public float Act2_ZoneContestedFraction;

        /// <summary>Fraction of zones that must be Hostile+ to trigger Act 3 (0.0-1.0).</summary>
        public float Act3_ZoneHostileFraction;

        // --- Front Phase Thresholds ---
        /// <summary>FrontPhase at which Act 2 begins (inclusive). Default: Phase2.</summary>
        public byte Act2_MinFrontPhase;

        /// <summary>FrontPhase at which Act 3 begins (inclusive). Default: Phase3.</summary>
        public byte Act3_MinFrontPhase;

        // --- Player Progress Thresholds ---
        /// <summary>Fraction of zones visited that contributes to act progression.</summary>
        public float Act2_MinVisitedFraction;

        // --- Time Fallback ---
        /// <summary>If total district time exceeds this (seconds), force Act 2. 0 = disabled.</summary>
        public float Act2_TimeFallbackSeconds;

        /// <summary>If total district time exceeds this (seconds), force Act 3. 0 = disabled.</summary>
        public float Act3_TimeFallbackSeconds;
    }
}
```

### ActTransitionEvent (IComponentData, IEnableableComponent)

```csharp
// File: Assets/Scripts/Expedition/Components/ActComponents.cs (continued)
namespace Hollowcore.Expedition
{
    /// <summary>
    /// Ephemeral event fired when the act changes. Baked disabled, enabled by ActTransitionSystem.
    /// Consumed by music, UI, and atmosphere systems in the same frame, then disabled.
    /// </summary>
    public struct ActTransitionEvent : IComponentData, IEnableableComponent
    {
        public ActPhase FromAct;
        public ActPhase ToAct;
    }
}
```

---

## Systems

### ActDetectionSystem

```csharp
// File: Assets/Scripts/Expedition/Systems/ActDetectionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: FrontAdvanceSystem (EPIC 3)
//
// Evaluates act state each frame based on emergent conditions. No scripted triggers.
//
// For each district entity with ActState + ActThresholds + FrontState:
//
// 1. If IsRevisitedCleared → force Act3_Intensity, skip evaluation
//
// 2. Compute zone metrics:
//    a. Count total active zones in district
//    b. Count zones at each FrontZoneState level (Safe, Contested, Hostile, Overrun)
//    c. contestedFraction = (Contested + Hostile + Overrun) / total
//    d. hostileFraction = (Hostile + Overrun) / total
//    e. visitedFraction = visitedZones / total
//
// 3. Compute candidate act:
//    candidateAct = Act1_Freedom  (default)
//
//    If ANY of the following are true → candidateAct = Act2_Squeeze:
//      - FrontState.Phase >= Act2_MinFrontPhase
//      - contestedFraction >= Act2_ZoneContestedFraction
//      - visitedFraction >= Act2_MinVisitedFraction
//      - TotalDistrictTime >= Act2_TimeFallbackSeconds (if > 0)
//
//    If ANY of the following are true → candidateAct = Act3_Intensity:
//      - FrontState.Phase >= Act3_MinFrontPhase
//      - hostileFraction >= Act3_ZoneHostileFraction
//      - TotalDistrictTime >= Act3_TimeFallbackSeconds (if > 0)
//
// 4. Acts only escalate, never de-escalate within a single visit:
//    newAct = max(CurrentAct, candidateAct)
//
// 5. If newAct != CurrentAct:
//    a. Set PreviousAct = CurrentAct
//    b. Set CurrentAct = newAct
//    c. Reset ActElapsedTime = 0
//    d. Enable ActTransitionEvent with FromAct/ToAct
//
// 6. Increment ActElapsedTime and TotalDistrictTime by deltaTime
```

### ActTransitionSystem

```csharp
// File: Assets/Scripts/Expedition/Systems/ActTransitionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: ActDetectionSystem
//
// Responds to ActTransitionEvent by triggering downstream effects.
//
// For each entity with enabled ActTransitionEvent:
//
// 1. Music transition:
//    a. Signal MusicManager with new act phase for cross-fade
//    b. Act1 → exploration theme, Act2 → tension theme, Act3 → combat theme
//
// 2. UI warnings:
//    a. Act1→Act2: "The Front advances..." banner (3s)
//    b. Act2→Act3: "District critical — retreat or fight" banner (5s)
//
// 3. Atmosphere shift:
//    a. Signal post-processing volume blend:
//       Act1 = normal, Act2 = desaturated + increased contrast, Act3 = red tint + vignette
//    b. Signal ambient audio change (EPIC 3 feeds Front-specific ambience)
//
// 4. Gameplay modifiers:
//    a. Act2: +25% enemy spawn rate modifier, +10% loot quality modifier
//    b. Act3: +50% enemy spawn rate, +25% loot quality, elite spawn chance doubled
//    → Applied via RunModifierStack (framework)
//
// 5. Disable ActTransitionEvent after processing
```

### ActRevisitSystem

```csharp
// File: Assets/Scripts/Expedition/Systems/ActRevisitSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: InitializationSystemGroup
//
// Runs once when a district finishes loading (after DistrictDeltaApplySystem).
// Checks if the district was previously cleared.
//
// 1. Read district's GraphNodeState.CompletionMask
// 2. If IsMainChainComplete:
//    a. Set ActState.IsRevisitedCleared = true
//    b. Set ActState.CurrentAct = Act3_Intensity (permanent)
//    c. Enable ActTransitionEvent (triggers Act 3 atmosphere immediately)
//    d. Seed new content: echoes, reanimated bodies, rare events
//       (create entities via ECB from district's revisit content table)
// 3. If not previously cleared but visited before:
//    a. Restore ActState from DistrictSaveState (may already be Act 2 or 3)
//    b. Front has advanced off-screen → act may escalate on next ActDetectionSystem tick
```

---

## Setup Guide

1. **Add ActState + ActThresholds + ActTransitionEvent** to the district entity prefab (same entity that carries FrontState)
2. **Bake ActThresholds** from DistrictDefinitionSO or a global `ActConfig` ScriptableObject:
   - Defaults: Act2_ZoneContestedFraction=0.3, Act3_ZoneHostileFraction=0.5, Act2_MinFrontPhase=2, Act3_MinFrontPhase=3
   - Act2_TimeFallbackSeconds=600 (10 min), Act3_TimeFallbackSeconds=1200 (20 min)
3. **Bake ActTransitionEvent** as IEnableableComponent (baked disabled)
4. **Wire music bridge**: ActTransitionSystem signals a static `MusicTransitionQueue` or managed system bridge (follows DamageVisualQueue pattern)
5. **Wire UI banner bridge**: ActTransitionSystem signals `ActUIBridge` MonoBehaviour adapter
6. **Wire post-processing bridge**: volume blend targets indexed by ActPhase (0, 1, 2)
7. **Configure RunModifierStack** entries for Act 2 and Act 3 gameplay modifiers

---

## Verification

- [ ] ActState initializes to Act1_Freedom on fresh district entry
- [ ] Act escalates to Act2_Squeeze when Front reaches Phase 2 or zone contested fraction threshold
- [ ] Act escalates to Act3_Intensity when Front reaches Phase 3 or zone hostile fraction threshold
- [ ] Acts never de-escalate within a single visit (Act 3 → Act 2 is impossible)
- [ ] Time fallback triggers act transitions if player stalls
- [ ] ActTransitionEvent fires exactly once per act change
- [ ] Music cross-fades on act transition
- [ ] UI banner displays on act transition with correct text
- [ ] Post-processing volume blends to correct profile per act
- [ ] Gameplay modifiers (spawn rate, loot quality) apply correctly per act
- [ ] Returning to a cleared district immediately sets Act3_Intensity
- [ ] Returning to a cleared district seeds new content (echoes, bodies, events)
- [ ] Per-district run loop completes in approximately 20-30 minutes at normal pace
- [ ] IsRevisitedCleared flag persists across save/load

---

## Validation

```csharp
// File: Assets/Scripts/Expedition/Components/ActComponents.cs (validation helper)
namespace Hollowcore.Expedition
{
    public partial struct ActThresholds
    {
#if UNITY_EDITOR
        /// <summary>Called by ActConfigSO.OnValidate or baker to sanity-check thresholds.</summary>
        public static void Validate(ActThresholds t, string context)
        {
            if (t.Act2_ZoneContestedFraction <= 0f || t.Act2_ZoneContestedFraction >= 1f)
                Debug.LogWarning($"[{context}] Act2_ZoneContestedFraction={t.Act2_ZoneContestedFraction} — should be (0,1)");
            if (t.Act3_ZoneHostileFraction <= t.Act2_ZoneContestedFraction)
                Debug.LogWarning($"[{context}] Act3 hostile threshold <= Act2 contested threshold — acts may skip Act 2");
            if (t.Act2_MinFrontPhase >= t.Act3_MinFrontPhase)
                Debug.LogWarning($"[{context}] Act2 front phase >= Act3 — acts may skip Act 2");
            if (t.Act2_TimeFallbackSeconds > 0 && t.Act3_TimeFallbackSeconds > 0
                && t.Act2_TimeFallbackSeconds >= t.Act3_TimeFallbackSeconds)
                Debug.LogError($"[{context}] Act2 time fallback >= Act3 time fallback — Act 2 would never trigger via time");
        }
#endif
    }
}
```

---

## Editor Tooling

```csharp
// File: Assets/Editor/ExpeditionWorkstation/ActTimeline.cs
// Custom editor panel: visual timeline showing act transitions for a district.
//
// Features:
//   - Horizontal timeline bar (0 → TargetRunMinutes)
//   - Three colored regions: green(Act1), yellow(Act2), red(Act3)
//   - Draggable threshold markers on timeline for time fallbacks
//   - Zone fraction gauges: contested% and hostile% shown as fill bars
//   - Front phase indicator with phase boundaries
//   - "Simulate Act Progression" slider: drag to simulate time progression
//     Shows which act would be active at each point given configurable Front advance rate
//   - Play mode: real-time act state display with transition event log
//   - Per-district dropdown to compare different ActThresholds configurations
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Expedition/Components/ActRuntimeConfig.cs
using Unity.Entities;

namespace Hollowcore.Expedition
{
    /// <summary>
    /// Runtime-tunable act detection overrides.
    /// Allows designers to adjust pacing without restarting.
    /// </summary>
    public struct ActRuntimeConfig : IComponentData
    {
        /// <summary>Multiplier on Act2_TimeFallbackSeconds. 1.0 = default. 0 = disable time fallback.</summary>
        public float Act2TimeFallbackScale;

        /// <summary>Multiplier on Act3_TimeFallbackSeconds.</summary>
        public float Act3TimeFallbackScale;

        /// <summary>Override: force specific act for all districts. -1 = disabled.</summary>
        public int DebugForceAct;

        /// <summary>Multiplier on all gameplay modifiers (spawn rate, loot quality). Default 1.0.</summary>
        public float GameplayModifierScale;

        public static ActRuntimeConfig Default => new()
        {
            Act2TimeFallbackScale = 1.0f,
            Act3TimeFallbackScale = 1.0f,
            DebugForceAct = -1,
            GameplayModifierScale = 1.0f,
        };
    }
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Expedition/Debug/ActStateDebugOverlay.cs
// In-game HUD overlay for act state debugging:
//
//   - Top banner: "ACT [1|2|3]: [Freedom|Squeeze|Intensity]" with act-specific color
//   - Sub-text: elapsed time in act, total district time
//   - Condition gauges:
//     - "Contested Zones: 4/12 (33%)" with threshold line at Act2 trigger
//     - "Hostile Zones: 2/12 (17%)" with threshold line at Act3 trigger
//     - "Front Phase: 2" with phase markers
//     - "Visited: 7/12 (58%)" with Act2 visit threshold marker
//   - Transition log: last 5 act transitions with timestamps
//   - Gameplay modifiers active: "+25% spawn rate, +10% loot quality"
//   - IsRevisitedCleared flag display
//   - Toggle: F10 key or debug menu
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/ExpeditionWorkstation/ActProgressionSimulator.cs
// IExpeditionWorkstationModule — "Act Pacing" tab.
//
// "Simulate Pacing Curve" (per district config):
//   Input: ActThresholds, Front advance rate, player exploration speed (zones/minute)
//   Simulate 1-minute ticks for up to 40 minutes:
//     - Each tick: advance Front, mark zones visited based on exploration speed
//     - Record act at each tick
//   Output: timeline chart showing act transitions over time
//   Show: "Act 1 duration: 8m, Act 2 duration: 12m, Act 3 duration: 10m"
//
// "Compare Thresholds":
//   Side-by-side two ActThresholds configs with same simulation inputs
//   Highlights: which transitions earlier, total time in each act
//
// "Revisit Scenario":
//   Simulate: player clears district (20 min), leaves for 10 min, returns
//   Front has advanced off-screen → verify Act 3 triggers immediately on return
//   Verify: new content seeded (echo count, event count)
```
