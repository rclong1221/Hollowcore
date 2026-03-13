# EPIC 3.1 Setup Guide: Front State & Phase Model

**Status:** Planned
**Requires:** Framework Roguelite/ (ZoneDefinitionSO, zone topology), EPIC 4.1 (ExpeditionGraphState, DistrictNode)

---

## Overview

The Front State & Phase Model is the core data layer for the district pressure system. Every district entity carries a `FrontState` tracking which phase the Front is in (Onset through Overrun), how far it has spread, and whether it is paused or contained. Every zone within a district carries a `FrontZoneData` entry determining its current safety level (Safe, Contested, Hostile, Overrun). A `FrontDefinitionSO` per district defines the spread pattern, advance curve, phase thresholds, and zone conversion order. This is the foundation that all other Front systems (advance, restrictions, pulses, bleed, counterplay) read from.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| District Prefab (Subscene) | Existing district entity | Receives FrontState and FrontZoneData |
| Framework | Roguelite/ zone system (ZoneDefinitionSO) | Zone topology and IDs |
| EPIC 4.1 | ExpeditionGraphState, DistrictNode | District-level graph for zone relationships |

### New Setup Required

1. Create the `Assets/Scripts/Front/` folder structure with assembly definition
2. Create `FrontComponents.cs` with all enums and component structs
3. Create `FrontDefinitionSO.cs` ScriptableObject definition
4. Create one `FrontDefinitionSO` asset per district (15 total)
5. Add `FrontAuthoring` to each district prefab
6. Create the 4 core systems (Init, PhaseEvaluation, ZoneConversion, PhaseChangedCleanup)
7. Reimport subscenes after adding authoring

---

## 1. Project Structure

**Create:** Assembly definition and folder structure

```
Assets/Scripts/Front/
    Hollowcore.Front.asmdef
    Components/
        FrontComponents.cs
    Definitions/
        FrontDefinitionSO.cs
        FrontDefinitionBlob.cs
    Systems/
        FrontStateInitSystem.cs
        FrontPhaseEvaluationSystem.cs
        FrontZoneConversionSystem.cs
        FrontPhaseChangedCleanupSystem.cs
    Authoring/
        FrontAuthoring.cs
```

### 1.1 Assembly Definition References

| Reference | Purpose |
|-----------|---------|
| `DIG.Shared` | Shared framework types |
| `Unity.Entities` | ECS core |
| `Unity.NetCode` | Ghost replication |
| `Unity.Collections` | NativeArrays, FixedStrings |
| `Unity.Burst` | Burst compilation |
| `Unity.Mathematics` | float2, float3, float4 |

---

## 2. Front Definition Assets

**Create:** `Assets > Create > Hollowcore/Front/Front Definition`
**Recommended location:** `Assets/Data/Front/Definitions/`
**Naming convention:** `FrontDef_[DistrictName].asset` -- e.g., `FrontDef_Necrospire.asset`

### 2.1 Identity

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **FrontDefinitionId** | Unique ID for this Front definition | (required) | Must be globally unique (1-15) |
| **DisplayName** | Human-readable name for debug/editor | (required) | Max 32 chars |
| **Description** | Narrative description of this Front type | (required) | TextArea |

### 2.2 Spread Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **SpreadPattern** | How the Front propagates through zones | Radial | Radial, Linear, Network |
| **OriginZoneIndex** | Zone where the Front starts spreading from | 0 | 0 to zone count - 1 |
| **ZoneConversionOrder** | Array of zone IDs in conversion priority order | (required) | Must contain all zone IDs |

**Spread Pattern Descriptions:**

| Pattern | Behavior | Best For |
|---------|----------|----------|
| **Radial** | Outward from origin in all directions | Corruption Bloom, infection spread |
| **Linear** | Advances along a single axis | Waterline Rise, flood, fire front |
| **Network** | Spreads along connected node edges | Reality Desync, Choir Crescendo, power grid failure |

