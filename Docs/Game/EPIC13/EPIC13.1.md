# EPIC 13.1: District Definition Template

**Status**: Planning
**Epic**: EPIC 13 — District Content Pipeline
**Priority**: Critical — Every district is built from this template
**Dependencies**: Framework: Roguelite/Definitions/ZoneDefinitionSO, Quest/QuestDefinitionSO, Loot/LootTableSO, AI/EncounterPoolSO

---

## Overview

The universal data structure every district follows. A single `DistrictDefinitionSO` is the master ScriptableObject that references all content for one district: identity, zone graph, factions, Front behavior, goals, boss, POIs, echo flavor, reanimation rules, currency, and reward focus. This is the spine of the content pipeline — all other 13.x sub-epics hang off this definition.

---

## Component Definitions

### DistrictId Enum

```csharp
// File: Assets/Scripts/District/Definitions/DistrictEnums.cs
namespace Hollowcore.District
{
    public enum DistrictId : byte
    {
        Necrospire = 1,
        Wetmarket = 2,
        GlitchQuarter = 3,
        ChromeCathedral = 4,
        TheShoals = 5,
        TheBurn = 6,
        Mirrortown = 7,
        TheLattice = 8,
        SynapseRow = 9,
        Quarantine = 10,
        OldGrowth = 11,
        TheAuction = 12,
        Deadwave = 13,
        TheNursery = 14,
        SkyfallRuins = 15
    }

    /// <summary>
    /// Primary reward focus for a district's loot tables.
    /// Renamed from RewardCategory to avoid collision with
    /// Hollowcore.Economy.RewardCategoryType (EPIC 10).
    /// </summary>
    public enum DistrictRewardFocus : byte
    {
        Limbs = 0,
        Currency = 1,
        Weapons = 2,
        Chassis = 3,
        Recipes = 4,
        Augments = 5,
        Intel = 6
    }
}
```

### ZoneGraphEntry

```csharp
// File: Assets/Scripts/District/Definitions/DistrictEnums.cs (continued)
using System;

namespace Hollowcore.District
{
    /// <summary>
    /// One node in the district zone graph. Serialized inline in DistrictDefinitionSO.
    /// Represents a single zone and its connections to other zones by index.
    /// </summary>
    [Serializable]
    public struct ZoneGraphEntry
    {
        public int ZoneIndex;
        public DIG.Roguelite.Zones.ZoneType Type;
        public DIG.Roguelite.Zones.ZoneDefinitionSO ZoneDefinition;

        [UnityEngine.Tooltip("Indices of zones this zone connects to.")]
        public int[] ConnectedZoneIndices;

        [UnityEngine.Tooltip("Faction that primarily controls this zone (index into DistrictDefinitionSO.Factions).")]
        public int PrimaryFactionIndex;
    }
}
```

### TopologyVariant

```csharp
// File: Assets/Scripts/District/Definitions/DistrictEnums.cs (continued)
namespace Hollowcore.District
{
    /// <summary>
    /// One topology variant for a district. Each district has 2-3 variants
    /// selected by run seed to prevent sameness across runs (GDD S17.3).
    /// </summary>
    [Serializable]
    public struct TopologyVariant
    {
        public string VariantName;

        [UnityEngine.Tooltip("Zone graph for this variant. Overrides the default graph.")]
        public ZoneGraphEntry[] ZoneGraph;

        [UnityEngine.Tooltip("Entry point zone indices (where the player can enter the district).")]
        public int[] EntryPointIndices;

        [UnityEngine.Tooltip("Prefab or scene references for zone geometry (IZoneProvider uses these).")]
        public UnityEngine.GameObject[] ZonePrefabs;
    }
}
```

### DistrictDefinitionSO

