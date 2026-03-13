# EPIC 4.1: Expedition Graph Data Model

**Status**: Planning
**Epic**: EPIC 4 — District Graph & Expedition Structure
**Priority**: Critical — Foundation for all expedition navigation
**Dependencies**: Framework: Roguelite/ (RunSeedUtility, RunLifecycleSystem), Unity.Entities, Unity.NetCode

---

## Overview

The expedition graph is the structural backbone of a run. It replaces the framework's linear ZoneSequenceSO with a connected graph of 5-7 district nodes linked by gate edges. An ExpeditionGraphSO template defines the possible topologies; at expedition start, the runtime generates an ExpeditionGraphState from the template plus the expedition seed. Each node carries per-district runtime state (FrontState, completion, visit history), and each edge tracks its unlock condition. A singleton ExpeditionGraphEntity holds the entire runtime graph in ECS.

---

## Component Definitions

### ExpeditionGraphSO (ScriptableObject)

```csharp
// File: Assets/Scripts/Expedition/Definitions/ExpeditionGraphSO.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hollowcore.Expedition.Definitions
{
    [CreateAssetMenu(fileName = "NewExpeditionGraph", menuName = "Hollowcore/Expedition/Graph Template")]
    public class ExpeditionGraphSO : ScriptableObject
    {
        [Header("Graph Structure")]
        public List<DistrictSlot> Slots = new();
        public List<GateConnectionTemplate> Gates = new();

        [Header("Generation Rules")]
        [Tooltip("Min/max districts to activate from template")]
        public int MinActiveNodes = 5;
        public int MaxActiveNodes = 7;

        [Tooltip("Index of the starting slot (always activated)")]
        public int StartSlotIndex;

        [Tooltip("Index of the boss slot (unlocked after clearing threshold)")]
        public int BossSlotIndex;

        [Tooltip("Districts that must be cleared before boss gate opens")]
        public int BossUnlockThreshold = 5;
    }

    [Serializable]
    public class DistrictSlot
    {
        public string SlotName;
        [Tooltip("Pool of DistrictDefinitionSOs that can fill this slot — seed selects one")]
        public List<DistrictDefinitionSO> CandidateDistricts;
        [Tooltip("Position in graph for layout visualization")]
        public Vector2 GraphPosition;
        [Tooltip("Minimum distance from start (in edges) — 0 = can be adjacent")]
        public int MinDepth;
        public int MaxDepth;
    }

    [Serializable]
    public class GateConnectionTemplate
    {
        public int SlotIndexA;
        public int SlotIndexB;
        [Tooltip("If true, this gate always exists. If false, seed may prune it")]
        public bool Guaranteed;
        [Tooltip("Condition to unlock this gate at runtime")]
        public GateUnlockCondition UnlockCondition;
    }

    public enum GateUnlockCondition
    {
        AlwaysOpen,
        DiscoverBothSides,
        DefeatElite,
        CompleteObjective,
        BossThreshold
    }
}
```

### DistrictDefinitionSO (ScriptableObject)

```csharp
// File: Assets/Scripts/Expedition/Definitions/DistrictDefinitionSO.cs
using UnityEngine;

namespace Hollowcore.Expedition.Definitions
{
    [CreateAssetMenu(fileName = "NewDistrict", menuName = "Hollowcore/Expedition/District Definition")]
    public class DistrictDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int DistrictId;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite MapIcon;

        [Header("Scene")]
        [Tooltip("Addressable scene key for this district")]
        public string SceneKey;

        [Header("Generation")]
        [Tooltip("Zone count range within this district")]
        public int MinZones = 8;
        public int MaxZones = 15;
        [Tooltip("Number of topology variants the seed can select")]
        public int TopologyVariants = 3;

        [Header("Front")]
        [Tooltip("Front definition driving this district's pressure system")]
        public FrontDefinitionSO FrontDefinition;

        [Header("Content")]
        [Tooltip("Factions that spawn in this district")]
        public FactionId[] ThreatFactions;
        [Tooltip("Target run duration for this district in minutes")]
        public float TargetRunMinutes = 25f;
    }

    public enum FactionId : byte
    {
        None = 0,
        Scavengers = 1,
        Choir = 2,
        Bloom = 3,
        Tide = 4,
        Wardens = 5,
        Infected = 6,
        Rogue_AI = 7,
        Gangs = 8,
        Wildlife = 9,
    }
}
```

