# EPIC 12.5: Scar Map Procgen Integration

**Status**: Planning
**Epic**: EPIC 12 — Scar Map
**Priority**: Critical — Required for marker placement correctness
**Dependencies**: EPIC 12.1 (Scar Map Data Model), EPIC 4 (District graph & seed-based generation), Framework: Persistence/

---

## Overview

The Scar Map operates on seed-based deterministic generation. Districts are generated from a fixed expedition seed, producing deterministic zone IDs and layouts. Markers are stored at (district_id, zone_id) coordinates — never world positions — so that when a player re-enters a district, the same seed regenerates the same layout and markers land in the correct zones. Storage is deliberately lightweight: only event markers and Front phase state, no geometry data. The persistence module (TypeId=25) handles serialization for both active expeditions and Compendium preservation, using a compact binary format with CRC32 integrity validation.

---

## Component Definitions

### ScarMapSeedState (IComponentData)

```csharp
// File: Assets/Scripts/ScarMap/Components/ScarMapProcgenComponents.cs
using Unity.Entities;

namespace Hollowcore.ScarMap
{
    /// <summary>
    /// Seed data linking Scar Map markers to deterministic generation.
    /// Stored alongside ScarMapState on the expedition entity.
    /// </summary>
    public struct ScarMapSeedState : IComponentData
    {
        /// <summary>Master expedition seed — same seed produces same district graph.</summary>
        public uint ExpeditionSeed;

        /// <summary>
        /// Number of districts in the graph (set at expedition start).
        /// Validates that marker DistrictIds are in range.
        /// </summary>
        public int TotalDistrictCount;

        /// <summary>
        /// Hash of the district graph topology (edges + node types).
        /// Used to validate Compendium Scar Maps against the expected graph.
        /// If hash mismatch: graph changed between versions, markers may not align.
        /// </summary>
        public uint GraphTopologyHash;
    }
}
```

### ScarMapZoneMapping (IBufferElementData)

```csharp
// File: Assets/Scripts/ScarMap/Components/ScarMapProcgenComponents.cs
using Unity.Entities;
using Unity.Mathematics;

namespace Hollowcore.ScarMap
{
    /// <summary>
    /// Maps zone IDs to normalized positions within a district silhouette.
    /// Generated deterministically from district seed when a district is entered.
    /// Used by the renderer to place markers at correct visual positions.
    /// Cached per-district — regenerated if district layout changes.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct ScarMapZoneMapping : IBufferElementData
    {
        /// <summary>District this mapping belongs to.</summary>
        public int DistrictId;

        /// <summary>Zone ID within the district.</summary>
        public int ZoneId;

        /// <summary>
        /// Normalized position within district silhouette (0-1 range).
        /// X: horizontal position, Y: vertical position.
        /// Computed from zone's world-space center projected onto silhouette bounds.
        /// </summary>
        public float2 NormalizedPosition;

        /// <summary>Zone type for visual differentiation (combat, safe, poi, gate).</summary>
        public byte ZoneType;
    }
}
```

### ScarMapPersistenceHeader

```csharp
// File: Assets/Scripts/ScarMap/Components/ScarMapProcgenComponents.cs
namespace Hollowcore.ScarMap
{
    /// <summary>
    /// Binary header for Scar Map persistence format.
    /// NOT an ECS component — used in serialization only.
    /// Follows the ISaveModule pattern from the Persistence/ framework.
    /// </summary>
    public struct ScarMapPersistenceHeader
    {
        /// <summary>Format version for forward compatibility.</summary>
        public ushort Version;

        /// <summary>Expedition seed for graph reconstruction.</summary>
        public uint ExpeditionSeed;

        /// <summary>Graph topology hash for validation.</summary>
        public uint GraphTopologyHash;

        /// <summary>Number of ScarMapMarker entries in the blob.</summary>
        public int MarkerCount;

        /// <summary>Number of ScarMapDistrictSummary entries.</summary>
        public int SummaryCount;

        /// <summary>Byte offset to marker array from start of payload.</summary>
        public int MarkerArrayOffset;

        /// <summary>Byte offset to summary array from start of payload.</summary>
        public int SummaryArrayOffset;

        /// <summary>CRC32 of the payload (excluding header).</summary>
        public uint PayloadCRC;

        public const ushort CurrentVersion = 1;
        public const int TypeId = 25;
    }
}
```

---

## Systems

### ScarMapZoneMappingSystem