```csharp
// File: Assets/Scripts/District/Definitions/DistrictDefinitionSO.cs
using UnityEngine;

namespace Hollowcore.District
{
    /// <summary>
    /// Master definition for a single district. Every district in the game has one of these.
    /// References all content: zones, factions, front, goals, boss, POIs, thematic elements.
    /// </summary>
    [CreateAssetMenu(fileName = "DistrictDefinition", menuName = "Hollowcore/District/District Definition")]
    public class DistrictDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public DistrictId Id;
        public string DisplayName;
        [TextArea(3, 6)] public string Description;
        public Sprite Icon;
        public string ArtTheme;

        [Header("Topology")]
        [Tooltip("Default zone graph (8-15 interconnected zones).")]
        public ZoneGraphEntry[] DefaultZoneGraph;
        [Tooltip("2-3 topology variants selected by seed for anti-sameness.")]
        public TopologyVariant[] TopologyVariants;
        [Tooltip("Default entry point zone indices.")]
        public int[] DefaultEntryPoints;

        [Header("Threats")]
        [Tooltip("Exactly 4 factions per district. Zone-based assignment.")]
        public FactionDefinitionSO[] Factions = new FactionDefinitionSO[4];

        [Header("Front")]
        [Tooltip("How the Front spreads and transforms this district.")]
        public FrontDefinitionSO FrontDefinition;

        [Header("Goals")]
        [Tooltip("Side goals (6-8) and main chain quests. Use QuestCategory.Main for the chain.")]
        public DIG.Quest.QuestDefinitionSO[] Goals;

        [Header("Boss")]
        [Tooltip("Boss definition for the district's final encounter.")]
        public ScriptableObject BossDefinition;

        [Header("POIs")]
        [Tooltip("5-6 named landmark locations with fixed positions per variant.")]
        public LandmarkPOIDefinition[] LandmarkPOIs;
        [Tooltip("Micro-POI pool for procedural environmental details.")]
        public MicroPOIPoolSO MicroPOIPool;

        [Header("Thematic")]
        [Tooltip("How echoes mutate in this district (identity drift, rotting memories, etc).")]
        public ScriptableObject EchoFlavor;
        [Tooltip("How dead bodies are used against the player in this district.")]
        public ScriptableObject ReanimationDefinition;

        [Header("Economy")]
        [Tooltip("District-specific currency variant.")]
        public ScriptableObject DistrictCurrency;
        [Tooltip("Primary reward focus for this district's loot tables.")]
        public DistrictRewardFocus PrimaryRewardFocus;

        /// <summary>Returns the zone graph for a given seed, selecting from topology variants.</summary>
        public ZoneGraphEntry[] GetZoneGraph(uint seed)
        {
            if (TopologyVariants == null || TopologyVariants.Length == 0)
                return DefaultZoneGraph;
            int variantIndex = (int)(seed % (uint)TopologyVariants.Length);
            return TopologyVariants[variantIndex].ZoneGraph ?? DefaultZoneGraph;
        }

        /// <summary>Returns entry points for a given seed.</summary>
        public int[] GetEntryPoints(uint seed)
        {
            if (TopologyVariants == null || TopologyVariants.Length == 0)
                return DefaultEntryPoints;
            int variantIndex = (int)(seed % (uint)TopologyVariants.Length);
            return TopologyVariants[variantIndex].EntryPointIndices ?? DefaultEntryPoints;
        }
    }
}
```

### DistrictState (IComponentData)

```csharp
// File: Assets/Scripts/District/Components/DistrictComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.District
{
    /// <summary>
    /// Runtime state for the currently active district. Stored on the RunState entity.
    /// Extends the framework ZoneState with district-level tracking.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct DistrictState : IComponentData
    {
        [GhostField] public byte DistrictId;
        [GhostField] public byte TopologyVariantIndex;
        [GhostField] public byte FrontPhase;
        [GhostField] public byte GoalsCompleted;
        [GhostField] public byte GoalsTotal;
        [GhostField] public byte MainChainStep;
        [GhostField] public bool BossUnlocked;
        [GhostField] public bool BossDefeated;
    }

    /// <summary>
    /// Link from RunState entity to a child entity holding the district's
    /// zone graph as a DynamicBuffer. Avoids bloating the RunState archetype.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct DistrictGraphLink : IComponentData
    {
        [GhostField] public Entity GraphEntity;
    }

    /// <summary>
    /// One entry in the runtime zone graph buffer. Lives on the GraphEntity.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ZoneGraphBufferEntry : IBufferElementData
    {
        public int ZoneIndex;
        public byte ZoneType;           // ZoneType cast
        public byte PrimaryFactionIndex;
        public int ZoneDefinitionHash;  // Resolved at district load
        public byte ConnectionMask;     // Bitmask of connected zone indices (up to 8)
    }
}
```

---

## Systems

### DistrictLoadSystem

```csharp
// File: Assets/Scripts/District/Systems/DistrictLoadSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: ZoneTransitionSystem (framework)
//
// Reads: RunState, DistrictDefinitionSO (resolved via blob or registry)
// Writes: DistrictState, DistrictGraphLink, ZoneGraphBufferEntry
//
// On run start or district transition:
//   1. Resolve DistrictDefinitionSO from RunState.CurrentDistrictId
//   2. Select topology variant from seed: variant = seed % TopologyVariants.Length
//   3. Populate DistrictState on RunState entity
//   4. Create graph child entity with ZoneGraphBufferEntry buffer
//   5. Set DistrictGraphLink on RunState entity
//   6. Initialize zone definitions in the framework ZoneSequenceResolverSystem
```

