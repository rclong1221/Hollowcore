# EPIC 4.3: District Loading & Transitions

**Status**: Planning
**Epic**: EPIC 4 — District Graph & Expedition Structure
**Priority**: Critical — Players must move between districts
**Dependencies**: EPIC 4.1 (Graph Data Model), EPIC 4.2 (District Persistence), Framework: SceneManagement/, Roguelite/ (IZoneProvider)

---

## Overview

District transitions are the seams between Hollowcore's large, persistent maps. When the player reaches a district exit gate, the game saves the current district, presents the Gate Selection UI (EPIC 6), unloads the current scene, generates or restores the target district, and spawns the player at the appropriate entry point. The DistrictLoadSystem implements the framework's IZoneProvider interface, bridging the expedition graph into the existing scene pipeline. Each district is an additive scene; common elements (player, UI, persistent systems) live in a persistent scene that is never unloaded.

---

## Component Definitions

### DistrictTransitionRequest (IComponentData)

```csharp
// File: Assets/Scripts/Expedition/Components/DistrictTransitionComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Expedition
{
    /// <summary>
    /// Singleton request entity created when the player initiates a district transition.
    /// Consumed by DistrictTransitionSystem over multiple frames.
    /// </summary>
    public struct DistrictTransitionRequest : IComponentData
    {
        /// <summary>Target node index in the expedition graph.</summary>
        public int TargetNodeIndex;

        /// <summary>Edge index the player is traversing (for gate animation/UI).</summary>
        public int GateEdgeIndex;

        /// <summary>Entry point index within the target district (0-2).</summary>
        public byte EntryPointIndex;

        /// <summary>Current phase of the transition state machine.</summary>
        public TransitionPhase Phase;
    }

    public enum TransitionPhase : byte
    {
        Requested = 0,
        SavingCurrentDistrict = 1,
        ShowingGateSelection = 2,
        UnloadingScene = 3,
        GeneratingTarget = 4,
        ApplyingDelta = 5,
        SpawningPlayer = 6,
        Complete = 7
    }

    /// <summary>
    /// Marks a gate entity in the world that the player can interact with to trigger a transition.
    /// </summary>
    public struct GateInteractable : IComponentData
    {
        /// <summary>Edge index in GraphEdgeState buffer.</summary>
        public int EdgeIndex;

        /// <summary>Which side of the edge this gate is on (0=A, 1=B).</summary>
        public byte Side;
    }

    /// <summary>
    /// Attached to the active district root entity. Tracks which scene is currently loaded.
    /// </summary>
    public struct ActiveDistrictScene : IComponentData
    {
        /// <summary>Node index of the currently loaded district.</summary>
        public int NodeIndex;

        /// <summary>Scene handle for async unload.</summary>
        public Unity.Entities.Hash128 SceneGUID;
    }
}
```

### DistrictEntryPoint (IComponentData)

```csharp
// File: Assets/Scripts/Expedition/Components/DistrictTransitionComponents.cs (continued)
using Unity.Mathematics;

namespace Hollowcore.Expedition
{
    /// <summary>
    /// Baked on entry point GameObjects within each district scene.
    /// Multiple entry points per district (1-3) based on which gate the player arrives through.
    /// </summary>
    public struct DistrictEntryPoint : IComponentData
    {
        /// <summary>Which entry point index this represents (0-2).</summary>
        public byte EntryIndex;

        /// <summary>World-space spawn position.</summary>
        public float3 SpawnPosition;

        /// <summary>Spawn facing direction (yaw in radians).</summary>
        public float SpawnYaw;
    }
}
```

---

## Systems

### DistrictTransitionSystem

