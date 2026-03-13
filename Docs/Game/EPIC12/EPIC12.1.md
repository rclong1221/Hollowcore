# EPIC 12.1: Scar Map Data Model

**Status**: Planning
**Epic**: EPIC 12 — Scar Map
**Priority**: Critical — Foundation for all Scar Map features
**Dependencies**: EPIC 4 (Districts as foundation), Framework: Roguelite/ (RunStatistics), Persistence/

---

## Overview

The Scar Map data model tracks every significant event during an expedition as a typed marker attached to a district and zone. Markers are the atomic unit of the Scar Map: each records what happened, where, and when. The model supports real-time updates as events occur during gameplay and persists across district transitions. MarkerType covers nine categories from player deaths to rival activity, Front gradients, and completed objectives. The data model is deliberately lightweight — storing event references at (district_id, zone_id) coordinates rather than world positions — enabling seed-based reconstruction when re-entering districts.

---

## Component Definitions

### MarkerType Enum

```csharp
// File: Assets/Scripts/ScarMap/Components/ScarMapComponents.cs
namespace Hollowcore.ScarMap
{
    /// <summary>
    /// Categories of events tracked on the Scar Map.
    /// Each marker type has specific metadata semantics.
    /// </summary>
    public enum MarkerType : byte
    {
        /// <summary>Body left behind. Metadata: gear inventory hash for preview.</summary>
        Skull = 0,

        /// <summary>Active echo mission. Metadata: packed (rewardType << 8 | difficulty).</summary>
        EchoSpiral = 1,

        /// <summary>Current Front phase per zone. Metadata: phase (0-4).</summary>
        FrontGradient = 2,

        /// <summary>District bleed connection. Metadata: target district ID.</summary>
        BleedTendril = 3,

        /// <summary>Seeded event (merchant, vault, legendary limb). Metadata: event type ID.</summary>
        Star = 4,

        /// <summary>Available body for resurrection. Metadata: quality tier.</summary>
        RevivalNode = 5,

        /// <summary>Rival operator last known position/status. Metadata: rival definition ID.</summary>
        RivalMarker = 6,

        /// <summary>Completed objective. Metadata: objective type ID.</summary>
        Completed = 7,

        /// <summary>Where the player died. Metadata: death cause enum.</summary>
        Death = 8
    }
}
```

### ScarMapMarker (IBufferElementData)

```csharp
// File: Assets/Scripts/ScarMap/Components/ScarMapComponents.cs
using Unity.Entities;

namespace Hollowcore.ScarMap
{
    /// <summary>
    /// A single event marker on the Scar Map.
    /// Buffer stored on the expedition singleton entity.
    /// Markers accumulate during the expedition and are never removed
    /// (except Completed replacing an EchoSpiral, or Death replacing a RevivalNode).
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct ScarMapMarker : IBufferElementData
    {
        /// <summary>District where the event occurred.</summary>
        public int DistrictId;

        /// <summary>Zone within the district (-1 = district-level marker).</summary>
        public int ZoneId;

        /// <summary>Type of event.</summary>
        public MarkerType Type;

        /// <summary>
        /// Type-specific metadata. Interpretation depends on MarkerType:
        /// - Skull: gear inventory hash (for loot preview tooltip)
        /// - EchoSpiral: (rewardType << 8) | difficulty
        /// - FrontGradient: phase value (0 = green, 1 = yellow, 2 = orange, 3 = red, 4 = critical)
        /// - BleedTendril: target district ID
        /// - Star: event type ID (0=merchant, 1=vault, 2=legendary, 3=quest)
        /// - RevivalNode: quality tier (1-5)
        /// - RivalMarker: rival definition ID
        /// - Completed: objective type ID
        /// - Death: death cause enum value
        /// </summary>
        public int Metadata;

        /// <summary>
        /// Expedition-relative timestamp (gate transition count when marker was created).
        /// Used for timeline display in end-of-run summary (EPIC 12.4).
        /// </summary>
        public int Timestamp;

        /// <summary>
        /// Hash for deduplication. Prevents duplicate markers on district re-entry.
        /// Computed as: Hash(DistrictId, ZoneId, Type, Metadata).
        /// </summary>
        public int MarkerHash;

        /// <summary>
        /// Whether this marker is still active (false = resolved/consumed).
        /// EchoSpiral → false when completed. RevivalNode → false when used.
        /// Inactive markers remain for timeline/narrative but render differently.
        /// </summary>
        public bool IsActive;
    }
}
```