```csharp
// File: Assets/Scripts/ScarMap/Systems/ScarMapZoneMappingSystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
// UpdateBefore: ScarMapRenderer (EPIC 12.2)
//
// Generates zone-to-position mappings for marker placement:
//   1. On district entry (or Scar Map opened for a district):
//      a. Check if ScarMapZoneMapping already cached for this district
//      b. If not: generate from district seed
//         - Use district-specific seed: Hash(ExpeditionSeed, DistrictId)
//         - Query district's zone layout (EPIC 4: zone count, types, adjacency)
//         - For each zone: compute normalized position within silhouette bounds
//           * Zone center in world space → project to 2D
//           * Normalize to (0,1) within district bounding rect
//         - Write ScarMapZoneMapping entries
//   2. Cached mappings are valid for the expedition lifetime
//      (same seed = same layout = same positions)
//   3. Compendium viewing: regenerate mappings from stored seed
//      - Read ScarMapSeedState.ExpeditionSeed from saved data
//      - Rerun zone layout generation (deterministic)
//      - Validate against GraphTopologyHash
```

### ScarMapMarkerPlacementSystem

```csharp
// File: Assets/Scripts/ScarMap/Systems/ScarMapMarkerPlacementSystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
// UpdateAfter: ScarMapZoneMappingSystem
// UpdateBefore: ScarMapRenderer (EPIC 12.2)
//
// Resolves marker positions for rendering:
//   1. For each ScarMapMarker in the buffer:
//      a. If ZoneId == -1 (district-level marker): place at district center
//      b. If ZoneId >= 0: look up ScarMapZoneMapping for (DistrictId, ZoneId)
//         - If mapping found: place at NormalizedPosition within district silhouette
//         - If mapping not found: fallback to district center (zone not yet generated)
//   2. Handle icon stacking:
//      a. Multiple markers at same (DistrictId, ZoneId): offset in a cluster pattern
//      b. Overview mode: cluster around district node
//      c. Detail mode: cluster around zone position
//   3. Write resolved positions to rendering buffer for ScarMapRenderer
```

### ScarMapPersistenceModule

```csharp
// File: Assets/Scripts/ScarMap/Systems/ScarMapPersistenceModule.cs
// Implements ISaveModule (TypeId = 25)
// NOT an ECS system — C# class following the Persistence/ framework pattern
//
// Serialization (Save):
//   1. Write ScarMapPersistenceHeader:
//      - Version = CurrentVersion (1)
//      - ExpeditionSeed from ScarMapSeedState
//      - GraphTopologyHash from ScarMapSeedState
//      - MarkerCount, SummaryCount
//      - Offsets computed from header size
//   2. Write ScarMapMarker array as contiguous blob:
//      - Each marker: DistrictId(4) + ZoneId(4) + Type(1) + Metadata(4) +
//        Timestamp(4) + MarkerHash(4) + IsActive(1) = 22 bytes per marker
//   3. Write ScarMapDistrictSummary array:
//      - Each summary: DistrictId(4) + DominantFrontPhase(1) + BodyCount(1) +
//        ActiveEchoCount(1) + StarEventCount(1) + HasRivalPresence(1) +
//        IsVisited(1) + CompletedCount(1) = 11 bytes per summary
//   4. Compute CRC32 of payload, write to header
//   5. Write header + payload to .dig file via write-ahead pattern (.tmp → .dig)
//
// Deserialization (Load):
//   1. Read header, validate Version and CRC32
//   2. If GraphTopologyHash mismatch: log warning (markers may be misaligned)
//   3. Read marker array → populate ScarMapMarker buffer
//   4. Read summary array → populate ScarMapDistrictSummary buffer
//   5. Restore ScarMapState counters from marker data
//   6. Set ScarMapSeedState from header
//
// Storage budget:
//   - Typical expedition: ~50-100 markers * 22 bytes = 1.1-2.2 KB markers
//   - District summaries: ~8-12 districts * 11 bytes = ~132 bytes
//   - Total per expedition: ~2.5 KB (extremely lightweight)
//   - Compendium cap: 20 expeditions = ~50 KB total
```

### ScarMapReentrySystem

```csharp
// File: Assets/Scripts/ScarMap/Systems/ScarMapReentrySystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: DistrictEntrySystem (EPIC 4)
//
// Handles marker validity on district re-entry:
//   1. On district entry:
//      a. District generated from seed → zone IDs are deterministic
//      b. Existing markers for this district reference zone IDs that still exist
//      c. Validate: for each marker with this DistrictId:
//         - Check ZoneId exists in regenerated layout
//         - If zone removed (graph version change): migrate marker to nearest zone
//   2. Update dynamic markers:
//      a. FrontGradient: refresh from current Front state (may have changed)
//      b. RevivalNode: check if body still exists (may have decayed)
//      c. RivalMarker: refresh from current RivalSimState
//   3. Markers that refer to consumed/resolved events stay as-is (IsActive=false)
//      — they're historical record, not live state
```

