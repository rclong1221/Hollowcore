# EPIC 13.5: POI System

**Status**: Planning
**Epic**: EPIC 13 — District Content Pipeline
**Priority**: High — POIs give districts identity and drive exploration
**Dependencies**: 13.1 (DistrictDefinitionSO), 13.2 (zone generation, ZoneSpawnPoint), Framework: Interaction/ (InteractableAuthoring, StationAuthoring), Roguelite/ (InteractablePoolSO, InteractableDirectorSystem)

---

## Overview

Points of Interest give each district its memorable locations and environmental storytelling. Two tiers: **Landmark POIs** (5-6 per district) are named, visually distinct locations at fixed positions within each topology variant — they are the places players remember and use as mental waypoints. **Micro POIs** are smaller environmental details placed procedurally from a weighted pool, scaled by zone type. Some POIs are interactable (vendors, body shops, lore terminals) using the framework Interaction/ system. POI state (visited, looted, interacted) is persisted in the district save for run continuity.

---

## Component Definitions

### POICategory Enum

```csharp
// File: Assets/Scripts/District/POI/POIEnums.cs
namespace Hollowcore.District.POI
{
    public enum POICategory : byte
    {
        Landmark = 0,
        Micro = 1
    }

    public enum POIInteractionType : byte
    {
        None = 0,           // Visual only, no interaction
        Vendor = 1,         // Opens shop UI
        BodyShop = 2,       // Chassis repair / limb install
        LoreTerminal = 3,   // Displays lore text, may give intel
        Workbench = 4,      // Crafting station
        Stash = 5,          // Player storage access
        HealStation = 6,    // Restores health/resources
        QuestGiver = 7,     // NPC that offers/completes quests
        EnvironmentHazard = 8 // Interactive hazard (lever, valve)
    }

    public enum POIState : byte
    {
        Undiscovered = 0,
        Discovered = 1,
        Visited = 2,
        Looted = 3,
        Completed = 4
    }
}
```

### LandmarkPOIDefinition

```csharp
// File: Assets/Scripts/District/POI/LandmarkPOIDefinition.cs
using System;
using UnityEngine;

namespace Hollowcore.District.POI
{
    /// <summary>
    /// Definition for a named landmark location. 5-6 per district.
    /// Landmarks have fixed positions per topology variant and specific layout templates.
    /// </summary>
    [Serializable]
    public struct LandmarkPOIDefinition
    {
        public string LandmarkName;
        [TextArea(1, 3)] public string Description;
        public Sprite Icon;
        public Sprite MinimapIcon;

        [Tooltip("Zone index where this landmark is located.")]
        public int ZoneIndex;

        [Tooltip("Scene composition prefab. Instantiated at the landmark anchor point.")]
        public GameObject CompositionPrefab;

        [Tooltip("Interaction type if this landmark is interactable.")]
        public POIInteractionType InteractionType;

        [Tooltip("Interactable prefab placed at this landmark. Null = non-interactable.")]
        public GameObject InteractablePrefab;

        [Tooltip("Lore entries unlocked on first visit.")]
        public int[] LoreEntryIds;

        [Tooltip("Composition rule: minimum distance from other landmarks.")]
        public float MinDistanceFromOtherLandmarks;
    }
}
```

### MicroPOIPoolSO

```csharp
// File: Assets/Scripts/District/POI/MicroPOIPoolSO.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hollowcore.District.POI
{
    /// <summary>
    /// Weighted pool of micro-POI prefabs for procedural placement.
    /// Each district has one pool. Placement density scales by zone type.
    /// </summary>
    [CreateAssetMenu(fileName = "MicroPOIPool", menuName = "Hollowcore/District/Micro POI Pool")]
    public class MicroPOIPoolSO : ScriptableObject
    {
        public string PoolName;
        public List<MicroPOIEntry> Entries = new();

        [Header("Density by Zone Type")]
        [Tooltip("Micro-POIs per 100 sq meters by zone type index.")]
        public float[] DensityByZoneType = new float[10]; // Indexed by ZoneType
    }

    [Serializable]
    public struct MicroPOIEntry
    {
        public GameObject Prefab;
        public string DisplayName;
        public float SelectionWeight;

        [Tooltip("Requires ground placement (raycast down to surface).")]
        public bool RequiresGround;

        [Tooltip("Can attach to walls.")]
        public bool CanAttachToWall;

        [Tooltip("Minimum spacing from other micro-POIs of the same type.")]
        public float MinSpacing;

        [Tooltip("Zone types this micro-POI can appear in. Empty = all.")]
        public DIG.Roguelite.Zones.ZoneType[] AllowedZoneTypes;
    }
}
```

