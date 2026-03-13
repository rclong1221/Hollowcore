# EPIC 3.1: Front State & Phase Model

**Status**: Planning
**Epic**: EPIC 3 — The Front (District Pressure System)
**Priority**: Critical — Foundation for all Front mechanics
**Dependencies**: Framework: Roguelite/ (ZoneDefinitionSO, zone topology), EPIC 4.1 (ExpeditionGraphState, DistrictNode)

---

## Overview

Core data model for the district pressure system. Every district entity carries a FrontState that tracks which phase the Front is in, how far it has spread, and whether it is paused or contained. Every zone within a district carries a FrontZoneState that determines its current safety level. A FrontDefinitionSO per district defines the spread pattern, advance curve, phase thresholds, and zone conversion order. A FrontPhaseChangedTag enableable component fires for one frame on phase transitions so reactive systems can respond without polling.

---

## Component Definitions

### FrontPhase Enum

```csharp
// File: Assets/Scripts/Front/Components/FrontComponents.cs
namespace Hollowcore.Front
{
    /// <summary>
    /// The four escalation phases of a district's Front.
    /// Phase values are sequential — higher = more dangerous.
    /// </summary>
    public enum FrontPhase : byte
    {
        Phase1_Onset = 1,       // Early warning — minor hazards, patrols appear
        Phase2_Escalation = 2,  // Significant pressure — routes closing, elite enemies
        Phase3_Crisis = 3,      // District failing — bleed starts, heavy hazards
        Phase4_Overrun = 4      // District lost — lethal without preparation
    }
}
```

### FrontZoneState Enum

```csharp
// File: Assets/Scripts/Front/Components/FrontComponents.cs (continued)
namespace Hollowcore.Front
{
    /// <summary>
    /// Per-zone safety level as the Front converts zones from safe to overrun.
    /// </summary>
    public enum FrontZoneState : byte
    {
        Safe = 0,       // Normal gameplay, all routes open
        Contested = 1,  // Increased enemy density, some hazards, alternate routes needed
        Hostile = 2,    // Heavy hazards, restricted traversal, elite enemies
        Overrun = 3     // Lethal without preparation, impassable without specific gear/abilities
    }
}
```

### FrontState (IComponentData)

```csharp
// File: Assets/Scripts/Front/Components/FrontComponents.cs (continued)
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Front
{
    /// <summary>
    /// Core state for a district's Front. One per district entity.
    /// Drives all advance, restriction, pulse, and bleed systems.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct FrontState : IComponentData
    {
        /// <summary>Current escalation phase (1-4).</summary>
        [GhostField] public FrontPhase Phase;

        /// <summary>Spread progress within current phase (0.0 = phase start, 1.0 = phase complete).</summary>
        [GhostField(Quantization = 1000)] public float SpreadProgress;

        /// <summary>
        /// Current advance rate multiplier. 1.0 = normal speed.
        /// Modified by alarms, failed objectives, containment, counterplay.
        /// </summary>
        [GhostField(Quantization = 100)] public float AdvanceRate;

        /// <summary>Zone ID where the Front originates. Baked from FrontDefinitionSO.</summary>
        [GhostField] public int OriginZoneId;

        /// <summary>
        /// Network tick until which advance is paused (0 = not paused).
        /// Set by counterplay actions (contain objectives, sabotage).
        /// </summary>
        [GhostField] public uint PausedUntilTick;

        /// <summary>
        /// Bleed accumulation counter. Incremented per gate transition while Phase >= 3.
        /// At threshold, hazards appear in adjacent district border zones.
        /// </summary>
        [GhostField] public int BleedCounter;

        /// <summary>
        /// True if containment objectives have been completed — blocks further phase advance.
        /// Can be cleared if containment is broken (e.g., sabotage, compound bleed).
        /// </summary>
        [GhostField] public bool IsContained;

        /// <summary>Reference to the FrontDefinitionSO (baked as hash/id for lookup).</summary>
        [GhostField] public int FrontDefinitionId;

        /// <summary>Total number of zones in this district (cached from definition).</summary>
        [GhostField] public int TotalZoneCount;

        /// <summary>Number of zones currently converted (Contested + Hostile + Overrun).</summary>
        [GhostField] public int ConvertedZoneCount;
    }
}
```

