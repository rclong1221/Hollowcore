# EPIC 4.4: Zone Structure Within Districts

**Status**: Planning
**Epic**: EPIC 4 — District Graph & Expedition Structure
**Priority**: High — Internal layout of each district map
**Dependencies**: EPIC 4.1 (Graph Data Model), EPIC 4.6 (Seed & Determinism), Framework: Roguelite/ (IZoneProvider, ZoneDefinitionSO)

---

## Overview

Each district in the expedition graph contains 8-15 interconnected zones forming a non-linear internal map. Zones are not a linear corridor but a connectivity graph with multiple paths, dead ends, shortcuts, and chokepoints. Each zone has a type (Combat, Elite, Boss, Shop, Event, Rest, Support), threat faction assignments, resource placements, and a FrontZoneState driven by EPIC 3. Entry points (1-3 per district) determine where the player starts based on which gate they entered through. Zone layouts are generated deterministically from the district seed with 2-3 topology variants per district type.

---

## Component Definitions

### ZoneDefinitionSO (Extended)

```csharp
// File: Assets/Scripts/Expedition/Definitions/ZoneDefinitionSO.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hollowcore.Expedition.Definitions
{
    public enum ZoneType : byte
    {
        Combat = 0,
        Elite = 1,
        Boss = 2,
        Shop = 3,
        Event = 4,
        Rest = 5,
        Support = 6,
    }

    /// <summary>
    /// Extended zone definition that replaces the framework's linear ZoneDefinitionSO.
    /// Describes a single zone within a district, including its connectivity to other zones.
    /// </summary>
    [CreateAssetMenu(fileName = "NewZone", menuName = "Hollowcore/Expedition/Zone Definition")]
    public class ZoneDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Deterministic ID derived from district seed + zone index")]
        public int ZoneId;
        public string DisplayName;
        public ZoneType Type;

        [Header("Connectivity")]
        [Tooltip("Indices of zones this zone connects to within the district")]
        public List<int> ConnectedZoneIndices = new();

        [Header("Threats")]
        [Tooltip("Primary faction that spawns in this zone")]
        public FactionId PrimaryFaction;
        [Tooltip("Optional secondary faction for mixed encounters")]
        public FactionId SecondaryFaction;
        [Tooltip("Base threat level multiplier (1.0 = district default)")]
        public float ThreatMultiplier = 1.0f;

        [Header("Resources")]
        public int LootSpawnCount;
        public int SalvageNodeCount;
        [Tooltip("Whether this zone can spawn a vendor")]
        public bool CanSpawnVendor;

        [Header("Generation")]
        [Tooltip("Scene composition prefab or tileset for this zone type")]
        public GameObject ZoneCompositionPrefab;
        [Tooltip("Landmark POI prefabs that can appear in this zone")]
        public List<GameObject> LandmarkPrefabs;
    }
}
```

### ZoneTopologyTemplate (ScriptableObject)

```csharp
// File: Assets/Scripts/Expedition/Definitions/ZoneTopologyTemplate.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hollowcore.Expedition.Definitions
{
    /// <summary>
    /// A predefined topology variant for a district type.
    /// Each district has 2-3 of these; the seed selects one.
    /// Defines zone count, types, and adjacency.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTopology", menuName = "Hollowcore/Expedition/Zone Topology")]
    public class ZoneTopologyTemplate : ScriptableObject
    {
        [Header("Layout")]
        public string VariantName;
        public int ZoneCount;
        public List<ZoneSlotTemplate> ZoneSlots = new();
        public List<ZoneConnectionTemplate> Connections = new();

        [Header("Entry Points")]
        [Tooltip("Zone indices that serve as entry points (1-3)")]
        public List<int> EntryPointZoneIndices = new();
    }

    [Serializable]
    public class ZoneSlotTemplate
    {
        public ZoneType Type;
        [Tooltip("Relative position within district for layout")]
        public Vector2 RelativePosition;
        [Tooltip("If true, this zone is always present regardless of seed pruning")]
        public bool Required;
    }

    [Serializable]
    public class ZoneConnectionTemplate
    {
        public int ZoneIndexA;
        public int ZoneIndexB;
        [Tooltip("If true, seed cannot prune this connection")]
        public bool Required;
        [Tooltip("If true, this is a one-way shortcut (A→B only)")]
        public bool OneWay;
    }
}
```