### GraphNodeState (IComponentData)

```csharp
// File: Assets/Scripts/Expedition/Components/ExpeditionGraphComponents.cs
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Expedition
{
    /// <summary>
    /// Per-node runtime state within the expedition graph.
    /// Stored as a buffer element on the ExpeditionGraphEntity singleton.
    /// </summary>
    public struct GraphNodeState : IBufferElementData
    {
        /// <summary>Baked ID from DistrictDefinitionSO.DistrictId.</summary>
        public int DistrictDefinitionId;

        /// <summary>Slot index in the ExpeditionGraphSO template.</summary>
        public int SlotIndex;

        /// <summary>Current Front phase (mirrors FrontState on district entity).</summary>
        public byte FrontPhase;

        /// <summary>Completion bitmask: bit 0 = main chain, bits 1-7 = side goals.</summary>
        public byte CompletionMask;

        /// <summary>Number of times the player has entered this district.</summary>
        public short VisitCount;

        /// <summary>Total seconds spent in this district.</summary>
        public float TimeSpentSeconds;

        /// <summary>Number of player deaths in this district.</summary>
        public short DeathCount;

        /// <summary>True if this node is active in the current expedition graph.</summary>
        public bool IsActive;

        /// <summary>Display name for UI.</summary>
        public FixedString64Bytes DisplayName;

        public bool IsMainChainComplete => (CompletionMask & 1) != 0;
    }
}
```

### GraphEdgeState (IBufferElementData)

```csharp
// File: Assets/Scripts/Expedition/Components/ExpeditionGraphComponents.cs (continued)
namespace Hollowcore.Expedition
{
    public enum GateState : byte
    {
        Locked = 0,
        Discovered = 1,
        Open = 2,
        Collapsed = 3,   // Permanently closed (Front overrun)
    }

    /// <summary>
    /// Per-edge runtime state. Stored as a buffer on ExpeditionGraphEntity.
    /// </summary>
    public struct GraphEdgeState : IBufferElementData
    {
        /// <summary>Index into GraphNodeState buffer for node A.</summary>
        public int NodeIndexA;

        /// <summary>Index into GraphNodeState buffer for node B.</summary>
        public int NodeIndexB;

        /// <summary>Current gate state.</summary>
        public GateState State;

        /// <summary>Unlock condition from template.</summary>
        public GateUnlockCondition UnlockCondition;

        /// <summary>True if the player has physically seen this gate.</summary>
        public bool PlayerDiscovered;
    }
}
```

### ExpeditionGraphState (IComponentData — Singleton)

```csharp
// File: Assets/Scripts/Expedition/Components/ExpeditionGraphComponents.cs (continued)
using Unity.NetCode;

namespace Hollowcore.Expedition
{
    /// <summary>
    /// Singleton on the ExpeditionGraphEntity. Holds expedition-wide metadata.
    /// The entity also carries DynamicBuffer<GraphNodeState> and DynamicBuffer<GraphEdgeState>.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.Server)]
    public struct ExpeditionGraphState : IComponentData
    {
        /// <summary>Master expedition seed (derives all sub-seeds).</summary>
        public uint ExpeditionSeed;

        /// <summary>Index into GraphNodeState buffer for the player's current district.</summary>
        public int CurrentNodeIndex;

        /// <summary>Total districts cleared (main chain complete).</summary>
        public int DistrictsCleared;

        /// <summary>Template ID (baked hash of ExpeditionGraphSO).</summary>
        public int TemplateId;

        /// <summary>Whether the boss gate has been unlocked.</summary>
        public bool BossGateUnlocked;

        /// <summary>Total expedition elapsed time in seconds.</summary>
        public float ElapsedTime;
    }
}
```

---

## Systems

### ExpeditionGraphGenerationSystem