### POIRuntimeState (IComponentData)

```csharp
// File: Assets/Scripts/District/POI/POIComponents.cs
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Hollowcore.District.POI
{
    /// <summary>
    /// Runtime state for a placed POI entity. Tracks discovery and interaction state.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct POIRuntimeState : IComponentData
    {
        [GhostField] public int POIId;
        [GhostField] public byte Category;          // POICategory cast
        [GhostField] public byte InteractionType;    // POIInteractionType cast
        [GhostField] public byte State;              // POIState cast
        [GhostField] public int ZoneIndex;
    }

    /// <summary>
    /// Tag for landmark POIs. Distinguishes from micro-POIs in queries.
    /// </summary>
    public struct LandmarkTag : IComponentData { }

    /// <summary>
    /// Discovery radius trigger. When a player enters this radius,
    /// the POI transitions from Undiscovered to Discovered and appears on the map.
    /// </summary>
    public struct POIDiscoveryRadius : IComponentData
    {
        public float Radius;
    }

    /// <summary>
    /// Buffer of POI save entries on the DistrictState entity.
    /// Persisted between zone transitions within a district run.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct POIPersistenceEntry : IBufferElementData
    {
        public int POIId;
        public byte State; // POIState cast
    }
}
```

---

## Systems

### LandmarkPlacementSystem

```csharp
// File: Assets/Scripts/District/POI/Systems/LandmarkPlacementSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: DistrictLoadSystem
//
// Reads: DistrictDefinitionSO.LandmarkPOIs, ZoneBoundary, topology variant
// Writes: POIRuntimeState entities with LandmarkTag, scene composition GameObjects
//
// On district load:
//   1. For each LandmarkPOIDefinition in the district:
//      a. Find anchor position in the zone (from ZoneSpawnPoint type=POI or fixed offset)
//      b. Validate composition rule: distance from other landmarks >= MinDistance
//      c. Instantiate CompositionPrefab at anchor
//      d. If InteractablePrefab != null, instantiate and link to Interaction/ system
//      e. Create POIRuntimeState entity { State = Undiscovered, LandmarkTag }
//   2. Restore POI states from POIPersistenceEntry buffer (re-entering district)
```

### MicroPOIPlacementSystem

```csharp
// File: Assets/Scripts/District/POI/Systems/MicroPOIPlacementSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: LandmarkPlacementSystem
//
// Reads: MicroPOIPoolSO, ZoneBoundary, ZoneState
// Writes: POIRuntimeState entities, micro-POI GameObjects
//
// On zone activation:
//   1. Calculate target count: density * zone area (from ZoneBoundary)
//   2. Weighted random selection from MicroPOIPoolSO.Entries using zone seed
//   3. For each selected entry:
//      a. Sample position within ZoneBoundary (avoiding spawn points and gates)
//      b. If RequiresGround: raycast down to find surface
//      c. If CanAttachToWall: raycast to find nearest wall
//      d. Enforce MinSpacing between same-type micro-POIs
//      e. Instantiate prefab, create POIRuntimeState entity
```

### POIDiscoverySystem

```csharp
// File: Assets/Scripts/District/POI/Systems/POIDiscoverySystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Reads: POIRuntimeState, POIDiscoveryRadius, PlayerTag + LocalTransform
// Writes: POIRuntimeState.State (Undiscovered → Discovered)
//
// Each frame:
//   1. For each POI with State == Undiscovered and POIDiscoveryRadius:
//      a. Check distance to all players
//      b. If any player within radius: set State = Discovered
//      c. Fire POIDiscoveredEvent for UI (minimap icon, notification)
//      d. Unlock lore entries if LoreEntryIds specified
```

### POIPersistenceSystem

```csharp
// File: Assets/Scripts/District/POI/Systems/POIPersistenceSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup (OrderLast)
//
// Reads: POIRuntimeState (all POI entities)
// Writes: POIPersistenceEntry buffer on DistrictState entity
//
// On zone transition or district save:
//   1. Clear existing POIPersistenceEntry buffer
//   2. For each POIRuntimeState entity:
//      a. Write { POIId, State } to buffer
//   3. Buffer is serialized with district save data (EPIC 4.2)
```