### ZoneState (IComponentData)

```csharp
// File: Assets/Scripts/Expedition/Components/ZoneComponents.cs
using Unity.Entities;
using Unity.Mathematics;

namespace Hollowcore.Expedition
{
    /// <summary>
    /// Runtime state for a single zone within the active district.
    /// One entity per zone, created during district generation.
    /// </summary>
    public struct ZoneState : IComponentData
    {
        /// <summary>Deterministic zone ID (districtSeed hash + zone index).</summary>
        public int ZoneId;

        /// <summary>Index within the district's zone list.</summary>
        public int ZoneIndex;

        /// <summary>Zone type from the topology template.</summary>
        public ZoneType Type;

        /// <summary>Front-driven zone state (Safe, Contested, Hostile, Overrun).</summary>
        public FrontZoneState FrontState;

        /// <summary>Primary faction present in this zone.</summary>
        public FactionId PrimaryFaction;

        /// <summary>Whether the player has visited this zone during the current visit.</summary>
        public bool PlayerVisited;

        /// <summary>Whether all enemies in this zone have been killed.</summary>
        public bool Cleared;

        /// <summary>AABB center for spatial queries (which zone is player in).</summary>
        public float3 BoundsCenter;

        /// <summary>AABB extents for spatial queries.</summary>
        public float3 BoundsExtents;
    }

    public enum FrontZoneState : byte
    {
        Safe = 0,
        Contested = 1,
        Hostile = 2,
        Overrun = 3,
    }

    /// <summary>
    /// Buffer of connected zone indices on each zone entity.
    /// </summary>
    public struct ZoneConnection : IBufferElementData
    {
        /// <summary>ZoneIndex of the connected zone.</summary>
        public int ConnectedZoneIndex;

        /// <summary>True if this is a one-way connection (from this zone only).</summary>
        public bool OneWay;
    }
}
```

### ZoneEntryPointLink (IComponentData)

```csharp
// File: Assets/Scripts/Expedition/Components/ZoneComponents.cs (continued)
namespace Hollowcore.Expedition
{
    /// <summary>
    /// Links a DistrictEntryPoint (EPIC 4.3) to the zone it resides in.
    /// Baked on entry point entities.
    /// </summary>
    public struct ZoneEntryPointLink : IComponentData
    {
        /// <summary>ZoneIndex within the district where this entry point is located.</summary>
        public int ZoneIndex;
    }
}
```

---

## Systems

### ZoneGenerationSystem

```csharp
// File: Assets/Scripts/Expedition/Systems/ZoneGenerationSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: InitializationSystemGroup
// UpdateAfter: Scene load callback
//
// Generates zone entities within a newly loaded district scene.
//
// 1. Read district seed from ExpeditionGraphState + current node index
//    zoneSeed = RunSeedUtility.DeriveHash(districtSeed, "zones")
// 2. Select topology variant:
//    variantIndex = zoneSeed % districtDefinition.TopologyVariants
//    Load corresponding ZoneTopologyTemplate
// 3. For each ZoneSlotTemplate in the topology:
//    a. Create zone entity with ZoneState component
//    b. ZoneId = RunSeedUtility.DeriveHash(districtSeed, zoneIndex)
//    c. Set Type, PrimaryFaction (from district definition + slot rules)
//    d. FrontState = Safe (or restored from DistrictSaveState if revisiting)
//    e. Compute BoundsCenter/Extents from zone composition placement
// 4. For each ZoneConnectionTemplate:
//    a. Add ZoneConnection buffer element to both zone entities
//    b. If !Required: seed-probability prune (ensure graph stays connected)
// 5. Validate connectivity: BFS from each entry point zone must reach all non-pruned zones
//    If disconnected, restore pruned connections until connected
```

### ZoneTrackingSystem