```csharp
// File: Assets/Scripts/Expedition/Systems/DistrictTransitionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup (OrderLast)
//
// State machine that drives the multi-frame transition sequence.
// Only one transition can be active at a time.
//
// Phase transitions:
//
// Requested:
//   1. Validate TargetNodeIndex exists and edge is Open
//   2. Disable player input (set PlayerInputLock)
//   3. Trigger extraction animation/VFX
//   4. → SavingCurrentDistrict
//
// SavingCurrentDistrict:
//   1. Enable DistrictSaveTag on current district entity (triggers DistrictSaveSystem)
//   2. Wait one frame for save to complete
//   3. → ShowingGateSelection
//
// ShowingGateSelection:
//   1. Signal Gate Selection UI to show (EPIC 6 bridge)
//   2. If no gate selection needed (direct transition), skip to UnloadingScene
//   3. Wait for UI callback with confirmed TargetNodeIndex
//   4. → UnloadingScene
//
// UnloadingScene:
//   1. Begin async scene unload via SceneManagement (framework)
//   2. Show loading screen
//   3. Wait for unload complete callback
//   4. → GeneratingTarget
//
// GeneratingTarget:
//   1. Look up TargetNodeIndex in DistrictPersistenceModule:
//      a. If no saved state: generate fresh from seed via IZoneProvider
//      b. If saved state exists: regenerate from seed (same layout)
//   2. Begin async scene load for target district
//   3. Wait for load complete
//   4. If saved state exists → ApplyingDelta, else → SpawningPlayer
//
// ApplyingDelta:
//   1. Enable DistrictDeltaApplyTag (triggers DistrictDeltaApplySystem from EPIC 4.2)
//   2. Wait one frame for delta application
//   3. → SpawningPlayer
//
// SpawningPlayer:
//   1. Query DistrictEntryPoint entities for matching EntryPointIndex
//   2. Teleport player to SpawnPosition with SpawnYaw
//   3. Update ExpeditionGraphState.CurrentNodeIndex
//   4. Increment GraphNodeState.VisitCount for target node
//   5. Re-enable player input
//   6. Hide loading screen
//   7. Trigger FrontOffScreenSimulation for all non-current districts (EPIC 3.2)
//   8. Destroy DistrictTransitionRequest entity
//   9. → Complete
```

### DistrictLoadSystem (IZoneProvider)

```csharp
// File: Assets/Scripts/Expedition/Systems/DistrictLoadSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: InitializationSystemGroup
//
// Implements IZoneProvider from the framework's Roguelite/ module.
// Bridges the expedition graph into the framework's zone loading pipeline.
//
// IZoneProvider.GetCurrentZoneId():
//   → Return ActiveDistrictScene.NodeIndex (or hash of DistrictDefinitionId)
//
// IZoneProvider.GetNextZoneId():
//   → NOT used for Hollowcore (non-linear). Returns -1.
//   → Transitions are driven by DistrictTransitionRequest, not sequential advance.
//
// IZoneProvider.GenerateZone(uint seed):
//   1. Derive district seed from expedition seed + node index
//      districtSeed = RunSeedUtility.DeriveHash(expeditionSeed, nodeIndex)
//   2. Select topology variant: variant = districtSeed % TopologyVariants
//   3. Load district scene via SceneManagement (addressable by SceneKey)
//   4. Zone entities within scene are generated by the district's internal zone generator
//   5. Return scene handle for tracking
//
// IZoneProvider.GetEntryPoint():
//   → Query DistrictEntryPoint with matching EntryIndex
//   → Return SpawnPosition + SpawnYaw
```

### GateInteractionSystem

```csharp
// File: Assets/Scripts/Expedition/Systems/GateInteractionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Detects player interaction with gate entities and creates transition requests.
//
// 1. For each GateInteractable entity:
//    a. Check proximity to player (within interaction range)
//    b. Check player interaction input (e.g., "Use" key)
//    c. Look up GraphEdgeState by EdgeIndex
//    d. If GateState != Open → show "Gate Locked" UI feedback, skip
//    e. Determine TargetNodeIndex (the other side of the edge from current node)
//    f. Determine EntryPointIndex based on which gate connects to target
//    g. Create entity with DistrictTransitionRequest component
```

---

## Setup Guide

1. **Implement IZoneProvider** in DistrictLoadSystem, register with framework's zone pipeline
2. **Create DistrictEntryPoint authoring** MonoBehaviour and place 1-3 entry point GameObjects per district scene at gate locations
3. **Create GateInteractable authoring** MonoBehaviour and place on gate GameObjects in each district scene, wiring EdgeIndex from the template
4. **Configure loading screen** prefab referenced by DistrictTransitionSystem (reuse existing framework loading screen if available)
5. **Set up persistent scene** containing player prefab, UI canvas, and expedition-wide singletons (ExpeditionGraphEntity, DistrictPersistenceRegistry)
6. **Create district scenes** as addressable assets, each with their own subscene containing zone entities and entry points
7. **Wire Gate Selection UI** bridge (EPIC 6) — for initial testing, skip gate selection and directly transition

---

## Verification