### DistrictGoalTrackingSystem

```csharp
// File: Assets/Scripts/District/Systems/DistrictGoalTrackingSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: QuestCompletionSystem (framework Quest/)
//
// Reads: QuestState changes from Quest/ framework, DistrictState
// Writes: DistrictState.GoalsCompleted, DistrictState.MainChainStep, DistrictState.BossUnlocked
//
// Each frame:
//   1. Check all district-associated quests for completion
//   2. Increment GoalsCompleted for each newly completed side goal
//   3. Advance MainChainStep for each completed main chain objective
//   4. When main chain final step completes, set BossUnlocked = true
```

---

## Setup Guide

1. **Create `Assets/Scripts/District/` folder** with subfolders: Definitions/, Components/, Systems/, Authoring/
2. **Create assembly definition** `Hollowcore.District.asmdef` referencing `DIG.Shared`, `DIG.Roguelite`, `DIG.Quest`, `Unity.Entities`, `Unity.NetCode`, `Unity.Collections`, `Unity.Burst`
3. **Create `Assets/Data/Districts/` folder** for DistrictDefinitionSO assets
4. Create one DistrictDefinitionSO per district (start with Necrospire for vertical slice)
5. Wire FactionDefinitionSOs, QuestDefinitionSOs, and FrontDefinitionSO into each DistrictDefinitionSO
6. DistrictState is added to RunState entity at district load time — no prefab changes needed

---

## Verification

- [ ] DistrictDefinitionSO created with all required fields for Necrospire
- [ ] Zone graph has 8-15 entries with valid connections
- [ ] 4 FactionDefinitionSO slots populated
- [ ] TopologyVariants array has 2-3 entries
- [ ] GetZoneGraph(seed) returns different variants for different seeds
- [ ] DistrictState correctly populated on RunState entity at district load
- [ ] DistrictGraphLink references valid child entity with ZoneGraphBufferEntry buffer
- [ ] ZoneType enum values match framework ZoneType (Combat, Elite, Boss, Shop, etc.)
- [ ] DistrictGoalTrackingSystem increments GoalsCompleted on quest completion
- [ ] BossUnlocked transitions to true when main chain completes

---

## BlobAsset Pipeline

```csharp
// File: Assets/Scripts/District/Blobs/DistrictBlobs.cs
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Hollowcore.District
{
    /// <summary>
    /// Baked district definition data. Read every zone transition to resolve
    /// factions, goals, Front config, and topology. CRITICAL hot path — must
    /// be in blob form, not managed SO references.
    /// </summary>
    public struct DistrictBlob
    {
        public byte DistrictId;
        public BlobString DisplayName;
        public int FactionCount;
        public int GoalCount;
        public int LandmarkCount;

        /// <summary>Zone graph entries for default topology.</summary>
        public BlobArray<ZoneGraphBlobEntry> DefaultZoneGraph;

        /// <summary>Per-topology-variant zone graphs.</summary>
        public BlobArray<TopologyVariantBlob> TopologyVariants;

        /// <summary>Faction IDs (indices into FactionBlob registry).</summary>
        public BlobArray<ushort> FactionIds;

        /// <summary>Goal quest IDs for cross-reference.</summary>
        public BlobArray<int> GoalQuestIds;

        /// <summary>Primary reward focus for loot resolution.</summary>
        public byte PrimaryRewardFocus; // DistrictRewardFocus cast
    }

    public struct ZoneGraphBlobEntry
    {
        public int ZoneIndex;
        public byte ZoneType;
        public byte PrimaryFactionIndex;
        public byte ConnectionMask; // Bitmask of connected indices (up to 8)
    }

    public struct TopologyVariantBlob
    {
        public BlobArray<ZoneGraphBlobEntry> ZoneGraph;
        public BlobArray<int> EntryPointIndices;
        public int ZoneCount;
    }

    /// <summary>
    /// Baked faction data. Loaded at district entry, read by spawn director
    /// and faction encounter resolver.
    /// </summary>
    public struct FactionBlob
    {
        public ushort FactionId;
        public BlobString DisplayName;
        public byte BaseAggression;
        public float AlarmRadius;
        public byte PatrolPattern;

        /// <summary>Enemy roster entries.</summary>
        public BlobArray<FactionEnemyBlobEntry> EnemyRoster;

        /// <summary>Preferred zone types (empty = all).</summary>
        public BlobArray<byte> PreferredZoneTypes;
    }

    public struct FactionEnemyBlobEntry
    {
        public int PrefabHash;      // Stable hash for prefab resolution
        public byte Tier;            // EnemyTier cast
        public float SpawnWeight;
        public int SpawnCost;
        public bool HasRippableLimbs;
    }
}

// Baker: DistrictDefinitionSO.BakeToBlob() called by DistrictRegistryAuthoring.Baker.
// FactionDefinitionSO.BakeToBlob() called per faction reference.
// AnimationCurve fields (e.g., FrontDefinitionSO spread curves) must use:
//   BlobArray<float2> where x=time, y=value, sampled at 32 points.
// Blob lifetime: allocated at subscene bake, lives for app duration.
```