```csharp
// File: Assets/Scripts/Expedition/Systems/ExpeditionGraphGenerationSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: InitializationSystemGroup
//
// Runs once when a new expedition starts (triggered by RunLifecycleSystem phase change).
//
// 1. Read ExpeditionGraphSO template from ExpeditionConfig singleton
// 2. Create ExpeditionGraphEntity with ExpeditionGraphState + buffers
// 3. Seed RNG from ExpeditionSeed via RunSeedUtility.DeriveHash("graph")
// 4. Activate MinActiveNodes..MaxActiveNodes slots:
//    a. StartSlotIndex always active
//    b. BossSlotIndex always active
//    c. Remaining slots activated by seed (respect MinDepth/MaxDepth constraints)
// 5. For each active slot, seed-select one DistrictDefinitionSO from CandidateDistricts
//    → populate GraphNodeState buffer
// 6. For each GateConnectionTemplate connecting two active nodes:
//    a. If Guaranteed, always include
//    b. Otherwise, seed-probability include (ensures graph stays connected)
//    → populate GraphEdgeState buffer (all gates start Locked except start node's edges → Discovered)
// 7. Set CurrentNodeIndex = start node, DistrictsCleared = 0
```

### ExpeditionGraphUpdateSystem

```csharp
// File: Assets/Scripts/Expedition/Systems/ExpeditionGraphUpdateSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Per-frame upkeep of graph state.
//
// 1. Increment ElapsedTime
// 2. Sync CurrentNodeIndex's GraphNodeState.FrontPhase from the active district's FrontState
// 3. Check gate unlock conditions:
//    For each Locked or Discovered edge:
//      - AlwaysOpen → Open
//      - DiscoverBothSides → Open if both adjacent nodes visited
//      - DefeatElite → Open if relevant kill flag set
//      - CompleteObjective → Open if node CompletionMask has required bit
//      - BossThreshold → Open if DistrictsCleared >= threshold
// 4. Check gate collapse:
//    For each Open edge:
//      - If either adjacent node's FrontPhase == Phase4_Overrun → Collapsed
// 5. If DistrictsCleared >= BossUnlockThreshold → BossGateUnlocked = true
```

### DistrictNodeTrackingSystem

```csharp
// File: Assets/Scripts/Expedition/Systems/DistrictNodeTrackingSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: ExpeditionGraphUpdateSystem
//
// Tracks visit statistics for the current district node.
//
// 1. Get CurrentNodeIndex from ExpeditionGraphState
// 2. Read current GraphNodeState for that index
// 3. Increment TimeSpentSeconds by deltaTime
// 4. Write back updated GraphNodeState
```

---

## Setup Guide

1. **Create `Assets/Scripts/Expedition/` folder** with subfolders: Components/, Definitions/, Systems/, Authoring/
2. **Create assembly definition** `Hollowcore.Expedition.asmdef` referencing `DIG.Shared`, `DIG.Roguelite`, `Unity.Entities`, `Unity.NetCode`, `Unity.Collections`, `Unity.Mathematics`
3. **Create an ExpeditionGraphSO** template asset at `Assets/Data/Expedition/DefaultGraph.asset`:
   - Define 7 DistrictSlots with candidate DistrictDefinitionSOs
   - Wire up GateConnectionTemplates (ensure graph is connected)
   - Set StartSlotIndex=0, BossSlotIndex=6, BossUnlockThreshold=5
4. **Create 2-3 DistrictDefinitionSO** assets at `Assets/Data/Expedition/Districts/` for initial testing
5. **Create an ExpeditionConfig** singleton authoring on a persistent subscene entity that references the template SO
6. Verify graph generation by entering play mode and inspecting the ExpeditionGraphEntity in the Entity Debugger

---

## Verification

- [ ] ExpeditionGraphSO template loads correctly with slots and gates
- [ ] ExpeditionGraphGenerationSystem creates ExpeditionGraphEntity on expedition start
- [ ] GraphNodeState buffer has 5-7 active entries with valid DistrictDefinitionIds
- [ ] GraphEdgeState buffer has correct node index pairs and initial states
- [ ] Same ExpeditionSeed produces identical graph topology
- [ ] Different seeds produce varied district assignments and edge pruning
- [ ] Start node edges are Discovered; all other edges start Locked
- [ ] Gate unlock conditions transition correctly (Locked → Discovered → Open)
- [ ] Gate collapse fires when adjacent node reaches Phase4_Overrun
- [ ] BossGateUnlocked triggers at correct DistrictsCleared threshold
- [ ] Entity Debugger shows singleton with both buffers populated

