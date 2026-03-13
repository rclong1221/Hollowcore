# EPIC 13.2: District Generation via IZoneProvider

**Status**: Planning
**Epic**: EPIC 13 — District Content Pipeline
**Priority**: Critical — Turns district definitions into playable geometry
**Dependencies**: 13.1 (DistrictDefinitionSO), Framework: Roguelite/IZoneProvider, Roguelite/ZoneDefinitionSO, Voxel/

---

## Overview

Each district needs a concrete `IZoneProvider` implementation that turns a seed + zone definition into playable 3D space. The framework defines the contract (`IZoneProvider.Initialize` / `IsReady` / `Activate` / `Deactivate` / `Dispose`); this sub-epic provides the Hollowcore-specific implementations and the base class shared by all districts. Four generation strategies are supported: prefab assembly, voxel generation, hybrid, and hand-crafted with procedural placement. Each district picks the strategy that best fits its topology. Anti-sameness rules from GDD S17.3 are enforced through topology variants and landmark composition.

---

## Component Definitions

### DistrictGenerationMode Enum

```csharp
// File: Assets/Scripts/District/Generation/DistrictGenerationEnums.cs
namespace Hollowcore.District.Generation
{
    /// <summary>
    /// How zone geometry is produced. Each district selects one mode.
    /// </summary>
    public enum DistrictGenerationMode : byte
    {
        /// <summary>Rooms assembled from a prefab pool, Ravenswatch-style.</summary>
        PrefabAssembly = 0,

        /// <summary>Full procedural voxel terrain using framework Voxel/ system.</summary>
        VoxelGeneration = 1,

        /// <summary>Prefab rooms with procedurally generated connectors.</summary>
        Hybrid = 2,

        /// <summary>Hand-crafted scene with procedural enemy/loot/POI placement.</summary>
        HandCraftedProcedural = 3
    }
}
```

### DistrictGeneratorConfig

```csharp
// File: Assets/Scripts/District/Generation/DistrictGeneratorConfigSO.cs
using UnityEngine;

namespace Hollowcore.District.Generation
{
    /// <summary>
    /// Per-district generation settings. Stored on DistrictDefinitionSO.ExtensionData
    /// or referenced directly. The DistrictGeneratorBase reads this to configure
    /// zone production.
    /// </summary>
    [CreateAssetMenu(fileName = "DistrictGeneratorConfig", menuName = "Hollowcore/District/Generator Config")]
    public class DistrictGeneratorConfigSO : ScriptableObject
    {
        [Header("Strategy")]
        public DistrictGenerationMode Mode = DistrictGenerationMode.PrefabAssembly;

        [Header("Prefab Assembly")]
        [Tooltip("Room prefab pool for PrefabAssembly or Hybrid mode.")]
        public ZoneRoomPoolSO RoomPool;
        [Tooltip("Connector prefab pool for Hybrid mode.")]
        public ZoneRoomPoolSO ConnectorPool;

        [Header("Voxel")]
        [Tooltip("Voxel generation parameters for VoxelGeneration mode.")]
        public ScriptableObject VoxelConfig;

        [Header("Hand-Crafted")]
        [Tooltip("Addressable scene paths per zone index for HandCraftedProcedural mode.")]
        public string[] ScenePaths;

        [Header("Common")]
        [Tooltip("NavMesh agent type ID for pathfinding bake.")]
        public int NavMeshAgentTypeId;
        [Tooltip("Seconds allowed for async generation before timeout.")]
        public float GenerationTimeoutSeconds = 10f;
    }
}
```

### ZoneRoomPoolSO

```csharp
// File: Assets/Scripts/District/Generation/ZoneRoomPoolSO.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hollowcore.District.Generation
{
    /// <summary>
    /// Pool of room prefabs for prefab assembly generation.
    /// Rooms are tagged by ZoneType and size, with connection sockets.
    /// </summary>
    [CreateAssetMenu(fileName = "ZoneRoomPool", menuName = "Hollowcore/District/Zone Room Pool")]
    public class ZoneRoomPoolSO : ScriptableObject
    {
        public List<ZoneRoomEntry> Entries = new();
    }

    [Serializable]
    public struct ZoneRoomEntry
    {
        public GameObject RoomPrefab;
        public string RoomName;
        public DIG.Roguelite.Zones.ZoneType AllowedType;
        public DIG.Roguelite.Zones.ZoneSizeHint Size;

        [Tooltip("Socket tags on this prefab (North, South, East, West, Up, Down).")]
        public string[] ConnectionSockets;

        [Tooltip("Selection weight within allowed type.")]
        public float Weight;
    }
}
```

### ZoneBoundary (IComponentData)