### ScarMapState (IComponentData)

```csharp
// File: Assets/Scripts/ScarMap/Components/ScarMapComponents.cs
using Unity.Entities;

namespace Hollowcore.ScarMap
{
    /// <summary>
    /// Top-level Scar Map state for the current expedition.
    /// Singleton on the expedition entity alongside the ScarMapMarker buffer.
    /// </summary>
    public struct ScarMapState : IComponentData
    {
        /// <summary>Expedition ID for persistence cross-reference.</summary>
        public int ExpeditionId;

        /// <summary>Expedition seed for deterministic district reconstruction.</summary>
        public uint ExpeditionSeed;

        /// <summary>Current gate transition count (clock for timestamps).</summary>
        public int CurrentTransition;

        /// <summary>Total markers created this expedition.</summary>
        public int TotalMarkerCount;

        /// <summary>Number of Skull markers (quick stat for UI).</summary>
        public int DeathCount;

        /// <summary>Number of active EchoSpiral markers.</summary>
        public int ActiveEchoCount;

        /// <summary>Number of Completed markers.</summary>
        public int CompletedObjectiveCount;

        /// <summary>Highest Front phase encountered across all districts.</summary>
        public byte PeakFrontPhase;

        /// <summary>Whether the Scar Map has unsaved changes (dirty flag for persistence).</summary>
        public bool IsDirty;
    }
}
```

### ScarMapDistrictSummary (IBufferElementData)

```csharp
// File: Assets/Scripts/ScarMap/Components/ScarMapComponents.cs
using Unity.Entities;

namespace Hollowcore.ScarMap
{
    /// <summary>
    /// Pre-computed per-district summary for fast hover/tooltip display.
    /// Updated by ScarMapAggregatorSystem whenever markers change.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ScarMapDistrictSummary : IBufferElementData
    {
        public int DistrictId;

        /// <summary>Dominant Front phase across zones.</summary>
        public byte DominantFrontPhase;

        /// <summary>Number of bodies in this district.</summary>
        public byte BodyCount;

        /// <summary>Number of active echoes.</summary>
        public byte ActiveEchoCount;

        /// <summary>Number of active star events.</summary>
        public byte StarEventCount;

        /// <summary>Whether any rival is currently in this district.</summary>
        public bool HasRivalPresence;

        /// <summary>Whether player has visited this district.</summary>
        public bool IsVisited;

        /// <summary>Number of completed objectives in this district.</summary>
        public byte CompletedCount;
    }
}
```

---

## Systems

### ScarMapMarkerSystem

```csharp
// File: Assets/Scripts/ScarMap/Systems/ScarMapMarkerSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup, OrderLast
//
// Central intake system — listens for gameplay events and creates markers:
//   1. Player death → Skull marker (gear hash from inventory) + Death marker (cause)
//   2. Echo spawned → EchoSpiral marker (reward type + difficulty)
//   3. Echo completed → set EchoSpiral.IsActive=false, add Completed marker
//   4. Front phase change → update/add FrontGradient markers for affected zones
//   5. District bleed → BleedTendril marker (source→target)
//   6. Seeded event discovered → Star marker (event type)
//   7. Body available for revival → RevivalNode marker (quality tier)
//   8. Rival activity → RivalMarker (rival definition ID) from EPIC 11.2 events
//   9. Objective completed → Completed marker
//
// Each marker creation:
//   a. Compute MarkerHash for deduplication
//   b. Check existing markers — skip if hash exists
//   c. Append to ScarMapMarker buffer
//   d. Increment ScarMapState counters
//   e. Set ScarMapState.IsDirty = true
//   f. Fire ScarMapMarkerAddedEvent for rendering system
```

### ScarMapAggregatorSystem

```csharp
// File: Assets/Scripts/ScarMap/Systems/ScarMapAggregatorSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: ScarMapMarkerSystem
//
// Recomputes district summaries when markers change:
//   1. If ScarMapState.IsDirty == false, early-out
//   2. Clear ScarMapDistrictSummary buffer
//   3. Iterate all ScarMapMarker entries:
//      a. Group by DistrictId
//      b. Compute DominantFrontPhase (max across FrontGradient markers)
//      c. Count bodies (Skull), echoes (active EchoSpiral), stars (Star)
//      d. Check for RivalMarker presence
//      e. Mark IsVisited if any marker exists for the district
//      f. Count Completed markers
//   4. Write results to ScarMapDistrictSummary buffer
//   5. Update ScarMapState.PeakFrontPhase
//   6. Set ScarMapState.IsDirty = false
```