---

## BlobAsset Pipeline

ExpeditionGraphSO is read at runtime during graph generation. Converting it to a BlobAsset eliminates managed references in Burst-compatible systems and makes the template data immutable and cache-friendly.

```csharp
// File: Assets/Scripts/Expedition/Blob/ExpeditionGraphBlob.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.Expedition.Blob
{
    /// <summary>
    /// Immutable blob representation of an ExpeditionGraphSO template.
    /// Created once at bake time, referenced by ExpeditionGraphGenerationSystem.
    /// </summary>
    public struct ExpeditionGraphBlob
    {
        public BlobArray<DistrictSlotBlob> Slots;
        public BlobArray<GateConnectionBlob> Gates;
        public int MinActiveNodes;
        public int MaxActiveNodes;
        public int StartSlotIndex;
        public int BossSlotIndex;
        public int BossUnlockThreshold;
    }

    public struct DistrictSlotBlob
    {
        public BlobString SlotName;
        /// <summary>DistrictDefinitionSO.DistrictId per candidate.</summary>
        public BlobArray<int> CandidateDistrictIds;
        public float GraphPositionX;
        public float GraphPositionY;
        public int MinDepth;
        public int MaxDepth;
    }

    public struct GateConnectionBlob
    {
        public int SlotIndexA;
        public int SlotIndexB;
        public bool Guaranteed;
        public GateUnlockCondition UnlockCondition;
    }
}
```

```csharp
// File: Assets/Scripts/Expedition/Definitions/ExpeditionGraphSO.cs (append method)
namespace Hollowcore.Expedition.Definitions
{
    public partial class ExpeditionGraphSO
    {
        /// <summary>
        /// Converts this SO into a BlobAsset for ECS consumption.
        /// Called by baker or bootstrap. AnimationCurves would use BlobArray<float2>
        /// but this SO has no curves.
        /// </summary>
        public BlobAssetReference<Blob.ExpeditionGraphBlob> BakeToBlob(BlobBuilder builder)
        {
            ref var root = ref builder.ConstructRoot<Blob.ExpeditionGraphBlob>();
            root.MinActiveNodes = MinActiveNodes;
            root.MaxActiveNodes = MaxActiveNodes;
            root.StartSlotIndex = StartSlotIndex;
            root.BossSlotIndex = BossSlotIndex;
            root.BossUnlockThreshold = BossUnlockThreshold;

            var slotArray = builder.Allocate(ref root.Slots, Slots.Count);
            for (int i = 0; i < Slots.Count; i++)
            {
                builder.AllocateString(ref slotArray[i].SlotName, Slots[i].SlotName);
                var candidates = builder.Allocate(ref slotArray[i].CandidateDistrictIds,
                    Slots[i].CandidateDistricts.Count);
                for (int j = 0; j < Slots[i].CandidateDistricts.Count; j++)
                    candidates[j] = Slots[i].CandidateDistricts[j].DistrictId;
                slotArray[i].GraphPositionX = Slots[i].GraphPosition.x;
                slotArray[i].GraphPositionY = Slots[i].GraphPosition.y;
                slotArray[i].MinDepth = Slots[i].MinDepth;
                slotArray[i].MaxDepth = Slots[i].MaxDepth;
            }

            var gateArray = builder.Allocate(ref root.Gates, Gates.Count);
            for (int i = 0; i < Gates.Count; i++)
            {
                gateArray[i].SlotIndexA = Gates[i].SlotIndexA;
                gateArray[i].SlotIndexB = Gates[i].SlotIndexB;
                gateArray[i].Guaranteed = Gates[i].Guaranteed;
                gateArray[i].UnlockCondition = Gates[i].UnlockCondition;
            }

            return builder.CreateBlobAssetReference<Blob.ExpeditionGraphBlob>(Allocator.Persistent);
        }
    }
}
```