```csharp
// File: Assets/Scripts/Expedition/Systems/ZoneTrackingSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Tracks which zone the player is currently in.
//
// 1. Read player world position
// 2. For each ZoneState entity:
//    a. AABB containment test (position within BoundsCenter +/- BoundsExtents)
//    b. If match: update ActiveZone singleton with current ZoneIndex
// 3. If player entered a new zone:
//    a. Set ZoneState.PlayerVisited = true
//    b. Trigger zone discovery UI event
//    c. If zone has undiscovered connections to new zones, reveal them on minimap
// 4. If no zone contains player (transition area): keep last known zone
```

### ZoneClearCheckSystem

```csharp
// File: Assets/Scripts/Expedition/Systems/ZoneClearCheckSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: ZoneTrackingSystem
//
// Checks if all enemies in a zone have been killed.
//
// 1. For each ZoneState where !Cleared:
//    a. Count alive enemies assigned to this ZoneIndex
//    b. If count == 0 and zone has been visited: set Cleared = true
//    c. Fire ZoneClearedEvent for UI/audio feedback
// 2. If zone is Elite or Boss type and cleared:
//    a. Check if clearing unlocks any gate conditions (feed into ExpeditionGraphUpdateSystem)
```

---

## Setup Guide

1. **Create `Assets/Scripts/Expedition/Definitions/` folder** for ZoneDefinitionSO and ZoneTopologyTemplate assets
2. **Create 2-3 ZoneTopologyTemplate assets** per district type at `Assets/Data/Expedition/Topologies/`:
   - Example: `Necrospire_TopologyA.asset` (12 zones, hub-and-spoke), `Necrospire_TopologyB.asset` (10 zones, branching corridor), `Necrospire_TopologyC.asset` (15 zones, open grid)