### ScarMapTransitionSystem

```csharp
// File: Assets/Scripts/ScarMap/Systems/ScarMapTransitionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: GateTransitionSystem (EPIC 4)
//
// Tracks gate transitions for timestamp clock:
//   1. On gate transition event:
//      a. Increment ScarMapState.CurrentTransition
//      b. Snapshot FrontGradient markers for all zones in departing district
//         (captures Front state at time of departure)
//      c. Set IsDirty = true to trigger aggregator refresh
```

---

## Setup Guide

1. **Create `Assets/Scripts/ScarMap/` folder** with subfolders: Components/, Systems/, Bridges/
2. **Create assembly definition** `Hollowcore.ScarMap.asmdef` referencing `DIG.Shared`, `Unity.Entities`, `Unity.Collections`, `Unity.Mathematics`
3. **Add ScarMapAuthoring** MonoBehaviour to expedition manager prefab:
   - Baker creates expedition entity with ScarMapState + ScarMapMarker buffer + ScarMapDistrictSummary buffer
   - ScarMapState.ExpeditionSeed set from expedition seed
4. **Wire event listeners** in ScarMapMarkerSystem:
   - Death events from EPIC 2
   - Echo events from EPIC 5
   - Front events from EPIC 3
   - Rival events from EPIC 11.2
   - Objective events from quest/objective systems
5. **Create test scenario**: manually fire 3-4 marker events (death, echo, Front change) and verify buffer population
6. Verify aggregator computes correct district summaries

---

## Verification

- [ ] ScarMapState singleton created on expedition start with correct seed
- [ ] ScarMapMarker buffer populated when gameplay events fire
- [ ] MarkerHash deduplication prevents duplicate markers on district re-entry
- [ ] Each MarkerType stores correct metadata format
- [ ] Timestamps increment with gate transitions
- [ ] IsActive toggled correctly (EchoSpiral→false on completion, RevivalNode→false on use)
- [ ] ScarMapAggregatorSystem produces accurate district summaries
- [ ] DominantFrontPhase correctly reflects max phase across zones
- [ ] ScarMapState counters (DeathCount, ActiveEchoCount, CompletedObjectiveCount) accurate
- [ ] IsDirty flag prevents unnecessary aggregator recalculation
- [ ] Marker buffer handles 32+ markers without overflow (InternalBufferCapacity=32, grows dynamically)
- [ ] All 9 MarkerType values produce valid markers with correct metadata
- [ ] ScarMapTransitionSystem increments transition counter on gate events

---

## BlobAsset Pipeline

```csharp
// File: Assets/Scripts/ScarMap/Blobs/ScarMapBlobs.cs
using Unity.Entities;
using Unity.Mathematics;

namespace Hollowcore.ScarMap
{
    /// <summary>
    /// Baked ScarMapMarker data for the rendering pipeline.
    /// Marker metadata that never changes at runtime (icon index, color)
    /// is baked into a blob so the renderer can burst-iterate without
    /// touching managed SO references.
    /// </summary>
    public struct ScarMapMarkerRenderBlob
    {
        /// <summary>Per-MarkerType render data. Indexed by (byte)MarkerType.</summary>
        public BlobArray<MarkerRenderEntry> Entries;
    }

    public struct MarkerRenderEntry
    {
        public int IconSpriteIndex;       // Index into shared marker icon atlas
        public float4 TintColor;          // RGBA tint
        public float PulseSpeed;          // 0 = no pulse
        public float BaseScale;           // Icon scale multiplier
        public byte RenderLayer;          // Sorting layer for overlap
    }
}
```

Baker: `ScarMapRenderConfigAuthoring.Baker` calls `BlobBuilder` to produce `BlobAssetReference<ScarMapMarkerRenderBlob>`, stored on the render config singleton entity. One blob for all marker types (9 entries), allocated once per expedition.

---

## Validation