---

## Setup Guide

1. **Add ScarMapProcgenComponents.cs** to `Assets/Scripts/ScarMap/Components/`
2. **Create ScarMapSeedAuthoring** on expedition manager prefab:
   - Baker reads expedition seed and computes GraphTopologyHash from district graph
   - Creates ScarMapSeedState component alongside ScarMapState
3. **Implement ScarMapPersistenceModule** following `ISaveModule` pattern:
   - Register TypeId=25 in the Persistence/ module registry
   - Implement `Save(EntityManager, BinaryWriter)` and `Load(EntityManager, BinaryReader)`
   - Follow write-ahead (.tmp → .dig) and CRC32 patterns from existing modules
4. **Wire district entry hook**: ScarMapReentrySystem subscribes to DistrictEntrySystem events
5. **Wire zone layout query**: ScarMapZoneMappingSystem calls into EPIC 4's zone layout API
6. **Test determinism**:
   - Enter district, note marker positions
   - Leave and re-enter same district
   - Verify markers appear at identical positions
7. **Test persistence**:
   - Complete a short expedition
   - Verify .dig file written with TypeId=25 header
   - Load from Compendium, verify markers reconstruct correctly

---

## Verification

- [ ] ScarMapSeedState populated with correct expedition seed and graph hash
- [ ] Zone mappings generated deterministically from district seed
- [ ] Same district seed always produces identical zone positions
- [ ] Markers placed at correct zone positions in detail view
- [ ] District-level markers (ZoneId=-1) placed at district center
- [ ] Icon stacking handles multiple markers at same zone without overlap
- [ ] ScarMapPersistenceModule.Save produces valid .dig file
- [ ] .dig file size matches expected budget (~2.5 KB typical)
- [ ] ScarMapPersistenceModule.Load reconstructs markers correctly
- [ ] CRC32 validation catches corrupted save files
- [ ] GraphTopologyHash mismatch logged as warning (graceful degradation)
- [ ] Re-entering a district shows previously-placed markers at correct positions
- [ ] FrontGradient markers refresh on re-entry to reflect current state
- [ ] RevivalNode markers update active status on re-entry
- [ ] Compendium Scar Maps reconstruct from seed without active expedition data
- [ ] Compendium storage cap at 20 expeditions enforced
- [ ] Write-ahead pattern prevents data loss on crash during save
- [ ] Version field enables future format migration

---

## Validation

```csharp
// File: Assets/Editor/ScarMap/ScarMapProcgenValidator.cs
// Validates seed-based marker placement correctness:
//   1. For each district in the expedition graph:
//      a. Generate zone mapping from seed
//      b. Verify all zone positions are within district silhouette bounds (0-1 normalized)
//      c. Verify no two zone positions overlap (min distance > 0.05)
//   2. Marker DistrictId range check: all in [1..TotalDistrictCount]
//   3. Marker ZoneId range check: all in [-1..zoneCount) for the referenced district
//   4. GraphTopologyHash consistency: hash computed from graph must match stored hash
//   5. Persistence round-trip: serialize → deserialize → compare all fields
//   6. CRC32 tampering test: flip 1 bit in payload, verify Load rejects
```

---

## Simulation & Testing

```csharp
// File: Assets/Tests/ScarMap/ScarMapProcgenTest.cs
// [Test] ZoneMapping_Deterministic
//   Generate zone mappings for district seed 12345 twice, verify identical results.
//
// [Test] ZoneMapping_DifferentSeeds_DifferentPositions
//   Generate zone mappings for seeds 12345 and 67890 for same district,
//   verify at least 50% of positions differ by > 0.1.
//
// [Test] Persistence_RoundTrip
//   Create expedition with 50 markers, save via ScarMapPersistenceModule,
//   load back, verify all markers match (DistrictId, ZoneId, Type, Metadata,
//   Timestamp, MarkerHash, IsActive).
//
// [Test] Persistence_CRC32_Integrity
//   Save valid data, corrupt 1 byte in the payload section,
//   verify Load throws or returns error due to CRC mismatch.
//
// [Test] Persistence_StorageBudget
//   Create expedition with 100 markers (worst case), verify file size < 5 KB.
//
// [Test] ReentrySystem_MarkersPreserved
//   Place 5 markers in district, exit, re-enter, verify all markers
//   appear at identical positions.
```