### FrontZoneData (IBufferElementData)

```csharp
// File: Assets/Scripts/Front/Components/FrontComponents.cs (continued)
namespace Hollowcore.Front
{
    /// <summary>
    /// Per-zone Front state. Buffer on the district entity.
    /// Indexed by zone order from FrontDefinitionSO's conversion sequence.
    /// </summary>
    [InternalBufferCapacity(0)] // External buffer — districts can have 20+ zones
    public struct FrontZoneData : IBufferElementData
    {
        /// <summary>Zone identifier matching the framework's zone ID system.</summary>
        public int ZoneId;

        /// <summary>Current zone safety state.</summary>
        public FrontZoneState State;

        /// <summary>
        /// Position in the Front's conversion order (0 = first to convert).
        /// Set at district generation from FrontDefinitionSO.
        /// </summary>
        public int ConversionOrder;

        /// <summary>
        /// Conversion progress for this specific zone (0.0 = unconverted, 1.0 = fully converted to next state).
        /// Zones convert individually as the Front spreads.
        /// </summary>
        public float ConversionProgress;
    }
}
```

### FrontPhaseChangedTag (IEnableableComponent)

```csharp
// File: Assets/Scripts/Front/Components/FrontComponents.cs (continued)
namespace Hollowcore.Front
{
    /// <summary>
    /// Enableable tag toggled for one frame when FrontState.Phase changes.
    /// Reactive systems (Pulse, UI, Bleed) check this instead of polling phase.
    /// Baked disabled on district entity.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct FrontPhaseChangedTag : IComponentData, IEnableableComponent
    {
        /// <summary>The phase that was just entered.</summary>
        [GhostField] public FrontPhase NewPhase;

        /// <summary>The phase that was just exited.</summary>
        [GhostField] public FrontPhase PreviousPhase;
    }
}
```

---

## ScriptableObject Definitions

### FrontDefinitionSO

```csharp
// File: Assets/Scripts/Front/Definitions/FrontDefinitionSO.cs
using UnityEngine;

namespace Hollowcore.Front.Definitions
{
    public enum FrontSpreadPattern : byte
    {
        Radial = 0,   // Outward from origin in all directions (Corruption Bloom)
        Linear = 1,   // Advances along a single axis (Waterline Rise)
        Network = 2   // Spreads along connected nodes (Reality Desync, Choir Crescendo)
    }

    [CreateAssetMenu(fileName = "NewFrontDef", menuName = "Hollowcore/Front/Front Definition")]
    public class FrontDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int FrontDefinitionId;
        public string DisplayName;
        [TextArea] public string Description;

        [Header("Spread")]
        public FrontSpreadPattern SpreadPattern;
        [Tooltip("Origin zone index within the district's zone list.")]
        public int OriginZoneIndex;
        [Tooltip("Zone conversion order. Index = conversion priority, value = zone ID.")]
        public int[] ZoneConversionOrder;

        [Header("Advance")]
        [Tooltip("Base advance speed (SpreadProgress units per second at AdvanceRate 1.0).")]
        public float BaseAdvanceSpeed = 0.02f;
        [Tooltip("Curve mapping SpreadProgress (0-1 across all phases) to speed multiplier.")]
        public AnimationCurve AdvanceCurve = AnimationCurve.Linear(0f, 1f, 1f, 1.5f);
        [Tooltip("Off-screen advance rate multiplier (0.3-0.5 of normal).")]
        [Range(0.1f, 1f)]
        public float OffScreenRateMultiplier = 0.4f;

        [Header("Phase Thresholds")]
        [Tooltip("SpreadProgress value (0-1) at which each phase begins. Index 0 unused (Phase1 starts at 0).")]
        public float[] PhaseThresholds = { 0f, 0.25f, 0.55f, 0.8f };

        [Header("Zone Conversion")]
        [Tooltip("How many zones convert per phase transition (can be fractional — rounds up).")]
        public float ZonesPerPhaseTransition = 3f;
        [Tooltip("Time in seconds for a single zone to transition between states (Safe→Contested, etc.).")]
        public float ZoneConversionDuration = 10f;

        [Header("Visuals")]
        [Tooltip("Ambient color tint applied to converted zones per FrontZoneState.")]
        public Color[] ZoneStateTints = new Color[4]; // Indexed by FrontZoneState
        [Tooltip("Particle system prefab for Front boundary edge.")]
        public GameObject BoundaryVFXPrefab;
        [Tooltip("Ambient audio event triggered per phase.")]
        public string[] PhaseAmbientAudioEvents = new string[4];
    }
}
```