```csharp
// File: Assets/Scripts/District/Components/ZoneBoundaryComponents.cs
using Unity.Entities;
using Unity.Mathematics;

namespace Hollowcore.District
{
    /// <summary>
    /// Axis-aligned bounding box for a generated zone. Created by the generator,
    /// read by spawn director, Front spread, and minimap systems.
    /// </summary>
    public struct ZoneBoundary : IComponentData
    {
        public float3 Min;
        public float3 Max;
        public int ZoneIndex;
    }

    /// <summary>
    /// Spawn point within a zone. Buffer on the zone boundary entity.
    /// Populated by the generator, consumed by SpawnDirectorSystem.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ZoneSpawnPoint : IBufferElementData
    {
        public float3 Position;
        public quaternion Rotation;
        public byte SpawnPointType; // 0=Enemy, 1=Loot, 2=POI, 3=Interactable
    }

    /// <summary>
    /// Gate/portal connecting two zones. Buffer on the zone boundary entity.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct ZoneGate : IBufferElementData
    {
        public float3 Position;
        public int ConnectedZoneIndex;
        public bool IsLocked;
    }

    /// <summary>
    /// Marks the Front origin point within the district.
    /// One entity per district, created by the generator.
    /// </summary>
    public struct FrontOriginMarker : IComponentData
    {
        public float3 Position;
        public int OriginZoneIndex;
    }
}
```

---

## Systems

### DistrictGeneratorBase (Abstract)

```csharp
// File: Assets/Scripts/District/Generation/DistrictGeneratorBase.cs
// Abstract base class implementing IZoneProvider with shared logic:
//
// Shared responsibilities:
//   1. Resolve DistrictDefinitionSO → topology variant from seed
//   2. Create ZoneBoundary entities with ZoneSpawnPoint + ZoneGate buffers
//   3. Place FrontOriginMarker entity at designated position
//   4. Validate all zones have at least 1 spawn point and 1 gate (except boss zone)
//   5. Build NavMesh data (calls Unity.AI.Navigation runtime bake or loads pre-baked)
//
// Subclasses override:
//   - GenerateZoneGeometry(int zoneIndex, ZoneGraphEntry entry, uint seed) → GameObject
//   - PlaceLandmarks(int zoneIndex, LandmarkPOIDefinition[] landmarks) → void
//   - PlaceMicroPOIs(int zoneIndex, MicroPOIPoolSO pool, uint seed) → void
//
// Anti-sameness enforcement:
//   - Seed selects topology variant (2-3 per district)
//   - Landmark composition rules: no two adjacent zones share the same landmark type
//   - Micro-POI density varies by zone type (higher in Exploration, lower in Combat)
//   - Front origin placement shifts per variant (changes map usage pattern)
```

### PrefabAssemblyGenerator

```csharp
// File: Assets/Scripts/District/Generation/PrefabAssemblyGenerator.cs
// Extends: DistrictGeneratorBase
// Implements: IZoneProvider
//
// Strategy: Selects room prefabs from ZoneRoomPoolSO, aligns connection sockets,
// instantiates into scene hierarchy. Used by most districts.
//
// Initialize(seed, zoneIndex, definition):
//   1. Filter room pool by ZoneType
//   2. Weighted random selection from pool using seed
//   3. Socket alignment: match North→South, East→West, Up→Down
//   4. Validate no overlapping geometry (AABB check)
//   5. If overlap, retry with next candidate (max 3 retries)
//
// Activate():
//   1. Enable all room GameObjects
//   2. Extract spawn points from child SpawnPointMarker components
//   3. Extract gates from child GateMarker components
//   4. Return ZoneActivationResult with positions
```

### VoxelDistrictGenerator

```csharp
// File: Assets/Scripts/District/Generation/VoxelDistrictGenerator.cs
// Extends: DistrictGeneratorBase
// Implements: IZoneProvider
//
// Strategy: Uses framework Voxel/ system for full procedural terrain.
// Suitable for organic districts (Old Growth, Quarantine).
//
// Initialize(seed, zoneIndex, definition):
//   1. Configure voxel generation parameters from VoxelConfig
//   2. Run async voxel generation pass
//   3. Carve zone boundaries from voxel data
//   4. Place spawn points on navigable surfaces
```

### HandCraftedGenerator

```csharp
// File: Assets/Scripts/District/Generation/HandCraftedGenerator.cs
// Extends: DistrictGeneratorBase
// Implements: IZoneProvider
//
// Strategy: Load pre-built scenes via Addressables, overlay procedural placement.
// Used for districts with complex set-piece topology (Necrospire, Chrome Cathedral).
//
// Initialize(seed, zoneIndex, definition):
//   1. Load Addressable scene for this zone index from ScenePaths[]
//   2. Wait for async scene load
//   3. Scan scene for SpawnPointMarker, GateMarker, POIAnchor components
//   4. Apply procedural enemy/loot placement on top of hand-crafted geometry
```