---

## Validation

```csharp
// File: Assets/Editor/District/DistrictCompletenessValidator.cs
// CRITICAL — the most complex validator in the project.
// Runs: build-time via IPreprocessBuildWithReport, and on-demand from District Workstation.
//
// For EVERY DistrictDefinitionSO asset found via AssetDatabase.FindAssets:
//
//   Identity:
//     [ERROR] DisplayName is null or empty
//     [ERROR] DistrictId is default (0)
//     [WARNING] Description is empty
//     [WARNING] Icon is null
//
//   Front:
//     [ERROR] FrontDefinition is null — every district MUST have a Front
//
//   Factions:
//     [ERROR] Factions array length != 4
//     [ERROR] Any Factions[i] is null
//     [ERROR] Any FactionDefinitionSO.EnemyRoster is empty (0 enemies)
//     [WARNING] Any faction has < 3 enemy types
//     [ERROR] FactionId collision between two factions in same district
//
//   Goals:
//     [ERROR] Goals array length < 2 (need at least 1 main chain + 1 side goal)
//     [ERROR] No goal marked IsMainChain in associated DistrictGoalExtensionSO
//     [WARNING] Fewer than 6 side goals (GDD specifies 6-8)
//
//   Zone Graph:
//     [ERROR] DefaultZoneGraph is null or empty
//     [ERROR] Zone graph has disconnected nodes (BFS from entry point must reach all)
//     [ERROR] No entry points defined (DefaultEntryPoints empty)
//     [ERROR] Any ConnectedZoneIndices reference out-of-range index
//     [WARNING] Zone count < 8 or > 15
//     [ERROR] Boss zone (ZoneType.Boss) missing
//
//   Topology Variants:
//     [ERROR] TopologyVariants is null or has 0 entries
//     [WARNING] Fewer than 2 variants (GDD §17.3 requires 2-3)
//     [ERROR] Any variant has null or empty ZoneGraph
//     [ERROR] Any variant zone count is 0
//     [ERROR] Any variant has disconnected graph
//
//   Boss:
//     [ERROR] BossDefinition is null
//
//   POIs:
//     [WARNING] LandmarkPOIs count < 5
//     [WARNING] MicroPOIPool is null
//
// Output: DistrictCompletenessReport { DistrictId, Errors[], Warnings[], Score(0-100%) }
```

---

## Editor Tooling

```csharp
// File: Assets/Editor/DistrictWorkstation/DistrictWorkstationWindow.cs
// EditorWindow: "Hollowcore/District Workstation"
// Follows DIG workstation pattern: sidebar tabs, IWorkstationModule interface.
//
// === Modules (sidebar tabs) ===
//
// 1. Zone Graph Editor (IWorkstationModule)
//    - Node-graph UI: each zone is a draggable node with ZoneType color coding
//    - Click-drag between nodes to create/remove connections
//    - Per-node inspector: ZoneType dropdown, PrimaryFactionIndex dropdown,
//      ZoneDefinitionSO field
//    - BFS connectivity check: disconnected nodes highlighted in red
//    - Entry point toggle: click node border to mark as entry point (green border)
//    - Auto-layout button: spring-based layout for clean graph visualization
//
// 2. Content Checklist Panel (IWorkstationModule)
//    - Shows required vs configured content for selected DistrictDefinitionSO:
//      Factions:  [✓ 4/4]  or  [✗ 2/4]
//      Goals:     [✓ 8]    or  [✗ 3 — need 6+]
//      Boss:      [✓]      or  [✗ missing]
//      Front:     [✓]      or  [✗ missing]
//      POIs:      [✓ 5/5]  or  [✗ 2/5]
//      Variants:  [✓ 3]    or  [✗ 1 — need 2+]
//    - Each row clickable: jumps to the relevant field in inspector
//    - Overall completeness score percentage in large text at top
//
// 3. Topology Variant Previewer (IWorkstationModule)
//    - Seed input field + "Generate" button
//    - Renders the zone graph that would result from GetZoneGraph(seed)
//    - Side-by-side comparison: up to 3 variants rendered simultaneously
//    - Highlights differences between variants (added/removed connections, zones)
//
// 4. Faction Distribution (IWorkstationModule)
//    - Pie chart: zones colored by PrimaryFactionIndex
//    - Per-faction zone list with zone names
//    - Warning if any faction has 0 zones assigned
//    - Enemy count per faction (from EnemyRoster.Length)
//
// 5. District Completeness Score (shown in header bar)
//    - Calls DistrictCompletenessValidator for the selected SO
//    - Shows: "Necrospire: 87% complete — 2 errors, 1 warning"
//    - Color-coded: green (90%+), yellow (70-89%), red (<70%)
```