---

## Systems

### FrontPhaseEvaluationSystem

```csharp
// File: Assets/Scripts/Front/Systems/FrontPhaseEvaluationSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: FrontAdvanceSystem (EPIC 3.2)
//
// Evaluates FrontState.SpreadProgress against phase thresholds and transitions phases.
//
// For each district entity with FrontState + FrontZoneData buffer:
//   1. Look up FrontDefinitionSO via FrontState.FrontDefinitionId
//   2. Compare SpreadProgress against PhaseThresholds array
//   3. If SpreadProgress crosses a threshold:
//      a. Update FrontState.Phase to new phase
//      b. Enable FrontPhaseChangedTag with old/new phase values
//   4. If FrontState.IsContained:
//      a. Clamp SpreadProgress — prevent further phase advance
//      b. Do NOT revert zones already converted
```

### FrontZoneConversionSystem

```csharp
// File: Assets/Scripts/Front/Systems/FrontZoneConversionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: FrontPhaseEvaluationSystem
//
// Converts zones from Safe → Contested → Hostile → Overrun as the Front spreads.
//
// For each district entity with FrontState + FrontZoneData buffer:
//   1. Determine target conversion count from SpreadProgress and ZonesPerPhaseTransition
//   2. Iterate FrontZoneData in ConversionOrder:
//      a. For zones below target conversion: increment ConversionProgress by deltaTime / ZoneConversionDuration
//      b. When ConversionProgress >= 1.0:
//         - Advance FrontZoneState to next level (Safe → Contested → Hostile → Overrun)
//         - Reset ConversionProgress to 0.0
//         - Increment FrontState.ConvertedZoneCount
//   3. Apply spread pattern logic:
//      - Radial: zones convert outward from OriginZoneId by distance
//      - Linear: zones convert in strict order
//      - Network: zones convert along adjacency edges from already-converted zones
```

### FrontPhaseChangedCleanupSystem

```csharp
// File: Assets/Scripts/Front/Systems/FrontPhaseChangedCleanupSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup, OrderLast
//
// Disables FrontPhaseChangedTag at end of frame so it fires for exactly one frame.
//
// For each entity with enabled FrontPhaseChangedTag:
//   1. SetComponentEnabled<FrontPhaseChangedTag>(entity, false)
```

### FrontStateInitSystem

```csharp
// File: Assets/Scripts/Front/Systems/FrontStateInitSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: InitializationSystemGroup
//
// Initializes FrontState on new district entities.
//
// For each district entity with FrontState where Phase == 0 (uninitialized):
//   1. Load FrontDefinitionSO via FrontDefinitionId
//   2. Set Phase = Phase1_Onset
//   3. Set SpreadProgress = 0
//   4. Set AdvanceRate = 1.0
//   5. Set OriginZoneId from definition
//   6. Populate FrontZoneData buffer from ZoneConversionOrder
//   7. Set all zones to FrontZoneState.Safe
//   8. Set TotalZoneCount, ConvertedZoneCount = 0
//   9. Bake disabled FrontPhaseChangedTag
```

---

## Authoring

### FrontAuthoring