---

## Setup Guide

1. **Create `Assets/Scripts/District/POI/` folder** with subfolders: Systems/, Components (POIComponents.cs, POIEnums.cs)
2. **Create `Assets/Data/Districts/Necrospire/POIs/`** folder
3. **Author 5 LandmarkPOIDefinitions** for Necrospire:
   - Hologram Shrine Plaza (ZoneIndex=0, InteractionType=LoreTerminal)
   - Relay Node Chapel (ZoneIndex=2, InteractionType=HealStation)
   - Credential Forge (ZoneIndex=4, InteractionType=BodyShop)
   - Purge Corridor (ZoneIndex=6, InteractionType=None — environmental only)
   - Upload Vault (ZoneIndex=8, InteractionType=Vendor)
4. **Create composition prefabs** for each landmark: `Assets/Prefabs/Districts/Necrospire/Landmarks/`
5. **Create MicroPOIPoolSO** for Necrospire with entries: broken terminals, grief totems, biometric scanners, drone nests
6. **Wire** LandmarkPOIs and MicroPOIPool into DistrictDefinitionSO
7. **Add POIDiscoveryRadius** (default 10m) to landmark composition prefabs via authoring component

---

## Verification

- [ ] 5-6 landmark POIs instantiated at district load with correct composition prefabs
- [ ] Landmark composition rule enforced: no two landmarks closer than MinDistance
- [ ] Micro-POIs placed with density scaling by zone type
- [ ] Micro-POI MinSpacing enforced between same-type entries
- [ ] POIDiscoverySystem transitions Undiscovered to Discovered when player approaches
- [ ] Discovered POIs appear on minimap with correct icon
- [ ] Interactable POIs (vendor, body shop, lore terminal) respond to framework Interaction/ system
- [ ] POIPersistenceEntry buffer saved and restored on zone transitions
- [ ] Re-entering a district restores POI states (visited landmarks stay visited)
- [ ] Lore entries unlocked on first landmark visit
- [ ] Micro-POIs respect RequiresGround and CanAttachToWall placement constraints

---

## Validation

```csharp
// File: Assets/Editor/District/POIValidator.cs
// Validates LandmarkPOIDefinition arrays and MicroPOIPoolSO assets:
//
//   1. Landmark distance rules:
//      [ERROR] Two landmarks in same zone with distance < MinDistanceFromOtherLandmarks
//   2. Landmark zone bounds:
//      [ERROR] ZoneIndex >= zone graph length
//      [ERROR] CompositionPrefab is null
//   3. Interaction wiring:
//      [ERROR] InteractionType != None but InteractablePrefab is null
//      [WARNING] InteractablePrefab set but InteractionType == None
//   4. Micro-POI density bounds:
//      [WARNING] DensityByZoneType[i] > 5.0 (unreasonably dense)
//      [WARNING] All densities are 0 (no micro-POIs will spawn)
//   5. Micro-POI entries:
//      [ERROR] Prefab is null in any MicroPOIEntry
//      [WARNING] SelectionWeight <= 0
//      [ERROR] MinSpacing < 0
```

---

## Live Tuning

| Parameter | Source | Effect |
|-----------|--------|--------|
| DensityByZoneType | MicroPOIPoolSO | Micro-POIs per 100 sq meters by zone type |
| MinSpacing | MicroPOIEntry | Minimum distance between same-type micro-POIs |
| DiscoveryRadius | POIDiscoveryRadius | Distance at which POI transitions to Discovered |
| MinDistanceFromOtherLandmarks | LandmarkPOIDefinition | Composition rule spacing |

---

## Debug Visualization

```csharp
// File: Assets/Scripts/District/Debug/POIDebugOverlay.cs
// Development builds, toggle: Ctrl+F12
//   - Landmark positions: large colored spheres with name labels
//   - Landmark discovery radius: wireframe sphere (green=discovered, grey=undiscovered)
//   - Micro-POI positions: small dots color-coded by POIState
//   - Interaction type icons floating above interactable POIs
//   - POI density heat map: per-zone coloring by micro-POI count
//   - Composition rule violations: red lines between landmarks that are too close
```