---

## Live Tuning

| Parameter | Source | Effect |
|-----------|--------|--------|
| Faction spawn weights | FactionDefinitionSO.EnemyRoster[i].SpawnWeight | Adjusts enemy type frequency per faction |
| Goal difficulty multipliers | DistrictGoalExtensionSO.CounterplayValue | Scales Front counterplay effect magnitude |
| Zone hazard intensity | BurnConfig/LatticeConfig/NecrospireConfig fields | Per-district environmental hazard rates |
| Front spread rate | FrontDefinitionSO spread curves | Speed of Front phase progression |
| Front phase thresholds | FrontDefinitionSO phase timing | When phases transition |

All fields editable at runtime via inspector on singleton entities. Changes persist until subscene reload.

---

## Debug Visualization

```csharp
// File: Assets/Scripts/District/Debug/DistrictDebugOverlay.cs
// Managed system, PresentationSystemGroup, gated behind DEVELOPMENT_BUILD
//
// Zone Graph Overlay (toggle: Ctrl+F9):
//   - Renders zone boundaries as colored wireframe volumes in world space
//   - Visited zones: solid fill (low alpha), Unvisited: wireframe only
//   - Zone color = faction color (from FactionDefinitionSO.FactionColor)
//   - Connection lines between zone centroids (yellow = open gate, red = locked)
//   - Zone labels floating above centroid: "Zone 4: Data Nexus [Elite] [Specters]"
//
// Faction Territory Overlay (toggle: Ctrl+F10):
//   - World-space boundary lines between faction territories
//   - Color-coded per faction, semi-transparent fill
//   - Faction icon at territory centroid
//
// Goal Progress Indicators (toggle: Ctrl+F11):
//   - Floating markers at objective locations showing progress
//   - "Sever the Grief-Link: 2/3 Grief Engines destroyed"
//   - Main chain step highlighted with distinct icon
//   - Completed goals: green checkmark, Skipped: red X
```

---

## Simulation & Testing

```csharp
// File: Assets/Tests/District/DistrictGenerationTest.cs
// [Test] DistrictVariety_100Seeds_ZoneConnectivity
//   For each of the 3 vertical slice districts:
//     1. Generate 100 districts from 100 different seeds
//     2. For each: verify zone graph is fully connected (BFS from entry)
//     3. Verify topology variant index = seed % variantCount
//     4. Collect variant distribution: must use all variants at least 20 times
//        (within statistical bounds for uniform distribution)
//
// [Test] DistrictVariety_FactionDistribution
//   For each generated district:
//     1. Count zones per faction
//     2. Verify no faction has 0 zones (all 4 must be represented)
//     3. Verify faction distribution roughly matches weights
//        (no faction has > 50% of zones)
//
// [Test] DistrictVariety_GoalCount
//   For each district definition:
//     1. Verify Goals.Length >= 7 (6 side + 1 main chain minimum)
//     2. Verify exactly 1 goal has IsMainChain == true
//     3. Verify all side goals have valid ObjectiveZoneIndices
//        within the zone graph range
//
// [Test] DistrictVariety_CrossSeedAnalysis
//   Generate 100 Necrospire districts:
//     1. Compute similarity score between consecutive seeds
//        (% of identical zone connections)
//     2. Assert average similarity < 70% (meaningful variety)
//     3. Log min/max/avg similarity for design review
```