```csharp
// File: Assets/Scripts/Front/Authoring/FrontAuthoring.cs
// Added to district prefab. Bakes FrontState, FrontZoneData buffer, and FrontPhaseChangedTag.
//
// Baker:
//   1. AddComponent<FrontState> with FrontDefinitionId from assigned FrontDefinitionSO
//   2. AddBuffer<FrontZoneData> — populated at runtime by FrontStateInitSystem
//   3. AddComponent<FrontPhaseChangedTag> and SetComponentEnabled(false)
//   4. Store FrontDefinitionSO reference for runtime lookup
//
// Inspector fields:
//   - FrontDefinitionSO reference (required)
//   - Optional: override BaseAdvanceSpeed for testing
```

---

## Setup Guide

1. **Create `Assets/Scripts/Front/` folder** with subfolders: Components/, Definitions/, Systems/, Authoring/
2. **Create assembly definition** `Hollowcore.Front.asmdef` referencing `DIG.Shared`, `Unity.Entities`, `Unity.NetCode`, `Unity.Collections`, `Unity.Burst`, `Unity.Mathematics`
3. **Create FrontComponents.cs** with all enums and component structs defined above
4. **Create FrontDefinitionSO.cs** in Definitions/
5. **Create one FrontDefinitionSO asset per district** in `Assets/Data/Front/Definitions/` (start with Necrospire: Radial pattern, BaseAdvanceSpeed 0.02, thresholds at 0/0.25/0.55/0.8)
6. **Add FrontAuthoring** to each district prefab, assign the correct FrontDefinitionSO
7. **Create all four systems** (FrontStateInitSystem, FrontPhaseEvaluationSystem, FrontZoneConversionSystem, FrontPhaseChangedCleanupSystem)
8. Reimport subscene after adding authoring to district prefabs
9. Verify in Entity Debugger that district entity has FrontState with Phase1_Onset after init

---

## Verification

- [ ] FrontState component created on district entity at spawn
- [ ] FrontState.Phase initializes to Phase1_Onset with SpreadProgress = 0
- [ ] FrontZoneData buffer populated with correct zone count and ConversionOrder
- [ ] SpreadProgress advances over time when AdvanceRate > 0
- [ ] Phase transitions occur at correct threshold values from FrontDefinitionSO
- [ ] FrontPhaseChangedTag enables for exactly one frame on phase transition
- [ ] FrontPhaseChangedTag.NewPhase and PreviousPhase are set correctly
- [ ] Zone conversion follows the defined spread pattern (Radial/Linear/Network)
- [ ] Zones transition Safe → Contested → Hostile → Overrun in correct order
- [ ] IsContained = true blocks further SpreadProgress advance
- [ ] PausedUntilTick pauses advance until the specified tick
- [ ] ConvertedZoneCount tracks correctly as zones change state
- [ ] FrontDefinitionSO inspector fields all serialize and bake correctly

---

## BlobAsset Pipeline