### DistrictZoneProviderRegistrySystem

```csharp
// File: Assets/Scripts/District/Systems/DistrictZoneProviderRegistrySystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: InitializationSystemGroup
//
// Reads: DistrictState, DistrictGeneratorConfigSO
// Writes: Registers IZoneProvider with framework ZoneTransitionSystem
//
// On district load:
//   1. Read DistrictGeneratorConfigSO.Mode
//   2. Instantiate correct generator: PrefabAssembly, Voxel, Hybrid, or HandCrafted
//   3. Register as the active IZoneProvider with the framework
//   4. Framework ZoneTransitionSystem calls Initialize/Activate/Deactivate lifecycle
```

---

## Setup Guide

1. **Create `Assets/Scripts/District/Generation/` folder**
2. **Create SpawnPointMarker and GateMarker MonoBehaviours** — simple marker components placed on room prefab children to designate spawn positions and gate locations
3. **Create first room prefab pool**: `Assets/Data/Districts/Necrospire/Rooms/` with 10-15 room prefabs for Necrospire zones
4. **Tag room prefabs** with connection sockets (empty GameObjects named `Socket_North`, `Socket_South`, etc.)
5. **Create DistrictGeneratorConfigSO** for Necrospire: Mode = HandCraftedProcedural (vertical slice uses hand-crafted scenes)
6. **Wire into DistrictDefinitionSO**: add DistrictGeneratorConfigSO to ExtensionData array
7. **Create NavMesh surfaces** on room prefabs or configure runtime bake parameters

---

## Verification

- [ ] DistrictGeneratorBase creates ZoneBoundary entities for all zones in graph
- [ ] ZoneSpawnPoint buffer populated with at least 1 point per zone
- [ ] ZoneGate buffer populated with connections matching ZoneGraphEntry.ConnectedZoneIndices
- [ ] FrontOriginMarker entity created at correct position
- [ ] PrefabAssemblyGenerator socket alignment produces connected rooms without overlap
- [ ] HandCraftedGenerator loads Addressable scene and extracts markers
- [ ] Topology variant selection produces visibly different layouts for different seeds
- [ ] Anti-sameness: same district with 3 different seeds → 2-3 different zone arrangements
- [ ] ZoneActivationResult.PlayerSpawnPosition is navigable (not inside geometry)
- [ ] NavMesh data present after generation (agent can pathfind)
- [ ] Framework ZoneTransitionSystem accepts and calls the registered IZoneProvider

---

## Validation

```csharp
// File: Assets/Editor/District/DistrictGenerationValidator.cs
// Runs after IZoneProvider.Initialize completes (editor playmode and build-time test):
//
//   1. Zone boundary overlap: no two ZoneBoundary AABBs intersect
//      (allow 0.1m tolerance for shared walls)
//   2. Spawn point count: every zone has >= 1 ZoneSpawnPoint (type=Enemy)
//   3. Gate connectivity: every ZoneGate.ConnectedZoneIndex references
//      a valid zone that also has a reciprocal gate back
//   4. FrontOriginMarker: exactly 1 per district, position inside a valid ZoneBoundary
//   5. NavMesh coverage: >= 80% of zone floor area is navigable
//   6. Socket alignment (PrefabAssembly): all room pairs have matched socket types
//   7. Scene path validity (HandCrafted): all ScenePaths[] resolve via Addressables
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/District/Debug/GenerationDebugOverlay.cs
// Development builds, toggle: Ctrl+F7
//   - ZoneBoundary wireframes (color by ZoneType)
//   - ZoneSpawnPoint dots: green=Enemy, blue=Loot, yellow=POI, cyan=Interactable
//   - ZoneGate positions: magenta spheres with connection lines
//   - FrontOriginMarker: red pulsing sphere
//   - NavMesh edges: white wireframe overlay
//   - Socket alignment (PrefabAssembly): show socket positions and match status
```

---

## Simulation & Testing

```csharp
// File: Assets/Tests/District/DistrictGenerationPerfTest.cs
// [Test] PrefabAssembly_10Zone_Under5Seconds
//   Generate a 10-zone district via PrefabAssemblyGenerator,
//   verify Initialize→IsReady completes within GenerationTimeoutSeconds.
//
// [Test] DeterministicGeneration_SameSeed
//   Generate same district with same seed twice,
//   verify identical ZoneBoundary positions and ZoneGate placements.
//
// [Test] AntiSameness_3Variants_VisuallyDistinct
//   Generate with seeds that select each topology variant,
//   verify zone count or connectivity differs between at least 2 pairs.
```