```csharp
// File: Assets/Scripts/Expedition/Components/ExpeditionGraphComponents.cs (add to entity)
namespace Hollowcore.Expedition
{
    /// <summary>
    /// Singleton holding the baked blob reference. Lives on ExpeditionGraphEntity.
    /// </summary>
    public struct ExpeditionGraphTemplateRef : IComponentData
    {
        public BlobAssetReference<Blob.ExpeditionGraphBlob> Value;
    }
}
```

---

## Validation

```csharp
// File: Assets/Scripts/Expedition/Definitions/ExpeditionGraphSO.cs (OnValidate)
namespace Hollowcore.Expedition.Definitions
{
    public partial class ExpeditionGraphSO
    {
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Start and boss slots must be valid indices
            if (StartSlotIndex < 0 || StartSlotIndex >= Slots.Count)
                Debug.LogError($"[ExpeditionGraphSO] StartSlotIndex {StartSlotIndex} out of range [0..{Slots.Count})", this);
            if (BossSlotIndex < 0 || BossSlotIndex >= Slots.Count)
                Debug.LogError($"[ExpeditionGraphSO] BossSlotIndex {BossSlotIndex} out of range", this);
            if (StartSlotIndex == BossSlotIndex)
                Debug.LogError("[ExpeditionGraphSO] Start and Boss cannot be the same slot", this);

            // Min/max active nodes
            if (MinActiveNodes < 2) MinActiveNodes = 2;
            if (MaxActiveNodes > Slots.Count) MaxActiveNodes = Slots.Count;
            if (MinActiveNodes > MaxActiveNodes) MinActiveNodes = MaxActiveNodes;

            // Boss threshold must be reachable
            if (BossUnlockThreshold > MaxActiveNodes - 1)
                Debug.LogWarning($"[ExpeditionGraphSO] BossUnlockThreshold {BossUnlockThreshold} exceeds MaxActiveNodes-1", this);

            // Every slot must have at least one candidate district
            for (int i = 0; i < Slots.Count; i++)
                if (Slots[i].CandidateDistricts == null || Slots[i].CandidateDistricts.Count == 0)
                    Debug.LogError($"[ExpeditionGraphSO] Slot {i} '{Slots[i].SlotName}' has no candidate districts", this);

            // Gate edge indices must reference valid slots
            for (int i = 0; i < Gates.Count; i++)
            {
                var g = Gates[i];
                if (g.SlotIndexA < 0 || g.SlotIndexA >= Slots.Count || g.SlotIndexB < 0 || g.SlotIndexB >= Slots.Count)
                    Debug.LogError($"[ExpeditionGraphSO] Gate {i} has invalid slot indices ({g.SlotIndexA}, {g.SlotIndexB})", this);
                if (g.SlotIndexA == g.SlotIndexB)
                    Debug.LogError($"[ExpeditionGraphSO] Gate {i} is a self-loop", this);
            }

            // Duplicate edge detection
            var edgeSet = new HashSet<(int, int)>();
            foreach (var g in Gates)
            {
                var key = g.SlotIndexA < g.SlotIndexB ? (g.SlotIndexA, g.SlotIndexB) : (g.SlotIndexB, g.SlotIndexA);
                if (!edgeSet.Add(key))
                    Debug.LogError($"[ExpeditionGraphSO] Duplicate edge between slots {key.Item1} and {key.Item2}", this);
            }

            // Graph connectivity: BFS from start must reach boss (using guaranteed edges)
            ValidateConnectivity();

            // Depth constraints: MinDepth/MaxDepth consistency
            foreach (var slot in Slots)
                if (slot.MinDepth > slot.MaxDepth)
                    Debug.LogError($"[ExpeditionGraphSO] Slot '{slot.SlotName}' MinDepth > MaxDepth", this);
        }

        private void ValidateConnectivity()
        {
            if (Slots.Count == 0 || Gates.Count == 0) return;
            var adj = new List<int>[Slots.Count];
            for (int i = 0; i < Slots.Count; i++) adj[i] = new List<int>();
            foreach (var g in Gates)
            {
                if (g.SlotIndexA >= 0 && g.SlotIndexA < Slots.Count &&
                    g.SlotIndexB >= 0 && g.SlotIndexB < Slots.Count)
                {
                    adj[g.SlotIndexA].Add(g.SlotIndexB);
                    adj[g.SlotIndexB].Add(g.SlotIndexA);
                }
            }
            var visited = new bool[Slots.Count];
            var queue = new Queue<int>();
            queue.Enqueue(StartSlotIndex);
            visited[StartSlotIndex] = true;
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                foreach (int n in adj[cur])
                    if (!visited[n]) { visited[n] = true; queue.Enqueue(n); }
            }
            if (!visited[BossSlotIndex])
                Debug.LogError("[ExpeditionGraphSO] Boss node is NOT reachable from start via any edges!", this);
            for (int i = 0; i < Slots.Count; i++)
                if (!visited[i])
                    Debug.LogWarning($"[ExpeditionGraphSO] Slot {i} '{Slots[i].SlotName}' is disconnected from start node", this);
        }
#endif
    }
}
```