### 2.3 Advance Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **BaseAdvanceSpeed** | SpreadProgress units per second at AdvanceRate 1.0 | 0.02 | 0.005-0.1 |
| **AdvanceCurve** | AnimationCurve mapping SpreadProgress (0-1) to speed multiplier | Linear(0,1 -> 1,1.5) | Must have 2+ keyframes |
| **OffScreenRateMultiplier** | Rate multiplier when district is not the active screen | 0.4 | 0.1-1.0 |

**Tuning tip:** BaseAdvanceSpeed of 0.02 means it takes ~50 seconds to advance 1.0 SpreadProgress at baseline. With the default AdvanceCurve (1.0 at start, 1.5 at end), the Front accelerates as it spreads. A full Phase 1-4 run takes approximately 3-4 minutes at AdvanceRate 1.0. Increase BaseAdvanceSpeed for more pressure, decrease for exploration-heavy districts.

### 2.4 Phase Thresholds

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **PhaseThresholds** | SpreadProgress values where each phase begins | [0, 0.25, 0.55, 0.8] | 4 entries, ascending, last < 1.0 |

| Index | Phase | Default Threshold | Meaning |
|-------|-------|------------------|---------|
| 0 | Phase1_Onset | 0.0 | Starts immediately |
| 1 | Phase2_Escalation | 0.25 | 25% spread |
| 2 | Phase3_Crisis | 0.55 | 55% spread |
| 3 | Phase4_Overrun | 0.80 | 80% spread |

**Tuning tip:** Phase 1 should last longest (25% of total spread) to give players time to orient. Phase 4 should feel like a countdown -- the gap between 0.8 and 1.0 is the "escape window." If playtesting shows Phase 3 feels too sudden, lower PhaseThresholds[2] to 0.45.

### 2.5 Zone Conversion

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **ZonesPerPhaseTransition** | Zones that convert per phase boundary crossing | 3 | 1-10 |
| **ZoneConversionDuration** | Seconds for a single zone to transition between states | 10 | 3-30 |

### 2.6 Visuals

| Field | Description | Default |
|-------|-------------|---------|
| **ZoneStateTints** | Color tint per FrontZoneState (4 entries) | [green, yellow, orange, red] |
| **BoundaryVFXPrefab** | Particle system for Front boundary edge | (recommended) |
| **PhaseAmbientAudioEvents** | Audio event names per phase (4 entries) | (recommended) |

---

## 3. District Prefab Setup -- FrontAuthoring

**Add Component:** `FrontAuthoring` on the district prefab root in the subscene

### 3.1 FrontAuthoring Inspector

| Field | Description | Default |
|-------|-------------|---------|
| **FrontDefinitionSO** | Reference to this district's Front definition | (required) |
| **OverrideBaseAdvanceSpeed** | Optional: override BaseAdvanceSpeed for testing | -1 (use SO value) |

### 3.2 Baked Components

The baker creates on the district entity:

| Component | Size | Ghost Config | Notes |
|-----------|------|-------------|-------|
| `FrontState` | 48 bytes | All | Phase, SpreadProgress, AdvanceRate, etc. |
| `FrontZoneData` buffer | 16 bytes x zone count | None (server authority) | External buffer (InternalBufferCapacity=0) |
| `FrontPhaseChangedTag` | 8 bytes | All | Baked disabled |

---

## 4. System Execution Order

| System | Update Group | Order | Purpose |
|--------|-------------|-------|---------|
| `FrontStateInitSystem` | InitializationSystemGroup | First | Populates FrontState and FrontZoneData on new districts |
| `FrontAdvanceSystem` (EPIC 3.2) | SimulationSystemGroup | Before PhaseEval | Increments SpreadProgress based on AdvanceRate |
| `FrontPhaseEvaluationSystem` | SimulationSystemGroup | After Advance | Checks thresholds, transitions phases |
| `FrontZoneConversionSystem` | SimulationSystemGroup | After PhaseEval | Converts zones Safe->Contested->Hostile->Overrun |
| `FrontPhaseChangedCleanupSystem` | SimulationSystemGroup, OrderLast | Last | Disables FrontPhaseChangedTag (one-frame event) |

---

## 5. FrontPhaseChangedTag Usage Pattern

`FrontPhaseChangedTag` is an `IEnableableComponent` baked disabled. It enables for exactly one frame when `FrontState.Phase` changes.