```csharp
// File: Assets/Editor/ScarMap/ScarMapDataValidator.cs
// Runs: OnValidate in ScarMapAuthoring, and as build-time IPreprocessBuildWithReport
//
// Checks:
//   1. Marker position bounds: every ScarMapMarker.DistrictId must be in [1..TotalDistrictCount]
//   2. ZoneId in [-1..maxZoneCount) for the referenced district
//   3. Duplicate marker detection: no two markers with identical MarkerHash in the buffer
//   4. MarkerType range: must be in [0..8] (9 defined values)
//   5. Metadata sanity: FrontGradient phase in [0..4], Star eventType in [0..3]
//   6. Timestamp monotonicity: timestamps must be non-decreasing
//   7. Layer visibility: every MarkerType has a matching entry in ScarMapMarkerRenderBlob
```

---

## Editor Tooling

```csharp
// File: Assets/Editor/ScarMap/ScarMapPreviewWindow.cs
// EditorWindow: "Hollowcore/Scar Map Preview"
//
// Renders the Scar Map with sample data in the editor without entering Play Mode:
//   1. Loads a DistrictDefinitionSO set and constructs a mock district graph
//   2. Runs ScarMapLayoutSystem's force-directed algorithm offline
//   3. Draws district silhouettes, gate lines, and marker icons using Handles/GUI
//   4. Marker placement tool: click to add markers, snap to nearest zone center
//   5. Front gradient preview: slider to scrub through phases 0-4, colors update live
//   6. Export mock data as ScarMapMarker buffer JSON for automated tests
//
// Marker Placement Tool:
//   - Toolbar button "Place Marker" enters placement mode
//   - Click on a district silhouette → snap to nearest zone centroid
//   - MarkerType dropdown, Metadata field, auto-computed MarkerHash
//   - Undo support via Undo.RecordObject
```

---

## Live Tuning

| Parameter | Location | Range | Default |
|-----------|----------|-------|---------|
| Marker icon visibility range | ScarMapRenderConfig.MarkerIconScale | 0.1–3.0 | 1.0 |
| Detail zoom scale | ScarMapRenderConfig.DetailZoomScale | 1.0–5.0 | 2.0 |
| Echo pulse speed | ScarMapRenderConfig.EchoSpiralPulseSpeed | 0.5–6.0 | 2.0 |
| Star twinkle speed | ScarMapRenderConfig.StarTwinkleSpeed | 0.5–8.0 | 3.0 |
| Bleed tendril flow | ScarMapRenderConfig.BleedTendrilFlowSpeed | 0.5–4.0 | 1.5 |
| Circuit line opacity | ScarMapRenderConfig.CircuitLineOpacity | 0.0–1.0 | 0.3 |
| District spacing | ScarMapRenderConfig.DistrictSpacing | 0.05–0.5 | 0.15 |

All fields exposed via `ScarMapRenderConfigAuthoring` inspector. Changes apply immediately in Play Mode (managed renderer reads singleton each frame).

---

## Debug Visualization

```csharp
// File: Assets/Scripts/ScarMap/Debug/ScarMapDebugOverlay.cs
// Managed system, PresentationSystemGroup, ClientSimulation | LocalSimulation
// Gated behind UNITY_EDITOR || DEVELOPMENT_BUILD
//
// Overlay toggles (per layer):
//   [F5] Markers: show/hide all marker icons
//   [F6] Routes: show/hide gate connection lines and route highlights
//   [F7] Echoes: show/hide EchoSpiral markers only
//   [F8] Front State: show/hide FrontGradient zone coloring
//   [F9] Bleed Tendrils: show/hide district bleed connections
//   [F10] Rival markers: show/hide rival positions
//
// Each toggle renders its layer independently.
// Marker counts displayed in corner: "Skulls: 3 | Echoes: 2 | Stars: 1"
```

---

## Simulation & Testing

```csharp
// File: Assets/Tests/ScarMap/ScarMapRenderingPerfTest.cs
// [Test] ScarMapRenderPerformance_100Markers_WithinFrameBudget
//
// 1. Create expedition entity with ScarMapState + 100 ScarMapMarker entries
//    (random DistrictId, ZoneId, MarkerType distribution)
// 2. Populate 12 ScarMapDistrictSummary entries
// 3. Run ScarMapAggregatorSystem for 1 frame → measure time (target: < 0.5ms)
// 4. Run ScarMapRenderer.OnUpdate for 1 frame → measure time (target: < 2ms)
// 5. Verify no GC allocations during renderer update (Burst-compatible paths)
// 6. Assert all 100 markers have resolved screen positions
// 7. Test with 200 markers to establish scaling behavior
```