```csharp
// File: Assets/Editor/Validation/ExpeditionGraphValidator.cs
// Build-time validation: scan all ExpeditionGraphSO assets
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Hollowcore.Editor.Validation
{
    public class ExpeditionGraphBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var guids = AssetDatabase.FindAssets("t:ExpeditionGraphSO");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var graph = AssetDatabase.LoadAssetAtPath<Hollowcore.Expedition.Definitions.ExpeditionGraphSO>(path);
                // OnValidate runs on load — errors surface in console
                EditorUtility.SetDirty(graph);
            }
        }
    }
}
#endif
```

---

## Editor Tooling

### Expedition Graph Visual Editor (Critical)

```csharp
// File: Assets/Editor/ExpeditionWorkstation/ExpeditionWorkstationWindow.cs
// DIG Workstation pattern: sidebar tabs, IWorkstationModule interface
//
// EditorWindow — the central tool for designing expedition graph templates.
// Follows the same pattern as VFXWorkstationWindow, DialogueWorkstation, ProgressionWorkstation.
//
// Modules (sidebar tabs):
//   1. GraphEditorModule — node-graph canvas for ExpeditionGraphSO
//   2. SeedExplorerModule — type seed → see resulting graph layout instantly
//   3. SimulationModule — batch-generate graphs, validate statistics
//   4. DistrictBrowserModule — browse DistrictDefinitionSO pool
//
// GraphEditorModule (IWorkstationModule):
//   - Canvas: DistrictSlots rendered as draggable nodes (position = GraphPosition)
//   - Edges: GateConnectionTemplates rendered as bezier curves between nodes
//   - Node colors: green=start, red=boss, grey=normal
//   - Edge styles: solid=guaranteed, dashed=prunable
//   - Click node: inspector panel shows CandidateDistricts, MinDepth/MaxDepth
//   - Click edge: inspector shows UnlockCondition, Guaranteed toggle
//   - Right-click canvas: "Add Slot" context menu
//   - Right-click node: "Add Edge From Here", "Delete Slot"
//   - Toolbar: "Validate Graph" button (runs OnValidate + BFS connectivity)
//   - Toolbar: "Preview With Seed" (generates runtime graph in editor, highlights active nodes/edges)
//
// SeedExplorerModule (IWorkstationModule):
//   - Text field: enter seed (uint or string → hash)
//   - "Generate" button: runs ExpeditionGraphGenerationSystem logic in editor
//   - Canvas: same node-graph view but shows GENERATED result
//     - Active nodes highlighted, inactive greyed
//     - Edges: included=solid, pruned=hidden
//     - Node labels show selected DistrictDefinitionSO name
//   - Stats panel: node count, edge count, average branching factor, path length start→boss
//   - "Regenerate" with different seed for quick comparison
```

```csharp
// File: Assets/Editor/ExpeditionWorkstation/IExpeditionWorkstationModule.cs
namespace Hollowcore.Editor.ExpeditionWorkstation
{
    public interface IExpeditionWorkstationModule
    {
        string TabName { get; }
        void OnGUI(UnityEngine.Rect area);
        void OnSelectionChanged(UnityEngine.Object selected);
    }
}
```