### 5.1 For Reactive Systems

Any system that needs to respond to phase transitions (Pulses, UI, Bleed) should:

```
// Query: entities with enabled FrontPhaseChangedTag
// Read: FrontPhaseChangedTag.NewPhase, .PreviousPhase
// Do NOT poll FrontState.Phase every frame — use this tag
```

### 5.2 One-Frame Lifecycle

1. `FrontPhaseEvaluationSystem` enables the tag and sets NewPhase/PreviousPhase
2. All reactive systems in the same frame read the tag
3. `FrontPhaseChangedCleanupSystem` (OrderLast) disables the tag

---

## Scene & Subscene Checklist

| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| Each District Subscene | `FrontAuthoring` on district entity | Assign correct FrontDefinitionSO |
| Global Config Subscene | FrontDefinition blob singleton (auto-created by baker) | Contains all 15 baked definitions |
| Bootstrap Scene | Nothing | All systems auto-register |

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| PhaseThresholds not in ascending order | Phases skip or never trigger | Validator enforces strictly ascending values |
| PhaseThresholds[0] not 0 | Phase 1 never starts | First threshold must be 0.0 |
| PhaseThresholds[3] >= 1.0 | Phase 4 unreachable | Last threshold must be < 1.0 |
| ZoneConversionOrder contains duplicate zone IDs | Some zones convert twice, others never | Validator checks for duplicates |
| ZoneConversionOrder missing zone IDs | Those zones stay Safe forever | Must contain all zone IDs in the district |
| AdvanceCurve.Evaluate(0) returns 0 | Front never starts advancing | Curve must return > 0 at all points |
| FrontAuthoring added but subscene not reimported | FrontState not baked, systems skip district | Right-click subscene, Reimport |
| FrontDefinitionId duplicated across districts | Wrong definition applied to districts | Build-time validator catches duplicates |
| SpreadPattern set to Network but zone adjacency not configured | Spread has no edges to follow, stuck at origin | Verify zone topology has adjacency data from EPIC 4.1 |
| OffScreenRateMultiplier set to 0 | Front never advances when player is in another district | Minimum 0.1 recommended |

---

## Verification

1. **Initialization** -- Enter play mode with a district. Console:
   ```
   [FrontStateInitSystem] Initialized Front for district E:XX (Phase1_Onset, 12 zones, Radial spread)
   ```

2. **Entity Debugger** -- Find district entity. Verify:
   - `FrontState.Phase` = Phase1_Onset
   - `FrontState.SpreadProgress` = 0
   - `FrontState.AdvanceRate` = 1.0
   - `FrontZoneData` buffer has entries for all district zones, all State=Safe

3. **Spread Progress** -- Wait in the district. `FrontState.SpreadProgress` should increment over time at BaseAdvanceSpeed.

4. **Phase Transition** -- When SpreadProgress crosses 0.25:
   ```
   [FrontPhaseEvaluationSystem] District E:XX phase transition: Phase1_Onset -> Phase2_Escalation (SpreadProgress=0.25)
   ```

5. **FrontPhaseChangedTag** -- On the frame of transition, `FrontPhaseChangedTag` should be enabled with correct NewPhase and PreviousPhase. On the next frame, it should be disabled.

6. **Zone Conversion** -- As SpreadProgress advances, zones in FrontZoneData should transition: Safe -> Contested -> Hostile -> Overrun in ConversionOrder.

7. **Containment** -- Set `FrontState.IsContained = true` (via debug command). SpreadProgress should stop advancing. Already-converted zones remain converted.

8. **Pause** -- Set `FrontState.PausedUntilTick` to a future tick. Advance should pause until that tick.

9. **All 4 Phases** -- Let the Front run through all phases. Each transition should fire FrontPhaseChangedTag exactly once.

10. **Debug Heatmap** -- Toggle `Front/Phase/ShowHeatmap`. Zones should color-code: green (Safe), yellow (Contested), orange (Hostile), red (Overrun). Origin zone should show pulsing marker.

11. **Build Validator** -- Run `Hollowcore > Validation > Front`. Should report 0 errors across all 15 FrontDefinitionSO assets.