- [ ] Player can interact with an Open gate entity to trigger a transition
- [ ] Locked gates show feedback and do not trigger transitions
- [ ] DistrictTransitionSystem progresses through all phases in order
- [ ] Current district is saved before unload (DistrictSaveSystem fires)
- [ ] Target district scene loads correctly (fresh generation from seed)
- [ ] Previously visited district regenerates from seed + saved delta applied
- [ ] Player spawns at correct DistrictEntryPoint position and facing
- [ ] ExpeditionGraphState.CurrentNodeIndex updates after transition
- [ ] GraphNodeState.VisitCount increments on re-entry
- [ ] FrontOffScreenSimulation fires for all non-current districts after transition
- [ ] Player input is locked during transition and re-enabled after spawn
- [ ] Loading screen shows during scene swap
- [ ] No entity leaks from previous district scene after unload
- [ ] Multiple rapid gate interactions do not create duplicate transition requests

---

## Editor Tooling

```csharp
// File: Assets/Editor/ExpeditionWorkstation/TransitionDebugPanel.cs
// Custom editor panel for debugging district transitions during play mode.
//
// Features:
//   - TransitionPhase state machine visualizer (current phase highlighted, previous phases greyed)
//   - Phase timing: elapsed time in each phase, total transition time
//   - "Force Transition To..." dropdown: select any active node → creates DistrictTransitionRequest
//   - "Skip Phase" button: advance state machine to next phase (debug fast-forward)
//   - Entry point visualizer: shows all DistrictEntryPoint positions in scene view as gizmos
//   - Gate visualizer: draws GateInteractable interaction radius spheres in scene view
//   - Active scene info: current scene GUID, load status, entity count
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Expedition/Components/TransitionRuntimeConfig.cs
using Unity.Entities;

namespace Hollowcore.Expedition
{
    /// <summary>
    /// Runtime-tunable settings for district transitions.
    /// Adjusted via debug console for iteration without rebuilds.
    /// </summary>
    public struct TransitionRuntimeConfig : IComponentData
    {
        /// <summary>Seconds to hold extraction animation before saving. Default 1.5.</summary>
        public float ExtractionAnimDuration;

        /// <summary>Minimum loading screen display time (prevents flicker). Default 0.5.</summary>
        public float MinLoadScreenSeconds;

        /// <summary>If true, skip gate selection UI and transition directly. Default false.</summary>
        public bool SkipGateSelection;

        /// <summary>If true, skip extraction animation. Default false.</summary>
        public bool SkipExtractionAnim;

        /// <summary>Gate interaction radius in world units. Default 3.0.</summary>
        public float GateInteractionRadius;

        public static TransitionRuntimeConfig Default => new()
        {
            ExtractionAnimDuration = 1.5f,
            MinLoadScreenSeconds = 0.5f,
            SkipGateSelection = false,
            SkipExtractionAnim = false,
            GateInteractionRadius = 3.0f,
        };
    }
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Expedition/Debug/TransitionDebugOverlay.cs
// In-game HUD overlay for transition state debugging:
//
//   - Top-left banner: "TRANSITION: [Phase] → [TargetDistrict]" during active transition
//   - Phase progress bar showing all 8 phases, current highlighted
//   - Timing: per-phase elapsed + total
//   - Gate proximity indicators: when near any GateInteractable, show:
//     - Gate state (Locked/Discovered/Open/Collapsed) with color
//     - Target district name
//     - Distance to gate center
//   - Entry point gizmos: wireframe spheres at spawn positions (visible through walls in debug mode)
//   - Toggle: active automatically when DistrictTransitionRequest exists
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/ExpeditionWorkstation/TransitionSimulationModule.cs
// IExpeditionWorkstationModule — "Transitions" tab.
//
// "Full Expedition Traverse" simulation:
//   1. Generate expedition graph from seed
//   2. Starting at start node, simulate visiting every reachable district:
//      - Save state, load target, apply delta (if revisiting), verify entry point
//   3. Verify: no node visited without Open gate, boss node reachable after clearing threshold
//   4. Report: total transitions, average load time estimate, any connectivity failures
//
// "Rapid Transition Stress Test":
//   Simulate 50 rapid transitions (back and forth between two districts)
//   Verify: no entity leaks (entity count stable), no duplicate transition requests,
//   persistence round-trips correctly on every transition
//
// "Entry Point Coverage":
//   For each district topology, verify all entry points are reachable and have valid
//   SpawnPosition/SpawnYaw values (not at origin, not inside geometry)
```