### Custom Property Drawers

```csharp
// File: Assets/Editor/ExpeditionWorkstation/DistrictSlotDrawer.cs
// CustomPropertyDrawer for DistrictSlot — shows candidate count badge,
// depth range bar, and miniature graph position preview.

// File: Assets/Editor/ExpeditionWorkstation/GateConnectionDrawer.cs
// CustomPropertyDrawer for GateConnectionTemplate — dropdowns for slot A/B
// (named, not raw indices), condition enum, guaranteed toggle with visual indicator.
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Expedition/Components/ExpeditionRuntimeConfig.cs
using Unity.Entities;

namespace Hollowcore.Expedition
{
    /// <summary>
    /// Runtime-tunable singleton for expedition graph behavior.
    /// Modified via debug console or editor live tuning window.
    /// NOT ghost-replicated — server-only tuning knobs.
    /// </summary>
    public struct ExpeditionRuntimeConfig : IComponentData
    {
        /// <summary>Probability threshold for pruning non-guaranteed edges (0.0-1.0). Default 0.3.</summary>
        public float EdgePruneProbability;

        /// <summary>Minimum edges per active node after pruning (connectivity floor). Default 1.</summary>
        public int MinEdgesPerNode;

        /// <summary>Override districts cleared for boss unlock. 0 = use template value.</summary>
        public int DebugBossUnlockOverride;

        /// <summary>If true, all gates start Open (debug fast traversal).</summary>
        public bool DebugAllGatesOpen;

        /// <summary>If true, skip gate selection UI on transition.</summary>
        public bool DebugSkipGateSelection;

        public static ExpeditionRuntimeConfig Default => new()
        {
            EdgePruneProbability = 0.3f,
            MinEdgesPerNode = 1,
            DebugBossUnlockOverride = 0,
            DebugAllGatesOpen = false,
            DebugSkipGateSelection = false,
        };
    }
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Expedition/Debug/ExpeditionGraphOverlay.cs
// Managed MonoBehaviour on debug HUD canvas.
//
// Draws the expedition graph as an in-game minimap overlay:
//   - Nodes: circles colored by state (green=current, blue=visited, grey=unvisited, red=overrun)
//   - Edges: lines colored by gate state (white=open, yellow=discovered, dark=locked, red=collapsed)
//   - Current node pulses
//   - Hover (debug cursor): shows node name, front phase, visit count
//   - Toggle: F8 key or debug menu
//
// Reads from ExpeditionGraphState + GraphNodeState/GraphEdgeState buffers each frame via
// EntityManager on the server/local world.

// File: Assets/Scripts/Expedition/Debug/DistrictZoneOverlay.cs
// Draws zone connectivity graph within the current district:
//   - Zones as rectangles at BoundsCenter projected to screen
//   - Connections as lines between zone centers
//   - Zone color = FrontZoneState (green/yellow/orange/red)
//   - Player position marker
//   - Zone names and clear/visited status labels
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/ExpeditionWorkstation/GraphSimulationModule.cs
// IExpeditionWorkstationModule — "Simulation" tab in Expedition Workstation.
//
// "Generate 1000 Graphs" button:
//   For seedOffset = 0..999:
//     seed = baseSeed + seedOffset
//     Run ExpeditionGraphGenerationSystem logic (editor-only, no ECS world)
//     Record: activeNodeCount, activeEdgeCount, pathLengthStartToBoss, branchingFactor,
//             districtDistribution (how often each DistrictDefinitionSO appears)
//
// Validation pass per graph:
//   - Assert: path exists from start to boss (fail count)
//   - Assert: all active nodes reachable from start
//   - Assert: activeNodeCount in [MinActiveNodes, MaxActiveNodes]
//   - Assert: branching factor > 1.0 (non-degenerate)
//
// Results display:
//   - Average/min/max active nodes
//   - Average/min/max branching factor (edges / nodes)
//   - Average/min/max path length start → boss
//   - District distribution histogram (bar chart)
//   - Failure count (unreachable boss, disconnected nodes)
//   - Export to CSV button
//
// Performance: runs synchronously in editor, ~50ms for 1000 graphs (pure math, no ECS).
```