3. **Add ZoneTopologyTemplate references** to each DistrictDefinitionSO
4. **Create ZoneAuthoring MonoBehaviour** that bakes ZoneState + ZoneConnection buffer from scene placement data
5. **Place zone composition prefabs** in district scenes — each zone's bounds define the AABB for ZoneTrackingSystem
6. **Place DistrictEntryPoint authoring objects** in zones that are entry points, add ZoneEntryPointLink
7. **Assign enemies to zones** via a ZoneAssignment IComponentData on enemy prefabs (baked from spawn point's zone index)

---

## Verification

- [ ] ZoneGenerationSystem creates correct number of zone entities from topology template
- [ ] Same district seed + topology variant produces identical zone IDs and connectivity
- [ ] Different seeds select different topology variants (within 2-3 options)
- [ ] ZoneConnection buffers are bidirectional for non-one-way connections
- [ ] Connectivity validation ensures all zones reachable from entry points
- [ ] ZoneTrackingSystem correctly identifies player's current zone via AABB
- [ ] Zone transitions trigger PlayerVisited flag and discovery UI
- [ ] ZoneClearCheckSystem marks zones as cleared when all enemies dead
- [ ] Entry points link to correct zones via ZoneEntryPointLink
- [ ] FrontZoneState initializes correctly (Safe for new districts, restored for revisited)
- [ ] 8-15 zones per district (within configured range)
- [ ] Multiple paths exist between entry points and key zones (non-linear)

---

## BlobAsset Pipeline

ZoneTopologyTemplate is read at runtime during zone generation. Converting to blob makes it Burst-accessible and eliminates managed allocations during district loading.

```csharp
// File: Assets/Scripts/Expedition/Blob/ZoneTopologyBlob.cs
using Unity.Entities;

namespace Hollowcore.Expedition.Blob
{
    public struct ZoneTopologyBlob
    {
        public BlobString VariantName;
        public int ZoneCount;
        public BlobArray<ZoneSlotBlob> ZoneSlots;
        public BlobArray<ZoneConnectionBlob> Connections;
        public BlobArray<int> EntryPointZoneIndices;
    }

    public struct ZoneSlotBlob
    {
        public byte Type; // ZoneType
        public float RelativePositionX;
        public float RelativePositionY;
        public bool Required;
    }

    public struct ZoneConnectionBlob
    {
        public int ZoneIndexA;
        public int ZoneIndexB;
        public bool Required;
        public bool OneWay;
    }
}
```

```csharp
// File: Assets/Scripts/Expedition/Definitions/ZoneTopologyTemplate.cs (append)
namespace Hollowcore.Expedition.Definitions
{
    public partial class ZoneTopologyTemplate
    {
        public BlobAssetReference<Blob.ZoneTopologyBlob> BakeToBlob(BlobBuilder builder)
        {
            ref var root = ref builder.ConstructRoot<Blob.ZoneTopologyBlob>();
            builder.AllocateString(ref root.VariantName, VariantName ?? "");
            root.ZoneCount = ZoneCount;

            var slots = builder.Allocate(ref root.ZoneSlots, ZoneSlots.Count);
            for (int i = 0; i < ZoneSlots.Count; i++)
            {
                slots[i].Type = (byte)ZoneSlots[i].Type;
                slots[i].RelativePositionX = ZoneSlots[i].RelativePosition.x;
                slots[i].RelativePositionY = ZoneSlots[i].RelativePosition.y;
                slots[i].Required = ZoneSlots[i].Required;
            }

            var conns = builder.Allocate(ref root.Connections, Connections.Count);
            for (int i = 0; i < Connections.Count; i++)
            {
                conns[i].ZoneIndexA = Connections[i].ZoneIndexA;
                conns[i].ZoneIndexB = Connections[i].ZoneIndexB;
                conns[i].Required = Connections[i].Required;
                conns[i].OneWay = Connections[i].OneWay;
            }

            var entries = builder.Allocate(ref root.EntryPointZoneIndices, EntryPointZoneIndices.Count);
            for (int i = 0; i < EntryPointZoneIndices.Count; i++)
                entries[i] = EntryPointZoneIndices[i];

            return builder.CreateBlobAssetReference<Blob.ZoneTopologyBlob>(Allocator.Persistent);
        }
    }
}
```

---

## Validation

```csharp
// File: Assets/Scripts/Expedition/Definitions/ZoneTopologyTemplate.cs (OnValidate)
namespace Hollowcore.Expedition.Definitions
{
    public partial class ZoneTopologyTemplate
    {
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Zone count consistency
            if (ZoneSlots.Count != ZoneCount)
                Debug.LogWarning($"[ZoneTopology] ZoneCount={ZoneCount} but ZoneSlots.Count={ZoneSlots.Count}", this);

            // Connection indices must be valid
            foreach (var c in Connections)
            {
                if (c.ZoneIndexA < 0 || c.ZoneIndexA >= ZoneSlots.Count ||
                    c.ZoneIndexB < 0 || c.ZoneIndexB >= ZoneSlots.Count)
                    Debug.LogError($"[ZoneTopology] Connection ({c.ZoneIndexA},{c.ZoneIndexB}) out of range", this);
                if (c.ZoneIndexA == c.ZoneIndexB)
                    Debug.LogError($"[ZoneTopology] Self-loop at zone {c.ZoneIndexA}", this);
            }

            // Entry points must reference valid zone indices
            foreach (int ep in EntryPointZoneIndices)
                if (ep < 0 || ep >= ZoneSlots.Count)
                    Debug.LogError($"[ZoneTopology] Entry point index {ep} out of range", this);

            // At least one entry point required
            if (EntryPointZoneIndices.Count == 0)
                Debug.LogError("[ZoneTopology] No entry points defined", this);

            // Connectivity: all zones reachable from any entry point (using required edges only)
            ValidateInternalConnectivity();

            // Exactly one Boss zone type per topology
            int bossCount = ZoneSlots.FindAll(s => s.Type == ZoneType.Boss).Count;
            if (bossCount == 0) Debug.LogWarning("[ZoneTopology] No Boss zone in topology", this);
            if (bossCount > 1) Debug.LogWarning($"[ZoneTopology] {bossCount} Boss zones — expected 0-1", this);
        }

        private void ValidateInternalConnectivity()
        {
            if (ZoneSlots.Count == 0) return;
            var adj = new List<int>[ZoneSlots.Count];
            for (int i = 0; i < ZoneSlots.Count; i++) adj[i] = new List<int>();
            foreach (var c in Connections)
            {
                if (c.ZoneIndexA < ZoneSlots.Count && c.ZoneIndexB < ZoneSlots.Count)
                {
                    adj[c.ZoneIndexA].Add(c.ZoneIndexB);
                    if (!c.OneWay) adj[c.ZoneIndexB].Add(c.ZoneIndexA);
                }
            }
            if (EntryPointZoneIndices.Count == 0) return;
            var visited = new bool[ZoneSlots.Count];
            var queue = new Queue<int>();
            queue.Enqueue(EntryPointZoneIndices[0]);
            visited[EntryPointZoneIndices[0]] = true;
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                foreach (int n in adj[cur])
                    if (!visited[n]) { visited[n] = true; queue.Enqueue(n); }
            }
            for (int i = 0; i < ZoneSlots.Count; i++)
                if (!visited[i] && ZoneSlots[i].Required)
                    Debug.LogError($"[ZoneTopology] Required zone {i} unreachable from entry point", this);
        }
#endif
    }
}
```

---

## Editor Tooling

```csharp
// File: Assets/Editor/ExpeditionWorkstation/ZoneTopologyEditorModule.cs
// IExpeditionWorkstationModule — "Zone Topology" tab.
//
// Visual editor for ZoneTopologyTemplate assets:
//   - Canvas: ZoneSlotTemplates rendered as colored rectangles (color = ZoneType)
//     - Combat=red, Elite=orange, Boss=purple, Shop=green, Event=blue, Rest=cyan, Support=grey
//   - Connections: lines between zone rectangles (solid=required, dashed=prunable, arrow=one-way)
//   - Drag zones to set RelativePosition
//   - Click zone: property panel shows Type, Required toggle
//   - Click connection: shows Required, OneWay toggles
//   - Right-click: "Add Zone", "Add Connection", "Set Entry Point"
//   - Entry points marked with arrow icon on zone
//   - "Auto-Layout" button: spring-force layout algorithm
//   - "Validate" button: runs OnValidate + highlights disconnected zones in red
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Expedition/Components/ZoneRuntimeConfig.cs
using Unity.Entities;

namespace Hollowcore.Expedition
{
    /// <summary>
    /// Runtime-tunable zone generation parameters.
    /// </summary>
    public struct ZoneRuntimeConfig : IComponentData
    {
        /// <summary>Probability of pruning non-required zone connections (0.0-1.0). Default 0.2.</summary>
        public float ConnectionPruneProbability;

        /// <summary>AABB expansion factor for zone bounds (margin for ZoneTrackingSystem). Default 1.0.</summary>
        public float BoundsExpansionFactor;

        /// <summary>If true, reveal all zones on minimap immediately (debug). Default false.</summary>
        public bool DebugRevealAllZones;

        public static ZoneRuntimeConfig Default => new()
        {
            ConnectionPruneProbability = 0.2f,
            BoundsExpansionFactor = 1.0f,
            DebugRevealAllZones = false,
        };
    }
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Expedition/Debug/ZoneDebugOverlay.cs
// In-game minimap overlay for zone structure within current district:
//
//   - Zones rendered as rectangles on 2D minimap overlay (top-right corner)
//   - Zone color = FrontZoneState: green(Safe), yellow(Contested), orange(Hostile), red(Overrun)
//   - Zone border: solid if visited, dashed if unvisited
//   - Connections: thin lines between zone centers on minimap
//   - Player dot: white circle at current position within zone
//   - Zone labels: type icon + clear/visited status
//   - Highlight current zone (pulsing border)
//   - Entry points marked with gate icon
//   - Toggle: M key or minimap button
//   - Expanded view: press Tab to enlarge minimap to full-screen zone map
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/ExpeditionWorkstation/ZoneSimulationModule.cs
// IExpeditionWorkstationModule — "Zone Simulation" tab.
//
// "Generate 100 District Variants" per topology template:
//   For each topology: generate 100 zone layouts with different seeds
//   Verify per layout:
//     - All required zones present
//     - All zones reachable from every entry point
//     - Zone count in [MinZones, MaxZones]
//     - At least 2 distinct paths between entry points and boss zone (non-linear)
//   Aggregate statistics:
//     - Average zone count, average connections per zone
//     - Pruning rate: % of prunable connections actually pruned
//     - Path diversity: number of unique shortest paths entry→boss
//     - Zone type distribution histogram
//
// "Traversal Simulation":
//   Simulate a player visiting zones in BFS order from entry point
//   Record: zones visited before boss accessible, average exploration %,
//   time estimate based on zone count * avg zone time
```