```csharp
// File: Assets/Scripts/Front/Definitions/FrontDefinitionBlob.cs
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Hollowcore.Front
{
    /// <summary>
    /// Burst-compatible baked data from FrontDefinitionSO.
    /// AdvanceCurve baked as BlobArray of float2 samples for Burst evaluation.
    /// </summary>
    public struct FrontDefinitionBlob
    {
        public int FrontDefinitionId;
        public byte SpreadPattern;         // FrontSpreadPattern cast
        public int OriginZoneIndex;
        public float BaseAdvanceSpeed;
        public float OffScreenRateMultiplier;
        public BlobArray<int> ZoneConversionOrder;
        public BlobArray<float> PhaseThresholds;   // 4 entries
        public float ZonesPerPhaseTransition;
        public float ZoneConversionDuration;
        public BlobArray<float2> AdvanceCurveSamples; // x=time, y=value, 64 samples
        public BlobArray<float4> ZoneStateTints;       // 4 entries, rgba
        public BlobString DisplayName;
    }
}

// File: Assets/Scripts/Front/Definitions/FrontDefinitionSO.cs (addition)
// public BlobAssetReference<FrontDefinitionBlob> BakeToBlob(BlobBuilder builder)
// {
//     ref var root = ref builder.ConstructRoot<FrontDefinitionBlob>();
//     // ... populate scalar fields ...
//
//     // AnimationCurve → BlobArray<float2> conversion:
//     const int SAMPLES = 64;
//     var curve = builder.Allocate(ref root.AdvanceCurveSamples, SAMPLES);
//     for (int i = 0; i < SAMPLES; i++)
//     {
//         float t = i / (float)(SAMPLES - 1);
//         curve[i] = new float2(t, AdvanceCurve.Evaluate(t));
//     }
//
//     var order = builder.Allocate(ref root.ZoneConversionOrder, ZoneConversionOrder.Length);
//     for (int i = 0; i < ZoneConversionOrder.Length; i++)
//         order[i] = ZoneConversionOrder[i];
//
//     var thresholds = builder.Allocate(ref root.PhaseThresholds, PhaseThresholds.Length);
//     for (int i = 0; i < PhaseThresholds.Length; i++)
//         thresholds[i] = PhaseThresholds[i];
//
//     return builder.CreateBlobAssetReference<FrontDefinitionBlob>(Allocator.Persistent);
// }
```

Baker creates a singleton entity with `BlobArray<FrontDefinitionBlob>` (one entry per district), enabling Burst-compiled systems to read advance curves and zone orders without managed SO access.

---

## Validation

```csharp
// File: Assets/Editor/Validation/FrontDefinitionValidation.cs
// OnValidate() rules for FrontDefinitionSO:
//
// - FrontDefinitionId must be unique across all FrontDefinitionSO assets
// - DisplayName must not be null or empty
// - BaseAdvanceSpeed must be > 0 and <= 1.0 (sane range)
// - OffScreenRateMultiplier must be in [0.1, 1.0]
// - PhaseThresholds must have exactly 4 entries
// - PhaseThresholds[0] must be 0
// - PhaseThresholds must be strictly ascending
// - PhaseThresholds[3] must be < 1.0 (Phase 4 must be reachable)
// - ZoneConversionOrder must not be null or empty
// - ZoneConversionOrder must not contain duplicate zone IDs
// - ZonesPerPhaseTransition must be > 0
// - ZoneConversionDuration must be > 0
// - AdvanceCurve must have at least 2 keyframes
// - AdvanceCurve.Evaluate(0) and Evaluate(1) must be > 0 (never zero speed)
// - ZoneStateTints must have exactly 4 entries
// - BoundaryVFXPrefab should not be null (warning)
//
// Build-time scan: verify exactly 15 FrontDefinitionSO assets, no duplicate IDs
```

---

## Editor Tooling

**Front Definition Inspector**:
- Custom Inspector for `FrontDefinitionSO`:
  - Phase threshold visualization: horizontal bar divided into 4 colored sections at threshold values
  - Advance curve preview: embedded AnimationCurve editor with phase boundaries overlaid as vertical lines
  - Zone conversion order: reorderable list with drag handles, zone ID labels from district data
  - Spread pattern diagram: mini visual showing Radial/Linear/Network spread preview

---

## Debug Visualization

**Front Phase Heatmap** (toggle via debug menu):
- Per-zone color overlay in scene view:
  - Safe = green, Contested = yellow, Hostile = orange, Overrun = red
- Conversion progress bars rendered per-zone at zone centroid positions
- Phase boundary line: visual edge between converted and unconverted zones
- Origin zone marker: pulsing icon at OriginZoneId position

**Front State HUD** (toggle via debug menu):
- Compact panel: current Phase, SpreadProgress bar, AdvanceRate, ConvertedZoneCount/TotalZoneCount
- IsContained / PausedUntilTick indicators
- FrontPhaseChangedTag flash indicator (blinks on phase transition frame)

**Activation**: Debug menu toggle `Front/Phase/ShowHeatmap` and `Front/Phase/ShowHUD`
